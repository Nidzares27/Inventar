using ClosedXML.Excel;
using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Services;
using Inventar.Utils;
using Inventar.ViewModels.Inventory;
using Inventar.ViewModels.Pdf;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SendGrid;
using System.Data;
using System.Drawing;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ZXing;
using ZXing.QrCode;
using static iText.Kernel.Font.PdfFontFactory;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Inventar.Controllers
{
    [Authorize]
    public class InventoryItemController : Controller
    {
        private readonly ITepihRepository _tepihRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly IPlacanjeRepository _placanjeRepository;
        private readonly ILogger<InventoryItemController> _logger;
        private readonly ISessionService _sessionService;
        private readonly IWebHostEnvironment _env;

        public InventoryItemController(ITepihRepository tepihRepository, ApplicationDbContext context, IPhotoService photoService, IPlacanjeRepository placanjeRepository, ILogger<InventoryItemController> logger, ISessionService sessionService, IWebHostEnvironment env)
        {
            this._tepihRepository = tepihRepository;
            this._context = context;
            this._photoService = photoService;
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
            Tepih tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

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
                    var qrCodeImageUrl = await GenerateQRCode($"{tepih.Name.ToUpper().Trim()}/{tepih.Model.ToUpper().Trim()}/{tepih.ProductNumber.ToUpper().Trim()}/{tepih.Width}/{tepih.Length}/{tepih.Color.ToUpper().Trim()}/{tepih.PerM2}");/*/{ tepih.Price}*/
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
                        istiProizvod[0].OnlinePrice ??= istiProizvod[0].Price;
                        if (string.IsNullOrWhiteSpace(istiProizvod[0].Slug))
                        {
                            istiProizvod[0].Slug = ProductSlugHelper.BuildDefaultSlug(istiProizvod[0]);
                        }
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
                        tepih.OnlinePrice ??= tepih.Price;
                        _tepihRepository.Add(tepih);
                        tepih.Slug = ProductSlugHelper.BuildDefaultSlug(tepih);
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
                        var uploadResult = await _photoService.UploadToCloudinary(fileName, stream, "TepisiQRCodes");

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
                // ------------------------------
                // DUPLICATE FAST-SCAN PROTECTION
                // ------------------------------
                var lastScan = HttpContext.Session.GetString("lastScanValue");
                var lastScanTimeString = HttpContext.Session.GetString("lastScanTime");

                if (lastScan == data && lastScanTimeString != null)
                {
                    var lastScanTime = DateTime.Parse(lastScanTimeString);

                    // If scanned again within 800 milliseconds → IGNORE
                    if ((DateTime.Now - lastScanTime).TotalMilliseconds < 800)
                    {
                        return Json(new { success = false, message = "Duplicate fast scan ignored" });
                    }
                }

                // Save current scan as last scan
                HttpContext.Session.SetString("lastScanValue", data);
                HttpContext.Session.SetString("lastScanTime", DateTime.Now.ToString());

                var extractData = data.Split("/");

                var item = new Tepih();
                if (extractData.Length == 7)
                {
                    if (System.String.IsNullOrEmpty(extractData[3]) && System.String.IsNullOrEmpty(extractData[4]))
                    {
                        var itemm = _context.Tepisi.FirstOrDefault(i => i.Name == extractData[0].Trim()
                        && i.Model == extractData[1].Trim()
                        && i.ProductNumber == extractData[2].Trim()
                        && i.Color == extractData[5].Trim()
                        && i.PerM2.ToString() == extractData[6].Trim()
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
                        && i.Width.ToString() == extractData[3].Trim()
                        && i.Length.ToString() == extractData[4].Trim()
                        && i.Color == extractData[5].Trim()
                        && i.PerM2.ToString() == extractData[6].Trim()
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
                        /*matchingvalue.Price -= rbt * matchingvalue.Price;*/ //OVO VJEROVATNO MORAMO KOMENTARISATI I PROMJENITI PRODAJE
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
                var storefrontImages = await _context.ProductImages
                    .Where(image => image.TepihId == tepih.Id && !image.Disabled)
                    .ToListAsync();

                foreach (var storefrontImage in storefrontImages)
                {
                    storefrontImage.Disabled = true;
                }

                _tepihRepository.Update(tepih);
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
            Tepih tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tepih == null)
            {
                _logger.LogWarning("EditTepih: Tepih with ID {id} not found.", id);
                return NotFound("Product not found.");
            }
            var tepihVM = new EditTepihViewModel
            {
                Id = tepih.Id,
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
                OnlinePrice = tepih.OnlinePrice,
                PerM2 = tepih.PerM2,
                Description = tepih.Description,
                ShortDescription = tepih.ShortDescription,
                SeoTitle = tepih.SeoTitle,
                SeoDescription = tepih.SeoDescription,
                Slug = tepih.Slug,
                IsPublished = tepih.IsPublished,
                ReservedQuantity = tepih.ReservedQuantity,
                AvailableQuantity = CalculateAvailableQuantity(tepih),
                ProductImages = MapProductImages(tepih.ProductImages),
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

            if (proizvod == null)
            {
                return NotFound("Product not found.");
            }

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
                OnlinePrice = proizvod.OnlinePrice ?? tepihVM.Price,
                PerM2 = tepihVM.PerM2,
                Description = tepihVM.Description,
                ShortDescription = proizvod.ShortDescription,
                SeoTitle = proizvod.SeoTitle,
                SeoDescription = proizvod.SeoDescription,
                Slug = proizvod.Slug,
                IsPublished = proizvod.IsPublished,
                ReservedQuantity = proizvod.ReservedQuantity,
                RowVersion = proizvod.RowVersion,
                Disabled = proizvod.Disabled
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadStorefrontImages(int id, List<IFormFile> files, string? altText)
        {
            var tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .FirstOrDefaultAsync(t => t.Id == id && !t.Disabled);

            if (tepih == null)
            {
                return NotFound("Product not found.");
            }

            if (files == null || files.Count == 0)
            {
                TempData["StorefrontErrorMessage"] = "Select at least one image to upload.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var activeImages = tepih.ProductImages.Where(image => !image.Disabled).ToList();
            var hasPrimary = activeImages.Any(image => image.IsPrimary);
            var nextSortOrder = activeImages.Any() ? activeImages.Max(image => image.SortOrder) + 1 : 1;
            var successfulUploads = 0;

            foreach (var file in files.Where(file => file.Length > 0))
            {
                if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["StorefrontErrorMessage"] = $"'{file.FileName}' is not an image.";
                    continue;
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                var uploadResult = await _photoService.UploadToCloudinary(
                    file.FileName,
                    stream,
                    "StorefrontProducts");

                if (uploadResult.SecureUrl == null || string.IsNullOrWhiteSpace(uploadResult.PublicId))
                {
                    TempData["StorefrontErrorMessage"] = $"Upload failed for '{file.FileName}'.";
                    continue;
                }

                _context.ProductImages.Add(new ProductImage
                {
                    TepihId = tepih.Id,
                    CloudinaryPublicId = uploadResult.PublicId,
                    Url = uploadResult.SecureUrl.ToString(),
                    ThumbnailUrl = uploadResult.SecureUrl.ToString(),
                    AltText = string.IsNullOrWhiteSpace(altText) ? tepih.Name : altText.Trim(),
                    IsPrimary = !hasPrimary,
                    SortOrder = nextSortOrder++,
                    Disabled = false,
                    CreatedUtc = DateTime.UtcNow
                });

                hasPrimary = true;
                successfulUploads++;
            }

            await _context.SaveChangesAsync();

            if (successfulUploads > 0)
            {
                TempData["StorefrontSuccessMessage"] = successfulUploads == 1
                    ? "1 image uploaded."
                    : $"{successfulUploads} images uploaded.";
            }
            else if (TempData["StorefrontErrorMessage"] == null)
            {
                TempData["StorefrontErrorMessage"] = "No images were uploaded.";
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryStorefrontImage(int id, int imageId)
        {
            var images = await _context.ProductImages
                .Where(image => image.TepihId == id && !image.Disabled)
                .ToListAsync();

            if (images.Count == 0)
            {
                return NotFound("No product images found.");
            }

            if (!images.Any(image => image.Id == imageId))
            {
                return NotFound("Product image not found.");
            }

            foreach (var image in images)
            {
                image.IsPrimary = image.Id == imageId;
            }

            await _context.SaveChangesAsync();
            TempData["StorefrontSuccessMessage"] = "Primary image updated.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStorefrontImage(int id, int imageId)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(productImage => productImage.Id == imageId && productImage.TepihId == id && !productImage.Disabled);

            if (image == null)
            {
                return NotFound("Product image not found.");
            }

            try
            {
                await _photoService.DeletePhotoAsync(image.CloudinaryPublicId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete storefront image {ImageId} from Cloudinary.", imageId);
                TempData["StorefrontErrorMessage"] = "Cloudinary image deletion failed. The record was still removed from the catalog.";
            }

            image.Disabled = true;
            image.IsPrimary = false;

            var replacementPrimary = await _context.ProductImages
                .Where(productImage => productImage.TepihId == id && !productImage.Disabled && productImage.Id != imageId)
                .OrderBy(productImage => productImage.SortOrder)
                .FirstOrDefaultAsync();

            if (replacementPrimary != null)
            {
                replacementPrimary.IsPrimary = true;
            }

            await _context.SaveChangesAsync();
            TempData["StorefrontSuccessMessage"] = "Image removed.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        private static int CalculateAvailableQuantity(Tepih tepih)
        {
            return Math.Max(tepih.Quantity - tepih.ReservedQuantity, 0);
        }

        private static List<StorefrontProductImageViewModel> MapProductImages(IEnumerable<ProductImage>? productImages)
        {
            return productImages?
                .Where(image => !image.Disabled)
                .OrderBy(image => image.SortOrder)
                .Select(image => new StorefrontProductImageViewModel
                {
                    Id = image.Id,
                    Url = image.Url,
                    ThumbnailUrl = image.ThumbnailUrl,
                    AltText = image.AltText,
                    IsPrimary = image.IsPrimary,
                    SortOrder = image.SortOrder
                })
                .ToList() ?? new List<StorefrontProductImageViewModel>();
        }

        private async Task<List<StorefrontProductImageViewModel>> LoadProductImagesAsync(int productId)
        {
            var productImages = await _context.ProductImages
                .Where(image => image.TepihId == productId && !image.Disabled)
                .OrderBy(image => image.SortOrder)
                .AsNoTracking()
                .ToListAsync();

            return MapProductImages(productImages);
        }

        private async Task<string?> ResolveSlugAsync(int productId, EditTepihViewModel tepihVM, Tepih existingProduct)
        {
            var requestedSlug = string.IsNullOrWhiteSpace(tepihVM.Slug)
                ? ProductSlugHelper.BuildDefaultSlug(new Tepih
                {
                    Id = productId,
                    Name = tepihVM.Name,
                    ProductNumber = tepihVM.ProductNumber,
                    Width = tepihVM.Width,
                    Length = tepihVM.Length,
                    Color = tepihVM.Color
                })
                : ProductSlugHelper.NormalizeSlug(tepihVM.Slug);

            if (string.IsNullOrWhiteSpace(requestedSlug))
            {
                requestedSlug = ProductSlugHelper.BuildDefaultSlug(existingProduct);
            }

            var slugExists = await _context.Tepisi
                .AnyAsync(product => product.Id != productId && product.Slug == requestedSlug);

            if (slugExists)
            {
                ModelState.AddModelError(nameof(EditTepihViewModel.Slug), "Slug must be unique.");
                return null;
            }

            return requestedSlug;
        }

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
            catch (System.Text.Json.JsonException ex) // provjeriti da li je odgovarajuci prefix
            {
                _logger.LogWarning(ex, "Failed to deserialize scanned products from session.");
                return new List<ScannedProductViewModel>();
            }
        }

        private string BuildSellerName()
        {
            var firstName = User.FindFirstValue(ClaimTypes.GivenName);
            var lastName = User.FindFirstValue(ClaimTypes.Surname);
            var fullName = $"{firstName} {lastName}".Trim();

            return string.IsNullOrWhiteSpace(fullName)
                ? (User.Identity?.Name ?? "Staff")
                : fullName;
        }

        private static string BuildStockUnavailableMessage(Tepih product, int requestedQuantity)
        {
            var availableQuantity = CalculateAvailableQuantity(product);
            return $"Nema dovoljno raspolozive količine za proizvod {product.Name} ({product.ProductNumber}). Traženo: {requestedQuantity}, dostupno: {availableQuantity}.";
        }

        private static string TruncateValue(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }


        [HttpGet]
        public IActionResult ScannedProductsToBePurchased()
        {
            var scannedProductsOverview = new ScannedProductsOverviewViewModel();
            scannedProductsOverview.Products = GetScannedProducts();

            if (TempData["SuccessMessage"] != null)
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            if (TempData["ErrorMessage"] != null)
                ViewBag.ErrorMessage = TempData["ErrorMessage"].ToString();

            return View(scannedProductsOverview);
        }

        [HttpPost]
        public async Task<IActionResult> ScannedProductsToBePurchased(ScannedProductsOverviewViewModel spovm)
        {
            try
            {
                var purchaseTime = DateTime.Now;
                spovm.PurchaseTime = DateTime.ParseExact(purchaseTime.ToString("HH:mm:ss dd/MM/yyyy"), "HH:mm:ss dd/MM/yyyy", null);
                spovm.Products = GetScannedProducts();
                if (spovm.Products == null || !spovm.Products.Any())
                {
                    TempData["ErrorMessage"] = "Nema proizvoda za prodaju.";
                    return RedirectToAction(nameof(ScannedProductsToBePurchased));
                }

                if (string.IsNullOrWhiteSpace(spovm.FullName))
                {
                    TempData["ErrorMessage"] = "Ime kupca je obavezno.";
                    return RedirectToAction(nameof(ScannedProductsToBePurchased));
                }

                if (spovm.Products.Any(product => product.Quantity <= 0))
                {
                    TempData["ErrorMessage"] = "Količina svakog proizvoda mora biti veća od nule.";
                    return RedirectToAction(nameof(ScannedProductsToBePurchased));
                }

                var sellerName = BuildSellerName();
                var customerFullName = TruncateValue(spovm.FullName.ToUpper().Trim(), 50);
                var plannedPaymentType = TruncateValue(
                    string.IsNullOrWhiteSpace(spovm.PlannedPaymentType) ? "OSTALO" : spovm.PlannedPaymentType.Trim(),
                    20);

                var requestedQuantities = spovm.Products
                    .GroupBy(product => product.Id)
                    .ToDictionary(group => group.Key, group => group.Sum(product => product.Quantity));

                var productIds = requestedQuantities.Keys.ToList();
                var productsById = await _context.Tepisi
                    .Where(product => productIds.Contains(product.Id))
                    .ToDictionaryAsync(product => product.Id);

                foreach (var requestedProduct in requestedQuantities)
                {
                    if (!productsById.TryGetValue(requestedProduct.Key, out var product) || product.Disabled)
                    {
                        TempData["ErrorMessage"] = $"Proizvod sa ID {requestedProduct.Key} nije pronađen.";
                        return RedirectToAction(nameof(ScannedProductsToBePurchased));
                    }

                    if (CalculateAvailableQuantity(product) < requestedProduct.Value)
                    {
                        TempData["ErrorMessage"] = BuildStockUnavailableMessage(product, requestedProduct.Value);
                        return RedirectToAction(nameof(ScannedProductsToBePurchased));
                    }
                }

                await using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    foreach (var scannedProduct in spovm.Products)
                    {
                        _context.Prodaje.Add(new Prodaja
                        {
                            TepihId = scannedProduct.Id,
                            Quantity = scannedProduct.Quantity,
                            CustomerFullName = customerFullName,
                            VrijemeProdaje = spovm.PurchaseTime,
                            Price = scannedProduct.Price,
                            Rabat = scannedProduct.Rabat,
                            PlannedPaymentType = plannedPaymentType,
                            Prodavac = TruncateValue(sellerName, 50),
                            Disabled = false
                        });
                    }

                    foreach (var requestedProduct in requestedQuantities)
                    {
                        productsById[requestedProduct.Key].Quantity -= requestedProduct.Value;
                    }

                    var customerExists = await _context.Kupci
                        .AnyAsync(customer => customer.CustomerFullName == customerFullName);

                    if (!customerExists)
                    {
                        _context.Kupci.Add(new Kupac
                        {
                            CustomerFullName = customerFullName
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }

                _sessionService.ClearScannedProducts(HttpContext.Session);

                if (spovm.PrintPDF)
                {
                    try
                    {
                        var pdfBytes = GeneratePurchasePdf(spovm, sellerName);
                        return File(pdfBytes, "application/pdf", "OrderDetails.pdf");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "PDF generation failed for purchase.");
                        TempData["SuccessMessage"] = "Uspješna prodaja";
                        TempData["ErrorMessage"] = "Prodaja je evidentirana, ali PDF nije mogao biti generisan.";
                        return RedirectToAction(nameof(ScannedProductsToBePurchased));
                    }
                }

                TempData["SuccessMessage"] = "Uspješna prodaja";
                return RedirectToAction(nameof(ScannedProductsToBePurchased));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Purchase process hit a concurrency conflict.");
                TempData["ErrorMessage"] = "Zaliha se promijenila tokom prodaje. Osvježite podatke i pokušajte ponovo.";
                return RedirectToAction(nameof(ScannedProductsToBePurchased));
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

                var headerTable = new Table(3)
                    .UseAllAvailableWidth()
                    .SetMarginBottom(5);

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.Seller}: {userFullName}"))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.LEFT));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"{spovm.FullName.ToUpper().Trim()}").SetFontSize(10).SimulateBold())
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.Time}: {spovm.PurchaseTime:dd/MM/yyyy HH:mm:ss}"))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                decimal? totalPrice = 0;
                decimal? totalM2 = 0;
                int totalQuantity = 0;

                var table = new Table(8).UseAllAvailableWidth();

                if (User.Identity.IsAuthenticated && (User.IsInRole("admin") || User.IsInRole("superadmin")))
                {
                    string[] headers = { @Inventar.Resources.Resource.ProductNumber, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Price, @Inventar.Resources.Resource.Size, @Inventar.Resources.Resource.Quantity, "m²", @Inventar.Resources.Resource.M2Total, @Inventar.Resources.Resource.Amount };

                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new iText.Layout.Element.Paragraph(header))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SimulateBold()
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
                    table.AddCell(CreateCenteredBoldCell(@Inventar.Resources.Resource.Total + ":").SetFontSize(9));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()).SetFontSize(9));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalM2 ?? 0, 2)}").SetFontSize(9));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalPrice ?? 0, 2)}€").SetFontSize(9));
                }
                else
                {
                    table = new Table(5).UseAllAvailableWidth();
                    string[] headers = { @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Size, @Inventar.Resources.Resource.Quantity, "m²", @Inventar.Resources.Resource.M2Total};

                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new iText.Layout.Element.Paragraph(header))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SimulateBold()
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

                    foreach (var item in groupedProducts)
                    {
                        table.AddCell(CreateCenteredCell(item.Name));
                        table.AddCell(CreateCenteredCell(item.Size));
                        table.AddCell(CreateCenteredCell(item.Quantity.ToString()));
                        table.AddCell(CreateCenteredCell(item.M2PerUnit?.ToString() ?? ""));
                        table.AddCell(CreateCenteredCell(item.M2Total?.ToString() ?? ""));

                        totalPrice += item.PriceTotal;
                        totalM2 += item.M2Total;
                        totalQuantity += item.Quantity;
                    }

                    // Totals row
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(@Inventar.Resources.Resource.Total + ":"));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalM2 ?? 0, 2)}"));
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
            var paragraph = new iText.Layout.Element.Paragraph(text)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetFontSize(9)
                .SimulateBold();
            return new Cell()
                .Add(paragraph)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(1);
        }

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

        public IActionResult SearchTepisi(
            string productNumber,
            string name,
            string model,
            string color,
            string size)
        {
            bool allEmpty =
                string.IsNullOrWhiteSpace(productNumber) &&
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(model) &&
                string.IsNullOrWhiteSpace(color) &&
                string.IsNullOrWhiteSpace(size);

            if (allEmpty)
                return Json(new List<object>()); // return empty

            var query = _context.Tepisi.AsQueryable();

            if (!string.IsNullOrWhiteSpace(productNumber))
                query = query.Where(t => t.ProductNumber.StartsWith(productNumber));

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(t => t.Name.StartsWith(name));

            if (!string.IsNullOrWhiteSpace(model))
                query = query.Where(t => t.Model.StartsWith(model));

            if (!string.IsNullOrWhiteSpace(color))
                query = query.Where(t => t.Color.StartsWith(color));

            // 🔥 SIZE FILTER: "50X100", "50X", "50", etc.
            if (!string.IsNullOrWhiteSpace(size))
            {
                var parts = size.Split('X', 'x');

                int? w = null;
                int? l = null;

                if (parts.Length > 0 && int.TryParse(parts[0], out int widthPart))
                    w = widthPart;

                if (parts.Length > 1 && int.TryParse(parts[1], out int lengthPart))
                    l = lengthPart;

                // match width
                if (w.HasValue)
                    query = query.Where(t => t.Width.HasValue && t.Width.Value.ToString().StartsWith(w.Value.ToString()));

                // match length if user typed second half
                if (l.HasValue)
                    query = query.Where(t => t.Length.HasValue && t.Length.Value.ToString().StartsWith(l.Value.ToString()));
            }

            var results = query
                .Where(t => !t.Disabled)
                .Take(30)
                .Select(t => new
                {
                    id = t.Id,
                    productNumber = t.ProductNumber,
                    name = t.Name,
                    model = t.Model,
                    color = t.Color,
                    width = t.Width,
                    length = t.Length
                })
                .ToList();

            return Json(results);
        }


        [HttpPost]
        public IActionResult AddProductById(int id)
        {
            return ManuallyAddProduct(id);
        }

        [HttpPost]
        public IActionResult ExportScannedProductsToExcel([FromBody] ScannedProductsOverviewViewModel spovm)
        {
            if (spovm == null || spovm.Products == null || !spovm.Products.Any())
                return BadRequest("No products to export.");

            // Determine sale time
            DateTime saleTime = spovm.PurchaseTime == default ? DateTime.Now : spovm.PurchaseTime;

            // Filename formatting
            var formattedTimeForFile = saleTime.ToString("dd-MM-yyyy HH.mm");
            var custForFile = (spovm.FullName.ToUpper() ?? "").Trim();
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                custForFile = custForFile.Replace(c, '-');

            var fileName = $"{formattedTimeForFile} {custForFile}.xlsx";

            // Display time (inside sheet)
            var displaySaleTime = saleTime.ToString("dd-MM-yyyy HH:mm");

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Products");

                // === HEADER ROW 1 (customer left, date right) ===
                ws.Range("A1:D1").Merge().Value = custForFile;
                ws.Range("A1:D1").Style.Font.Bold = true;
                ws.Range("A1:D1").Style.Font.FontSize = 14;
                ws.Range("A1:D1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Range("E1:H1").Merge().Value = displaySaleTime;
                ws.Range("E1:H1").Style.Font.Bold = true;
                ws.Range("E1:H1").Style.Font.FontSize = 14;
                ws.Range("E1:H1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Row 2 empty

                int headerRow = 3;

                // === HEADERS ===
                ws.Cell(headerRow, 1).Value = "";
                ws.Cell(headerRow, 2).Value = "IME ROBA";
                ws.Cell(headerRow, 3).Value = "CIJENA";
                // D & E are "DIMENZIJA"
                ws.Range("D3:E3").Merge().Value = "DIMENZIJA";
                ws.Range("D3:E3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range("D3:E3").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(headerRow, 6).Value = "KOL.";
                ws.Cell(headerRow, 7).Value = "m²";
                ws.Cell(headerRow, 8).Value = "UKUPNO m²";
                ws.Cell(headerRow, 9).Value = "IZNOS";

                // Style header row
                for (int c = 1; c <= 9; c++)
                {
                    ws.Cell(headerRow, c).Style.Font.Bold = true;
                    ws.Cell(headerRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(headerRow, c).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                ws.SheetView.FreezeRows(headerRow);

                var groupedProducts = from p in spovm.Products
                                      group p by new { p.Name, p.Length, p.Width, p.M2PerUnit, p.ProductNumber, p.Price, p.Rabat, p.PerM2} into g
                                      select new ReceiptWithSellerViewModel
                                      {
                                          ProductNumber = g.Key.ProductNumber,
                                          Name = g.Key.Name,
                                          Price = g.Average(p => p.Price),
                                          Size = $"{g.Key.Width}X{g.Key.Length}",
                                          Length = g.Key.Length,
                                          Width = g.Key.Width,
                                          M2PerUnit = g.Key.M2PerUnit,
                                          M2Total = g.Sum(p => p.M2Total),
                                          Quantity = g.Sum(p => p.Quantity),
                                          PriceTotal = g.Sum(p => p.PriceTotal),
                                          Rabat = (int?)g.Average(p => p.Rabat)
                                      };

                // === DATA ROWS ===
                int row = headerRow + 1;
                foreach (var p in groupedProducts) //spovm.Products
                {
                    string sizeText = (p.Width.HasValue && p.Length.HasValue) ? $"{p.Width}X{p.Length}" : "";

                    double? widthM = p.Width.HasValue ? p.Width.Value / 100.0 : (double?)null;
                    double? lengthM = p.Length.HasValue ? p.Length.Value / 100.0 : (double?)null;

                    double? m2PerUnit = null;
                    if (/*p.PerM2 &&*/ widthM.HasValue && lengthM.HasValue)
                        m2PerUnit = Math.Round(widthM.Value * lengthM.Value, 2);

                    ws.Cell(row, 1).Value = p.ProductNumber ?? "";
                    ws.Cell(row, 2).Value = p.Name ?? "";
                    ws.Cell(row, 2).Style.Alignment.WrapText = true;

                    double cijena = p.Rabat != null || p.Rabat > 0 ?(double)p.Price - ((double)p.Price * ((double)p.Rabat / 100)) : (double)p.Price;

                    ws.Cell(row, 3).Value = Convert.ToDouble(cijena);
                    ws.Cell(row, 3).Style.NumberFormat.Format = "0.00 €";

                    ws.Cell(row, 4).Value = widthM.HasValue ? widthM.Value : "";
                    ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";

                    ws.Cell(row, 5).Value = lengthM.HasValue ? lengthM.Value : "";
                    ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";

                    ws.Cell(row, 6).Value = p.Quantity;

                    // m2 per unit
                    if (m2PerUnit.HasValue)
                    {
                        ws.Cell(row, 7).Value = m2PerUnit.Value;
                        ws.Cell(row, 7).Style.NumberFormat.Format = "0.00";
                    }
                    else ws.Cell(row, 7).Value = "";

                    // Total m2
                    ws.Cell(row, 8).FormulaA1 = $"=IF(G{row}=\"\",\"\",G{row}*F{row})";
                    ws.Cell(row, 8).Style.NumberFormat.Format = "0.00";

                    // Total amount
                    ws.Cell(row, 9).FormulaA1 = $"=IF(H{row}=\"\",C{row}*F{row},C{row}*H{row})";
                    ws.Cell(row, 9).Style.NumberFormat.Format = "0.00 €";

                    for (int c = 1; c <= 9; c++)
                    {
                        ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(row, c).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    row++;
                }

                int dataStart = headerRow + 1;
                int dataEnd = row - 1;

                // === TOTALS ROW ===
                int totalsRow = dataEnd + 1;

                ws.Cell(totalsRow, 2).Value = "UKUPNO:";
                ws.Cell(totalsRow, 2).Style.Font.Bold = true;

                // TOTAL KOL. (column 6)
                ws.Cell(totalsRow, 6).FormulaA1 = $"=SUM(F{dataStart}:F{dataEnd})";
                ws.Cell(totalsRow, 6).Style.Font.Bold = true;

                // TOTAL m² (column 8)
                ws.Cell(totalsRow, 8).FormulaA1 = $"=SUM(H{dataStart}:H{dataEnd})";
                ws.Cell(totalsRow, 8).Style.Font.Bold = true;
                ws.Cell(totalsRow, 8).Style.NumberFormat.Format = "0.00";

                // TOTAL amount (column 9)
                ws.Cell(totalsRow, 9).FormulaA1 = $"=SUM(I{dataStart}:I{dataEnd})";
                ws.Cell(totalsRow, 9).Style.Font.Bold = true;
                ws.Cell(totalsRow, 9).Style.NumberFormat.Format = "0.00 €";

                // Gray background
                for (int c = 1; c <= 9; c++)
                {
                    ws.Cell(totalsRow, c).Style.Fill.BackgroundColor = XLColor.LightGray;
                    ws.Cell(totalsRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // === APPLY BORDERS TO HEADER + DATA + TOTALS ===
                var tableRange = ws.Range(headerRow, 1, totalsRow, 9);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // === PAGE SETUP ===
                ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
                ws.PageSetup.PaperSize = XLPaperSize.A5Paper;
                ws.PageSetup.Margins.Top = 0.25;
                ws.PageSetup.Margins.Bottom = 0.25;
                ws.PageSetup.Margins.Left = 0.2;
                ws.PageSetup.Margins.Right = 0.2;
                ws.PageSetup.FitToPages(1, 0);

                ws.PageSetup.SetRowsToRepeatAtTop(1, headerRow);

                // Widths
                ws.Column(1).Width = 12;
                ws.Column(2).Width = 40;  // NAME priority
                ws.Column(3).Width = 12;
                ws.Column(4).Width = 10;
                ws.Column(5).Width = 10;
                ws.Column(6).Width = 8;
                ws.Column(7).Width = 8;
                ws.Column(8).Width = 10;
                ws.Column(9).Width = 12;

                using var ms = new MemoryStream();
                workbook.SaveAs(ms);
                return File(ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }




    }
}
