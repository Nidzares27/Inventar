using System.Security.Cryptography;
using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Services;

public class CheckoutService : ICheckoutService
{
    private readonly StorefrontDbContext _dbContext;
    private readonly StorefrontSettings _settings;

    public CheckoutService(StorefrontDbContext dbContext, IOptions<StorefrontSettings> settings)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
    }

    public async Task<CheckoutResult> CreateCashOnDeliveryOrderAsync(
        CheckoutRequest request,
        IReadOnlyCollection<CartItem> cartItems,
        CancellationToken cancellationToken = default)
    {
        if (cartItems.Count == 0)
        {
            return Failure("Korpa je prazna.");
        }

        var requestedQuantities = cartItems
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var products = await _dbContext.Products
            .Include(product => product.ProductImages)
            .Where(product => requestedQuantities.Keys.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var requestedProduct in requestedQuantities)
        {
            if (!products.TryGetValue(requestedProduct.Key, out var product)
                || product.Disabled
                || !product.IsPublished)
            {
                return Failure("Jedan od proizvoda iz korpe više nije dostupan.");
            }

            if (product.AvailableQuantity < requestedProduct.Value)
            {
                return Failure($"Proizvod {product.Name} trenutno nema traženu raspoloživu količinu.");
            }
        }

        var utcNow = DateTime.UtcNow;
        var order = new WebOrder
        {
            OrderNumber = await GenerateOrderNumberAsync(cancellationToken),
            Status = StorefrontOrderStatuses.Pending,
            CustomerFirstName = request.FirstName.Trim(),
            CustomerLastName = request.LastName.Trim(),
            CustomerEmail = request.Email.Trim(),
            CustomerPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            ShippingAddressLine1 = request.AddressLine1.Trim(),
            ShippingAddressLine2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? null : request.AddressLine2.Trim(),
            ShippingCity = request.City.Trim(),
            ShippingPostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim(),
            ShippingCountry = request.Country.Trim(),
            Currency = "EUR",
            PaymentStatus = StorefrontPaymentStatuses.Pending,
            FulfillmentStatus = StorefrontFulfillmentStatuses.Unfulfilled,
            PaymentProvider = StorefrontPaymentProviders.CashOnDelivery,
            CreatedUtc = utcNow,
            CustomerNote = string.IsNullOrWhiteSpace(request.CustomerNote) ? null : request.CustomerNote.Trim()
        };

        decimal itemsTotal = 0m;

        foreach (var line in cartItems)
        {
            var product = products[line.ProductId];
            var primaryImage = product.ProductImages
                .Where(image => !image.Disabled)
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .FirstOrDefault();

            var lineTotal = product.EffectivePrice * line.Quantity;
            itemsTotal += lineTotal;

            order.Items.Add(new WebOrderItem
            {
                TepihId = product.Id,
                ProductName = product.Name,
                ProductNumber = product.ProductNumber,
                Model = product.Model,
                Color = product.Color,
                Length = product.Length,
                Width = product.Width,
                PerM2 = product.PerM2,
                Quantity = line.Quantity,
                UnitPrice = product.EffectivePrice,
                LineTotal = lineTotal,
                PrimaryImageUrl = primaryImage?.Url
            });

            order.Reservations.Add(new InventoryReservation
            {
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
            ChangedBy = "Customer",
            Note = "Narudžba kreirana. Plaćanje pouzećem.",
            ChangedUtc = utcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CheckoutResult
        {
            Succeeded = true,
            Message = "Narudžba je uspješno kreirana.",
            OrderNumber = order.OrderNumber
        };
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
