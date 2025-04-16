using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Repository;
using Inventar.Services;
using Inventar.Utils;
using Inventar.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace Inventar.Controllers
{
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
                             M2PerUnit = (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000,
                             M2Total = ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * prodaja.Quantity,

                         });
            return View(query);
        }

        public async Task<IActionResult> Delete(int id, string returnUrl)
        {
            Prodaja prodaja = await _salesRepository.GetByIdAsyncNoTracking(id);
            ViewBag.ReturnUrl = returnUrl; // Store returnUrl in ViewBag
            return View(prodaja);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteProdaja(int id, string returnUrl)
        {
            Prodaja prodaja = await _salesRepository.GetByIdAsync(id);
            Tepih proizvod = await _tepihRepository.GetByIdAsync(prodaja.TepihId);
            proizvod.Quantity += prodaja.Quantity;
            _tepihRepository.Update(proizvod);
            _salesRepository.Delete(prodaja);

            return Redirect(returnUrl ?? "/Home/Index"); // Redirect back to the previous page
        }

        public async Task<IActionResult> Edit(int id, string returnUrl)
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
                M2Total = ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * prodaja.Quantity,
                Length = proizvod.Length,
                Width = proizvod.Width,
            };
            ViewBag.ReturnUrl = returnUrl; // Store returnUrl in ViewBag

            return View(prodajaVM);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditProdajaViewModel prodajaVM, string returnUrl)
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
                return Redirect(returnUrl ?? "/Home/Index"); // Redirect back to the previous page
            }
            else
            {
                return View(prodajaVM);
            }
        }

        //public async Task<IActionResult> PerProducts(string? customerFullName)
        //{
        //    var vm = new PerProductViewModel();
        //    vm.CustomerFullName = customerFullName;
        //    vm.SalesReport = new List<SalesReportViewModel>();
        //    return View(vm);
        //}
        //[HttpPost]
        //public async Task<IActionResult> PerProducts(string? customerFullName, DateTime? startDate, DateTime? endDate)
        //{
        //    var query = from sale in _context.Prodaje
        //                join product in _context.Tepisi
        //                on sale.TepihId equals product.Id
        //                where (string.IsNullOrEmpty(customerFullName) || sale.CustomerFullName == customerFullName)
        //                && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
        //                && (!endDate.HasValue || sale.VrijemeProdaje <= endDate.Value)
        //                group new { sale, product } by new
        //                {
        //                    product.Id,
        //                    product.Name,
        //                    product.Model,
        //                    product.Length,
        //                    product.Width,
        //                    product.Color
        //                }
        //    into grouped
        //                select new SalesReportViewModel
        //                {
        //                    ProductId = grouped.Key.Id,
        //                    Name = grouped.Key.Name,
        //                    Model = grouped.Key.Model,
        //                    Length = grouped.Key.Length,
        //                    Width = grouped.Key.Width,
        //                    Size = $"{grouped.Key.Length} X {grouped.Key.Width}",
        //                    Color = grouped.Key.Color,
        //                    TotalQuantity = grouped.Sum(g => g.sale.Quantity),
        //                    TotalPrice = grouped.Sum(g => g.sale.Price)
        //                };

        //    var salesReport = await query.ToListAsync();
        //    var vm = new PerProductViewModel();
        //    vm.CustomerFullName = customerFullName;
        //    if(startDate.HasValue && endDate.HasValue)
        //    {
        //        vm.StartDate = DateOnly.FromDateTime((DateTime)startDate);
        //        vm.EndDate = DateOnly.FromDateTime((DateTime)endDate);
        //    }
        //    vm.SalesReport = salesReport;
        //    return View(vm);
        //}

        //public async Task<IActionResult> PerProducts(string? customerFullName)
        //{
        //    var vm = new PerProductViewModel();

        //    vm.CustomerFullName = customerFullName ?? TempData["customerFullName"] as string;

        //    DateTime? startDate = null;
        //    DateTime? endDate = null;

        //    startDate = TempData["startDate"] as DateTime?;
        //    endDate = TempData["endDate"] as DateTime?;

        //    if (startDate.HasValue)
        //        vm.StartDate = DateOnly.FromDateTime(startDate.Value);

        //    if (endDate.HasValue)
        //        vm.EndDate = DateOnly.FromDateTime(endDate.Value);

        //    var query = from sale in _context.Prodaje
        //                join product in _context.Tepisi
        //                    on sale.TepihId equals product.Id
        //                where (string.IsNullOrEmpty(vm.CustomerFullName) || sale.CustomerFullName == vm.CustomerFullName)
        //                      && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
        //                      && (!endDate.HasValue || sale.VrijemeProdaje <= endDate.Value)
        //                group new { sale, product } by new
        //                {
        //                    product.Id,
        //                    product.Name,
        //                    product.Model,
        //                    product.Length,
        //                    product.Width,
        //                    product.Color
        //                }
        //        into grouped
        //                select new SalesReportViewModel
        //                {
        //                    ProductId = grouped.Key.Id,
        //                    Name = grouped.Key.Name,
        //                    Model = grouped.Key.Model,
        //                    Length = grouped.Key.Length,
        //                    Width = grouped.Key.Width,
        //                    Size = $"{grouped.Key.Length} X {grouped.Key.Width}",
        //                    Color = grouped.Key.Color,
        //                    TotalQuantity = grouped.Sum(g => g.sale.Quantity),
        //                    TotalPrice = grouped.Sum(g => g.sale.Price)
        //                };

        //    vm.SalesReport = await query.ToListAsync();

        //    return View(vm);
        //}
        //[HttpPost]
        //public async Task<IActionResult> PerProducts(string? customerFullName, DateTime? startDate, DateTime? endDate)
        //{
        //    // Save filter parameters to TempData
        //    TempData["customerFullName"] = customerFullName;
        //    TempData["startDate"] = startDate?.ToString("o"); // ISO 8601 format
        //    TempData["endDate"] = endDate?.ToString("o");

        //    return RedirectToAction("PerProducts");
        //}

        [HttpGet]
        public IActionResult PerProducts(string? customerFullName)
        {
            var vm = new PerProductViewModel
            {
                CustomerFullName = customerFullName,
                SalesReport = new List<SalesReportViewModel>()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> PerProductsPartial(string? customerFullName, DateTime? startDate, DateTime? endDate)
        {
            var query = from sale in _context.Prodaje
                        join product in _context.Tepisi on sale.TepihId equals product.Id
                        where (string.IsNullOrEmpty(customerFullName) || sale.CustomerFullName == customerFullName)
                              && (!startDate.HasValue || sale.VrijemeProdaje >= startDate.Value)
                              && (!endDate.HasValue || sale.VrijemeProdaje <= endDate.Value)
                        group new { sale, product } by new
                        {
                            product.Id,
                            product.Name,
                            product.Model,
                            product.Length,
                            product.Width,
                            product.Color
                        } into grouped
                        select new SalesReportViewModel
                        {
                            ProductId = grouped.Key.Id,
                            Name = grouped.Key.Name,
                            Model = grouped.Key.Model,
                            Length = grouped.Key.Length,
                            Width = grouped.Key.Width,
                            Size = $"{grouped.Key.Length} X {grouped.Key.Width}",
                            Color = grouped.Key.Color,
                            TotalQuantity = grouped.Sum(g => g.sale.Quantity),
                            TotalPrice = grouped.Sum(g => g.sale.Price)
                        };

            var salesReport = await query.ToListAsync();
            var vm = new PerProductViewModel();
            vm.CustomerFullName = customerFullName;
            if (startDate.HasValue && endDate.HasValue)
            {
                vm.StartDate = DateOnly.FromDateTime((DateTime)startDate);
                vm.EndDate = DateOnly.FromDateTime((DateTime)endDate);
            }
            vm.SalesReport = salesReport;

            return PartialView("_SalesReportTable", vm);
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
                            M2Total = grouped.Sum(g => ((decimal)(g.product.Length * g.product.Width) / 10000) * g.sale.Quantity),
                            TotalSpent = grouped.Sum(g =>
                                g.product.PerM2
                                ? ((decimal)(g.product.Length * g.product.Width) / 10000) * g.sale.Quantity * g.sale.Price
                                : g.sale.Price * g.sale.Quantity)
                        };

            var salesReport = await query.ToListAsync();
            var total = salesReport.Sum(r => r.TotalSpent);
            var totalM2 = salesReport.Sum(r => r.M2Total);

            var vm = new PerDayViewModel
            {
                SalesReport = salesReport,
                Date = DateOnly.FromDateTime(date),
                TotalSpentSum = Math.Round(total, 2),
                TotalM2 = Math.Round(totalM2, 2)
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_PerDayTablePartial", vm);
            }

            return View(vm);
        }

        //public IActionResult PerDay()
        //{
        //    var vm = new PerDayViewModel();
        //    vm.SalesReport = new List<PerDayCustomersPurchaseSummary>();
        //    return View(vm);
        //}

        //[HttpPost]
        //public async Task<IActionResult> PerDay(DateTime date)
        //{
        //    var nextDay = date.AddDays(1);
        //    var query = from sale in _context.Prodaje
        //                join product in _context.Tepisi
        //                on sale.TepihId equals product.Id
        //                where (sale.VrijemeProdaje >= date && sale.VrijemeProdaje < nextDay)
        //                group new { sale, product } by new
        //                {
        //                    sale.CustomerFullName,
        //                }
        //    into grouped
        //                select new PerDayCustomersPurchaseSummary
        //                {
        //                    CustomerName = grouped.Key.CustomerFullName,
        //                    M2Total = grouped.Sum(g => ((decimal)(g.product.Length * g.product.Width) / 10000) * g.sale.Quantity),
        //                    TotalSpent = grouped.Sum(g =>
        //                        g.product.PerM2
        //                        ? ((decimal)(g.product.Length * g.product.Width) / 10000) * g.sale.Quantity * g.sale.Price
        //                        : g.sale.Price * g.sale.Quantity)
        //                };

        //    var salesReport = await query.ToListAsync();

        //    var total = query.Sum(r => r.TotalSpent);
        //    var totalM2 = query.Sum(r => r.M2Total);
        //    var vm = new PerDayViewModel();
        //    vm.SalesReport = salesReport;
        //    vm.Date = DateOnly.FromDateTime(date);
        //    vm.TotalSpentSum = Math.Round(total,2);
        //    vm.TotalM2 = Math.Round(totalM2, 2);

        //    return View(vm);
        //}
    }
}
