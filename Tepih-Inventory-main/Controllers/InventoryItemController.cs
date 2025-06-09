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
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Inventar.ViewModels.Inventory;
using iText.Kernel.Geom;
using iText.Commons.Utils;

namespace Inventar.Controllers
{
    [Authorize]
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

        public async Task<IActionResult> Details(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            return View(tepih);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Tepih tepih)
        {
            if (ModelState.IsValid)
            {
                if (tepih.PerM2 && (tepih.Width == null || tepih.Length == null))
                {
                    TempData["MissingLengthWidth"] = "Proizvod koji se prodaje po m² mora imati Dužinu i Širinu!";
                    return View(tepih);
                }

                var time = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                //var qrCodeImageUrl = await GenerateQRCode($"{tepih.Name}/{tepih.Model}/{tepih.ProductNumber}/{tepih.Length}/{tepih.Width}/{tepih.Color}/{tepih.PerM2}/{tepih.Price}");
                var qrCodeImageUrl = await GenerateQRCode($"{tepih.Name}/{tepih.Model}/{tepih.Width}/{tepih.Length}/{tepih.Color}");

                var url = "";
                if (qrCodeImageUrl is OkObjectResult okResult)
                {
                    var value = okResult.Value as dynamic;
                    url = value?.url;
                }

                var istiProizvod = await _context.Tepisi
                    .Where(c => c.Name == tepih.Name && c.Model == tepih.Model &&
                                c.ProductNumber == tepih.ProductNumber &&
                                c.Length == tepih.Length && c.Width == tepih.Width &&
                                c.Color == tepih.Color && c.PerM2 == tepih.PerM2)
                    .ToListAsync();

                if (istiProizvod.Count == 1)
                {
                    istiProizvod[0].Quantity += tepih.Quantity;
                    istiProizvod[0].Price = tepih.Price;
                    _tepihRepository.Update(istiProizvod[0]);
                    tepih.Id = istiProizvod[0].Id;
                }
                else
                {
                    tepih.QRCodeUrl = url;
                    tepih.DateTime = time;
                    _tepihRepository.Add(tepih);
                }

                // Save changes and redirect with the ID only
                await _context.SaveChangesAsync(); // or use _tepihRepository.SaveChangesAsync() if applicable
                return RedirectToAction("GenerateCloudinaryImagePdf", "Pdf", new { id = tepih.Id });
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
                var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
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
            var extractData = data.Split("/");
            //var extractData = data.Split("\\");

            var item = new Tepih();

            if (extractData.Length == 8) {
                if (decimal.TryParse(extractData[7], out decimal value))
                {
                    int decimalPlaces = extractData[7].Contains(".") ? extractData[7].Split('.')[1].Length : 0;

                    if (decimalPlaces == 1)
                    {
                        extractData[7] = value.ToString("F2");
                    }
                    if (decimalPlaces == 0)
                    {
                        extractData[7] = value.ToString("F2");
                    }
                    if (decimalPlaces >= 2)
                    {
                        extractData[7] = value.ToString("F2");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid decimal");
                }

                if (String.IsNullOrEmpty(extractData[3]) && String.IsNullOrEmpty(extractData[4]))
                {
                    var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0]
                    && i.Model == extractData[1]
                    && i.ProductNumber == extractData[2]
                    && i.Color == extractData[5]
                    && i.PerM2.ToString() == extractData[6]
                    && i.Price.ToString() == extractData[7]);
                    if (itemm == null)
                    {
                        return Json(new { success = false, message = "Product not found" });
                    }
                    item = itemm;
                }
                else
                {
                    var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0]
                    && i.Model == extractData[1]
                    && i.ProductNumber == extractData[2]
                    && i.Length.ToString() == extractData[3]
                    && i.Width.ToString() == extractData[4]
                    && i.Color == extractData[5]
                    && i.PerM2.ToString() == extractData[6]
                    && i.Price.ToString() == extractData[7]);
                    if (itemm == null)
                    {
                        return Json(new { success = false, message = "Product not found" });
                    }
                    item = itemm;
                }
            }
            else if (extractData.Length == 5)
            {
                if (String.IsNullOrEmpty(extractData[2]) && String.IsNullOrEmpty(extractData[3]))
                {
                    var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0]
                    && i.Model == extractData[1]
                    && i.Color == extractData[4]);
                    if (itemm == null)
                    {
                        return Json(new { success = false, message = "Product not found" });
                    }
                    item = itemm;
                }
                else
                {
                    var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0]
                    && i.Model == extractData[1]
                    && i.Length.ToString() == extractData[3]
                    && i.Width.ToString() == extractData[2]
                    && i.Color == extractData[4]);
                    if (itemm == null)
                    {
                        return Json(new { success = false, message = "Product not found" });
                    }
                    item = itemm;
                }
            }

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
                    if (matchingvalue.PerM2)
                    {
                        matchingvalue.M2Total = ((decimal)((int)item.Length * (int)item.Width) / 10000) * matchingvalue.Quantity;
                    }
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
                        M2PerUnit = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2) : null,
                        M2Total = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000),2) : null,
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
                return Json(new { success = true });

            }
            return Json(new { success = false, message = "QR Code not recognized." });
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
                    matchingvalue.PriceTotal = (matchingvalue.Price * (decimal)matchingvalue.M2Total);
                }

                if (matchingvalue.Rabat != null)
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
                Description = tepih.Description
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
                    Description = tepihVM.Description
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

            //var firstName = User.FindFirstValue(ClaimTypes.GivenName);
            //var lastName = User.FindFirstValue(ClaimTypes.Surname);
            var fullName = User.FindFirstValue(ClaimTypes.Name);
            var firstName = "";
            var lastName = "";

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var index = fullName.Skip(1)
                                    .Select((c, i) => new { c, i })
                                    .FirstOrDefault(x => char.IsUpper(x.c))?.i;

                if (index != null)
                {
                    index += 1; // adjust because we skipped first char
                    firstName = fullName.Substring(0, index.Value);
                    lastName = fullName.Substring(index.Value);
                }
                else
                {
                    firstName = fullName; // fallback if no capital letter found
                }
            }

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
                    PlannedPaymentType = spovm.PlannedPaymentType,
                    Prodavac = $"{firstName} {lastName}"
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

            if (spovm.PrintPDF)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf, PageSize.A5);

                    // Optional: Set smaller margins for A5
                    document.SetMargins(20, 10, 20, 10); // top, right, bottom, left
                    document.SetFontSize(7);

                    document.Add(new iText.Layout.Element.Paragraph($"Prodavac: {firstName} {lastName}").SetMarginBottom(1));
                    document.Add(new iText.Layout.Element.Paragraph($"Kupac: {spovm.FullName}").SetMarginBottom(1));
                    document.Add(new iText.Layout.Element.Paragraph($"Datum: {spovm.PurchaseTime}").SetMarginBottom(1));
                    document.Add(new iText.Layout.Element.Paragraph("\n"));

                    Table table = new Table(8).UseAllAvailableWidth();
                    string[] headers = { "Sifra", "Ime", "Cijena", "Velicina", "Kol.", "m²", "ukupno m²", "Iznos" };

                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new iText.Layout.Element.Paragraph(header))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SetBold()
                            .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                            .SetPadding(1)
                            .SetHeight(10));
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
                                    Price = grouped.Average(g => g.product.Price),
                                    Length = grouped.Key.Length,
                                    Width = grouped.Key.Width,
                                    Size = grouped.Key.Length != null && grouped.Key.Width != null ? $"{grouped.Key.Width}X{grouped.Key.Length}" : "",
                                    M2PerUnit = grouped.Key.Length != null && grouped.Key.Width != null ? grouped.Key.M2PerUnit : null,
                                    M2Total = grouped.Key.Length != null && grouped.Key.Width != null ? grouped.Sum(g => g.product.M2Total) : null,
                                    Quantity = grouped.Sum(g => g.product.Quantity),
                                    PriceTotal = grouped.Sum(g => g.product.PriceTotal)
                                };

                    var salesReport = query.ToList();

                    decimal? totalSum = 0;
                    decimal? totalM2 = 0;
                    int totalQuantity = 0;

                    foreach (var item in salesReport)
                    {
                        table.AddCell(CreateCenteredCell(item.ProductNumber));
                        table.AddCell(CreateCenteredCell(item.Name));
                        table.AddCell(CreateCenteredCell(Math.Round(item.Price, 2).ToString() + "€"));
                        table.AddCell(CreateCenteredCell(item.Size));
                        table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2PerUnit.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2Total.ToString()));
                        table.AddCell(CreateCenteredCell(Math.Round(item.PriceTotal, 2).ToString() + "€"));

                        totalSum += item.PriceTotal;
                        totalM2 += item.M2Total;
                        totalQuantity += item.Quantity;
                    }

                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell("UKUPNO:"));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(Math.Round((decimal)totalM2, 2).ToString()));
                    table.AddCell(CreateCenteredBoldCell(Math.Round((decimal)totalSum, 2).ToString() + "€"));
                    document.Add(table);
                    document.Close();

                    return File(stream.ToArray(), "application/pdf", "OrderDetails.pdf");
                }
            }
            TempData["SuccessMessage"] = "Uspješna prodaja";
            return View("ScannedProductsToBePurchased", spovm);
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

                if (item.PerM2)
                {
                    item.M2Total = item.Quantity * item.M2PerUnit;
                }

                if (!item.PerM2)
                {
                    item.PriceTotal = item.Price * item.Quantity;
                }
                else
                {
                    item.PriceTotal = (item.Price * (decimal)item.M2Total);
                }

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                var response = new
                {
                    qty = item.Quantity,
                    m2Total = item.M2Total,
                };

                return Json(response) ;
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

        private Cell CreateCenteredCell(string text)
        {
            return new Cell()
                .Add(new iText.Layout.Element.Paragraph(text))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(1)
                    .SetHeight(10);
        }
        private Cell CreateCenteredBoldCell(string text)
        {
            return new Cell()
                .Add(new iText.Layout.Element.Paragraph(text))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(1)
                    .SetHeight(10)
                    .SetBold();
        }
        public IActionResult ManuallyAddProduct(int id)
        {
            var item = _context.Tepisi.FirstOrDefault(i => i.Id == id);
            if (item == null)
            {
                TempData["ProductNotFound"] = "Product not found!";
                return RedirectToAction("QRCodeScanning");
            }

            if (item != null)
            {
                List<ScannedProductViewModel> scannedProds = GetScannedProducts();
                var matchingvalue = scannedProds.FirstOrDefault(i => i.Id == item.Id);

                if (matchingvalue != null)
                {
                    matchingvalue.Quantity++;
                    if (matchingvalue.PerM2)
                    {
                        matchingvalue.M2Total = ((decimal)((int)item.Length * (int)item.Width) / 10000) * matchingvalue.Quantity;
                    }
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
                        M2PerUnit = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2) : null,
                        M2Total = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2) : null,
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
                return RedirectToAction("QRCodeScanning","InventoryItem");
            }

            ViewBag.Error = "QR Code not found!";
            return View("Error");
        }

        [HttpPost]
        public IActionResult ClearSession()
        {
            HttpContext.Session.Remove("scannedProducts");
            return Ok();
        }
    }
}