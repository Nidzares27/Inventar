using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Repository;
using Inventar.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Macs;

namespace Inventar.Controllers
{
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
                             M2PerUnit = (decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000,
                             M2Total = ((decimal)((int)proizvod.Length * (int)proizvod.Width) / 10000) * kupovina.Quantity,

                         });
            
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
                BuyerId = kupac.Id
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
                BuyerId = kupac.Id
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
                };

                _placanjeRepository.Update(placanjeEdit);

                return RedirectToAction("PaymentHistory", new { id = vm.BuyerId });
            }
            else
            {
                return View(vm);
            }
        }
    }
}
