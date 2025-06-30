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

namespace Inventar.Controllers
{
    [Authorize]
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
      //  public async Task<IActionResult> Index()
      //  {
      //      var prodaje = await _salesRepository.GetAll();
      //      var proizvodi = await _tepihRepository.GetAll();
      //      List<SummaryViewModel> query;

      //      query = await (from prodaja in _context.Prodaje
      //                     join proizvod in _context.Tepisi on prodaja.TepihId equals proizvod.Id
      //                     where prodaja.Disabled != true
      //                     group new { prodaja, proizvod } by new { prodaja.CustomerFullName, prodaja.VrijemeProdaje, prodaja.Prodavac, prodaja.PlannedPaymentType } into gr
      //                     select new SummaryViewModel
      //                     {
      //                         CustomerFullName = gr.Key.CustomerFullName,
      //                         VrijemeProdaje = gr.Key.VrijemeProdaje,
      //                         Prodavac = gr.Key.Prodavac,
      //                         PlannedPaymentType = gr.Key.PlannedPaymentType,
      //                         TotalQuantity = gr.Sum(g => g.prodaja.Quantity),
      //                         M2Total = gr.Sum(g => g.proizvod.PerM2 ? ((decimal)(g.proizvod.Length * g.proizvod.Width) / 10000) * g.prodaja.Quantity : 0),
      //                         TotalPrice = gr.Sum(g => g.proizvod.PerM2
      //? g.prodaja.Price * (((decimal)((int)g.proizvod.Length * (int)g.proizvod.Width) / 10000) * g.prodaja.Quantity)
      //: g.prodaja.Price * g.prodaja.Quantity)
      //                     }).ToListAsync();

      //      var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString();
      //      ViewBag.ReturnFromDetails = referer;
      //      return View(query);
      //  }

        public async Task<IActionResult> Index()
        {
            try
            {
                var prodaje = await _salesRepository.GetAll();
                var proizvodi = await _tepihRepository.GetAll();

                var query = await (from prodaja in _context.Prodaje
                                   join proizvod in _context.Tepisi on prodaja.TepihId equals proizvod.Id
                                   where prodaja.Disabled != true
                                   group new { prodaja, proizvod } by new
                                   {
                                       prodaja.CustomerFullName,
                                       prodaja.VrijemeProdaje,
                                       prodaja.Prodavac,
                                       prodaja.PlannedPaymentType
                                   } into gr
                                   select new SummaryViewModel
                                   {
                                       CustomerFullName = gr.Key.CustomerFullName,
                                       VrijemeProdaje = gr.Key.VrijemeProdaje,
                                       Prodavac = gr.Key.Prodavac,
                                       PlannedPaymentType = gr.Key.PlannedPaymentType,
                                       TotalQuantity = gr.Sum(g => g.prodaja.Quantity),
                                       M2Total = gr.Sum(g => g.proizvod.PerM2
                                           ? ((decimal)(g.proizvod.Length * g.proizvod.Width) / 10000) * g.prodaja.Quantity
                                           : 0),
                                       TotalPrice = gr.Sum(g => g.proizvod.PerM2
                                           ? g.prodaja.Price * (((decimal)(g.proizvod.Length * g.proizvod.Width) / 10000) * g.prodaja.Quantity)
                                           : g.prodaja.Price * g.prodaja.Quantity)
                                   }).ToListAsync();

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
                IEnumerable<Prodaja> prodaje = await _salesRepository.GetAll();
                IEnumerable<Tepih> proizvodi = await _tepihRepository.GetAll();

                var query = (from prodaja in prodaje
                             join proizvod in proizvodi on prodaja.TepihId equals proizvod.Id
                             where proizvod.Disabled != true && prodaja.Disabled != true
                             select new ProdajaViewModel
                             {
                                 Id = prodaja.Id,
                                 TepihId = prodaja.TepihId,
                                 Name = proizvod.Name,
                                 ProductNumber = proizvod.ProductNumber,
                                 Model = proizvod.Model,
                                 Length = proizvod.Length,
                                 Width = proizvod.Width,
                                 Color = proizvod.Color,
                                 Price = prodaja.Price,
                                 PerM2 = proizvod.PerM2,
                                 Quantity = prodaja.Quantity,
                                 CustomerFullName = prodaja.CustomerFullName,
                                 VrijemeProdaje = prodaja.VrijemeProdaje,
                                 M2PerUnit = proizvod.PerM2 ? (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000 : null,
                                 M2Total = proizvod.PerM2 ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * prodaja.Quantity : null,
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
                var prodaje = await _salesRepository.GetAll();
                var proizvodi = await _tepihRepository.GetAll();

                var query = (from prodaja in prodaje
                             join proizvod in proizvodi on prodaja.TepihId equals proizvod.Id
                             where prodaja.CustomerFullName == customer && prodaja.VrijemeProdaje == saleTime
                             select new SaleDetailsViewModel
                             {
                                 Id = prodaja.Id,
                                 TepihId = prodaja.TepihId,
                                 Name = proizvod.Name,
                                 ProductNumber = proizvod.ProductNumber,
                                 Model = proizvod.Model,
                                 Length = proizvod.Length,
                                 Width = proizvod.Width,
                                 Color = proizvod.Color,
                                 Price = prodaja.Price,
                                 PerM2 = proizvod.PerM2,
                                 Quantity = prodaja.Quantity,
                                 M2PerUnit = proizvod.PerM2 ? (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000 : null,
                                 M2Total = proizvod.PerM2 ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * prodaja.Quantity : null,
                                 Disabled = proizvod.Disabled,
                                 Seller = prodaja.Prodavac //dodato
                             }).ToList();

                ViewBag.CustomerFullName = customer;
                ViewBag.SaleTime = saleTime.ToString("dd-MM-yyyy HH:mm:ss");
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

        //public async Task<IActionResult> Delete(int id, string returnUrl, string returnFromDetails)
        //{
        //    Prodaja prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);
        //    ViewBag.ReturnUrl = returnUrl;
        //    ViewBag.ReturnFromDetails = returnFromDetails;

        //    return View(prodaja);
        //}

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


        //[HttpPost, ActionName("Delete")]
        //public async Task<IActionResult> DeleteProdaja(int id, string returnUrl, string CustomerFullName, DateTime VrijemeProdaje, string returnFromDetails)
        //{
        //    Prodaja prodaja = await _salesRepository.GetByIdAsync(id);
        //    Tepih proizvod = await _tepihRepository.GetByIdAsync(prodaja.TepihId);
        //    proizvod.Quantity += prodaja.Quantity;
        //    _tepihRepository.Update(proizvod);
        //    _salesRepository.Delete(prodaja);

        //    var splitedReturnFromDetails = returnFromDetails.Split("/");
        //    if (splitedReturnFromDetails.Last() == "AllSales")
        //    {
        //        return RedirectToAction("AllSales", "Sales");
        //    }
        //    if (splitedReturnFromDetails[splitedReturnFromDetails.Count() - 2] == "ShowBuys")
        //    {
        //        return RedirectToAction("Index", "Buyer", new { id = splitedReturnFromDetails.Last() });
        //    }
        //    return RedirectToAction("Details", new
        //    {
        //        customer = CustomerFullName,
        //        saleTime = VrijemeProdaje,
        //        returnFromDetails = returnFromDetails
        //    });
        //}

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

                // Revert product quantity
                proizvod.Quantity += prodaja.Quantity;
                _tepihRepository.Update(proizvod);

                // Delete the sale
                _salesRepository.Delete(prodaja);

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

                // Default redirection to Details
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
                    TepihId = prodaja.TepihId,
                    CustomerFullName = prodaja.CustomerFullName,
                    Quantity = prodaja.Quantity,
                    VrijemeProdaje = prodaja.VrijemeProdaje,
                    Price = prodaja.Price,
                    PerM2 = proizvod.PerM2,
                    M2Total = proizvod.PerM2 ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * prodaja.Quantity : null,
                    Length = proizvod.Length,
                    Width = proizvod.Width,
                    Prodavac = prodaja.Prodavac,
                    PlannedPaymentType = prodaja.PlannedPaymentType,
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

        //[HttpPost]
        //public async Task<IActionResult> Edit(int id, EditProdajaViewModel prodajaVM, string returnUrl, string returnFromDetails)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        ModelState.AddModelError("", "Editovanje prodaje nije uspjelo");
        //        return View("Edit", prodajaVM);
        //    }

        //    var prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);

        //    if (prodaja != null)
        //    {
        //        var proizvod = await _tepihRepository.GetByIdAsyncNoTracking(prodajaVM.TepihId);

        //        var prodajaEdit = new Prodaja
        //        {
        //            Id = id,
        //            TepihId = prodajaVM.TepihId,
        //            CustomerFullName = prodajaVM.CustomerFullName,
        //            VrijemeProdaje = prodajaVM.VrijemeProdaje,
        //            Quantity = prodajaVM.Quantity,
        //            Price = prodajaVM.Price,
        //            Prodavac = prodajaVM.Prodavac,
        //        };

        //        _salesRepository.Update(prodajaEdit);

        //        if (prodajaVM.Quantity > prodaja.Quantity)
        //        {
        //            proizvod.Quantity -= prodajaVM.Quantity - prodaja.Quantity;
        //            _tepihRepository.Update(proizvod);
        //        }
        //        if (prodajaVM.Quantity < prodaja.Quantity)
        //        {
        //            proizvod.Quantity += prodaja.Quantity - prodajaVM.Quantity;
        //            _tepihRepository.Update(proizvod);
        //        }
        //        var splitedReturnFromDetails = returnFromDetails.Split("/");
        //        if (splitedReturnFromDetails.Last() == "AllSales")
        //        {
        //            return RedirectToAction("AllSales", "Sales");
        //        }
        //        if (splitedReturnFromDetails[splitedReturnFromDetails.Count() - 2] == "ShowBuys")
        //        {
        //            return RedirectToAction("ShowBuys", "Buyer", new { id = splitedReturnFromDetails.Last() });
        //        }
        //        return RedirectToAction("Details", new
        //        {
        //            customer = prodajaVM.CustomerFullName,
        //            saleTime = prodajaVM.VrijemeProdaje,
        //            returnFromDetails = returnFromDetails
        //        });
        //    }
        //    else
        //    {
        //        return View(prodajaVM);
        //    }
        //}

        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditProdajaViewModel prodajaVM, string returnUrl, string returnFromDetails)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje prodaje nije uspjelo");
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
                    Prodavac = prodajaVM.Prodavac,
                    PlannedPaymentType = prodajaVM.PlannedPaymentType
                };

                _salesRepository.Update(prodajaEdit);

                // Adjust product stock if quantity changed
                if (prodajaVM.Quantity > prodaja.Quantity)
                {
                    proizvod.Quantity -= prodajaVM.Quantity - prodaja.Quantity;
                }
                else if (prodajaVM.Quantity < prodaja.Quantity)
                {
                    proizvod.Quantity += prodaja.Quantity - prodajaVM.Quantity;
                }
                _tepihRepository.Update(proizvod);

                // Safe redirection logic
                var splitedReturnFromDetails = returnFromDetails?.Split("/") ?? Array.Empty<string>();
                if (splitedReturnFromDetails.Last() == "AllSales")
                {
                    return RedirectToAction("AllSales", "Sales");
                }
                if (splitedReturnFromDetails[splitedReturnFromDetails.Count() - 2] == "ShowBuys")
                {
                    return RedirectToAction("ShowBuys", "Buyer", new { id = splitedReturnFromDetails.Last() });
                }

                return RedirectToAction("Details", new
                {
                    customer = prodajaVM.CustomerFullName,
                    saleTime = prodajaVM.VrijemeProdaje,
                    returnFromDetails = returnFromDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sales Controller - Edit post: Error editing sale with an ID: {id}", id);
                return View("Edit", prodajaVM);
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
                if (grouped)
                {
                    salesReport = await (from sale in _context.Prodaje
                                         join product in _context.Tepisi on sale.TepihId equals product.Id
                                         where (string.IsNullOrEmpty(customerFullName) || sale.CustomerFullName == customerFullName)
                                               && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
                                               && (!endDate.HasValue || sale.VrijemeProdaje <= endDateModified)
                                               && product.Disabled != true
                                               && sale.Disabled != true
                                         group new { sale, product } by new
                                         {
                                             product.Name,
                                             product.Length,
                                             product.Width,
                                             product.ProductNumber,
                                         } into groupedSales
                                         select new SalesReportViewModel
                                         {
                                             Name = groupedSales.Key.Name,
                                             Length = groupedSales.Key.Length,
                                             Width = groupedSales.Key.Width,
                                             Size = groupedSales.Key.Width != null && groupedSales.Key.Length != null ? $"{groupedSales.Key.Width}X{groupedSales.Key.Length}" : "",
                                             ProductNumber = groupedSales.Key.ProductNumber,
                                             Price = groupedSales.First().sale.Price,
                                             TotalQuantity = groupedSales.Sum(g => g.sale.Quantity),
                                             TotalPrice = groupedSales.Sum(g => g.product.PerM2
                                                 ? g.sale.Price * (((decimal)((int)g.product.Length * (int)g.product.Width) / 10000) * g.sale.Quantity)
                                                 : g.sale.Price * g.sale.Quantity),
                                             PerM2 = groupedSales.First().product.PerM2
                                         }).ToListAsync();
                }
                else
                {
                    salesReport = await (from sale in _context.Prodaje
                                         join product in _context.Tepisi on sale.TepihId equals product.Id
                                         where (string.IsNullOrEmpty(customerFullName) || sale.CustomerFullName == customerFullName)
                                               && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
                                               && (!endDate.HasValue || sale.VrijemeProdaje <= endDateModified)
                                               && product.Disabled != true
                                               && sale.Disabled != true
                                         group new { sale, product } by new
                                         {
                                             product.Id,
                                             product.Name,
                                             product.Model,
                                             product.Length,
                                             product.Width,
                                             product.Color,
                                             product.PerM2,
                                             product.ProductNumber,
                                         } into groupedd
                                         select new SalesReportViewModel
                                         {
                                             ProductId = groupedd.Key.Id,
                                             Name = groupedd.Key.Name,
                                             Model = groupedd.Key.Model,
                                             ProductNumber = groupedd.Key.ProductNumber,
                                             Length = groupedd.Key.Length,
                                             Width = groupedd.Key.Width,
                                             Size = groupedd.Key.Width != null && groupedd.Key.Length != null ? $"{groupedd.Key.Width}X{groupedd.Key.Length}" : "",
                                             Color = groupedd.Key.Color,
                                             TotalQuantity = groupedd.Sum(g => g.sale.Quantity),
                                             TotalPrice = groupedd.Sum(g => g.product.PerM2
                                                 ? g.sale.Price * (((decimal)((int)g.product.Length * (int)g.product.Width) / 10000) * g.sale.Quantity)
                                                 : g.sale.Price * g.sale.Quantity),
                                             PerM2 = groupedd.Key.PerM2
                                         }).ToListAsync();
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

                var entries = entriesQuery.Select(p => new SalesEntryViewModel
                {
                    VrijemeProdaje = p.VrijemeProdaje,
                    CustomerFullName = p.CustomerFullName,
                    ProductId = p.TepihId,
                    ProductNumber = productNumber,
                    Name = name,
                    Model = model,
                    Color = color,
                    Length = p.Tepih.Length,
                    Width = p.Tepih.Width,
                    Price = p.Price,
                    Quantity = p.Quantity,
                    PerM2 = p.Tepih.PerM2
                }).ToList();

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
                        Size = size,
                        M2PerProduct = m2PerProduct,
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
                p.Tepih.Length == length &&
                p.Tepih.Width == width &&
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
                    Length = p.Tepih.Length,
                    Width = p.Tepih.Width,
                    ProductNumber = p.Tepih.ProductNumber,
                    Name = p.Tepih.Name,
                    Price = p.Price,
                    Quantity = p.Quantity,
                    PerM2 = p.Tepih.PerM2
                }).ToListAsync();

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
                        Size = width != null && length != null ? $"{width}X{length}" : "",
                        M2PerProduct = width != null && length != null
                            ? Math.Round(((int)length * (int)width) / 10000m, 2)
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
                var query = from sale in _context.Prodaje
                            join product in _context.Tepisi on sale.TepihId equals product.Id
                            where sale.VrijemeProdaje >= date && sale.VrijemeProdaje < nextDay
                            group new { sale, product } by new { sale.CustomerFullName } into grouped
                            select new PerDayCustomersPurchaseSummary
                            {
                                CustomerName = grouped.Key.CustomerFullName,
                                M2Total = grouped.Sum(g => g.product.PerM2 ? ((decimal)(g.product.Length * g.product.Width) / 10000) * g.sale.Quantity : 0),
                                TotalQuantity = grouped.Sum(g => g.sale.Quantity),
                                TotalSpent = grouped.Sum(g =>
                                    g.product.PerM2
                                    ? ((decimal)(g.product.Length * g.product.Width) / 10000) * g.sale.Quantity * g.sale.Price
                                    : g.sale.Price * g.sale.Quantity)
                            };

                var salesReport = await query.ToListAsync();
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
