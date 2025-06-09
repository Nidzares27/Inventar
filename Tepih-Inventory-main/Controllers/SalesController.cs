using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Repository;
using Inventar.Services;
using Inventar.Utils;
using Inventar.ViewModels.Sales;
using Inventar.ViewModels.Shared;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static iText.Svg.SvgConstants;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.StyledXmlParser.Css.Media;
using Microsoft.AspNetCore.Authorization;

namespace Inventar.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISalesRepository _salesRepository;
        private readonly ITepihRepository _tepihRepository;

        public SalesController(ApplicationDbContext context, ISalesRepository salesRepository, ITepihRepository tepihRepository)
        {
            this._context = context;
            this._salesRepository = salesRepository;
            this._tepihRepository = tepihRepository;
        }
        public async Task<IActionResult> Index()
        {
            var prodaje = await _salesRepository.GetAll();
            var proizvodi = await _tepihRepository.GetAll();
            List<SummaryViewModel> query;

            query = await (from prodaja in _context.Prodaje
                         join proizvod in _context.Tepisi on prodaja.TepihId equals proizvod.Id
                         group new {prodaja, proizvod } by new { prodaja.CustomerFullName, prodaja.VrijemeProdaje, prodaja.Prodavac, prodaja.PlannedPaymentType } into gr
                         select new SummaryViewModel
                         {
                             CustomerFullName = gr.Key.CustomerFullName,
                             VrijemeProdaje = gr.Key.VrijemeProdaje,
                             Prodavac = gr.Key.Prodavac,
                             PlannedPaymentType = gr.Key.PlannedPaymentType,
                             TotalQuantity = gr.Sum(g => g.prodaja.Quantity),
                             M2Total = gr.Sum(g => g.proizvod.PerM2 ? ((decimal)(g.proizvod.Length * g.proizvod.Width) / 10000) * g.prodaja.Quantity : 0),
                             TotalPrice = gr.Sum(g => g.proizvod.PerM2
    ? g.prodaja.Price * (((decimal)((int)g.proizvod.Length * (int)g.proizvod.Width) / 10000) * g.prodaja.Quantity)
    : g.prodaja.Price * g.prodaja.Quantity)
                         }).ToListAsync();

            var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString();
            ViewBag.ReturnFromDetails = referer;
            return View(query);
        }

        public async Task<IActionResult> AllSales()
        {
            IEnumerable<Prodaja> prodaje = await _salesRepository.GetAll();
            IEnumerable<Tepih> proizvodi = await _tepihRepository.GetAll();

            var query = (from prodaja in prodaje
                         join proizvod in proizvodi on prodaja.TepihId equals proizvod.Id
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

        public async Task<IActionResult> Details(string customer, DateTime saleTime, string? returnFromDetails)
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
                         }).ToList();

            ViewBag.CustomerFullName = customer;
            ViewBag.SaleTime = saleTime.ToString("dd-MM-yyyy HH:mm:ss");
            var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString() + Request.QueryString.Value.ToString();
            ViewBag.ReturnFromDetails = returnFromDetails;
            ViewBag.ReturnUrl = referer;

            return View(query);
        }

        public async Task<IActionResult> Delete(int id, string returnUrl, string returnFromDetails)
        {
            Prodaja prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ReturnFromDetails = returnFromDetails;

            return View(prodaja);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteProdaja(int id, string returnUrl, string CustomerFullName, DateTime VrijemeProdaje, string returnFromDetails)
        {
            Prodaja prodaja = await _salesRepository.GetByIdAsync(id);
            Tepih proizvod = await _tepihRepository.GetByIdAsync(prodaja.TepihId);
            proizvod.Quantity += prodaja.Quantity;
            _tepihRepository.Update(proizvod);
            _salesRepository.Delete(prodaja);

            var splitedReturnFromDetails = returnFromDetails.Split("/");
            if (splitedReturnFromDetails.Last() == "AllSales")
            {
                return RedirectToAction("AllSales", "Sales");
            }
            if (splitedReturnFromDetails[splitedReturnFromDetails.Count() - 2] == "ShowBuys")
            {
                return RedirectToAction("Index", "Buyer", new { id = splitedReturnFromDetails.Last() });
            }
            return RedirectToAction("Details", new
            {
                customer = CustomerFullName,
                saleTime = VrijemeProdaje,
                returnFromDetails = returnFromDetails
            });
        }

        public async Task<IActionResult> Edit(int id, string returnUrl, string returnFromDetails)
        {
            Prodaja prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);
            Tepih proizvod = await _tepihRepository.GetByIdAsyncNoTracking(prodaja.TepihId);
            if (prodaja == null) return View("Error");
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
                Prodavac = prodaja.Prodavac
            };
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ReturnFromDetails = returnFromDetails;

            return View(prodajaVM);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditProdajaViewModel prodajaVM, string returnUrl, string returnFromDetails)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje prodaje nije uspjelo");
                return View("Edit", prodajaVM);
            }

            var prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);

            if (prodaja != null)
            {
                var proizvod = await _tepihRepository.GetByIdAsyncNoTracking(prodajaVM.TepihId);

                var prodajaEdit = new Prodaja
                {
                    Id = id,
                    TepihId = prodajaVM.TepihId,
                    CustomerFullName = prodajaVM.CustomerFullName,
                    VrijemeProdaje = prodajaVM.VrijemeProdaje,
                    Quantity = prodajaVM.Quantity,
                    Price = prodajaVM.Price,
                    Prodavac = prodajaVM.Prodavac,
                };

                _salesRepository.Update(prodajaEdit);

                if (prodajaVM.Quantity > prodaja.Quantity )
                {
                    proizvod.Quantity -= prodajaVM.Quantity - prodaja.Quantity;
                    _tepihRepository.Update(proizvod);
                }
                if (prodajaVM.Quantity < prodaja.Quantity)
                {
                    proizvod.Quantity += prodaja.Quantity - prodajaVM.Quantity;
                    _tepihRepository.Update(proizvod);
                }
                var splitedReturnFromDetails = returnFromDetails.Split("/");
                if (splitedReturnFromDetails.Last() == "AllSales")
                {
                    return RedirectToAction("AllSales", "Sales");
                }
                if (splitedReturnFromDetails[splitedReturnFromDetails.Count() - 2] == "ShowBuys")
                {
                    return RedirectToAction("ShowBuys", "Buyer", new {id = splitedReturnFromDetails.Last()});
                }
                return RedirectToAction("Details", new { customer = prodajaVM.CustomerFullName, saleTime = prodajaVM.VrijemeProdaje,
                    returnFromDetails = returnFromDetails
                });
            }
            else
            {
                return View(prodajaVM);
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
            if (grouped)
            {
                salesReport = await (from sale in _context.Prodaje
                                     join product in _context.Tepisi on sale.TepihId equals product.Id
                                     where (string.IsNullOrEmpty(customerFullName) || sale.CustomerFullName == customerFullName)
                                           && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
                                           && (!endDate.HasValue || sale.VrijemeProdaje <= endDateModified)
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
            var entriesQuery = _context.Prodaje.Where(p => p.TepihId == productId);

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
            var query = _context.Prodaje
                .Where(p => p.Tepih.Name == name &&
                            p.Tepih.Length == length &&
                            p.Tepih.Width == width &&
                            p.Tepih.ProductNumber == productNumber);

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

    }
}
