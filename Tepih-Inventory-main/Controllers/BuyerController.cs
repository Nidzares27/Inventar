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
        private readonly IDugRepository _dugRepository;

        public BuyerController(ApplicationDbContext context, IKupacRepository kupacRepository, ITepihRepository tepihRepository, ISalesRepository salesRepository, IPlacanjeRepository placanjeRepository, ILogger<BuyerController> logger, IDugRepository dugRepository)
        {
            this._context = context;
            this._kupacRepository = kupacRepository;
            this._tepihRepository = tepihRepository;
            this._salesRepository = salesRepository;
            this._placanjeRepository = placanjeRepository;
            this._logger = logger;
            this._dugRepository = dugRepository;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var buyers = await _kupacRepository.GetAll();

                // Pre-fetch all relevant data
                var allPayments = await _placanjeRepository.GetAll();
                var allSales = await _salesRepository.GetAllWithTepih();
                var allDebts = await _dugRepository.GetAll();
                var buyerViewModels = new List<BuyerViewModel>();

                foreach (var buyer in buyers)
                {
                    var buyerPayments = allPayments
                        .Where(p => p.CustomerName == buyer.CustomerFullName)
                        .Sum(p => p.Amount);

                    var buyerSales = allSales
                        .Where(s => s.CustomerFullName == buyer.CustomerFullName);

                    var buyerDebts = allDebts
                        .Where(p => p.CustomerFullName == buyer.CustomerFullName)
                        .Sum(p => p.DebtAmount);

                    //var buyerDebt = buyerDebts ?? 0;

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
                    totalDebt += buyerDebts;

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

                var dugovi = await _context.Dugovanja.Where(c => c.CustomerFullName == kupac.CustomerFullName).ToListAsync();
                foreach (var item in dugovi)
                {
                    _dugRepository.Delete(item);
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

                var debtsQuery = _context.Dugovanja
                    .Where(p => p.CustomerFullName == buyer.CustomerFullName);

                var pastPaymentsQuery = paymentsQuery;
                var pastSalesQuery = salesQuery;
                var pastDebtsQuery = debtsQuery;

                var endDateModified = new DateTime();
                if (endDate != null)
                {
                    endDateModified = endDate.Value.AddHours(23).AddMinutes(59).AddSeconds(59);
                }

                if (startDate.HasValue)
                {
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentTime >= startDate.Value);
                    salesQuery = salesQuery.Where(p => p.VrijemeProdaje >= startDate.Value);
                    debtsQuery = debtsQuery.Where(p => p.DebtTime >= startDate.Value);
                    pastPaymentsQuery = pastPaymentsQuery.Where(p => p.PaymentTime < startDate.Value);
                    pastSalesQuery = pastSalesQuery.Where(p => p.VrijemeProdaje < startDate.Value);
                    pastDebtsQuery = pastDebtsQuery.Where(p => p.DebtTime < startDate.Value);
                }

                if (endDate.HasValue)
                {
                    paymentsQuery = paymentsQuery.Where(p => p.PaymentTime <= endDateModified);
                    salesQuery = salesQuery.Where(p => p.VrijemeProdaje <= endDateModified);
                    debtsQuery = debtsQuery.Where(p => p.DebtTime <= endDateModified);

                }

                var payments = await paymentsQuery.ToListAsync();
                var sales = await salesQuery.ToListAsync();
                var pastPayments = await pastPaymentsQuery.ToListAsync();
                var pastSales = await pastSalesQuery.ToListAsync();
                var debts = await debtsQuery.ToListAsync();
                var pastDebts = await pastDebtsQuery.ToListAsync();

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

                var debtItems = debts.Select(p => new BuyerActivityItem
                {
                    ActivityTime = p.DebtTime,
                    Type = "Dugovanje",
                    Amount = p.DebtAmount,
                    Info = "N/A",
                    Disabled = false
                });

                IEnumerable<BuyerActivityItem> pastGroupedSales;
                IEnumerable<BuyerActivityItem> pastPaymentItems;
                IEnumerable<BuyerActivityItem> pastDebtItems;


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

                    pastDebtItems = pastDebts.Select(p => new BuyerActivityItem
                    {
                        ActivityTime = p.DebtTime,
                        Type = "Dugovanje",
                        Amount = p.DebtAmount,
                        Info = "N/A",
                        Disabled = false
                    });
                }
                else
                {
                    pastGroupedSales = groupedSales.Where(s => s.Disabled == true);
                    pastPaymentItems = paymentItems.Where(s => s.Disabled == true);
                    pastDebtItems = debtItems.Where(s => s.Disabled == true);//nije neophodno posto je Ienumerable svakako prazan

                }

                var salesUndisabled = groupedSales.Where(s => s.Disabled != true);
                var paymentsUndisabled = paymentItems.Where(s => s.Disabled != true);

                var pastTotalSales = pastGroupedSales.Sum(s => s.Amount);
                var pastTotalPayments = pastPaymentItems.Sum(p => p.Amount);
                var pastTotalDugovanja = pastDebtItems.Sum(p => p.Amount);
                var pastTotalDebt = pastTotalSales + pastTotalDugovanja - pastTotalPayments;

                var totalSalesUndisabled = salesUndisabled.Sum(s => s.Amount);
                var totalPaymentsUndisabled = paymentsUndisabled.Sum(p => p.Amount);
                var totalDugovanjaUndisabled = debtItems.Sum(p => p.Amount);
                var totalDebtUndisabled = totalSalesUndisabled + totalDugovanjaUndisabled - totalPaymentsUndisabled;

                var totalDebt = totalDebtUndisabled + pastTotalDebt;

                var activities = groupedSales
                    .Concat(paymentItems)
                    .Concat(debtItems)
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

        public async Task<IActionResult> Debt()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Debt(string buyerName, decimal debtAmount)
        {
            if (!ModelState.IsValid)
                return View();

            try
            {
                var kupac = await _kupacRepository.GetByNameAsync(buyerName.ToUpper().Trim());

                if (kupac == null)
                {
                    _kupacRepository.Add(new Kupac { CustomerFullName = buyerName.ToUpper().Trim() });
                }
                _dugRepository.Add(new Dug { CustomerFullName = buyerName.ToUpper().Trim(), DebtAmount = Math.Round(debtAmount, 2), DebtTime = DateTime.Now });

                TempData["SuccessMessage"] = "Uspješno dodat dug!";
                return View();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occurred while adding new debt: Buyer name = {name}; Debt amount = {amount}", buyerName,debtAmount);
                return StatusCode(500, "An error occurred while adding new debt!!!");
            }

        }

        public async Task<IActionResult> DebtHistory(int buyerId)
        {
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(buyerId);
            if (kupac == null)
            {
                _logger.LogError("Debt History: Couldn't find a buyer with an ID: {id}", buyerId);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }
            var dugovanja = await _dugRepository.GetAllByNameAsync(kupac.CustomerFullName);
            ViewBag.CustomerName = kupac.CustomerFullName;
            return View(dugovanja);
        }

        public async Task<IActionResult> DeleteDebt(int id)
        {
            var dug = await _dugRepository.GetByIdAsync(id);
            if (dug == null)
            {
                _logger.LogError("Delete Debt: Couldn't find a debt with an ID of: {id}", id);
                return NotFound("Debt not found!!! Please try with another one to see if the error keeps happening.");
            }
            var kupac = await _kupacRepository.GetByNameAsync(dug.CustomerFullName);
            if (kupac == null)
            {
                _logger.LogError("Delete Debt: Couldn't find a buyer with a name of: {name}", dug.CustomerFullName);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            var data = new DeleteEditDebtViewModel
            {
                Id = id,
                CustomerFullName = kupac.CustomerFullName,
                DebtAmount = dug.DebtAmount,
                DebtTime = dug.DebtTime,
                BuyerId = kupac.Id
            };

            return View(data);
        }


        [HttpPost, ActionName("DeleteDebt")]
        public async Task<IActionResult> DeleteDebtt(int id, int buyerId)
        {
            Dug dug = await _dugRepository.GetByIdAsync(id);
            if (dug == null)
            {
                _logger.LogError("Delete Debt Post: Couldn't find a debt with an ID of: {id}", id);
                return NotFound("Debt not found!!! Please try with another one to see if the error keeps happening.");
            }

            _dugRepository.Delete(dug);

            return RedirectToAction("DebtHistory", new { buyerId = buyerId });
        }

        public async Task<IActionResult> EditDebt(int id)
        {
            Dug dug = await _dugRepository.GetByIdAsyncNoTracking(id);
            if (dug == null)
            {
                _logger.LogError("Edit Debt: Couldn't find a debt with an ID of: {id}", id);
                return NotFound("Debt not found!!! Please try with another one to see if the error keeps happening.");
            }
            var kupac = await _kupacRepository.GetByNameAsync(dug.CustomerFullName);
            if (kupac == null)
            {
                _logger.LogError("Edit Debt: Couldn't find a buyer with a name of: {name}", dug.CustomerFullName);
                return NotFound("Buyer not found!!! Please try with another one to see if the error keeps happening.");
            }

            var data = new DeleteEditDebtViewModel
            {
                Id = id,
                CustomerFullName = kupac.CustomerFullName,
                DebtAmount = dug.DebtAmount,
                DebtTime = dug.DebtTime,
                BuyerId = kupac.Id
            };

            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> EditDebt(int id, DeleteEditDebtViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje plaćanja nije uspjelo.");
                return View("EditPayment", vm);
            }

            var dug = await _dugRepository.GetByIdAsyncNoTracking(id);
            if (dug == null)
            {
                ModelState.AddModelError("", "Dug nije pronađen.");
                _logger.LogError("Edit Debt Post: Couldn't find a debt with an ID of: {id}", id);
                return View("EditDebt", vm);
            }

            var updatedDebt = new Dug
            {
                Id = id,
                CustomerFullName = vm.CustomerFullName,
                DebtAmount = vm.DebtAmount,
                DebtTime = vm.DebtTime
            };

            _dugRepository.Update(updatedDebt);

            return RedirectToAction("DebtHistory", new { buyerId = vm.BuyerId });
        }
    }
}