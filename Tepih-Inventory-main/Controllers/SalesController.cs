using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.ViewModels.Sales;
using Inventar.ViewModels.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using Inventar.Migrations;
using Inventar.ViewModels.Inventory;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Extensions.Logging;
using static iText.Kernel.Font.PdfFontFactory;
using iText.Layout;
using Inventar.Utils;

namespace Inventar.Controllers
{
    [Authorize(Roles = "admin,superadmin")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISalesRepository _salesRepository;
        private readonly ITepihRepository _tepihRepository;
        private readonly ILogger<SalesController> _logger;

        public SalesController(ApplicationDbContext context, ISalesRepository salesRepository, ITepihRepository tepihRepository, ILogger<SalesController> logger)
        {
            this._context = context;
            this._salesRepository = salesRepository;
            this._tepihRepository = tepihRepository;
            this._logger = logger;
        }

        private static int? GetSaleWidth(Tepih product, Prodaja sale)
        {
            return PoMjeriHelper.GetEffectiveWidth(product, sale);
        }

        private static int? GetSaleLength(Tepih product, Prodaja sale)
        {
            return PoMjeriHelper.GetEffectiveLength(product, sale);
        }

        private static decimal? GetSaleM2PerUnit(Tepih product, Prodaja sale)
        {
            return PoMjeriHelper.CalculateM2PerUnit(product.PerM2, GetSaleWidth(product, sale), GetSaleLength(product, sale));
        }

        private static decimal? GetSaleM2Total(Tepih product, Prodaja sale)
        {
            return PoMjeriHelper.CalculateM2Total(product.PerM2, GetSaleWidth(product, sale), GetSaleLength(product, sale), sale.Quantity);
        }

        private static decimal ApplyDiscount(decimal amount, int? rabat)
        {
            if (!rabat.HasValue || rabat.Value <= 0)
            {
                return amount;
            }

            return amount - ((decimal)rabat.Value / 100m * amount);
        }

        private static decimal GetSaleTotalPrice(Tepih product, Prodaja sale)
        {
            return InventoryPricingHelper.CalculateDiscountedLineTotal(
                product.PerM2,
                product.PoMjeri,
                sale.Price,
                GetSaleWidth(product, sale),
                GetSaleLength(product, sale),
                sale.Quantity,
                sale.Rabat);
        }

        private static decimal GetSaleTotalPrice(bool perM2, bool poMjeri, decimal price, int? width, int? length, int quantity, int? rabat)
        {
            return InventoryPricingHelper.CalculateDiscountedLineTotal(
                perM2,
                poMjeri,
                price,
                width,
                length,
                quantity,
                rabat);
        }

        private static string FormatSize(int? width, int? length)
        {
            return width.HasValue && length.HasValue
                ? $"{width.Value}X{length.Value}"
                : string.Empty;
        }

        private static int CalculateAvailableQuantity(Tepih product)
        {
            return Math.Max(product.Quantity - product.ReservedQuantity, 0);
        }

        private async Task<int> GetRemainingPoMjeriLengthAsync(Tepih product)
        {
            if (!product.PoMjeri || !product.Length.HasValue)
            {
                return product.Length ?? 0;
            }

            var sales = await _context.Prodaje
                .Where(sale => sale.TepihId == product.Id && !sale.Disabled)
                .AsNoTracking()
                .ToListAsync();

            return PoMjeriHelper.CalculateRemainingLength(product, sales);
        }

        private static decimal ResolveDirectSaleTargetTotal(Prodaja sale, Tepih currentProduct)
        {
            return sale.DirectSaleOriginalTotal ?? GetSaleTotalPrice(currentProduct, sale);
        }

        private static decimal CalculateReplacementPrice(
            decimal targetTotal,
            bool perM2,
            bool poMjeri,
            int? width,
            int? length,
            int quantity,
            int? rabat)
        {
            var factor = InventoryPricingHelper.CalculateDiscountedLineTotal(
                perM2,
                poMjeri,
                1m,
                width,
                length,
                quantity,
                rabat);

            if (factor <= 0m)
            {
                return 0m;
            }

            return Math.Round(targetTotal / factor, 4, MidpointRounding.AwayFromZero);
        }

        private async Task<object> BuildPoMjeriReplacementPromptAsync(Tepih product)
        {
            var remainingLength = await GetRemainingPoMjeriLengthAsync(product);

            return new
            {
                success = true,
                requiresCustomSize = true,
                productId = product.Id,
                name = TextEncodingHelper.Decode(product.Name),
                model = TextEncodingHelper.Decode(product.Model),
                productNumber = TextEncodingHelper.Decode(product.ProductNumber),
                color = TextEncodingHelper.Decode(product.Color),
                unId = TextEncodingHelper.Decode(product.UnID),
                fixedWidth = product.Width,
                originalWidth = product.Width,
                originalLength = product.Length,
                originalSize = PoMjeriHelper.FormatSize(product.Width, product.Length),
                remainingWidth = product.Width,
                remainingLength,
                availableRemainingLength = remainingLength,
                remainingSize = PoMjeriHelper.FormatRemainingSize(product.Width, remainingLength)
            };
        }

        private async Task RemoveDirectSaleProductIfUnusedAsync(int productId)
        {
            var product = await _context.Tepisi
                .FirstOrDefaultAsync(item => item.Id == productId && item.CreatedForDirectSale);

            if (product == null)
            {
                return;
            }

            var hasSales = await _context.Prodaje
                .AnyAsync(sale => sale.TepihId == productId && !sale.Disabled);

            if (!hasSales)
            {
                _context.Tepisi.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<(bool Success, string Message)> ApplyDirectSaleReplacementAsync(
            Prodaja sale,
            Tepih placeholderProduct,
            Tepih replacementProduct,
            int? customWidth = null,
            int? customLength = null)
        {
            if (!placeholderProduct.CreatedForDirectSale)
            {
                return (false, "Zamjena je dozvoljena samo za proizvod kreiran za direktnu prodaju.");
            }

            if (replacementProduct.Disabled || replacementProduct.CreatedForDirectSale)
            {
                return (false, "Izabrani proizvod nije dostupan za zamjenu.");
            }

            int? effectiveWidth = replacementProduct.Width;
            int? effectiveLength = replacementProduct.Length;
            int? consumedLength = null;

            if (replacementProduct.PoMjeri)
            {
                if (!replacementProduct.Width.HasValue || !replacementProduct.Length.HasValue || !customLength.HasValue)
                {
                    return (false, "Za po mjeri proizvod unesite ispravne dimenzije.");
                }

                var remainingWidth = replacementProduct.Width.Value;
                var remainingLength = await GetRemainingPoMjeriLengthAsync(replacementProduct);
                var fixedWidth = remainingWidth;

                if (customLength.Value > remainingLength)
                {
                    return (false, "Unesena dužina ne može biti veća od preostale dužine.");
                }

                var maxAvailableQuantity = PoMjeriHelper.CalculateMaxAvailableQuantity(
                    remainingWidth,
                    remainingLength,
                    fixedWidth,
                    customLength.Value);

                if (maxAvailableQuantity < sale.Quantity)
                {
                    return (false, $"Moguće je naručiti najviše {maxAvailableQuantity} komada za dati proizvod.");
                }

                effectiveWidth = fixedWidth;
                effectiveLength = customLength.Value;
                consumedLength = PoMjeriHelper.CalculateConsumedLengthPerUnit(remainingWidth, fixedWidth, customLength.Value);
            }
            else if (CalculateAvailableQuantity(replacementProduct) < sale.Quantity)
            {
                return (false, $"Nema dovoljno raspoložive količine za proizvod {replacementProduct.Name} ({replacementProduct.ProductNumber}).");
            }

            var targetTotal = ResolveDirectSaleTargetTotal(sale, placeholderProduct);
            var replacementPrice = CalculateReplacementPrice(
                targetTotal,
                replacementProduct.PerM2,
                replacementProduct.PoMjeri,
                effectiveWidth,
                effectiveLength,
                sale.Quantity,
                sale.Rabat);

            sale.TepihId = replacementProduct.Id;
            sale.Price = replacementPrice;
            sale.CustomWidth = replacementProduct.PoMjeri ? effectiveWidth : null;
            sale.CustomLength = replacementProduct.PoMjeri ? effectiveLength : null;
            sale.ConsumedLength = replacementProduct.PoMjeri ? consumedLength : null;
            sale.DirectSaleOriginalTotal = targetTotal;

            if (!replacementProduct.PoMjeri)
            {
                replacementProduct.Quantity -= sale.Quantity;
            }

            await _context.SaveChangesAsync();
            await RemoveDirectSaleProductIfUnusedAsync(placeholderProduct.Id);
            return (true, string.Empty);
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var sales = await _context.Prodaje
                    .Include(sale => sale.Tepih)
                    .Where(sale => sale.Disabled != true && sale.Tepih.Disabled != true)
                    .AsNoTracking()
                    .ToListAsync();

                var query = sales
                    .GroupBy(sale => new
                    {
                        sale.CustomerFullName,
                        sale.VrijemeProdaje,
                        sale.Prodavac,
                        sale.PlannedPaymentType
                    })
                    .Select(group => new SummaryViewModel
                    {
                        CustomerFullName = group.Key.CustomerFullName,
                        VrijemeProdaje = group.Key.VrijemeProdaje,
                        Prodavac = group.Key.Prodavac,
                        PlannedPaymentType = group.Key.PlannedPaymentType,
                        TotalQuantity = group.Sum(entry => entry.Quantity),
                        M2Total = group.Sum(entry => GetSaleM2Total(entry.Tepih, entry) ?? 0m),
                        TotalPrice = group.Sum(entry => GetSaleTotalPrice(entry.Tepih, entry)),
                        IsDirectSaleGroup = group.Any(entry => entry.Tepih.CreatedForDirectSale)
                    })
                    .OrderByDescending(item => item.VrijemeProdaje)
                    .ToList();

                var referer = $"{Request.Scheme}://{Request.Host}{Request.Path}";
                ViewBag.ReturnFromDetails = referer;

                return View(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Index: Error loading grouped sales");
                ModelState.AddModelError("", "Došlo je do greške prilikom učitavanja podataka.");
                return StatusCode(500, "An error occurred while loading data! Please try again.");
            }
        }

        public async Task<IActionResult> AllSales()
        {
            try
            {
                var sales = await _context.Prodaje
                    .Include(sale => sale.Tepih)
                    .Where(sale => sale.Disabled != true && sale.Tepih.Disabled != true)
                    .AsNoTracking()
                    .ToListAsync();

                var query = sales.Select(prodaja => new ProdajaViewModel
                {
                    Id = prodaja.Id,
                    TepihId = prodaja.TepihId,
                    Name = prodaja.Tepih.Name,
                    ProductNumber = prodaja.Tepih.ProductNumber,
                    Model = prodaja.Tepih.Model,
                    Length = GetSaleLength(prodaja.Tepih, prodaja),
                    Width = GetSaleWidth(prodaja.Tepih, prodaja),
                    Color = prodaja.Tepih.Color,
                    Price = prodaja.Price,
                    PerM2 = prodaja.Tepih.PerM2,
                    PoMjeri = prodaja.Tepih.PoMjeri,
                    Quantity = prodaja.Quantity,
                    CustomerFullName = prodaja.CustomerFullName,
                    VrijemeProdaje = prodaja.VrijemeProdaje,
                    M2PerUnit = GetSaleM2PerUnit(prodaja.Tepih, prodaja),
                    M2Total = GetSaleM2Total(prodaja.Tepih, prodaja),
                    Rabat = prodaja.Rabat,
                    PriceTotal = GetSaleTotalPrice(prodaja.Tepih, prodaja),
                    IsDirectSaleProduct = prodaja.Tepih.CreatedForDirectSale
                });
                var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString() + Request.QueryString.Value.ToString();
                ViewBag.ReturnUrl = referer;
                return View(query);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - AllSales: Error loading all sales");
                ModelState.AddModelError("", "Došlo je do greške prilikom učitavanja podataka.");
                return StatusCode(500, "An error occurred while loading data! Please try again.");
            }
        }

        public async Task<IActionResult> Details(string customer, DateTime saleTime, string? returnFromDetails)
        {
            try
            {
                var sales = await _context.Prodaje
                    .Include(sale => sale.Tepih)
                    .Where(sale => sale.CustomerFullName == customer && sale.VrijemeProdaje == saleTime)
                    .AsNoTracking()
                    .ToListAsync();

                var query = sales.Select(prodaja => new SaleDetailsViewModel
                {
                    Id = prodaja.Id,
                    TepihId = prodaja.TepihId,
                    Name = prodaja.Tepih.Name,
                    ProductNumber = prodaja.Tepih.ProductNumber,
                    Model = prodaja.Tepih.Model,
                    Length = GetSaleLength(prodaja.Tepih, prodaja),
                    Width = GetSaleWidth(prodaja.Tepih, prodaja),
                    Color = prodaja.Tepih.Color,
                    Price = prodaja.Price,
                    PerM2 = prodaja.Tepih.PerM2,
                    PoMjeri = prodaja.Tepih.PoMjeri,
                    Quantity = prodaja.Quantity,
                    M2PerUnit = GetSaleM2PerUnit(prodaja.Tepih, prodaja),
                    M2Total = GetSaleM2Total(prodaja.Tepih, prodaja),
                    Disabled = prodaja.Tepih.Disabled,
                    Seller = prodaja.Prodavac,
                    Rabat = prodaja.Rabat,
                    PriceTotal = GetSaleTotalPrice(prodaja.Tepih, prodaja),
                    IsDirectSaleProduct = prodaja.Tepih.CreatedForDirectSale
                }).ToList();

                ViewBag.CustomerFullName = customer;
                ViewBag.SaleTime = saleTime.ToString("dd-MM-yyyy HH:mm:ss");
                ViewBag.SaleTimeIso = saleTime.ToString("o");
                var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString() + Request.QueryString.Value.ToString();
                ViewBag.ReturnFromDetails = returnFromDetails;
                ViewBag.ReturnUrl = referer;

                return View(query);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Details: Error loading details sales");
                ModelState.AddModelError("", "Došlo je do greške prilikom učitavanja podataka.");
                return StatusCode(500, "An error occurred while loading data! Please try again.");
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroupedSale(string customer, DateTime saleTime, string? returnFromDetails)
        {
            try
            {
                var sales = await _context.Prodaje
                    .Include(sale => sale.Tepih)
                    .Where(sale => sale.CustomerFullName == customer && sale.VrijemeProdaje == saleTime)
                    .ToListAsync();

                if (sales.Count == 0)
                {
                    _logger.LogError("Sales Controller - DeleteGroupedSale: Couldn't find grouped sale for customer {customer} at {saleTime}", customer, saleTime);
                    return NotFound("Grouped sale not found.");
                }

                var directSaleProductIds = new HashSet<int>();

                foreach (var sale in sales)
                {
                    var product = sale.Tepih;
                    if (product == null)
                    {
                        _logger.LogError("Sales Controller - DeleteGroupedSale: Couldn't find a product with an ID: {id}", sale.TepihId);
                        return NotFound("Product not found for this grouped sale.");
                    }

                    if (!product.PoMjeri && !product.CreatedForDirectSale)
                    {
                        product.Quantity += sale.Quantity;
                    }

                    if (product.CreatedForDirectSale)
                    {
                        directSaleProductIds.Add(product.Id);
                    }

                    _context.Prodaje.Remove(sale);
                }

                await _context.SaveChangesAsync();

                foreach (var productId in directSaleProductIds)
                {
                    await RemoveDirectSaleProductIfUnusedAsync(productId);
                }

                var splited = returnFromDetails?.Split("/") ?? Array.Empty<string>();

                if (splited.LastOrDefault() == "AllSales")
                {
                    return RedirectToAction("AllSales", "Sales");
                }

                if (splited.Length >= 2 && splited[splited.Length - 2] == "ShowBuys")
                {
                    var buyerId = splited.LastOrDefault();
                    return RedirectToAction("Index", "Buyer", new { id = buyerId });
                }

                return RedirectToAction("Index", "Sales");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - DeleteGroupedSale: Error deleting grouped sale for customer {customer} at {saleTime}", customer, saleTime);
                ModelState.AddModelError("", "Došlo je do greške prilikom brisanja prodaje.");
                return StatusCode(500, "An error occurred while deleting grouped sale! Please try again.");
            }
        }

        public async Task<IActionResult> Delete(int id, string returnUrl, string returnFromDetails)
        {
            try
            {
                var prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);

                if (prodaja == null)
                {
                    _logger.LogError("Sales Controller - Delete: Couldn't find a sale with an ID: {id}", id);
                    return NotFound("Sale not found!!! Please try with another one to see if the error keeps happening.");
                }

                ViewBag.ReturnUrl = returnUrl;
                ViewBag.ReturnFromDetails = returnFromDetails;

                return View(prodaja);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Delete: Error loading delete page for sale with ID: {id}", id);
                ModelState.AddModelError("", "Došlo je do greške prilikom učitavanja podataka o prodaji.");
                return StatusCode(500, "An error occurred while loading Delete page! Please try again.");
            }
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteProdaja(int id, string returnUrl, string CustomerFullName, DateTime VrijemeProdaje, string returnFromDetails)
        {
            try
            {
                var prodaja = await _salesRepository.GetByIdAsync(id);
                if (prodaja == null)
                {
                    _logger.LogError("Sales Controller - DeleteProdaja: Couldn't find a sale with an ID: {id}", id);
                    return NotFound("Sale not found!!! Please try with another one to see if the error keeps happening.");
                }

                var proizvod = await _tepihRepository.GetByIdAsync(prodaja.TepihId);
                if (proizvod == null)
                {
                    _logger.LogError("Sales Controller - Delete: Couldn't find a product with an ID: {id}", prodaja.TepihId);
                    return NotFound("Product not found for this sale!!!");
                }

                if (!proizvod.PoMjeri && !proizvod.CreatedForDirectSale)
                {
                    proizvod.Quantity += prodaja.Quantity;
                    _tepihRepository.Update(proizvod);
                }

                _salesRepository.Delete(prodaja);
                if (proizvod.CreatedForDirectSale)
                {
                    await RemoveDirectSaleProductIfUnusedAsync(proizvod.Id);
                }

                // Safe string parsing
                var splited = returnFromDetails?.Split("/") ?? Array.Empty<string>();

                if (splited.LastOrDefault() == "AllSales")
                {
                    return RedirectToAction("AllSales", "Sales");
                }

                if (splited.Length >= 2 && splited[splited.Length - 2] == "ShowBuys")
                {
                    var buyerId = splited.LastOrDefault();
                    return RedirectToAction("Index", "Buyer", new { id = buyerId });
                }

                return RedirectToAction("Details", new
                {
                    customer = CustomerFullName,
                    saleTime = VrijemeProdaje,
                    returnFromDetails = returnFromDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - DeleteProdaja: Error deleting sale with ID: {id}", id);
                ModelState.AddModelError("", "Došlo je do greške prilikom brisanja prodaji.");
                return StatusCode(500, "An error occurred while deleting a sale! Please try again.");
            }
        }

        public async Task<IActionResult> Edit(int id, string returnUrl, string returnFromDetails)
        {
            try
            {
                Prodaja prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);
                if (prodaja == null)
                {
                    _logger.LogError("Sales Controller - Edit: Couldn't find a sale with an ID: {id}", id);
                    return NotFound("Sale not found!!! Please try with another one to see if the error keeps happening.");
                };
                Tepih proizvod = await _tepihRepository.GetByIdAsyncNoTracking(prodaja.TepihId);
                if (proizvod == null)
                {
                    _logger.LogError("Sales Controller - Edit: Couldn't find a product with an ID: {id}", prodaja.TepihId);
                    return NotFound("Product not found for this sale!!!");
                }

                var prodajaVM = new EditProdajaViewModel
                {
                    Id = prodaja.Id,
                    TepihId = prodaja.TepihId,
                    CustomerFullName = prodaja.CustomerFullName,
                    Quantity = prodaja.Quantity,
                    VrijemeProdaje = prodaja.VrijemeProdaje,
                    Price = prodaja.Price,
                    DirectSaleOriginalTotal = prodaja.DirectSaleOriginalTotal,
                    TotalPrice = GetSaleTotalPrice(proizvod, prodaja),
                    PerM2 = proizvod.PerM2,
                    PoMjeri = proizvod.PoMjeri,
                    IsDirectSaleProduct = proizvod.CreatedForDirectSale,
                    M2Total = GetSaleM2Total(proizvod, prodaja),
                    Length = GetSaleLength(proizvod, prodaja),
                    Width = GetSaleWidth(proizvod, prodaja),
                    OriginalLength = proizvod.Length,
                    OriginalWidth = proizvod.Width,
                    ConsumedLength = prodaja.ConsumedLength,
                    Prodavac = prodaja.Prodavac,
                    PlannedPaymentType = prodaja.PlannedPaymentType,
                    Rabat = prodaja.Rabat,
                    ProductName = proizvod.Name,
                    ProductModel = proizvod.Model,
                    ProductColor = proizvod.Color
                };
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.ReturnFromDetails = returnFromDetails;

                return View(prodajaVM);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Edit: Error loading Edit page for a sale with ID: {id}", id);
                ModelState.AddModelError("", "Došlo je do greške prilikom ucitavanja Edit stranice za prodaju.");
                return StatusCode(500, "An error occurred while loading Edit Sale page! Please try again.");
            }

        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditProdajaViewModel prodajaVM, string returnUrl, string returnFromDetails)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .Select(entry => new
                    {
                        Field = entry.Key,
                        Errors = entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray()
                    })
                    .ToList();

                _logger.LogWarning(
                    "Sales Controller - Edit post: validation failed for sale {SaleId}. Errors: {@ValidationErrors}",
                    id,
                    validationErrors);

                ModelState.AddModelError("", "Editovanje prodaje nije uspjelo");
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.ReturnFromDetails = returnFromDetails;
                return View("Edit", prodajaVM);
            }
            try
            {
                var prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);
                if (prodaja == null)
                {
                    _logger.LogError("Sales Controller - Edit post: Couldn't find a sale with an ID: {id}", id);
                    return NotFound("Sale not found!!! Please try with another one to see if the error keeps happening.");
                };

                var proizvod = await _tepihRepository.GetByIdAsyncNoTracking(prodajaVM.TepihId);
                if (proizvod == null)
                {
                    _logger.LogError("Sales Controller - Edit: Couldn't find a product with an ID: {id}", prodaja.TepihId);
                    return NotFound("Product not found for this sale!!!");
                }

                var prodajaEdit = new Prodaja
                {
                    Id = id,
                    TepihId = prodajaVM.TepihId,
                    CustomerFullName = prodajaVM.CustomerFullName,
                    VrijemeProdaje = prodajaVM.VrijemeProdaje,
                    Quantity = prodajaVM.Quantity,
                    Price = prodajaVM.Price,
                    DirectSaleOriginalTotal = prodaja.DirectSaleOriginalTotal,
                    Prodavac = prodajaVM.Prodavac,
                    PlannedPaymentType = prodajaVM.PlannedPaymentType,
                    Rabat = prodajaVM.Rabat,
                    CustomWidth = prodaja.CustomWidth,
                    CustomLength = prodaja.CustomLength,
                    ConsumedLength = prodaja.ConsumedLength,
                    Disabled = prodaja.Disabled
                };

                _salesRepository.Update(prodajaEdit);

                // Adjust product stock if quantity changed
                if (!proizvod.PoMjeri && !proizvod.CreatedForDirectSale && prodajaVM.Quantity > prodaja.Quantity)
                {
                    proizvod.Quantity -= prodajaVM.Quantity - prodaja.Quantity;
                }
                else if (!proizvod.PoMjeri && !proizvod.CreatedForDirectSale && prodajaVM.Quantity < prodaja.Quantity)
                {
                    proizvod.Quantity += prodaja.Quantity - prodajaVM.Quantity;
                }
                _tepihRepository.Update(proizvod);

                TempData["SuccessMessage"] = "Prodaja je uspjesno izmijenjena.";

                return RedirectToAction(nameof(Edit), new
                {
                    id,
                    returnUrl = returnUrl,
                    returnFromDetails = returnFromDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Edit post: Error editing sale with an ID: {id}", id);
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.ReturnFromDetails = returnFromDetails;
                return View("Edit", prodajaVM);
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrepareReplacementSelection(int saleId, int productId)
        {
            try
            {
                var sale = await _context.Prodaje
                    .Include(item => item.Tepih)
                    .FirstOrDefaultAsync(item => item.Id == saleId && !item.Disabled);

                if (sale == null)
                {
                    return Json(new { success = false, message = "Prodaja nije pronađena." });
                }

                if (!sale.Tepih.CreatedForDirectSale)
                {
                    return Json(new { success = false, message = "Zamjena je dostupna samo za proizvod kreiran za direktnu prodaju." });
                }

                var replacementProduct = await _context.Tepisi
                    .FirstOrDefaultAsync(product => product.Id == productId && !product.Disabled && !product.CreatedForDirectSale);

                if (replacementProduct == null)
                {
                    return Json(new { success = false, message = "Proizvod nije pronađen." });
                }

                if (replacementProduct.PoMjeri)
                {
                    return Json(await BuildPoMjeriReplacementPromptAsync(replacementProduct));
                }

                var result = await ApplyDirectSaleReplacementAsync(sale, sale.Tepih, replacementProduct);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Proizvod je uspjesno zamijenjen.";
                }
                return Json(new
                {
                    success = result.Success,
                    message = result.Success ? "Proizvod je uspješno zamijenjen." : result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - PrepareReplacementSelection failed for sale {SaleId} and product {ProductId}", saleId, productId);
                return StatusCode(500, "An error occurred while replacing the product.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReplacePlaceholderProduct([FromBody] ReplaceSaleProductRequestViewModel model)
        {
            if (!ModelState.IsValid || !model.CustomLength.HasValue)
            {
                return Json(new { success = false, message = "Unesite ispravne dimenzije." });
            }

            try
            {
                var sale = await _context.Prodaje
                    .Include(item => item.Tepih)
                    .FirstOrDefaultAsync(item => item.Id == model.SaleId && !item.Disabled);

                if (sale == null)
                {
                    return Json(new { success = false, message = "Prodaja nije pronađena." });
                }

                if (!sale.Tepih.CreatedForDirectSale)
                {
                    return Json(new { success = false, message = "Zamjena je dostupna samo za proizvod kreiran za direktnu prodaju." });
                }

                var replacementProduct = await _context.Tepisi
                    .FirstOrDefaultAsync(product => product.Id == model.ProductId && !product.Disabled && !product.CreatedForDirectSale);

                if (replacementProduct == null)
                {
                    return Json(new { success = false, message = "Proizvod nije pronađen." });
                }

                var result = await ApplyDirectSaleReplacementAsync(
                    sale,
                    sale.Tepih,
                    replacementProduct,
                    model.CustomWidth,
                    model.CustomLength);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Proizvod je uspjesno zamijenjen.";
                }

                return Json(new
                {
                    success = result.Success,
                    message = result.Success ? "Proizvod je uspješno zamijenjen." : result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - ReplacePlaceholderProduct failed for sale {SaleId} and product {ProductId}", model.SaleId, model.ProductId);
                return StatusCode(500, "An error occurred while replacing the product.");
            }
        }

        [HttpGet]
        public IActionResult PerProducts(string? customerFullName)
        {
            var vm = new PerProductViewModel
            {
                CustomerFullName = customerFullName,
                SalesReport = new List<SalesReportViewModel>(),
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> PerProductsPartial(string? customerFullName, DateTime? startDate, DateTime? endDate, bool grouped)
        {
            List<SalesReportViewModel> salesReport;
            var endDateModified = new DateTime();
            if (endDate != null)
            {
                 endDateModified = endDate.Value.AddHours(23).AddMinutes(59).AddSeconds(59);
            }
            try
            {
                var sales = await (from sale in _context.Prodaje
                                   join product in _context.Tepisi on sale.TepihId equals product.Id
                                   where (string.IsNullOrEmpty(customerFullName) || sale.CustomerFullName == customerFullName)
                                         && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
                                         && (!endDate.HasValue || sale.VrijemeProdaje <= endDateModified)
                                         && product.Disabled != true
                                         && sale.Disabled != true
                                   select new
                                   {
                                       ProductId = product.Id,
                                       product.Name,
                                       product.Model,
                                       product.ProductNumber,
                                       product.Color,
                                       product.PerM2,
                                       product.PoMjeri,
                                       Length = sale.CustomLength ?? product.Length,
                                       Width = sale.CustomWidth ?? product.Width,
                                       sale.Quantity,
                                       sale.Price,
                                       sale.Rabat
                                   }).ToListAsync();

                if (grouped)
                {
                    salesReport = sales
                        .GroupBy(sale => new
                        {
                            sale.Name,
                            sale.Length,
                            sale.Width,
                            sale.ProductNumber,
                            sale.PerM2,
                            sale.PoMjeri
                        })
                        .Select(groupedSales => new SalesReportViewModel
                        {
                            Name = groupedSales.Key.Name,
                            Length = groupedSales.Key.Length,
                            Width = groupedSales.Key.Width,
                            Size = FormatSize(groupedSales.Key.Width, groupedSales.Key.Length),
                            ProductNumber = groupedSales.Key.ProductNumber,
                            Price = groupedSales.First().Price,
                            TotalQuantity = groupedSales.Sum(g => g.Quantity),
                            TotalPrice = groupedSales.Sum(g => GetSaleTotalPrice(g.PerM2, g.PoMjeri, g.Price, g.Width, g.Length, g.Quantity, g.Rabat)),
                            PerM2 = groupedSales.Key.PerM2,
                            PoMjeri = groupedSales.Key.PoMjeri
                        })
                        .ToList();
                }
                else
                {
                    salesReport = sales
                        .GroupBy(sale => new
                        {
                            sale.ProductId,
                            sale.Name,
                            sale.Model,
                            sale.ProductNumber,
                            sale.Length,
                            sale.Width,
                            sale.Color,
                            sale.PerM2,
                            sale.PoMjeri
                        })
                        .Select(groupedd => new SalesReportViewModel
                        {
                            ProductId = groupedd.Key.ProductId,
                            Name = groupedd.Key.Name,
                            Model = groupedd.Key.Model,
                            ProductNumber = groupedd.Key.ProductNumber,
                            Length = groupedd.Key.Length,
                            Width = groupedd.Key.Width,
                            Size = FormatSize(groupedd.Key.Width, groupedd.Key.Length),
                            Color = groupedd.Key.Color,
                            TotalQuantity = groupedd.Sum(g => g.Quantity),
                            TotalPrice = groupedd.Sum(g => GetSaleTotalPrice(g.PerM2, g.PoMjeri, g.Price, g.Width, g.Length, g.Quantity, g.Rabat)),
                            PerM2 = groupedd.Key.PerM2,
                            PoMjeri = groupedd.Key.PoMjeri
                        })
                        .ToList();
                }

                var vm = new PerProductViewModel
                {
                    CustomerFullName = customerFullName,
                    SalesReport = salesReport,
                    IsGrouped = grouped
                };

                if (startDate.HasValue && endDate.HasValue)
                {
                    vm.StartDate = DateOnly.FromDateTime(startDate.Value);
                    vm.EndDate = DateOnly.FromDateTime(endDate.Value);
                }

                return PartialView("_SalesReportTable", vm);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Per products partial: Error loading per products partial view!");
                return StatusCode(500, "An error occurred while loading Per Products table! Please try again.");
            }
        }

        [HttpGet]
        public IActionResult DetailsUngrouped(
            int productId,
            string productNumber,
            string name,
            string model,
            string size,
            int? length,
            int? width,
            decimal? m2PerProduct,
            string color,
            string? buyer,
            DateTime? startDate,
            DateTime? endDate)
        {
            try
            {
                var entriesQuery = _context.Prodaje.Where(p => p.TepihId == productId && p.Disabled != true);

                if (!string.IsNullOrEmpty(buyer))
                {
                    entriesQuery = entriesQuery.Where(p => p.CustomerFullName == buyer);
                }

                if (startDate.HasValue && endDate.HasValue)
                {
                    entriesQuery = entriesQuery.Where(p => p.VrijemeProdaje.Date >= startDate.Value.Date &&
                                                           p.VrijemeProdaje.Date <= endDate.Value.Date);
                }

                if (length.HasValue && width.HasValue)
                {
                    entriesQuery = entriesQuery.Where(p =>
                        (p.CustomLength ?? p.Tepih.Length) == length &&
                        (p.CustomWidth ?? p.Tepih.Width) == width);
                }

                var entries = entriesQuery.Select(p => new SalesEntryViewModel
                {
                    VrijemeProdaje = p.VrijemeProdaje,
                    CustomerFullName = p.CustomerFullName,
                    ProductId = p.TepihId,
                    ProductNumber = productNumber,
                    Name = name,
                    Model = model,
                    Color = color,
                    Length = p.CustomLength ?? p.Tepih.Length,
                    Width = p.CustomWidth ?? p.Tepih.Width,
                    Price = p.Price,
                    Quantity = p.Quantity,
                    PerM2 = p.Tepih.PerM2,
                    PoMjeri = p.Tepih.PoMjeri,
                    Rabat = p.Rabat
                }).ToList();

                var labelLength = length ?? entries.FirstOrDefault()?.Length;
                var labelWidth = width ?? entries.FirstOrDefault()?.Width;
                var labelSize = FormatSize(labelWidth, labelLength);
                var labelM2 = entries.FirstOrDefault()?.PerM2 == true
                    ? PoMjeriHelper.CalculateM2PerUnit(true, labelWidth, labelLength)
                    : m2PerProduct;

                var viewModel = new SalesEntryGroupViewModel
                {
                    Grouped = false,
                    Entries = entries,
                    StartDate = startDate,
                    EndDate = endDate,
                    Labels = new LabelsViewModel
                    {
                        ProductId = productId,
                        ProductNumber = productNumber,
                        Name = name,
                        Model = model,
                        Size = string.IsNullOrWhiteSpace(labelSize) ? size : labelSize,
                        M2PerProduct = labelM2,
                        Color = color,
                        CustName = buyer ?? null
                    }
                };

                return View("DetailsUngrouped", viewModel);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - DetailsUngrouped: Error loading DetailsUngrouped view!");
                return StatusCode(500, "An error occurred while loading Detailed (Ungrouped) sale page! Please try again.");
            }

        }

        [HttpGet]
        public async Task<IActionResult> DetailsGrouped(
            string name,
            int? length,
            int? width,
            string productNumber,
            string? buyer,
            DateTime? startDate,
            DateTime? endDate)
        {
            try
            {
                var query = _context.Prodaje
    .Where(p => p.Tepih.Name == name &&
                (p.CustomLength ?? p.Tepih.Length) == length &&
                (p.CustomWidth ?? p.Tepih.Width) == width &&
                p.Tepih.ProductNumber == productNumber &&
                p.Disabled != true);

                if (!string.IsNullOrEmpty(buyer))
                {
                    query = query.Where(p => p.CustomerFullName == buyer);
                }

                if (startDate.HasValue && endDate.HasValue)
                {
                    query = query.Where(p => p.VrijemeProdaje.Date >= startDate.Value.Date &&
                                             p.VrijemeProdaje.Date <= endDate.Value.Date);
                }

                var entries = await query.Select(p => new SalesEntryViewModel
                {
                    VrijemeProdaje = p.VrijemeProdaje,
                    CustomerFullName = p.CustomerFullName,
                    ProductId = p.TepihId,
                    Model = p.Tepih.Model,
                    Color = p.Tepih.Color,
                    Length = p.CustomLength ?? p.Tepih.Length,
                    Width = p.CustomWidth ?? p.Tepih.Width,
                    ProductNumber = p.Tepih.ProductNumber,
                    Name = p.Tepih.Name,
                    Price = p.Price,
                    Quantity = p.Quantity,
                    PerM2 = p.Tepih.PerM2,
                    PoMjeri = p.Tepih.PoMjeri,
                    Rabat = p.Rabat
                }).ToListAsync();

                var labelLength = length ?? entries.FirstOrDefault()?.Length;
                var labelWidth = width ?? entries.FirstOrDefault()?.Width;

                var vm = new SalesEntryGroupViewModel
                {
                    Grouped = true,
                    Entries = entries,
                    StartDate = startDate,
                    EndDate = endDate,
                    Labels = new LabelsViewModel
                    {
                        ProductNumber = productNumber,
                        Name = name,
                        Size = FormatSize(labelWidth, labelLength),
                        M2PerProduct = entries.FirstOrDefault()?.PerM2 == true
                            ? PoMjeriHelper.CalculateM2PerUnit(true, labelWidth, labelLength)
                            : null,
                        CustName = buyer ?? null
                    }
                };

                return View("DetailsGrouped", vm);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - DetailsGrouped: Error loading DetailsUngrouped view!");
                return StatusCode(500, "An error occurred while loading Detailed (Grouped) sale page! Please try again.");
            }
        }

        public IActionResult PerDay()
        {
            var vm = new PerDayViewModel
            {
                SalesReport = new List<PerDayCustomersPurchaseSummary>()
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> PerDay(DateTime date)
        {
            var nextDay = date.AddDays(1);

            try
            {
                var sales = await (from sale in _context.Prodaje
                                   join product in _context.Tepisi on sale.TepihId equals product.Id
                                   where sale.VrijemeProdaje >= date
                                         && sale.VrijemeProdaje < nextDay
                                         && sale.Disabled != true
                                         && product.Disabled != true
                                   select new
                                   {
                                       sale.CustomerFullName,
                                       product.PerM2,
                                       Length = sale.CustomLength ?? product.Length,
                                       Width = sale.CustomWidth ?? product.Width,
                                       sale.Quantity,
                                       sale.Price
                                   }).ToListAsync();

                var salesReport = sales
                    .GroupBy(sale => sale.CustomerFullName)
                    .Select(grouped => new PerDayCustomersPurchaseSummary
                    {
                        CustomerName = grouped.Key,
                        M2Total = grouped.Sum(g => PoMjeriHelper.CalculateM2Total(g.PerM2, g.Width, g.Length, g.Quantity) ?? 0m),
                        TotalQuantity = grouped.Sum(g => g.Quantity),
                        TotalSpent = grouped.Sum(g =>
                        {
                            var m2Total = PoMjeriHelper.CalculateM2Total(g.PerM2, g.Width, g.Length, g.Quantity);
                            return g.PerM2
                                ? (m2Total ?? 0m) * g.Price
                                : g.Price * g.Quantity;
                        })
                    })
                    .ToList();

                var total = salesReport.Sum(r => r.TotalSpent);
                var totalM2 = salesReport.Sum(r => r.M2Total);
                var totalQty = salesReport.Sum(r => r.TotalQuantity);

                var vm = new PerDayViewModel
                {
                    SalesReport = salesReport,
                    Date = DateOnly.FromDateTime(date),
                    TotalSpentSum = Math.Round(total, 2),
                    TotalM2 = Math.Round((decimal)totalM2, 2),
                    TotalQuantity = totalQty
                };

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView("_PerDayTablePartial", vm);
                }

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Per Day partial: Error loading Per day partial view!");
                return StatusCode(500, "An error occurred while loading Per-day table page! Please try again.");
            }
        }

        public IActionResult DisableOldYearView()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableOldYearRecords()
        {
            int targetYear = DateTime.Now.Year - 2;
            var yearStart = new DateTime(targetYear, 1, 1);
            var nextYearStart = new DateTime(targetYear + 1, 1, 1);

            try
            {
                var oldSales = await _context.Prodaje
                    .Where(p => p.VrijemeProdaje >= yearStart && p.VrijemeProdaje < nextYearStart)
                    .ToListAsync();

                var oldPayments = await _context.Placanja
                    .Where(p => p.PaymentTime >= yearStart && p.PaymentTime < nextYearStart)
                    .ToListAsync();

                oldSales.ForEach(p => p.Disabled = true);
                oldPayments.ForEach(p => p.Disabled = true);

                await _context.SaveChangesAsync();

                TempData["Message"] = @Inventar.Resources.Resource.DeleteRecordsConfirmation + $" {targetYear}.";
                return RedirectToAction("DisableOldYearView");
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - DisableOldYearRecords: Error disabling records from 2 year ago!");
                return StatusCode(500, "An error occurred while disabling records from 2 year ago! Please try again.");
            }
        }
    }
}
