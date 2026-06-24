using System.Security.Cryptography;
using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Services;

public class CheckoutService : ICheckoutService
{
    private readonly StorefrontDbContext _dbContext;
    private readonly StorefrontSettings _settings;
    private readonly StorefrontPoMjeriInventoryService _poMjeriInventoryService;

    public CheckoutService(
        StorefrontDbContext dbContext,
        IOptions<StorefrontSettings> settings,
        StorefrontPoMjeriInventoryService poMjeriInventoryService)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
        _poMjeriInventoryService = poMjeriInventoryService;
    }

    public async Task<CheckoutResult> CreateCashOnDeliveryOrderAsync(
        CheckoutRequest request,
        IReadOnlyCollection<CartItem> cartItems,
        int? storefrontCustomerId = null,
        CancellationToken cancellationToken = default)
    {
        if (cartItems.Count == 0)
        {
            return Failure("Korpa je prazna.");
        }

        var productIds = cartItems.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Include(product => product.ProductImages)
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var poMjeriPlans = new Dictionary<string, PoMjeriPlanResult>(StringComparer.Ordinal);
        var plannedPoMjeriCart = new List<CartItem>();
        var variantCache = new Dictionary<string, List<StorefrontProduct>>(StringComparer.Ordinal);

        foreach (var line in cartItems)
        {
            if (!products.TryGetValue(line.ProductId, out var product)
                || product.Disabled
                || !product.IsPublished)
            {
                return Failure("Jedan od proizvoda iz korpe više nije dostupan.");
            }

            if (!line.PoMjeri)
            {
                if (product.AvailableQuantity < line.Quantity)
                {
                    return Failure($"Proizvod {product.Name} trenutno nema traženu raspoloživu količinu.");
                }

                continue;
            }

            if (!line.CustomWidth.HasValue || !line.CustomLength.HasValue || string.IsNullOrWhiteSpace(line.SelectedColor))
            {
                return Failure($"Po mjeri proizvod {product.Name} nema ispravne dimenzije za narudžbinu.");
            }

            var cacheKey = StorefrontPoMjeriPlanner.BuildGroupKey(product);
            if (!variantCache.TryGetValue(cacheKey, out var variants))
            {
                variants = await LoadGroupVariantsAsync(product, cancellationToken);
                variantCache[cacheKey] = variants;
            }

            var snapshot = await _poMjeriInventoryService.LoadSnapshotAsync(
                variants,
                plannedPoMjeriCart,
                cancellationToken: cancellationToken);

            var evaluation = StorefrontPoMjeriPlanner.Evaluate(
                variants,
                snapshot,
                line.SelectedColor,
                line.CustomWidth.Value,
                line.CustomLength.Value,
                line.Quantity);

            if (!evaluation.IsValid || evaluation.BestPlan == null)
            {
                return Failure(evaluation.Message);
            }

            poMjeriPlans[line.LineId] = evaluation;
            plannedPoMjeriCart.Add(new CartItem
            {
                LineId = line.LineId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PoMjeri = true,
                CustomWidth = line.CustomWidth,
                CustomLength = line.CustomLength,
                SelectedColor = line.SelectedColor,
                Allocations = evaluation.BestPlan.Slices
                    .Select(slice => new CartItemAllocation
                    {
                        SourceProductId = slice.ProductId,
                        Quantity = slice.Quantity,
                        ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit
                    })
                    .ToList()
            });
        }

        var utcNow = DateTime.UtcNow;
        var order = new WebOrder
        {
            StorefrontCustomerId = storefrontCustomerId,
            OrderNumber = await GenerateOrderNumberAsync(cancellationToken),
            Status = StorefrontOrderStatuses.Pending,
            CustomerFirstName = TextEncodingHelper.NormalizeInput(request.FirstName) ?? string.Empty,
            CustomerLastName = TextEncodingHelper.NormalizeInput(request.LastName) ?? string.Empty,
            CustomerEmail = request.Email.Trim(),
            CustomerPhone = TextEncodingHelper.NormalizeInput(request.Phone),
            ShippingAddressLine1 = TextEncodingHelper.NormalizeInput(request.AddressLine1) ?? string.Empty,
            ShippingAddressLine2 = TextEncodingHelper.NormalizeInput(request.AddressLine2),
            ShippingCity = TextEncodingHelper.NormalizeInput(request.City) ?? string.Empty,
            ShippingPostalCode = TextEncodingHelper.NormalizeInput(request.PostalCode),
            ShippingCountry = TextEncodingHelper.NormalizeInput(request.Country) ?? string.Empty,
            Currency = "EUR",
            PaymentStatus = StorefrontPaymentStatuses.Pending,
            FulfillmentStatus = StorefrontFulfillmentStatuses.Unfulfilled,
            PaymentProvider = StorefrontPaymentProviders.CashOnDelivery,
            CreatedUtc = utcNow,
            CustomerNote = TextEncodingHelper.NormalizeInput(request.CustomerNote)
        };

        decimal itemsTotal = 0m;

        foreach (var line in cartItems)
        {
            var product = products[line.ProductId];
            var primaryImage = product.ProductImages
                .Where(image => !image.Disabled && !string.Equals(image.MediaType, "video", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .FirstOrDefault();

            if (line.PoMjeri)
            {
                var cacheKey = StorefrontPoMjeriPlanner.BuildGroupKey(product);
                var sourceLookup = variantCache[cacheKey].ToDictionary(item => item.Id);
                var pricing = StorefrontPricingHelper.BuildPoMjeriPricing(
                    sourceLookup,
                    poMjeriPlans[line.LineId].BestPlan!.Slices,
                    line.CustomLength);
                var unitPrice = pricing.UnitPrice;
                var lineTotal = unitPrice * line.Quantity;
                itemsTotal += lineTotal;

                var orderItem = new WebOrderItem
                {
                    TepihId = product.Id,
                    ProductName = TextEncodingHelper.Decode(product.Name) ?? product.Name,
                    ProductNumber = TextEncodingHelper.Decode(product.ProductNumber) ?? product.ProductNumber,
                    Model = TextEncodingHelper.Decode(product.Model) ?? product.Model,
                    Color = TextEncodingHelper.Decode(line.SelectedColor),
                    Length = line.CustomLength,
                    Width = line.CustomWidth,
                    PerM2 = true,
                    PoMjeri = true,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                    PrimaryImageUrl = primaryImage?.Url
                };

                order.Items.Add(orderItem);

                var plan = poMjeriPlans[line.LineId];
                foreach (var slice in plan.BestPlan!.Slices)
                {
                    order.Reservations.Add(new InventoryReservation
                    {
                        WebOrderItem = orderItem,
                        TepihId = slice.ProductId,
                        Quantity = slice.Quantity,
                        CutWidth = line.CustomWidth,
                        CutLength = line.CustomLength,
                        ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit,
                        Status = InventoryReservationStatuses.Active,
                        CreatedUtc = utcNow,
                        ExpiresUtc = utcNow.AddHours(_settings.ReservationHours),
                        Reason = "Cash on delivery checkout"
                    });
                }

                continue;
            }

            var regularPricing = StorefrontPricingHelper.BuildPricing(product, product.Width, product.Length);
            var regularLineTotal = regularPricing.UnitPrice * line.Quantity;
            itemsTotal += regularLineTotal;

            var regularOrderItem = new WebOrderItem
            {
                TepihId = product.Id,
                ProductName = TextEncodingHelper.Decode(product.Name) ?? product.Name,
                ProductNumber = TextEncodingHelper.Decode(product.ProductNumber) ?? product.ProductNumber,
                Model = TextEncodingHelper.Decode(product.Model) ?? product.Model,
                Color = TextEncodingHelper.Decode(product.Color),
                Length = product.Length,
                Width = product.Width,
                PerM2 = product.PerM2,
                PoMjeri = false,
                Quantity = line.Quantity,
                UnitPrice = regularPricing.UnitPrice,
                LineTotal = regularLineTotal,
                PrimaryImageUrl = primaryImage?.Url
            };

            order.Items.Add(regularOrderItem);
            order.Reservations.Add(new InventoryReservation
            {
                WebOrderItem = regularOrderItem,
                TepihId = product.Id,
                Quantity = line.Quantity,
                Status = InventoryReservationStatuses.Active,
                CreatedUtc = utcNow,
                ExpiresUtc = utcNow.AddHours(_settings.ReservationHours),
                Reason = "Cash on delivery checkout"
            });

            product.ReservedQuantity += line.Quantity;
        }

        var shippingTotal = itemsTotal > 0 ? _settings.FlatShippingCost : 0m;

        order.ItemsTotal = itemsTotal;
        order.ShippingTotal = shippingTotal;
        order.DiscountTotal = 0m;
        order.GrandTotal = itemsTotal + shippingTotal;

        _dbContext.WebOrders.Add(order);
        _dbContext.WebOrderStatusHistory.Add(new WebOrderStatusHistory
        {
            WebOrder = order,
            Status = order.Status,
            ChangedBy = "e-comm",
            Note = "Narud\u017Ebina kreirana. Pla\u0107anje pouze\u0107em.",
            ChangedUtc = utcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CheckoutResult
        {
            Succeeded = true,
            Message = "Narud\u017Ebina je uspje\u0161no kreirana.",
            OrderNumber = order.OrderNumber
        };
    }

    private async Task<List<StorefrontProduct>> LoadGroupVariantsAsync(StorefrontProduct product, CancellationToken cancellationToken)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(item =>
                item.IsPublished &&
                !item.Disabled &&
                item.Slug != null &&
                item.PoMjeri &&
                item.Name == product.Name &&
                item.ProductNumber == product.ProductNumber &&
                item.Model == product.Model)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var orderNumber = $"WEB-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetHexString(6)}";
            var exists = await _dbContext.WebOrders
                .AsNoTracking()
                .AnyAsync(order => order.OrderNumber == orderNumber, cancellationToken);

            if (!exists)
            {
                return orderNumber;
            }
        }

        return $"WEB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
    }

    private static CheckoutResult Failure(string message)
    {
        return new CheckoutResult
        {
            Succeeded = false,
            Message = message
        };
    }
}
