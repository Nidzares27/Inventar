using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using ZXing.QrCode;
using ZXing;
using Inventar.Models;
using Inventar.Interfaces;
using Inventar.Data;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using Inventar.Utils;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.Security.Claims;
using Inventar.ViewModels.Inventory;
using iText.Kernel.Geom;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Inventar.Services;
using SendGrid;
using static System.Net.Mime.MediaTypeNames;
using iText.IO.Font;
using iText.Kernel.Font;
using static iText.Kernel.Font.PdfFontFactory;
using static System.Formats.Asn1.AsnWriter;

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
        private readonly ILogger<InventoryItemController> _logger;
        private readonly ISessionService _sessionService;
        private readonly IWebHostEnvironment _env;

        public InventoryItemController(ITepihRepository tepihRepository, ApplicationDbContext context, IPhotoService photoService, ISalesRepository salesRepository, IKupacRepository kupacRepository, IPlacanjeRepository placanjeRepository, ILogger<InventoryItemController> logger, ISessionService sessionService, IWebHostEnvironment env)
        {
            this._tepihRepository = tepihRepository;
            this._context = context;
            this._photoService = photoService;
            this._salesRepository = salesRepository;
            this._kupacRepository = kupacRepository;
            this._placanjeRepository = placanjeRepository;
            this._logger = logger;
            this._sessionService = sessionService;
            this._env = env;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var tepisi = await _tepihRepository.GetAllUndisabledAsync();

                return View(tepisi ?? new List<Tepih>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory!");
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);

            if (tepih == null)
            {
                _logger.LogError("Inventory Details: No product was found matching this ID: {id}", id);
                return NotFound("No product was found with this ID!");
            }

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
                if (!tepih.PerM2 && ((tepih.Width != null && tepih.Length == null) || (tepih.Width == null && tepih.Length != null)))
                {
                    TempData["MissingLengthWidth"] = "Proizvod koji se NE prodaje po m² može se kreirati bez Dužine i Širine ILI sa Dužinom i Širinom. (Nije dozvoljeno unijeti samo širinu ili samo dužinu)!";
                    return View(tepih);
                }

                var time = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                try
                {
                    var qrCodeImageUrl = await GenerateQRCode($"{tepih.Name.ToUpper().Trim()}/{tepih.Model.ToUpper().Trim()}/{tepih.ProductNumber.ToUpper().Trim()}/{tepih.Length}/{tepih.Width}/{tepih.Color.ToUpper().Trim()}/{tepih.PerM2}");/*/{ tepih.Price}*/
                    //var qrCodeImageUrl = await GenerateQRCode($"{tepih.Name.ToUpper().Trim()}/{tepih.Model.ToUpper().Trim()}/{tepih.Width}/{tepih.Length}/{tepih.Color.ToUpper().Trim()}");

                    var url = "";
                    if (qrCodeImageUrl is OkObjectResult okResult)
                    {
                        var value = okResult.Value as dynamic;
                        url = value?.url;
                    }

                    var istiProizvod = await _context.Tepisi
                        .Where(c => c.Name == tepih.Name.ToUpper().Trim() && c.Model == tepih.Model.ToUpper().Trim() &&
                                    c.ProductNumber == tepih.ProductNumber.ToUpper().Trim() &&
                                    c.Length == tepih.Length && c.Width == tepih.Width &&
                                    c.Color == tepih.Color.ToUpper().Trim() && c.PerM2 == tepih.PerM2 &&
                                    c.Disabled == false)
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
                        tepih.Name = tepih.Name.ToUpper().Trim();
                        tepih.Model = tepih.Model.ToUpper().Trim();
                        tepih.ProductNumber = tepih.ProductNumber.ToUpper().Trim();
                        tepih.Color = tepih.Color.ToUpper().Trim();
                        tepih.QRCodeUrl = url;
                        tepih.DateTime = time;
                        tepih.Disabled = false;
                        _tepihRepository.Add(tepih);
                    }

                    await _context.SaveChangesAsync();
                    return RedirectToAction("GenerateCloudinaryImagePdf", "Pdf", new { id = tepih.Id });
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error creating new product!");
                    return StatusCode(500, "An error occurred while creating new product!");
                }
            }
            return View(tepih);
        }

        //public async Task<IActionResult> GenerateQRCode(string data)
        //{
        //    // Step 1: Generate the QR code using ZXing.Net
        //    var qrCodeWriter = new BarcodeWriterPixelData
        //    {
        //        Format = BarcodeFormat.QR_CODE,
        //        Options = new QrCodeEncodingOptions
        //        {
        //            Height = 250,    // Set height
        //            Width = 250,     // Set width
        //            Margin = 1       // Set margin
        //        }
        //    };

        //    // Generate QR code pixel data
        //    var pixelData = qrCodeWriter.Write(data);

        //    // Step 2: Create Bitmap from pixel data
        //    using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        //    {
        //        // Copy the pixel data to the bitmap
        //        var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
        //        try
        //        {
        //            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
        //        }
        //        finally
        //        {
        //            bitmap.UnlockBits(bitmapData);
        //        }

        //        // Step 3: Save the bitmap to a memory stream as PNG
        //        using (var stream = new MemoryStream())
        //        {
        //            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); // Save as PNG
        //            stream.Position = 0; // Reset stream position for upload

        //            // Step 4: Upload to Cloudinary
        //            var newGuid = $"{Guid.NewGuid()}.png";

        //            var uploadResult = await _photoService.UploadToCloudinary(newGuid, stream);

        //            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
        //            {
        //                // Return the Cloudinary URL of the uploaded QR code
        //                return Ok(new { url = uploadResult.SecureUrl.ToString() });
        //            }
        //            else
        //            {
        //                return StatusCode(500, "QR code upload to Cloudinary failed");
        //            }
        //        }
        //    }
        //}
        [HttpPost]
        public async Task<IActionResult> GenerateQRCode(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                _logger.LogError("Data for writting QR code is missing");
                return BadRequest("QR code data must not be empty.");
            }

            try
            {
                // Step 1: Generate the QR code using ZXing.Net
                var qrCodeWriter = new BarcodeWriterPixelData
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Height = 250,
                        Width = 250,
                        Margin = 1
                    }
                };

                var pixelData = qrCodeWriter.Write(data);

                using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var bitmapData = bitmap.LockBits(
                        new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        bitmap.PixelFormat);

                    try
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }

                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;

                        var fileName = $"{Guid.NewGuid()}.png";
                        var uploadResult = await _photoService.UploadToCloudinary(fileName, stream);

                        if (uploadResult == null || uploadResult.SecureUrl == null)
                        {
                            _logger.LogError("QR code upload failed: no response from image service.");
                            return StatusCode(500, "QR code upload failed: no response from image service.");
                        }

                        if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            return Ok(new { url = uploadResult.SecureUrl.ToString() });
                        }
                        _logger.LogError("QR code upload to Cloudinary failed.");
                        return StatusCode((int)uploadResult.StatusCode, "QR code upload to Cloudinary failed.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QR code generation/upload failed.");
                return StatusCode(500, "An error occurred while generating the QR code.");
            }
        }


        [HttpGet]
        public async Task<IActionResult> QRCodeScanning(int? id)
        {
            List<ScannedProductViewModel> scannedProds = GetScannedProducts();
            scannedProds.Reverse();
            return View(scannedProds);
        }

        [HttpGet]
        public async Task<IActionResult> QRCodeScanning2()
        {
            return View("QRCodeScanning");
        }

        public async Task<IActionResult> ProcessQRCode(string data)
        {
            try
            {
                var extractData = data.Split("/");
                //var extractData = data.Split("\\");

                var item = new Tepih();
                if (extractData.Length == 7) /*8*/
                {
                    //if (decimal.TryParse(extractData[7], out decimal value))
                    //{
                    //    int decimalPlaces = extractData[7].Contains(".") ? extractData[7].Split('.')[1].Length : 0;

                    //    if (decimalPlaces == 1)
                    //    {
                    //        extractData[7] = value.ToString("F2");
                    //    }
                    //    if (decimalPlaces == 0)
                    //    {
                    //        extractData[7] = value.ToString("F2");
                    //    }
                    //    if (decimalPlaces >= 2)
                    //    {
                    //        extractData[7] = value.ToString("F2");
                    //    }
                    //}
                    //else
                    //{
                    //    _logger.LogWarning("ProcessQRCode: Price has invalid format: {price}", extractData[7]);
                    //    return StatusCode(500, "An error because price for product you are trying to scan is not well formated.");
                    //}

                    if (System.String.IsNullOrEmpty(extractData[3]) && System.String.IsNullOrEmpty(extractData[4]))
                    {
                        var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0].Trim()
                        && i.Model == extractData[1].Trim()
                        && i.ProductNumber == extractData[2].Trim()
                        && i.Color == extractData[5].Trim()
                        && i.PerM2.ToString() == extractData[6].Trim()
                        //&& i.Price.ToString() == extractData[7].Trim()
                        && i.Disabled != true);
                        if (itemm == null)
                        {
                            _logger.LogWarning("ProcessQRCode: Couldn't find a product with properties matching QR Code data: {data}", data);
                            return Json(new { success = false, message = "Product not found" });
                        }
                        item = itemm;
                    }
                    else
                    {
                        var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0].Trim()
                        && i.Model == extractData[1].Trim()
                        && i.ProductNumber == extractData[2].Trim()
                        && i.Length.ToString() == extractData[3].Trim()
                        && i.Width.ToString() == extractData[4].Trim()
                        && i.Color == extractData[5].Trim()
                        && i.PerM2.ToString() == extractData[6].Trim()
                        //&& i.Price.ToString() == extractData[7].Trim()
                        && i.Disabled != true);
                        if (itemm == null)
                        {
                            _logger.LogWarning("ProcessQRCode: Couldn't find a product with properties matching QR Code data: {data}", data);
                            return Json(new { success = false, message = "Product not found" });
                        }
                        item = itemm;
                    }
                }
                else if (extractData.Length == 5)
                {
                    if (System.String.IsNullOrEmpty(extractData[2]) && System.String.IsNullOrEmpty(extractData[3]))
                    {
                        var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0].Trim()
                        && i.Model == extractData[1].Trim()
                        && i.Color == extractData[4].Trim()
                        && i.Disabled != true);
                        if (itemm == null)
                        {
                            _logger.LogWarning("ProcessQRCode: Couldn't find a product with properties matching QR Code data: {data}", data);
                            return Json(new { success = false, message = "Product not found" });
                        }
                        item = itemm;
                    }
                    else
                    {
                        var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0].Trim()
                        && i.Model == extractData[1].Trim()
                        && i.Width.ToString() == extractData[2].Trim()
                        && i.Length.ToString() == extractData[3].Trim()
                        && i.Color == extractData[4].Trim()
                        && i.Disabled != true);
                        if (itemm == null)
                        {
                            _logger.LogWarning("ProcessQRCode: Couldn't find a product with properties matching QR Code data: {data}", data);
                            return Json(new { success = false, message = "Product not found" });
                        }
                        item = itemm;
                    }
                }
                else
                {
                    _logger.LogWarning("ProcessQRCode: QR Code data doesn't follow required structure: {data}", data);
                    return Json(new { success = false, message = "Product not found" });
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
                            M2Total = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2) : null,
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
                _logger.LogError("ProcessQRCode: Something went wrong while proccessing QR Code data: {data}", data);
                return Json(new { success = false, message = "QR Code not recognized." });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "QR code processing went wrong for QR code with data: {data}.",data);
                return StatusCode(500, "An error occurred while processing the QR code.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] ScannedProductViewModel modell)
        {
            try
            {
                if (modell.Price < 0)
                {
                    _logger.LogWarning("Invalid price submitted for product ID {Id}. Price: {Price}", modell.Id, modell.Price);
                    return Json(new { success = false, message = "Invalid price." });
                }

                List<ScannedProductViewModel> scannedProds = GetScannedProducts();
                var matchingvalue = scannedProds.FirstOrDefault(i => i.Id == modell.Id);
                if (matchingvalue == null) {
                    _logger.LogError("Update price for scanned product: Product was not found in scanned products: {prodId}. Full model: {model} ", modell.Id, modell);
                    return Json(new { success = false });
                }
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
            catch(Exception ex)
            {
                _logger.LogError(ex, "Applying discount or changing price mannualy went wrong.");
                return StatusCode(500, "An error occurred while updating price for a product.");
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            if (tepih == null)
            {
                _logger.LogError("Delete product: No product was found matching this ID: {id}", id);
                return NotFound("No product was found for deleting!");
            }
            return View(tepih);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteTepih(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsync(id);
            if (tepih == null)
            {
                _logger.LogWarning("DeleteTepih: Tepih with ID {id} not found.", id);
                return NotFound("Product not found.");
            }

            if (!string.IsNullOrWhiteSpace(tepih.QRCodeUrl))
            {
                var publicId = CloudinaryHelper.GetPublicIdFromUrlFromFolder(tepih.QRCodeUrl);

                try
                {
                    await _photoService.DeletePhotoAsync(publicId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete QR code image for product {id} with URL: {url}", id, tepih.QRCodeUrl);
                    return StatusCode(500, "An error occurred while deleting the QR code image.");
                }
            }

            try
            {
                tepih.Disabled = true;
                _tepihRepository.Update(tepih);
                //_tepihRepository.Delete(tepih);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disabling Tepih with ID {id}", id);
                return StatusCode(500, "An error occurred while deleting the product.");
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            if (tepih == null)
            {
                _logger.LogWarning("EditTepih: Tepih with ID {id} not found.", id);
                return NotFound("Product not found.");
            }
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
                Description = tepih.Description,
                Disabled = tepih.Disabled
            };

            return View(tepihVM);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditTepihViewModel tepihVM)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje tepiha nije uspjelo");
                _logger.LogWarning("EditTepih post: ModelState is Invalid!");
                return View("Edit", tepihVM);
            }

            var proizvod = await _tepihRepository.GetByIdAsyncNoTracking(id);

            if (proizvod != null)
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
                    Description = tepihVM.Description,
                    Disabled = tepihVM.Disabled
                };

                try
                {
                    _tepihRepository.Update(tepihEdit);
                    return RedirectToAction("Index");
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex,"EditTepih post: Editing product with id {id} failed!!! ",id);
                    return StatusCode(500, "An error occurred while editing the product.");
                }
            }
            else
            {
                return View(tepihVM);
            }
        }
        //public List<ScannedProductViewModel> GetScannedProducts()
        //{
        //    var stepFormData = new List<ScannedProductViewModel>();

        //    var serializedModel = HttpContext.Session.GetString("scannedProducts");

        //    if (!string.IsNullOrEmpty(serializedModel))
        //    {
        //        stepFormData = JsonConvert.DeserializeObject<List<ScannedProductViewModel>>(serializedModel);
        //    }

        //    return stepFormData;
        //}

        private const string ScannedProductsSessionKey = "scannedProducts";

        public List<ScannedProductViewModel> GetScannedProducts()
        {
            var serialized = HttpContext.Session.GetString(ScannedProductsSessionKey);

            if (string.IsNullOrWhiteSpace(serialized))
                return new List<ScannedProductViewModel>();

            try
            {
                return JsonConvert.DeserializeObject<List<ScannedProductViewModel>>(serialized)
                       ?? new List<ScannedProductViewModel>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize scanned products from session.");
                return new List<ScannedProductViewModel>();
            }
        }


        [HttpGet]
        public IActionResult ScannedProductsToBePurchased()
        {
            var scannedProductsOverview = new ScannedProductsOverviewViewModel();
            scannedProductsOverview.Products = GetScannedProducts();

            // If there was a message, show it (in your View)
            if (TempData["SuccessMessage"] != null)
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            if (TempData["ErrorMessage"] != null)
                ViewBag.ErrorMessage = TempData["ErrorMessage"].ToString();

            return View(scannedProductsOverview);
        }

        //[HttpPost]
        //public async Task<IActionResult> ScannedProductsToBePurchased(ScannedProductsOverviewViewModel spovm)
        //{
        //    var purchTime = DateTime.Now;
        //    spovm.PurchaseTime = DateTime.ParseExact(purchTime.ToString("HH:mm:ss dd/MM/yyyy"), "HH:mm:ss dd/MM/yyyy", null);
        //    spovm.Products = GetScannedProducts();

        //    var firstName = User.FindFirstValue(ClaimTypes.GivenName);
        //    var lastName = User.FindFirstValue(ClaimTypes.Surname);

        //    decimal toPay = 0;
        //    foreach (var prod in spovm.Products)
        //    {
        //        var sale = new Prodaja()
        //        {
        //            TepihId = prod.Id,
        //            Quantity = prod.Quantity,
        //            CustomerFullName = spovm.FullName,
        //            VrijemeProdaje = spovm.PurchaseTime,
        //            Price = prod.Price,
        //            PlannedPaymentType = spovm.PlannedPaymentType,
        //            Prodavac = $"{firstName} {lastName}"
        //        };
        //        _salesRepository.Add(sale);

        //        Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(prod.Id);
        //        if (tepih == null) return View("Error");
        //        tepih.Quantity -= prod.Quantity;
        //        _tepihRepository.Update(tepih);
        //    }
        //    var kupci = await _kupacRepository.GetAll();
        //    bool exists = kupci.Any(s => s.CustomerFullName == spovm.FullName);
        //    if (!exists)
        //    {
        //        Kupac kupac = new Kupac()
        //        {
        //            CustomerFullName = spovm.FullName,
        //        };
        //        _kupacRepository.Add(kupac);
        //    }

        //    if (spovm.PrintPDF)
        //    {
        //        using (MemoryStream stream = new MemoryStream())
        //        {
        //            PdfWriter writer = new PdfWriter(stream);
        //            PdfDocument pdf = new PdfDocument(writer);
        //            Document document = new Document(pdf, PageSize.A5);

        //            // Optional: Set smaller margins for A5
        //            document.SetMargins(20, 10, 20, 10); // top, right, bottom, left
        //            document.SetFontSize(7);

        //            document.Add(new iText.Layout.Element.Paragraph($"Prodavac: {firstName} {lastName}").SetMarginBottom(1));
        //            document.Add(new iText.Layout.Element.Paragraph($"Kupac: {spovm.FullName}").SetMarginBottom(1));
        //            document.Add(new iText.Layout.Element.Paragraph($"Datum: {spovm.PurchaseTime}").SetMarginBottom(1));
        //            document.Add(new iText.Layout.Element.Paragraph("\n"));

        //            Table table = new Table(8).UseAllAvailableWidth();
        //            string[] headers = { "Sifra", "Ime", "Cijena", "Velicina", "Kol.", "m²", "ukupno m²", "Iznos" };

        //            foreach (var header in headers)
        //            {
        //                table.AddHeaderCell(new Cell()
        //                    .Add(new iText.Layout.Element.Paragraph(header))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
        //                    .SetBold()
        //                    .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
        //                    .SetPadding(1)
        //                    .SetHeight(10));
        //            }
        //            var query = from product in spovm.Products
        //                        group new { product } by new
        //                        {
        //                            product.Name,
        //                            product.Length,
        //                            product.Width,
        //                            product.M2PerUnit,
        //                            product.ProductNumber,
        //                            product.Price
        //                        }
        //            into grouped
        //                        select new ReceiptViewModel
        //                        {
        //                            ProductNumber = grouped.Key.ProductNumber,
        //                            Name = grouped.Key.Name,
        //                            Price = grouped.Average(g => g.product.Price),
        //                            Length = grouped.Key.Length,
        //                            Width = grouped.Key.Width,
        //                            Size = grouped.Key.Length != null && grouped.Key.Width != null ? $"{grouped.Key.Width}X{grouped.Key.Length}" : "",
        //                            M2PerUnit = grouped.Key.Length != null && grouped.Key.Width != null ? grouped.Key.M2PerUnit : null,
        //                            M2Total = grouped.Key.Length != null && grouped.Key.Width != null ? grouped.Sum(g => g.product.M2Total) : null,
        //                            Quantity = grouped.Sum(g => g.product.Quantity),
        //                            PriceTotal = grouped.Sum(g => g.product.PriceTotal)
        //                        };

        //            var salesReport = query.ToList();

        //            decimal? totalSum = 0;
        //            decimal? totalM2 = 0;
        //            int totalQuantity = 0;

        //            foreach (var item in salesReport)
        //            {
        //                table.AddCell(CreateCenteredCell(item.ProductNumber));
        //                table.AddCell(CreateCenteredCell(item.Name));
        //                table.AddCell(CreateCenteredCell(Math.Round(item.Price, 2).ToString() + "€"));
        //                table.AddCell(CreateCenteredCell(item.Size));
        //                table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
        //                table.AddCell(CreateCenteredCell(item.M2PerUnit.ToString()));
        //                table.AddCell(CreateCenteredCell(item.M2Total.ToString()));
        //                table.AddCell(CreateCenteredCell(Math.Round(item.PriceTotal, 2).ToString() + "€"));

        //                totalSum += item.PriceTotal;
        //                totalM2 += item.M2Total;
        //                totalQuantity += item.Quantity;
        //            }

        //            table.AddCell(CreateCenteredBoldCell(""));
        //            table.AddCell(CreateCenteredBoldCell(""));
        //            table.AddCell(CreateCenteredBoldCell(""));
        //            table.AddCell(CreateCenteredBoldCell("UKUPNO:"));
        //            table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
        //            table.AddCell(CreateCenteredBoldCell(""));
        //            table.AddCell(CreateCenteredBoldCell(Math.Round((decimal)totalM2, 2).ToString()));
        //            table.AddCell(CreateCenteredBoldCell(Math.Round((decimal)totalSum, 2).ToString() + "€"));
        //            document.Add(table);
        //            document.Close();

        //            return File(stream.ToArray(), "application/pdf", "OrderDetails.pdf");
        //        }
        //    }
        //    TempData["SuccessMessage"] = "Uspješna prodaja";
        //    return View("ScannedProductsToBePurchased", spovm);
        //}

        [HttpPost]
        public async Task<IActionResult> ScannedProductsToBePurchased(ScannedProductsOverviewViewModel spovm)
        {
            try
            {
                var purchaseTime = DateTime.Now;
                spovm.PurchaseTime = DateTime.ParseExact(purchaseTime.ToString("HH:mm:ss dd/MM/yyyy"), "HH:mm:ss dd/MM/yyyy", null);
                spovm.Products = GetScannedProducts();

                var firstName = User.FindFirstValue(ClaimTypes.GivenName);
                var lastName = User.FindFirstValue(ClaimTypes.Surname);
                var fullName = $"{firstName} {lastName}";

                foreach (var prod in spovm.Products)
                {
                    var sale = new Prodaja
                    {
                        TepihId = prod.Id,
                        Quantity = prod.Quantity,
                        CustomerFullName = spovm.FullName.ToUpper().Trim(),
                        VrijemeProdaje = spovm.PurchaseTime,
                        Price = prod.Price,
                        PlannedPaymentType = spovm.PlannedPaymentType,
                        Prodavac = fullName
                    };

                    _salesRepository.Add(sale);

                    try
                    {
                        Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(prod.Id);
                        if (tepih == null)
                        {
                            _logger.LogWarning("Tepih not found with ID {Id} during sale", prod.Id);
                            return NotFound("Product not found.");
                        }

                        tepih.Quantity -= prod.Quantity;
                        _tepihRepository.Update(tepih);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update Tepih quantity in stock for product ID {Id} after sale", prod.Id);
                        return StatusCode(500, "Greška pri ažuriranju količine proizvoda u inventaru nakon kupovine!");
                    }
                }

                try
                {
                    var kupci = await _kupacRepository.GetAll();
                    bool exists = kupci.Any(s => s.CustomerFullName.ToUpper() == spovm.FullName.ToUpper().Trim());
                    if (!exists)
                    {
                        _kupacRepository.Add(new Kupac { CustomerFullName = spovm.FullName.ToUpper().Trim() });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Adding new buyer: Error while checking/adding customer.");
                    return StatusCode(500, "Greška pri kreiranju novog kupca.");
                }

                if (spovm.PrintPDF)
                {
                    try
                    {
                        var pdfBytes = GeneratePurchasePdf(spovm, fullName);
                        return File(pdfBytes, "application/pdf", "OrderDetails.pdf");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "PDF generation failed for purchase.");
                        TempData["ErrorMessage"] = "Greška prilikom generisanja PDF-a.";
                        return RedirectToAction("ScannedProductsToBePurchased");
                        //return View("ScannedProductsToBePurchased", spovm);
                    }
                }

                TempData["SuccessMessage"] = "Uspješna prodaja";
                return RedirectToAction("ScannedProductsToBePurchased");
                //return View("ScannedProductsToBePurchased", spovm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during purchase process.");
                return StatusCode(500, "Dogodila se greška prilikom obrade kupovine.");
            }
        }


        private byte[] GeneratePurchasePdf(ScannedProductsOverviewViewModel spovm, string userFullName)
        {
            try
            {
                using var stream = new MemoryStream();
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A5);
                document.SetMargins(20, 10, 20, 10);
                document.SetFontSize(7);

                // Load font with error check
                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font file missing at {Path}", fontPath);
                    throw new Exception("Font path doesn't exist!");
                    //return StatusCode(500, "Font file not found.");
                }

                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);

                document.Add(new Paragraph($"Prodavac: {userFullName}").SetMarginBottom(1));
                document.Add(new Paragraph($"Kupac: {spovm.FullName.ToUpper().Trim()}").SetMarginBottom(1));
                document.Add(new Paragraph($"Datum: {spovm.PurchaseTime:dd/MM/yyyy HH:mm:ss}").SetMarginBottom(1));
                document.Add(new Paragraph("\n"));

                decimal? totalPrice = 0;
                decimal? totalM2 = 0;
                int totalQuantity = 0;

                var table = new Table(8).UseAllAvailableWidth();

                if (User.Identity.IsAuthenticated && (User.IsInRole("admin") || User.IsInRole("superadmin")))
                {
                    string[] headers = { "Šifra", "Ime", "Cijena", "Veličina", "Kol.", "m²", "ukupno m²", "Iznos" };

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

                    var groupedProducts = from p in spovm.Products
                                          group p by new { p.Name, p.Length, p.Width, p.M2PerUnit, p.ProductNumber, p.Price } into g
                                          select new ReceiptViewModel
                                          {
                                              ProductNumber = g.Key.ProductNumber,
                                              Name = g.Key.Name,
                                              Price = g.Average(p => p.Price),
                                              Size = $"{g.Key.Width}X{g.Key.Length}",
                                              M2PerUnit = g.Key.M2PerUnit,
                                              M2Total = g.Sum(p => p.M2Total),
                                              Quantity = g.Sum(p => p.Quantity),
                                              PriceTotal = g.Sum(p => p.PriceTotal)
                                          };

                    //decimal? totalPrice = 0;
                    //decimal? totalM2 = 0;
                    //int totalQuantity = 0;

                    foreach (var item in groupedProducts)
                    {
                        table.AddCell(CreateCenteredCell(item.ProductNumber));
                        table.AddCell(CreateCenteredCell(item.Name));
                        table.AddCell(CreateCenteredCell($"{Math.Round(item.Price, 2)}€"));
                        table.AddCell(CreateCenteredCell(item.Size));
                        table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2PerUnit?.ToString() ?? ""));
                        table.AddCell(CreateCenteredCell(item.M2Total?.ToString() ?? ""));
                        table.AddCell(CreateCenteredCell($"{Math.Round(item.PriceTotal, 2)}€"));

                        totalPrice += item.PriceTotal;
                        totalM2 += item.M2Total;
                        totalQuantity += item.Quantity;
                    }

                    // Totals row
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell("UKUPNO:"));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalM2 ?? 0, 2)}"));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalPrice ?? 0, 2)}€"));
                }
                else
                {
                    table = new Table(5).UseAllAvailableWidth();
                    string[] headers = {"Ime", "Veličina", "Količina", "m²", "ukupno m²"};

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

                    var groupedProducts = from p in spovm.Products
                                          group p by new { p.Name, p.Length, p.Width, p.M2PerUnit, p.ProductNumber, p.Price } into g
                                          select new ReceiptViewModel
                                          {
                                              ProductNumber = g.Key.ProductNumber,
                                              Name = g.Key.Name,
                                              Price = g.Average(p => p.Price),
                                              Size = $"{g.Key.Width}X{g.Key.Length}",
                                              M2PerUnit = g.Key.M2PerUnit,
                                              M2Total = g.Sum(p => p.M2Total),
                                              Quantity = g.Sum(p => p.Quantity),
                                              PriceTotal = g.Sum(p => p.PriceTotal)
                                          };

                    //decimal? totalPrice = 0;
                    //decimal? totalM2 = 0;
                    //int totalQuantity = 0;

                    foreach (var item in groupedProducts)
                    {
                        //table.AddCell(CreateCenteredCell(item.ProductNumber));
                        table.AddCell(CreateCenteredCell(item.Name));
                        //table.AddCell(CreateCenteredCell($"{Math.Round(item.Price, 2)}€"));
                        table.AddCell(CreateCenteredCell(item.Size));
                        table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2PerUnit?.ToString() ?? ""));
                        table.AddCell(CreateCenteredCell(item.M2Total?.ToString() ?? ""));
                        //table.AddCell(CreateCenteredCell($"{Math.Round(item.PriceTotal, 2)}€"));

                        totalPrice += item.PriceTotal;
                        totalM2 += item.M2Total;
                        totalQuantity += item.Quantity;
                    }

                    // Totals row
                    //table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(""));
                    //table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell("UKUPNO:"));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalM2 ?? 0, 2)}"));
                    //table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalPrice ?? 0, 2)}€"));
                }

                document.Add(table);
                document.Close();
                return stream.ToArray();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "PDF generation failed for purchase: {Customer}", spovm.FullName);
                throw;
            }

        }


        //[HttpPost]
        //public IActionResult DeleteScannedProduct(int id)
        //{
        //    var scannedProds = GetScannedProducts();
        //    var item = scannedProds.FirstOrDefault(i => i.Id == id);
        //    if (item != null)
        //    {
        //        scannedProds.Remove(item);
        //        HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
        //    }
        //    return View("QRCodeScanning", scannedProds);
        //}
        [HttpPost]
        public IActionResult DeleteScannedProduct(int id)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting scanned product with ID: {Id}", id);
                return StatusCode(500, "An error occurred while removing the product.");
            }
        }


        //[HttpPost]
        //public IActionResult UpdateQuantity(int id, string action)
        //{
        //    var scannedProds = GetScannedProducts();
        //    var item = scannedProds.FirstOrDefault(i => i.Id == id);
        //    if (item != null)
        //    {
        //        if (action == "increase")
        //        {
        //            item.Quantity += 1;
        //        }
        //        else if (action == "decrease" && item.Quantity > 1)
        //        {
        //            item.Quantity -= 1;
        //        }

        //        if (item.PerM2)
        //        {
        //            item.M2Total = item.Quantity * item.M2PerUnit;
        //        }

        //        if (!item.PerM2)
        //        {
        //            item.PriceTotal = item.Price * item.Quantity;
        //        }
        //        else
        //        {
        //            item.PriceTotal = (item.Price * (decimal)item.M2Total);
        //        }

        //        HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
        //        var response = new
        //        {
        //            qty = item.Quantity,
        //            m2Total = item.M2Total,
        //        };

        //        return Json(response);
        //    }
        //    return NotFound();
        //}
        [HttpPost]
        public IActionResult UpdateQuantity(int id, string action)
        {
            try
            {
                var scannedProds = GetScannedProducts();
                var item = scannedProds.FirstOrDefault(i => i.Id == id);

                if (item == null)
                {
                    _logger.LogWarning("UpdateQuantity: No scanned product found with ID {Id}");
                    return NotFound("Product couldn't be found!");
                }

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
                    item.PriceTotal = item.Price * (decimal)item.M2Total;
                }
                else
                {
                    item.PriceTotal = item.Price * item.Quantity;
                }

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));

                var response = new
                {
                    qty = item.Quantity,
                    m2Total = item.M2Total,
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating quantity for scanned product ID: {Id}", id);
                return StatusCode(500, "An error occurred while updating the quantity.");
            }
        }


        //[HttpGet]
        //public async Task<JsonResult> SearchPeople(string query)
        //{
        //    var matches = await _context.Kupci
        //        .Where(p => p.CustomerFullName.Contains(query))
        //        .Select(p => p.CustomerFullName)
        //        .ToListAsync();

        //    return Json(matches);
        //}
        [HttpGet]
        public async Task<JsonResult> SearchPeople(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Json(new List<string>());
                }

                var matches = await _context.Kupci
                    .Where(p => p.CustomerFullName.Contains(query))
                    .Select(p => p.CustomerFullName)
                    .ToListAsync();

                return Json(matches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchPeople failed for query: {Query}", query);
                return Json(new { error = "An error occurred while searching." });
            }
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
        //public IActionResult ManuallyAddProduct(int id)
        //{
        //    var item = _context.Tepisi.FirstOrDefault(i => i.Id == id);
        //    if (item == null || item.Disabled == true)
        //    {
        //        TempData["ProductNotFound"] = "Product not found!";
        //        return RedirectToAction("QRCodeScanning");
        //    }

        //    if (item != null)
        //    {
        //        List<ScannedProductViewModel> scannedProds = GetScannedProducts();
        //        var matchingvalue = scannedProds.FirstOrDefault(i => i.Id == item.Id);

        //        if (matchingvalue != null)
        //        {
        //            matchingvalue.Quantity++;
        //            if (matchingvalue.PerM2)
        //            {
        //                matchingvalue.M2Total = ((decimal)((int)item.Length * (int)item.Width) / 10000) * matchingvalue.Quantity;
        //            }
        //        }
        //        else
        //        {
        //            var tepihVM = new ScannedProductViewModel
        //            {
        //                Id = item.Id,
        //                ProductNumber = item.ProductNumber,
        //                Model = item.Model,
        //                Name = item.Name,
        //                Quantity = 1,
        //                Length = item.Length,
        //                Width = item.Width,
        //                M2PerUnit = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2) : null,
        //                M2Total = item.PerM2 ? Math.Round(((decimal)((int)item.Length * (int)item.Width) / 10000), 2) : null,
        //                Color = item.Color,
        //                Price = item.Price,
        //                PerM2 = item.PerM2,
        //            };
        //            if (!tepihVM.PerM2)
        //            {
        //                tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.Quantity), 2);
        //            }
        //            if (tepihVM.PerM2)
        //            {
        //                tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.M2Total), 2);
        //            }

        //            scannedProds.Add(tepihVM);
        //        }

        //        HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
        //        return RedirectToAction("QRCodeScanning", "InventoryItem");
        //    }

        //    ViewBag.Error = "QR Code not found!";
        //    return View("Error");
        //}

        public IActionResult ManuallyAddProduct(int id)
        {
            try
            {
                var item = _context.Tepisi.FirstOrDefault(i => i.Id == id);
                if (item == null || item.Disabled == true)
                {
                    TempData["ProductNotFound"] = "Product not found!";
                    _logger.LogWarning("ManuallyAddProduct: product with id {ProductId} was not found!", id);
                    return RedirectToAction("QRCodeScanning");
                }

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
                        tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.Quantity), 2);
                    }
                    else
                    {
                        tepihVM.PriceTotal = Math.Round((decimal)(tepihVM.Price * tepihVM.M2Total), 2);
                    }

                    scannedProds.Add(tepihVM);
                }

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                return RedirectToAction("QRCodeScanning", "InventoryItem");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to manually add product with id {ProductId}", id);
                return StatusCode(500, "An unexpected error occurred while manually adding the product.");
            }
        }


        [HttpPost]
        public IActionResult ClearSession()
        {
            try
            {
                //HttpContext.Session.Remove("scannedProducts");
                _sessionService.ClearScannedProducts(HttpContext.Session);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear 'scannedProducts' from session.");
                return StatusCode(500, "An error occurred while clearing session data.");
            }
        }

    }
}