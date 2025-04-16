using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using ZXing.QrCode;
using ZXing;
using Inventar.Models;
using Inventar.Interfaces;
using System.Collections;
using Inventar.Data;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using Inventar.ViewModels;
using CloudinaryDotNet.Actions;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Http;
using Inventar.Utils;
using Newtonsoft.Json;
using System;
using iText.Commons.Bouncycastle.Asn1.X509;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using iText.StyledXmlParser.Jsoup.Safety;
using Microsoft.EntityFrameworkCore;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.IO;
using iText.Layout.Properties;
using Org.BouncyCastle.Crypto.Macs;
using Microsoft.AspNetCore.Localization;

namespace Inventar.Controllers
{
    public class InventoryItemController : Controller
    {
        private readonly ITepihRepository _tepihRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly ISalesRepository _salesRepository;
        private readonly IKupacRepository _kupacRepository;
        private readonly IPlacanjeRepository _placanjeRepository;

        public InventoryItemController(ITepihRepository tepihRepository, ApplicationDbContext context, IPhotoService photoService, ISalesRepository salesRepository, IKupacRepository kupacRepository, IPlacanjeRepository placanjeRepository)
        {
            this._tepihRepository = tepihRepository;
            this._context = context;
            this._photoService = photoService;
            this._salesRepository = salesRepository;
            this._kupacRepository = kupacRepository;
            this._placanjeRepository = placanjeRepository;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<Tepih> tepisi = await _tepihRepository.GetAll();
            return View(tepisi);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task< IActionResult> Create(Tepih tepih)
        {
            ViewBag.OldProductUpdate = "";
            if (ModelState.IsValid)
            {
                var time = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                // Generate QR Code for the item
                var qrCodeImageUrl = await GenerateQRCode($"{time}");
                var url = "";

                if (qrCodeImageUrl is OkObjectResult okResult)
                {
                    // Retrieve the actual object from the OkObjectResult
                    var value = okResult.Value as dynamic;

                    // Extract the URL from the object
                    url = value?.url;
                }

                var istiProizvod = await _context.Tepisi.Where(c => c.Name.Equals(tepih.Name) && c.Model.Equals(tepih.Model) && c.ProductNumber.Equals(tepih.ProductNumber) && c.Length.Equals(tepih.Length) && c.Width.Equals(tepih.Width) && c.Color.Equals(tepih.Color) && c.Price.Equals(tepih.Price)).ToListAsync();
                if (istiProizvod.Count == 1) {
                    istiProizvod[0].Quantity += tepih.Quantity;
                    _tepihRepository.Update(istiProizvod[0]);
                    ViewBag.OldProductUpdate = "Proizvod koji ste pokusali da kreirate vec postoji. Istom ce biti azurirana kolicina, a ID ovog proizvoda je " + istiProizvod[0].Id;
                    return View(tepih);
                }
                else
                {
                    tepih.QRCodeUrl = url;
                    tepih.DateTime = time;
                    _tepihRepository.Add(tepih);
                    return RedirectToAction("GenerateCloudinaryImagePdf", "Pdf", tepih);
                }
            }
            return View(tepih);
        }

        public async Task<IActionResult> GenerateQRCode(string data)
        {
            // Step 1: Generate the QR code using ZXing.Net
            var qrCodeWriter = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 250,    // Set height
                    Width = 250,     // Set width
                    Margin = 1       // Set margin
                }
            };

            // Generate QR code pixel data
            var pixelData = qrCodeWriter.Write(data);

            // Step 2: Create Bitmap from pixel data
            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                // Copy the pixel data to the bitmap
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                // Step 3: Save the bitmap to a memory stream as PNG
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); // Save as PNG
                    stream.Position = 0; // Reset stream position for upload

                    // Step 4: Upload to Cloudinary
                    var newGuid = $"{Guid.NewGuid()}.png";

                    var uploadResult = await _photoService.UploadToCloudinary(newGuid, stream);

                    if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        // Return the Cloudinary URL of the uploaded QR code
                        return Ok(new { url = uploadResult.SecureUrl.ToString() });
                    }
                    else
                    {
                        return StatusCode(500, "QR code upload to Cloudinary failed");
                    }
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> QRCodeScanning(int? id)
        {
            List<ScannedProductViewModel> scannedProds = GetScannedProducts();
            return View(scannedProds);
        }

        [HttpGet]
        public async Task<IActionResult> QRCodeScanning2()
        {
            return View("QRCodeScanning");
        }

        public IActionResult ProcessQRCode(string data)
        {
            // Search for the inventory item based on the QR code data
            var item = _context.Tepisi.FirstOrDefault(i => i.DateTime == data);

            if (item != null)
            {
                List<ScannedProductViewModel> scannedProds = GetScannedProducts();
                var matchingvalue = scannedProds.FirstOrDefault(i => i.Id == item.Id);

                bool isPageReload = Request.Headers["Cache-Control"].ToString().Contains("max-age=0");
                if (isPageReload)
                {
                    return View("QRCodeScanning", scannedProds);
                }
                if (matchingvalue != null)
                {
                    matchingvalue.Quantity++;
                    matchingvalue.M2Total = ((decimal)((int)item.Length * (int)item.Width) / 10000) * matchingvalue.Quantity;
                }
                else
                {
                    var tepihVM = new ScannedProductViewModel
                    {
                        Id = item.Id,
                        ProductNumber = item.ProductNumber,
                        Model = item.Model,
                        Name = item.Name,
                        Quantity = 1,
                        Length = item.Length,
                        Width = item.Width,
                        M2PerUnit = Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2),
                        M2Total = Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000),2),
                        Color = item.Color,
                        Price = item.Price,
                        PerM2 = item.PerM2,

                    };
                    if (!tepihVM.PerM2)
                    {
                        tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.Quantity), 2);
                    }
                    if (tepihVM.PerM2)
                    {
                        tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.M2Total), 2);

                    }

                    scannedProds.Add(tepihVM);
                }

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                return Ok();
            }

            // If not found, show an error or handle accordingly
            ViewBag.Error = "QR Code not found!";
            return View("Error");
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] ScannedProductViewModel modell)
        {
            List<ScannedProductViewModel> scannedProds = GetScannedProducts();
            var matchingvalue = scannedProds.FirstOrDefault(i => i.Id == modell.Id);
            if (matchingvalue != null)
            {
                matchingvalue.Price = modell.Price;
                matchingvalue.Rabat = modell.Rabat;
                if (!matchingvalue.PerM2)
                {
                    matchingvalue.PriceTotal = matchingvalue.Price * matchingvalue.Quantity;
                }
                if (matchingvalue.PerM2)
                {
                    matchingvalue.PriceTotal = (matchingvalue.Price * matchingvalue.M2Total);
                }

                if (matchingvalue.Rabat != null /*|| matchingvalue.Rabat != 0*/)
                {
                    var rbt = (decimal)matchingvalue.Rabat / (decimal)100;
                    matchingvalue.PriceTotal -= rbt * matchingvalue.PriceTotal;
                    matchingvalue.Price -= rbt * matchingvalue.Price;
                }
                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
            }

            return Json(new { success = true });
        }

        public async Task<IActionResult> Delete(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            return View(tepih);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteTepih(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsync(id);

            if (tepih.QRCodeUrl != null && tepih.QRCodeUrl.Length > 0)
            {
                if (!string.IsNullOrEmpty(tepih.QRCodeUrl))
                {
                    var publicId = CloudinaryHelper.GetPublicIdFromUrlFromFolder(tepih.QRCodeUrl);

                    try
                    {
                        await _photoService.DeletePhotoAsync(publicId);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Could not delete photo");
                    }
                }
            }
            _tepihRepository.Delete(tepih);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            if (tepih == null) return View("Error");
            var tepihVM = new EditTepihViewModel
            {
                Name = tepih.Name,
                ProductNumber = tepih.ProductNumber,
                Model = tepih.Model,
                DateTime = tepih.DateTime,
                Quantity = tepih.Quantity,
                QRCodeUrl = tepih.QRCodeUrl,
                Length = tepih.Length,
                Width = tepih.Width,
                Color = tepih.Color,
                Price = tepih.Price,
                PerM2 = tepih.PerM2,

            };

            return View(tepihVM);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditTepihViewModel tepihVM)
        {

            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje tepiha nije uspjelo");
                return View("Edit", tepihVM);
            }

            var putnik = await _tepihRepository.GetByIdAsyncNoTracking(id);

            if (putnik != null)
            {
                var tepihEdit = new Tepih
                {
                    Id = id,
                    Name = tepihVM.Name,
                    ProductNumber = tepihVM.ProductNumber,
                    Model = tepihVM.Model,
                    DateTime = tepihVM.DateTime,
                    Quantity = tepihVM.Quantity,
                    QRCodeUrl = tepihVM.QRCodeUrl,
                    Length = tepihVM.Length,
                    Width = tepihVM.Width,
                    Color = tepihVM.Color,
                    Price = tepihVM.Price,
                    PerM2 = tepihVM.PerM2,

                };

                _tepihRepository.Update(tepihEdit);

                return RedirectToAction("Index");
            }
            else
            {
                return View(tepihVM);
            }
        }
        public List<ScannedProductViewModel> GetScannedProducts()
        {
            var stepFormData = new List<ScannedProductViewModel>();

            var serializedModel = HttpContext.Session.GetString("scannedProducts");

            if (!string.IsNullOrEmpty(serializedModel))
            {
                stepFormData = JsonConvert.DeserializeObject<List<ScannedProductViewModel>>(serializedModel);
            }

            return stepFormData;
        }

        [HttpGet]
        public IActionResult ScannedProductsToBePurchased()
        {
            var scannedProductsOverview = new ScannedProductsOverviewViewModel();
            scannedProductsOverview.Products = GetScannedProducts();
            return View(scannedProductsOverview);
        }
        [HttpPost]
        public async Task< IActionResult> ScannedProductsToBePurchased(ScannedProductsOverviewViewModel spovm)
        {
            var purchTime = DateTime.Now;
            spovm.PurchaseTime = DateTime.ParseExact(purchTime.ToString("HH:mm:ss dd/MM/yyyy"), "HH:mm:ss dd/MM/yyyy", null);
            spovm.Products = GetScannedProducts();

            decimal toPay = 0;
            foreach (var prod in spovm.Products)
            {
                var sale = new Prodaja()
                {
                    TepihId = prod.Id,
                    Quantity = prod.Quantity,
                    CustomerFullName = spovm.FullName,
                    VrijemeProdaje = spovm.PurchaseTime,
                    Price = prod.Price,
                };
                _salesRepository.Add(sale);

                Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(prod.Id);
                if (tepih == null) return View("Error");
                tepih.Quantity -= prod.Quantity;
                _tepihRepository.Update(tepih);

                toPay += prod.PriceTotal;
            }
            var kupci = await _kupacRepository.GetAll();
            bool exists = kupci.Any(s => s.CustomerFullName == spovm.FullName);
            if (!exists)
            {
                Kupac kupac = new Kupac()
                {
                    CustomerFullName = spovm.FullName,
                    LeftToPay = toPay
                };
                _kupacRepository.Add(kupac);
            }else
            {
                var kupacc = await _kupacRepository.GetByNameAsync(spovm.FullName);
                kupacc.LeftToPay += toPay;
            }
            Placanje placanje = new Placanje()
            {
                CustomerName = spovm.FullName,
                PaymentTime = spovm.PurchaseTime,
                Amount = spovm.AmountPaid
            };
            _placanjeRepository.Add(placanje);
            if (spovm.PrintPDF)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Add Customer Name and Date
                    document.Add(new iText.Layout.Element.Paragraph($"Kupac: {spovm.FullName}"));
                    document.Add(new iText.Layout.Element.Paragraph($"Datum: {spovm.PurchaseTime}"));
                    document.Add(new iText.Layout.Element.Paragraph("\n"));

                    // Create Table
                    //Table table = new Table(9).UseAllAvailableWidth();
                    //string[] headers = { "Id", "Ime", "Model", "Velicina", "m² ukupno", "Boja", "Cijena", "Kolicina", "Ukupna cijena" };
                    Table table = new Table(8).UseAllAvailableWidth();
                    string[] headers = {"Sifra", "Ime", "Cijena", "Velicina", "Kolicina", "m²", "m² ukupno", "Ukupna cijena" };


                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new iText.Layout.Element.Paragraph(header))
                            .SetTextAlignment(TextAlignment.CENTER)  // Horizontal Center
                            .SetVerticalAlignment(VerticalAlignment.MIDDLE)  // Vertical Center
                            .SetBold()
                            .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                            .SetPadding(5)
                            .SetHeight(25)); // Adjust height if needed
                    }
                    var query = from product in spovm.Products
                                group new { product } by new
                                {
                                    product.Name,
                                    product.Length,
                                    product.Width,
                                    product.M2PerUnit,
                                    product.ProductNumber,
                                    product.Price
                                }
                    into grouped
                                select new ReceiptViewModel
                                {
                                    ProductNumber = grouped.Key.ProductNumber,
                                    Name = grouped.Key.Name,
                                    Price = grouped.Average(g=>g.product.Price),
                                    Length = grouped.Key.Length,
                                    Width = grouped.Key.Width,
                                    Size = $"{grouped.Key.Length} X {grouped.Key.Width}",
                                    M2PerUnit = grouped.Key.M2PerUnit,
                                    M2Total = grouped.Sum(g => g.product.M2Total),
                                    Quantity = grouped.Sum(g => g.product.Quantity),
                                    PriceTotal = grouped.Sum(g => g.product.PriceTotal)
                                };

                    var salesReport = query.ToList();

                    decimal? totalSum = 0;
                    decimal totalM2 = 0;
                    int totalQuantity = 0;
                    // Add Table Data
                    foreach (var item in salesReport)
                    {
                        table.AddCell(CreateCenteredCell(item.ProductNumber));
                        table.AddCell(CreateCenteredCell(item.Name));
                        table.AddCell(CreateCenteredCell(Math.Round(item.Price,2).ToString() + "€"));
                        //table.AddCell(CreateCenteredCell(item.Length + "X" + item.Width));
                        table.AddCell(CreateCenteredCell(item.Size));
                        table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2PerUnit.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2Total.ToString()));
                        table.AddCell(CreateCenteredCell(Math.Round(item.PriceTotal, 2).ToString() + "€"));

                        totalSum += item.PriceTotal; // Calculate total
                        totalM2 += item.M2Total;
                        totalQuantity += item.Quantity;
                    }

                    //decimal? totalSum = 0;
                    //// Add Table Data
                    //foreach (var item in spovm.Products)
                    //{
                    //    table.AddCell(CreateCenteredCell(item.Id.ToString()));
                    //    table.AddCell(CreateCenteredCell(item.Name));
                    //    table.AddCell(CreateCenteredCell(item.Model));
                    //    table.AddCell(CreateCenteredCell(item.Length + "X" + item.Width));
                    //    table.AddCell(CreateCenteredCell(item.M2Total.ToString()));
                    //    table.AddCell(CreateCenteredCell(item.Color));
                    //    table.AddCell(CreateCenteredCell(item.Price.ToString() + "€"));
                    //    table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
                    //    table.AddCell(CreateCenteredCell(Math.Round(item.PriceTotal,2).ToString() + "€"));

                    //    totalSum += item.PriceTotal; // Calculate total
                    //}
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell("UKUPNO:"));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(Math.Round(totalM2, 2).ToString()));
                    table.AddCell(CreateCenteredBoldCell(Math.Round((decimal)totalSum, 2).ToString() + "€"));
                    document.Add(table);

                    // Add Total Sum Below the Table (Right-Aligned)
                    //document.Add(new iText.Layout.Element.Paragraph($"Total: {Math.Round((decimal)totalSum,2)}€")
                    //    .SetTextAlignment(TextAlignment.RIGHT)
                    //    .SetBold()
                    //    .SetMarginTop(10));
                    document.Close();

                    return File(stream.ToArray(), "application/pdf", "OrderDetails.pdf");
                }
            }

            return RedirectToAction("Index", "Sales");
        }
        [HttpPost]
        public IActionResult DeleteScannedProduct(int id)
        {
            var scannedProds = GetScannedProducts();
            var item = scannedProds.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                scannedProds.Remove(item);
                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
            }
            return View("QRCodeScanning", scannedProds);
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int id, string action)
        {
            var scannedProds = GetScannedProducts();
            var item = scannedProds.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                if (action == "increase")
                {
                    item.Quantity += 1;
                }
                else if (action == "decrease" && item.Quantity > 1)
                {
                    item.Quantity -= 1;
                }
                item.M2Total = item.Quantity * item.M2PerUnit;
                if (!item.PerM2)
                {
                    item.PriceTotal = item.Price * item.Quantity;
                }
                if (item.PerM2)
                {
                    item.PriceTotal = (item.Price * item.M2Total);
                }
                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                var response = new
                {
                    qty = item.Quantity,
                    m2Total = item.M2Total,
                };

                return Json(response) ; // Return updated quantity to update UI
                //item.Quantity
            }
            return NotFound();
        }

        [HttpGet]
        public async Task<JsonResult> SearchPeople(string query)
        {
            var matches = await _context.Kupci
                .Where(p => p.CustomerFullName.Contains(query))
                .Select(p => p.CustomerFullName)
                .ToListAsync();

            return Json(matches);
        }

        // Helper Method for Centering Cell Text
        private Cell CreateCenteredCell(string text)
        {
            return new Cell()
                .Add(new iText.Layout.Element.Paragraph(text))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(5)
                    .SetHeight(25); // Ensures text is vertically centered
        }
        private Cell CreateCenteredBoldCell(string text)
        {
            return new Cell()
                .Add(new iText.Layout.Element.Paragraph(text))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(5)
                    .SetHeight(25) // Ensures text is vertically centered
                    .SetBold();
        }
        public IActionResult ManuallyAddProduct(int id)
        {
            // Search for the inventory item based on the QR code data
            //var item = await _tepihRepository.GetByIdAsyncNoTracking(id);
            var item = _context.Tepisi.FirstOrDefault(i => i.Id == id);


            if (item != null)
            {
                List<ScannedProductViewModel> scannedProds = GetScannedProducts();
                var matchingvalue = scannedProds.FirstOrDefault(i => i.Id == item.Id);

                //bool isPageReload = Request.Headers["Cache-Control"].ToString().Contains("max-age=0");
                //if (isPageReload)
                //{
                //    return View("QRCodeScanning", scannedProds);
                //}
                if (matchingvalue != null)
                {
                    matchingvalue.Quantity++;
                    matchingvalue.M2Total = ((decimal)((int)item.Length * (int)item.Width) / 10000) * matchingvalue.Quantity;
                }
                else
                {
                    var tepihVM = new ScannedProductViewModel
                    {
                        Id = item.Id,
                        ProductNumber = item.ProductNumber,
                        Model = item.Model,
                        Name = item.Name,
                        Quantity = 1,
                        Length = item.Length,
                        Width = item.Width,
                        M2PerUnit = Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2),
                        M2Total = Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2),
                        Color = item.Color,
                        Price = item.Price,
                        PerM2 = item.PerM2,

                    };
                    if (!tepihVM.PerM2)
                    {
                        tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.Quantity),2);
                    }
                    if (tepihVM.PerM2)
                    {
                        tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.M2Total),2);
                    }

                    scannedProds.Add(tepihVM);
                }

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                //return Ok();
                return RedirectToAction("QRCodeScanning","InventoryItem");
            }

            // If not found, show an error or handle accordingly
            ViewBag.Error = "QR Code not found!";
            return View("Error");
        }
    }
}
