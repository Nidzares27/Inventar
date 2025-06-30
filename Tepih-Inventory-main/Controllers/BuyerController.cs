using CloudinaryDotNet.Core;
using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.ViewModels.Buyer;
using Inventar.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Macs;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

namespace Inventar.Controllers
{
    [Authorize]
    public class BuyerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IKupacRepository _kupacRepository;
        private readonly ITepihRepository _tepihRepository;
        private readonly ISalesRepository _salesRepository;
        private readonly IPlacanjeRepository _placanjeRepository;
        private readonly ILogger<BuyerController> _logger;

        public BuyerController(ApplicationDbContext context, IKupacRepository kupacRepository, ITepihRepository tepihRepository, ISalesRepository salesRepository, IPlacanjeRepository placanjeRepository, ILogger<BuyerController> logger)
        {
            this._context = context;
            this._kupacRepository = kupacRepository;
            this._tepihRepository = tepihRepository;
            this._salesRepository = salesRepository;
            this._placanjeRepository = placanjeRepository;
            this._logger = logger;
        }
        //public async Task<IActionResult> Index()
        //{
        //    var kupci = await _kupacRepository.GetAll();
        //    var kupcii = new List<BuyerViewModel>();
        //    foreach (var item in kupci)
        //    {
        //        decimal platio = 0;
        //        decimal dug = 0;

        //        var placanja = await _placanjeRepository.GetAllByNameAsync(item.CustomerFullName);
        //        var kupovine = await _salesRepository.GetAllByNameAsync(item.CustomerFullName);

        //        foreach (var item1 in placanja)
        //        {
        //            platio += item1.Amount;
        //        }
        //        foreach (var item2 in kupovine)
        //        {
        //            var proizvod = await _tepihRepository.GetByIdAsyncNoTracking(item2.TepihId);
        //            dug += (proizvod.PerM2 ? item2.Price * (((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * item2.Quantity) : item2.Price * item2.Quantity);
        //        }
        //        var kupac = new BuyerViewModel
        //        {
        //            Id = item.Id,
        //            CustomerFullName = item.CustomerFullName,
        //            LeftToPay = @Math.Round(dug, 2), /*item.LeftToPay*/
        //            Paid = platio,
        //        };
        //        kupcii.Add(kupac);

        //    }
        //    return View(kupcii);
        //}
        public async Task<IActionResult> Index()
        {
            try
            {
                var buyers = await _kupacRepository.GetAll();

                // Pre-fetch all relevant data
                var allPayments = await _placanjeRepository.GetAll();
                var allSales = await _salesRepository.GetAllWithTepih();
                var buyerViewModels = new List<BuyerViewModel>();

                foreach (var buyer in buyers)
                {
                    var buyerPayments = allPayments
                        .Where(p => p.CustomerName == buyer.CustomerFullName)
                        .Sum(p => p.Amount);

                    var buyerSales = allSales
                        .Where(s => s.CustomerFullName == buyer.CustomerFullName);

                    decimal totalDebt = 0;
                    foreach (var sale in buyerSales)
                    {
                        var carpet = sale.Tepih;
                        if (carpet == null) continue;

                        decimal unitPrice = sale.Price;
                        decimal quantity = sale.Quantity;
                        decimal area = (carpet.PerM2 && carpet.Length.HasValue && carpet.Width.HasValue)
                            ? ((carpet.Length.Value * carpet.Width.Value) / 10000m)
                            : 1;

                        totalDebt += unitPrice * area * quantity;
                    }

                    buyerViewModels.Add(new BuyerViewModel
                    {
                        Id = buyer.Id,
                        CustomerFullName = buyer.CustomerFullName,
                        LeftToPay = Math.Round(totalDebt, 2),
                        Paid = buyerPayments
                    });
                }

                return View(buyerViewModels);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading buyers!");
                return StatusCode(500, "An error occurred while loading buyers!");
            }

        }

        //public async Task<IActionResult> ShowBuys(int id)
        //{
        //    Kupac kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
        //    var kupovine = await _context.Prodaje.Where(c => c.CustomerFullName.Equals(kupac.CustomerFullName)).ToListAsync();
        //    IEnumerable<Tepih> proizvodi = await _tepihRepository.GetAll();

        //    var query = (from kupovina in kupovine
        //                 join proizvod in proizvodi on kupovina.TepihId equals proizvod.Id
        //                 where proizvod.Disabled != true && kupovina.Disabled != true
        //                 select new ProdajaViewModel
        //                 {
        //                     Id = kupovina.Id,
        //                     TepihId = kupovina.TepihId,
        //                     Name = proizvod.Name,
        //                     Model = proizvod.Model,
        //                     Length = proizvod.Length,
        //                     Width = proizvod.Width,
        //                     Color = proizvod.Color,
        //                     Price = kupovina.Price,
        //                     PerM2 = proizvod.PerM2,
        //                     Quantity = kupovina.Quantity,
        //                     CustomerFullName = kupovina.CustomerFullName,
        //                     VrijemeProdaje = kupovina.VrijemeProdaje,
        //                     M2PerUnit = proizvod.PerM2 ? (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000 : null,
        //                     M2Total = proizvod.PerM2 ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * kupovina.Quantity : null,

        //                 });
        //    var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString() + Request.QueryString.Value.ToString();
        //    ViewBag.ReturnUrl = referer;
        //    return View(query);
        //}

        public async Task<IActionResult> ShowBuys(int id)
        {
            try
            {
                var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
                if (kupac == null)
                {
                    _logger.LogError("Couldn't find a buyer with an ID: {id}",id);
                    return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
                }

                var kupovine = await _context.Prodaje
                    .Where(c => c.CustomerFullName == kupac.CustomerFullName)
                    .ToListAsync();

                var proizvodi = await _tepihRepository.GetAll();

                var query = from kupovina in kupovine
                            join proizvod in proizvodi on kupovina.TepihId equals proizvod.Id
                            where proizvod.Disabled != true && kupovina.Disabled != true
                            select new ProdajaViewModel
                            {
                                Id = kupovina.Id,
                                TepihId = kupovina.TepihId,
                                Name = proizvod.Name,
                                Model = proizvod.Model,
                                Length = proizvod.Length,
                                Width = proizvod.Width,
                                Color = proizvod.Color,
                                Price = kupovina.Price,
                                PerM2 = proizvod.PerM2,
                                Quantity = kupovina.Quantity,
                                CustomerFullName = kupovina.CustomerFullName,
                                VrijemeProdaje = kupovina.VrijemeProdaje,
                                M2PerUnit = proizvod.PerM2
                                    ? (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000
                                    : null,
                                M2Total = proizvod.PerM2
                                    ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * kupovina.Quantity
                                    : null
                            };

                var referer = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
                ViewBag.ReturnUrl = referer;

                return View(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading buys for a buyer with an ID: {id}", id);
                return StatusCode(500, "An error occurred while loading data.");
            }
        }


        //public async Task<IActionResult> GroupedBuys(int id)
        //{
        //    Kupac kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
        //    var prodaje = await _context.Prodaje.Where(c => c.CustomerFullName.Equals(kupac.CustomerFullName)).ToListAsync();
        //    var proizvodi = await _tepihRepository.GetAll();

        //    var query = (from prodaja in prodaje
        //                 join proizvod in proizvodi on prodaja.TepihId equals proizvod.Id
        //                 where prodaja.Disabled != true
        //                 group prodaja by new { prodaja.CustomerFullName, prodaja.VrijemeProdaje, prodaja.Prodavac, prodaja.PlannedPaymentType } into g
        //                 select new SummaryViewModel
        //                 {
        //                     CustomerFullName = g.Key.CustomerFullName,
        //                     VrijemeProdaje = g.Key.VrijemeProdaje,
        //                     Prodavac = g.Key.Prodavac,
        //                     PlannedPaymentType = g.Key.PlannedPaymentType,
        //                     CustomerId = id

        //                 }).ToList();
        //    var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString();
        //    ViewBag.ReturnFromDetails = referer;
        //    return View(query);
        //}

        public async Task<IActionResult> GroupedBuys(int id)
        {
            try
            {
                var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
                if (kupac == null)
                {
                    _logger.LogError("Grouped Buys: Couldn't find a buyer with an ID: {id}", id);
                    return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
                }

                var prodaje = await _context.Prodaje
                    .AsNoTracking()
                    .Where(c => c.CustomerFullName == kupac.CustomerFullName)
                    .ToListAsync();

                var proizvodi = await _tepihRepository.GetAll();

                var query = (from prodaja in prodaje
                             join proizvod in proizvodi on prodaja.TepihId equals proizvod.Id
                             where prodaja.Disabled != true
                             group new { prodaja, proizvod } by new
                             {
                                 prodaja.CustomerFullName,
                                 prodaja.VrijemeProdaje,
                                 prodaja.Prodavac,
                                 prodaja.PlannedPaymentType
                             } into g
                             select new SummaryViewModel
                             {
                                 CustomerFullName = g.Key.CustomerFullName,
                                 VrijemeProdaje = g.Key.VrijemeProdaje,
                                 Prodavac = g.Key.Prodavac,
                                 PlannedPaymentType = g.Key.PlannedPaymentType,
                                 CustomerId = id,
                             }).ToList();

                var referer = $"{Request.Scheme}://{Request.Host}{Request.Path}";
                ViewBag.ReturnFromDetails = referer;
                return View(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading GroupedBuys for a buyer with an ID: {id}", id);
                return StatusCode(500, "An error occurred while generating grouped buys.");
            }
        }

        //public async Task<IActionResult> Delete(int id)
        //{
        //    Kupac kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
        //    return View(kupac);
        //}
        public async Task<IActionResult> Delete(int id)
        {
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            if (kupac == null)
            {
                _logger.LogError("Delete Buyer: Couldn't find a buyer with an ID: {id}", id);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }
            return View(kupac);
        }

        //[HttpPost, ActionName("Delete")]
        //public async Task<IActionResult> DeleteKupac(int id)
        //{
        //    Kupac kupac = await _kupacRepository.GetByIdAsync(id);
        //    var kupovine = await _context.Prodaje.Where(c => c.CustomerFullName.Equals(kupac.CustomerFullName)).ToListAsync();
        //    foreach (var item in kupovine)
        //    {
        //        _salesRepository.Delete(item);
        //    }

        //    _kupacRepository.Delete(kupac);

        //    return RedirectToAction("Index");
        //}

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteKupac(int id)
        {
            var kupac = await _kupacRepository.GetByIdAsync(id);
            if (kupac == null)
            {
                _logger.LogError("Delete Buyer: Couldn't find a buyer with an ID: {id}", id);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var kupovine = await _context.Prodaje
                    .Where(c => c.CustomerFullName == kupac.CustomerFullName)
                    .ToListAsync();

                foreach (var item in kupovine)
                {
                    _salesRepository.Delete(item);
                }

                _kupacRepository.Delete(kupac);

                await _context.SaveChangesAsync(); // Important if repositories don't call SaveChanges internally

                await transaction.CommitAsync();

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "An error occurred while deleting the buyer with an ID: {id}", id);
                return StatusCode(500, "An error occurred while deleting the buyer.");
            }
        }


        public async Task<IActionResult> MakePayment(int id)
        {
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            if (kupac == null)
            {
                _logger.LogError("Make payment: Couldn't find a buyer with an ID: {id}", id);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            var uplata = new MakePaymentViewModel
            {
                Id = id,
                Name = kupac.CustomerFullName
            };

            return View(uplata);
        }

        //[HttpPost]
        //public async Task<IActionResult> MakePayment(MakePaymentViewModel vm)
        //{
        //    var kupac = await _kupacRepository.GetByIdAsyncNoTracking(vm.Id);
        //    Placanje uplata = new Placanje()
        //    {
        //        CustomerName = kupac.CustomerFullName,
        //        Amount = vm.AmountPaid,
        //        PaymentTime = DateTime.Now,
        //        PaymentType = vm.PaymentType,
        //    };
        //    _placanjeRepository.Add(uplata);
        //    return RedirectToAction("Index");
        //}

        [HttpPost]
        public async Task<IActionResult> MakePayment(MakePaymentViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(vm.Id); 
            if (kupac == null)
            {
                _logger.LogError("Make payment: Couldn't find a buyer with an ID: {id}", vm.Id);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            var uplata = new Placanje
            {
                CustomerName = kupac.CustomerFullName,
                Amount = vm.AmountPaid,
                PaymentTime = DateTime.Now,
                PaymentType = vm.PaymentType
            };

            _placanjeRepository.Add(uplata);
            _placanjeRepository.Save();

            return RedirectToAction("Index");
        }


        public async Task<IActionResult> PaymentHistory(int id)
        {
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            if (kupac == null)
            {
                _logger.LogError("Payment History: Couldn't find a buyer with an ID: {id}", id);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }
            var uplate = await _placanjeRepository.GetAllByNameAsync(kupac.CustomerFullName);
            var unDisabledUplate = uplate.Where(u => u.Disabled != true);
            ViewBag.CustomerName = kupac.CustomerFullName;
            return View(unDisabledUplate);
        }

        //public async Task<IActionResult> DeletePayment(int id)
        //{
        //    Placanje placanje = await _placanjeRepository.GetByIdAsync(id);
        //    Kupac kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);
        //    var data = new DeleteEditPaymentViewModel
        //    {
        //        Id = id,
        //        CustomerName = kupac.CustomerFullName,
        //        Amount = placanje.Amount,
        //        PaymentTime = placanje.PaymentTime,
        //        BuyerId = kupac.Id,
        //        PaymentType = placanje.PaymentType,
        //    };
        //    return View(data);
        //}

        public async Task<IActionResult> DeletePayment(int id)
        {
            var placanje = await _placanjeRepository.GetByIdAsync(id);
            if (placanje == null)
            {
                _logger.LogError("Delete Payment: Couldn't find a payment with an ID of: {id}", id);
                return NotFound("Payment not found!!! Please try with another one to see if the error keeps happening.");
            }

            var kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);
            if (kupac == null)
            {
                _logger.LogError("Delete Payment: Couldn't find a buyer with a name of: {name}", placanje.CustomerName);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            var data = new DeleteEditPaymentViewModel
            {
                Id = id,
                CustomerName = kupac.CustomerFullName,
                Amount = placanje.Amount,
                PaymentTime = placanje.PaymentTime,
                BuyerId = kupac.Id,
                PaymentType = placanje.PaymentType,
            };

            return View(data);
        }


        [HttpPost, ActionName("DeletePayment")]
        public async Task<IActionResult> DeletePaymentt(int id)
        {
            Placanje placanje = await _placanjeRepository.GetByIdAsync(id);
            if (placanje == null)
            {
                _logger.LogError("Delete Payment Post: Couldn't find a payment with an ID of: {id}", id);
                return NotFound("Payment not found!!! Please try with another one to see if the error keeps happening.");
            }
            Kupac kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);
            if (kupac == null)
            {
                _logger.LogError("Delete Payment Post: Couldn't find a buyer with a name of: {name}", placanje.CustomerName);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            _placanjeRepository.Delete(placanje);

            return RedirectToAction("PaymentHistory", new {id = kupac.Id });
        }

        public async Task<IActionResult> EditPayment(int id)
        {
            Placanje placanje = await _placanjeRepository.GetByIdAsyncNoTracking(id);
            if (placanje == null)
            {
                _logger.LogError("Edit Payment: Couldn't find a payment with an ID of: {id}", id);
                return NotFound("Payment not found!!! Please try with another one to see if the error keeps happening.");
            }
            Kupac kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);
            if (kupac == null)
            {
                _logger.LogError("Edit Payment: Couldn't find a buyer with a name of: {name}", placanje.CustomerName);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }
            var data = new DeleteEditPaymentViewModel
            {
                Id = id,
                CustomerName = kupac.CustomerFullName,
                Amount = placanje.Amount,
                PaymentTime = placanje.PaymentTime,
                BuyerId = kupac.Id,
                PaymentType = placanje.PaymentType,
            };

            return View(data);
        }

        //[HttpPost]
        //public async Task<IActionResult> EditPayment(int id, int buyerId, DeleteEditPaymentViewModel vm)
        //{

        //    if (!ModelState.IsValid)
        //    {
        //        ModelState.AddModelError("", "Editovanje placanja nije uspjelo");
        //        return View("EditPayment", vm);
        //    }

        //    var payment = await _placanjeRepository.GetByIdAsyncNoTracking(id);

        //    if (payment != null)
        //    {
        //        var placanjeEdit = new Placanje
        //        {
        //            Id = id,
        //            CustomerName = vm.CustomerName,
        //            Amount = vm.Amount,
        //            PaymentTime = vm.PaymentTime,
        //            PaymentType = vm.PaymentType,
        //        };

        //        _placanjeRepository.Update(placanjeEdit);

        //        return RedirectToAction("PaymentHistory", new { id = vm.BuyerId });
        //    }
        //    else
        //    {
        //        return View(vm);
        //    }
        //}

        [HttpPost]
        public async Task<IActionResult> EditPayment(int id, DeleteEditPaymentViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje plaćanja nije uspjelo.");
                return View("EditPayment", vm);
            }

            var payment = await _placanjeRepository.GetByIdAsyncNoTracking(id);
            if (payment == null)
            {
                ModelState.AddModelError("", "Plaćanje nije pronađeno.");
                _logger.LogError("Edit Payment Post: Couldn't find a payment with an ID of: {id}", id);
                return View("EditPayment", vm);
            }

            var updatedPayment = new Placanje
            {
                Id = id,
                CustomerName = vm.CustomerName,
                Amount = vm.Amount,
                PaymentTime = vm.PaymentTime,
                PaymentType = vm.PaymentType,
            };

            _placanjeRepository.Update(updatedPayment);

            return RedirectToAction("PaymentHistory", new { id = vm.BuyerId });
        }


        public async Task<IActionResult> BuyerActivity(int buyerId, DateTime? startDate, DateTime? endDate)
        {
            var buyer = await _context.Kupci.FirstOrDefaultAsync(k => k.Id == buyerId);
            if (buyer == null)
            {
                _logger.LogError("Buyer Activity: Couldn't find a buyer with an ID of: {id}", buyerId);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            try
            {
                var paymentsQuery = _context.Placanja
    .Where(p => p.CustomerName == buyer.CustomerFullName);

                var salesQuery = _context.Prodaje
                    .Include(p => p.Tepih)
                    .Where(p => p.CustomerFullName == buyer.CustomerFullName);

                var pastPaymentsQuery = paymentsQuery;
                var pastSalesQuery = salesQuery;

                var endDateModified = new DateTime();
                if (endDate != null)
                {
                    endDateModified = endDate.Value.AddHours(23).AddMinutes(59).AddSeconds(59);
                }

                if (startDate.HasValue)
                {
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentTime >= startDate.Value);
                    salesQuery = salesQuery.Where(p => p.VrijemeProdaje >= startDate.Value);
                    pastPaymentsQuery = pastPaymentsQuery.Where(p => p.PaymentTime < startDate.Value);
                    pastSalesQuery = pastSalesQuery.Where(p => p.VrijemeProdaje < startDate.Value);
                }

                if (endDate.HasValue)
                {
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentTime <= endDateModified);
                    salesQuery = salesQuery.Where(p => p.VrijemeProdaje <= endDateModified);
                }

                var payments = await paymentsQuery.ToListAsync();
                var sales = await salesQuery.ToListAsync();
                var pastPayments = await pastPaymentsQuery.ToListAsync();
                var pastSales = await pastSalesQuery.ToListAsync();

                var groupedSales = sales
                    .GroupBy(p => new { p.VrijemeProdaje, p.Prodavac, p.Disabled })
                    .Select(g => new BuyerActivityItem
                    {
                        ActivityTime = g.Key.VrijemeProdaje,
                        Type = "Prodaja",
                        Amount = g.Sum(prodaja =>
                            prodaja.Tepih.PerM2
                                ? prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)
                                : prodaja.Price * prodaja.Quantity
                        ),
                        Info = g.Key.Prodavac,
                        Disabled = g.Key.Disabled
                    });

                var paymentItems = payments.Select(p => new BuyerActivityItem
                {
                    ActivityTime = p.PaymentTime,
                    Type = "Uplata",
                    Amount = p.Amount,
                    Info = p.PaymentType ?? "N/A",
                    Disabled = p.Disabled
                });

                IEnumerable<BuyerActivityItem> pastGroupedSales;
                IEnumerable<BuyerActivityItem> pastPaymentItems;

                if (startDate.HasValue)
                {
                    pastGroupedSales = pastSales
                        .GroupBy(p => new { p.VrijemeProdaje, p.Prodavac, p.Disabled })
                        .Select(g => new BuyerActivityItem
                        {
                            ActivityTime = g.Key.VrijemeProdaje,
                            Type = "Prodaja",
                            Amount = g.Sum(prodaja =>
                                prodaja.Tepih.PerM2
                                    ? prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)
                                    : prodaja.Price * prodaja.Quantity
                            ),
                            Info = g.Key.Prodavac,
                            Disabled = g.Key.Disabled
                        });

                    pastPaymentItems = pastPayments.Select(p => new BuyerActivityItem
                    {
                        ActivityTime = p.PaymentTime,
                        Type = "Uplata",
                        Amount = p.Amount,
                        Info = p.PaymentType ?? "N/A",
                        Disabled = p.Disabled
                    });
                }
                else
                {
                    pastGroupedSales = groupedSales.Where(s => s.Disabled == true);
                    pastPaymentItems = paymentItems.Where(s => s.Disabled == true);
                }

                //var salesDisabled = groupedSales.Where(s => s.Disabled == true);
                //var paymentsDisabled = paymentItems.Where(s => s.Disabled == true);

                var salesUndisabled = groupedSales.Where(s => s.Disabled != true);
                var paymentsUndisabled = paymentItems.Where(s => s.Disabled != true);

                //var totalSalesDisabled = salesDisabled.Sum(s => s.Amount);
                //var totalPaymentsDisabled = paymentsDisabled.Sum(p => p.Amount);
                //var totalDebtDisabled = totalSalesDisabled - totalPaymentsDisabled;

                //NOVO
                var pastTotalSales = pastGroupedSales.Sum(s => s.Amount);
                var pastTotalPayments = pastPaymentItems.Sum(p => p.Amount);
                var pastTotalDebt = pastTotalSales - pastTotalPayments;

                var totalSalesUndisabled = salesUndisabled.Sum(s => s.Amount);
                var totalPaymentsUndisabled = paymentsUndisabled.Sum(p => p.Amount);
                var totalDebtUndisabled = totalSalesUndisabled - totalPaymentsUndisabled;

                //var totalSales = groupedSales.Sum(s => s.Amount);
                //var totalPayments = paymentItems.Sum(p => p.Amount);

                //var totalDebt = totalSales - totalPayments;
                var totalDebt = totalDebtUndisabled + pastTotalDebt;


                var activities = groupedSales
                    .Concat(paymentItems)
                    .OrderByDescending(a => a.ActivityTime)
                    .ToList();

                var model = new BuyerActivityViewModel
                {
                    BuyerId = buyer.Id,
                    BuyerName = buyer.CustomerFullName,
                    StartDate = startDate,
                    EndDate = endDate,
                    Activities = activities,
                    TotalDebt = totalDebt,
                    //TotalDebtDisabled = totalDebtDisabled,
                    TotalDebtUndisabled = totalDebtUndisabled,
                    PastTotalDebt = pastTotalDebt
                };

                return View(model);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading  buyers activity for a buyer with an ID: {id}", buyerId);
                return StatusCode(500, "An error occurred while loading  buyers activity!!!");
            }
        }

    }
}