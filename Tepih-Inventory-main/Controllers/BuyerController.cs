using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Repository;
using Inventar.ViewModels;
using Inventar.ViewModels.Buyer;
using Inventar.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Macs;

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

        public BuyerController(ApplicationDbContext context, IKupacRepository kupacRepository, ITepihRepository tepihRepository, ISalesRepository salesRepository, IPlacanjeRepository placanjeRepository)
        {
            this._context = context;
            this._kupacRepository = kupacRepository;
            this._tepihRepository = tepihRepository;
            this._salesRepository = salesRepository;
            this._placanjeRepository = placanjeRepository;
        }
        public async Task<IActionResult> Index()
        {
            var kupci = await _kupacRepository.GetAll();
            var kupcii = new List<BuyerViewModel>();
            foreach (var item in kupci)
            {
                decimal platio = 0;

                var placanja = await _placanjeRepository.GetAllByNameAsync(item.CustomerFullName);
                foreach (var item1 in placanja)
                {
                    platio += item1.Amount;
                }
                var kupac = new BuyerViewModel
                {
                    Id = item.Id,
                    CustomerFullName = item.CustomerFullName,
                    LeftToPay = item.LeftToPay,
                    Paid = platio,
                };
                kupcii.Add(kupac);
                
            }
            return View(kupcii);
        }
        public async Task<IActionResult> ShowBuys(int id)
        {
            Kupac kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            var kupovine = await _context.Prodaje.Where(c => c.CustomerFullName.Equals(kupac.CustomerFullName)).ToListAsync();
            IEnumerable<Tepih> proizvodi = await _tepihRepository.GetAll();

            var query = (from kupovina in kupovine
                         join proizvod in proizvodi on kupovina.TepihId equals proizvod.Id
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
                             M2PerUnit = proizvod.PerM2 ? (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000 : null,
                             M2Total = proizvod.PerM2 ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * kupovina.Quantity : null,

                         });
            var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString() + Request.QueryString.Value.ToString();
            ViewBag.ReturnUrl = referer;
            return View(query);            
        }

        public async Task<IActionResult> GroupedBuys(int id)
        {
            Kupac kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            var prodaje = await _context.Prodaje.Where(c => c.CustomerFullName.Equals(kupac.CustomerFullName)).ToListAsync();
            var proizvodi = await _tepihRepository.GetAll();

            var query = (from prodaja in prodaje
                         join proizvod in proizvodi on prodaja.TepihId equals proizvod.Id
                         group prodaja by new { prodaja.CustomerFullName, prodaja.VrijemeProdaje, prodaja.Prodavac, prodaja.PlannedPaymentType } into g
                         select new SummaryViewModel
                         {
                             CustomerFullName = g.Key.CustomerFullName,
                             VrijemeProdaje = g.Key.VrijemeProdaje,
                             Prodavac = g.Key.Prodavac,
                             PlannedPaymentType = g.Key.PlannedPaymentType,
                             CustomerId = id

                         }).ToList();
            var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString();
            ViewBag.ReturnFromDetails = referer;
            return View(query);
        }

        public async Task<IActionResult> Details(string customer, DateTime saleTime)
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
                             M2Total = proizvod.PerM2 ? ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * prodaja.Quantity : 0,
                         }).ToList();

            ViewBag.CustomerFullName = customer;
            ViewBag.SaleTime = saleTime.ToString("dd-MM-yyyy HH:mm:ss");
            var referer = Request.Scheme.ToString() + "://" + Request.Host.Value.ToString() + Request.Path.Value.ToString() + Request.QueryString.Value.ToString();
            ViewBag.ReturnUrl = referer;

            return View(query);
        }

        public async Task<IActionResult> Delete(int id)
        {
            Kupac kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            return View(kupac);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteKupac(int id)
        {
            Kupac kupac = await _kupacRepository.GetByIdAsync(id);
            var kupovine = await _context.Prodaje.Where(c => c.CustomerFullName.Equals(kupac.CustomerFullName)).ToListAsync();
            foreach (var item in kupovine)
            {
                _salesRepository.Delete(item);
            }

            _kupacRepository.Delete(kupac);

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> MakePayment(int id)
        {
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
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
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(vm.Id);
            Placanje uplata = new Placanje()
            {
                CustomerName = kupac.CustomerFullName,
                Amount = vm.AmountPaid,
                PaymentTime = DateTime.Now,
                PaymentType = vm.PaymentType,
            };
            _placanjeRepository.Add(uplata);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> PaymentHistory(int id)
        {
            var kupac = await _kupacRepository.GetByIdAsyncNoTracking(id);
            var uplate = await _placanjeRepository.GetAllByNameAsync(kupac.CustomerFullName);
            ViewBag.CustomerName = kupac.CustomerFullName;
            return View(uplate);
        }

        public async Task<IActionResult> DeletePayment(int id)
        {
            Placanje placanje = await _placanjeRepository.GetByIdAsync(id);
            Kupac kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);
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
            Kupac kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);

            _placanjeRepository.Delete(placanje);

            return RedirectToAction("PaymentHistory", new {id = kupac.Id });
        }

        public async Task<IActionResult> EditPayment(int id)
        {
            Placanje placanje = await _placanjeRepository.GetByIdAsyncNoTracking(id);
            if (placanje == null) return View("Error");
            Kupac kupac = await _kupacRepository.GetByNameAsync(placanje.CustomerName);
            if (kupac == null) return View("Error");
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
        public async Task<IActionResult> EditPayment(int id, int buyerId ,DeleteEditPaymentViewModel vm)
        {

            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje placanja nije uspjelo");
                return View("EditPayment", vm);
            }

            var payment = await _placanjeRepository.GetByIdAsyncNoTracking(id);

            if (payment != null)
            {
                var placanjeEdit = new Placanje
                {
                    Id = id,
                    CustomerName = vm.CustomerName,
                    Amount = vm.Amount,
                    PaymentTime = vm.PaymentTime,
                    PaymentType = vm.PaymentType,
                };

                _placanjeRepository.Update(placanjeEdit);

                return RedirectToAction("PaymentHistory", new { id = vm.BuyerId });
            }
            else
            {
                return View(vm);
            }
        }

        public async Task<IActionResult> BuyerActivity(int buyerId, DateTime? startDate, DateTime? endDate)
        {
            var buyer = await _context.Kupci.FirstOrDefaultAsync(k => k.Id == buyerId);
            if (buyer == null) return NotFound();

            var paymentsQuery = _context.Placanja
                .Where(p => p.CustomerName == buyer.CustomerFullName);

            var salesQuery = _context.Prodaje
                .Include(p => p.Tepih)
                .Where(p => p.CustomerFullName == buyer.CustomerFullName);

            var endDateModified = new DateTime();
            if (endDate != null)
            {
                endDateModified = endDate.Value.AddHours(23).AddMinutes(59).AddSeconds(59);
            }

            if (startDate.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentTime >= startDate.Value);
                salesQuery = salesQuery.Where(p => p.VrijemeProdaje >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentTime <= endDateModified);
                salesQuery = salesQuery.Where(p => p.VrijemeProdaje <= endDateModified);
            }

            var payments = await paymentsQuery.ToListAsync();
            var sales = await salesQuery.ToListAsync();

            var groupedSales = sales
                .GroupBy(p => new { p.VrijemeProdaje, p.Prodavac })
                .Select(g => new BuyerActivityItem
                {
                    ActivityTime = g.Key.VrijemeProdaje,
                    Type = "Prodaja",
                    Amount = g.Sum(prodaja =>
                        prodaja.Tepih.PerM2
                            ? prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)
                            : prodaja.Price * prodaja.Quantity
                    ),
                    Info = g.Key.Prodavac
                });

            var paymentItems = payments.Select(p => new BuyerActivityItem
            {
                ActivityTime = p.PaymentTime,
                Type = "Uplata",
                Amount = p.Amount,
                Info = p.PaymentType ?? "N/A"
            });

            var totalSales = groupedSales.Sum(s => s.Amount);
            var totalPayments = paymentItems.Sum(p => p.Amount);
            var totalDebt = totalSales - totalPayments;

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
                TotalDebt = totalDebt
            };

            return View(model);
        }
    }
}