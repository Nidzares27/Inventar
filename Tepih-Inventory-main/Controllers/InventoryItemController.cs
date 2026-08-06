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
using System.Globalization;
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
using AppResource = Inventar.Resources.Resource;

namespace Inventar.Controllers
{
    [Authorize]
    public class InventoryItemController : Controller
    {
        private const string DirectSaleTypePerUnit = "perunit";
        private const string DirectSaleTypePerM2 = "perm2";
        private const string DirectSaleTypePerMeasure = "permeasure";
        private const string LastScanValueSessionKey = "lastScanValue";
        private const string LastScanTimeSessionKey = "lastScanTime";
        private const string CreateFormStateTempDataKey = "CreateFormState";
        private const int DuplicateFastScanWindowMs = 800;

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
                var tepisi = (await _tepihRepository.GetAllUndisabledAsync())?.ToList() ?? new List<Tepih>();
                TextEncodingHelper.DecodeProductsForDisplay(tepisi);

                ViewBag.RemainingLengths = await LoadRemainingPoMjeriLengthsAsync(
                    tepisi.Where(product => product.PoMjeri).Select(product => product.Id));

                return View(tepisi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory!");
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> QRCodesGrouped()
        {
            try
            {
                var tepisi = await _context.Tepisi
                    .AsNoTracking()
                    .Where(product => !product.Disabled && !product.CreatedForDirectSale)
                    .ToListAsync();

                var groupedRows = tepisi
                    .GroupBy(product => new
                    {
                        ProductNumber = product.ProductNumber ?? string.Empty,
                        Name = product.Name ?? string.Empty,
                        Model = product.Model ?? string.Empty,
                        Color = product.Color ?? string.Empty
                    })
                    .Select(group => new GroupedQrCodeRowViewModel
                    {
                        RawProductNumber = group.Key.ProductNumber,
                        RawName = group.Key.Name,
                        RawModel = group.Key.Model,
                        RawColor = group.Key.Color,
                        ProductNumber = TextEncodingHelper.Decode(group.Key.ProductNumber) ?? string.Empty,
                        Name = TextEncodingHelper.Decode(group.Key.Name) ?? string.Empty,
                        Model = TextEncodingHelper.Decode(group.Key.Model) ?? string.Empty,
                        Color = TextEncodingHelper.Decode(group.Key.Color) ?? string.Empty,
                        TotalQuantity = group.Sum(item => item.Quantity),
                        IsPoMjeriGroup = group.All(item => item.PoMjeri)
                    })
                    .OrderBy(group => group.ProductNumber)
                    .ThenBy(group => group.Name)
                    .ThenBy(group => group.Model)
                    .ThenBy(group => group.Color)
                    .ToList();

                return View(new GroupedQrCodesViewModel
                {
                    Groups = groupedRows
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grouped QR codes inventory view.");
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Pairs(string? name, string? model, string? color, bool submitted = false)
        {
            var viewModel = new PairsViewModel
            {
                Submitted = submitted,
                Filter = new PairsFilterViewModel
                {
                    Name = name?.Trim(),
                    Model = model?.Trim(),
                    Color = color?.Trim()
                }
            };

            await PopulatePairsOptionsAsync(viewModel);
            viewModel.GroupColumns = BuildPairsGroupingColumns(viewModel.Filter);

            if (!submitted)
            {
                return View(viewModel);
            }

            var normalizedName = TextEncodingHelper.NormalizeInput(name);
            var normalizedModel = TextEncodingHelper.NormalizeInput(model);
            var normalizedColor = TextEncodingHelper.NormalizeInput(color);

            if (string.IsNullOrWhiteSpace(normalizedName) &&
                string.IsNullOrWhiteSpace(normalizedModel) &&
                string.IsNullOrWhiteSpace(normalizedColor))
            {
                viewModel.ValidationMessage = @Inventar.Resources.Resource.AtLeastOneParam;
                return View(viewModel);
            }

            try
            {
                var query = _context.Tepisi
                    .AsNoTracking()
                    .Where(product => !product.Disabled && !product.CreatedForDirectSale && product.Quantity >= 1);

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    query = query.Where(product => (product.Name ?? string.Empty).StartsWith(normalizedName));
                }

                if (!string.IsNullOrWhiteSpace(normalizedModel))
                {
                    query = query.Where(product => (product.Model ?? string.Empty).StartsWith(normalizedModel));
                }

                if (!string.IsNullOrWhiteSpace(normalizedColor))
                {
                    query = query.Where(product => (product.Color ?? string.Empty).StartsWith(normalizedColor));
                }

                var products = await query
                    .OrderBy(product => product.Name)
                    .ThenBy(product => product.Model)
                    .ThenBy(product => product.Color)
                    .ThenBy(product => product.Width)
                    .ThenBy(product => product.Length)
                    .ThenBy(product => product.Id)
                    .ToListAsync();

                TextEncodingHelper.DecodeProductsForDisplay(products);

                var remainingLengths = await LoadRemainingPoMjeriLengthsAsync(
                    products.Where(product => product.PoMjeri).Select(product => product.Id));

                var sizeRows = products
                    .Select(product =>
                    {
                        var remainingLength = product.PoMjeri && remainingLengths.TryGetValue(product.Id, out var length)
                            ? length
                            : product.Length ?? 0;

                        var displaySize = product.PoMjeri
                            ? PoMjeriHelper.FormatRemainingSize(product.Width, remainingLength)
                            : PoMjeriHelper.FormatSize(product.Width, product.Length);

                        return new PairsRowViewModel
                        {
                            ProductName = string.IsNullOrWhiteSpace(product.Name) ? "-" : product.Name,
                            Model = string.IsNullOrWhiteSpace(product.Model) ? "-" : product.Model,
                            Color = string.IsNullOrWhiteSpace(product.Color) ? "-" : product.Color,
                            Size = string.IsNullOrWhiteSpace(displaySize) ? "-" : displaySize,
                            Quantity = product.Quantity,
                            SortWidth = product.Width ?? 0,
                            SortLength = product.PoMjeri ? remainingLength : product.Length ?? 0
                        };
                    })
                    .GroupBy(row => new
                    {
                        row.ProductName,
                        row.Model,
                        row.Color,
                        row.Size,
                        row.SortWidth,
                        row.SortLength
                    })
                    .Select(group => new PairsRowViewModel
                    {
                        ProductName = group.Key.ProductName,
                        Model = group.Key.Model,
                        Color = group.Key.Color,
                        Size = group.Key.Size,
                        Quantity = group.Sum(item => item.Quantity),
                        SortWidth = group.Key.SortWidth,
                        SortLength = group.Key.SortLength
                    })
                    .OrderBy(row => row.ProductName)
                    .ThenBy(row => row.Model)
                    .ThenBy(row => row.Color)
                    .ThenBy(row => row.SortWidth)
                    .ThenBy(row => row.SortLength)
                    .ToList();

                viewModel.Rows = BuildPairsDisplayRows(sizeRows, viewModel.GroupColumns);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory pairs view.");
                return RedirectToAction("Index", "Home");
            }
        }

        private async Task PopulatePairsOptionsAsync(PairsViewModel viewModel)
        {
            var optionRows = await _context.Tepisi
                .AsNoTracking()
                .Where(product => !product.Disabled && !product.CreatedForDirectSale)
                .Select(product => new
                {
                    product.Name,
                    product.Model,
                    product.Color
                })
                .ToListAsync();

            viewModel.NameOptions = optionRows
                .Select(row => TextEncodingHelper.Decode(row.Name)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Cast<string>()
                .ToList();

            viewModel.ModelOptions = optionRows
                .Select(row => TextEncodingHelper.Decode(row.Model)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Cast<string>()
                .ToList();

            viewModel.ColorOptions = optionRows
                .Select(row => TextEncodingHelper.Decode(row.Color)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Cast<string>()
                .ToList();
        }

        private static List<PairsGroupingColumnViewModel> BuildPairsGroupingColumns(PairsFilterViewModel filter)
        {
            var hasName = !string.IsNullOrWhiteSpace(filter.Name);
            var hasModel = !string.IsNullOrWhiteSpace(filter.Model);
            var hasColor = !string.IsNullOrWhiteSpace(filter.Color);

            var definitions = new List<(string Key, string Header, bool IsProvided)>
            {
                ("name", Inventar.Resources.Resource.Name, hasName),
                ("model", "Model", hasModel),
                ("color", Inventar.Resources.Resource.Color, hasColor)
            };

            return definitions
                .OrderByDescending(definition => definition.IsProvided)
                .Select(definition => new PairsGroupingColumnViewModel
                {
                    Key = definition.Key,
                    Header = definition.Header
                })
                .ToList();
        }

        private static List<PairsDisplayRowViewModel> BuildPairsDisplayRows(
            IReadOnlyCollection<PairsRowViewModel> rows,
            IReadOnlyList<PairsGroupingColumnViewModel> groupColumns)
        {
            if (rows.Count == 0)
            {
                return new List<PairsDisplayRowViewModel>();
            }

            IOrderedEnumerable<PairsRowViewModel>? orderedRows = null;
            foreach (var column in groupColumns)
            {
                orderedRows = orderedRows == null
                    ? rows.OrderBy(row => GetPairsGroupValue(row, column.Key), StringComparer.CurrentCultureIgnoreCase)
                    : orderedRows.ThenBy(row => GetPairsGroupValue(row, column.Key), StringComparer.CurrentCultureIgnoreCase);
            }

            var orderedList = (orderedRows ?? rows.OrderBy(row => row.ProductName, StringComparer.CurrentCultureIgnoreCase))
                .ThenBy(row => row.SortWidth)
                .ThenBy(row => row.SortLength)
                .ThenBy(row => row.Size, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var displayRows = orderedList
                .Select(row => (
                    Data: row,
                    Display: new PairsDisplayRowViewModel
                    {
                        GroupCells = Enumerable.Repeat<PairsDisplayCellViewModel?>(null, groupColumns.Count).ToList(),
                        Size = row.Size,
                        Quantity = row.Quantity
                    }))
                .ToList();

            PopulatePairsDisplayCells(displayRows, groupColumns, 0, new int[groupColumns.Count]);
            ApplyPairsDetailHighlight(displayRows, groupColumns);
            return displayRows.Select(entry => entry.Display).ToList();
        }

        private static void PopulatePairsDisplayCells(
            List<(PairsRowViewModel Data, PairsDisplayRowViewModel Display)> rows,
            IReadOnlyList<PairsGroupingColumnViewModel> groupColumns,
            int level,
            int[] levelCounters)
        {
            if (level >= groupColumns.Count || rows.Count == 0)
            {
                return;
            }

            var groupedRows = rows
                .GroupBy(row => GetPairsGroupValue(row.Data, groupColumns[level].Key), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var group in groupedRows)
            {
                var rowEntries = group.ToList();
                var toneClass = levelCounters[level] % 2 == 0
                    ? "pairs-table__group-cell--tone-a"
                    : "pairs-table__group-cell--tone-b";

                rowEntries[0].Display.GroupCells[level] = new PairsDisplayCellViewModel
                {
                    Value = group.Key,
                    RowSpan = rowEntries.Count,
                    CssClass = $"pairs-table__group-cell pairs-table__group-cell--level-{level} {toneClass}{(level == 2 ? " pairs-table__group-cell--third-column-end" : string.Empty)}"
                };

                levelCounters[level]++;

                if (level == 0)
                {
                    rowEntries[0].Display.IsTopLevelStart = true;
                }

                if (level == 2)
                {
                    rowEntries[^1].Display.IsThirdColumnGroupEnd = true;
                }

                PopulatePairsDisplayCells(rowEntries, groupColumns, level + 1, levelCounters);
            }
        }

        private static void ApplyPairsDetailHighlight(
            List<(PairsRowViewModel Data, PairsDisplayRowViewModel Display)> rows,
            IReadOnlyList<PairsGroupingColumnViewModel> groupColumns)
        {
            if (rows.Count == 0 || groupColumns.Count == 0)
            {
                return;
            }

            var multiSizeGroupIndex = 0;

            foreach (var group in rows.GroupBy(
                         row => string.Join(
                             "\u001F",
                             groupColumns.Select(column => GetPairsGroupValue(row.Data, column.Key))),
                         StringComparer.CurrentCultureIgnoreCase))
            {
                var rowEntries = group.ToList();
                if (rowEntries.Count < 2)
                {
                    continue;
                }

                var detailCssClass = multiSizeGroupIndex % 2 == 0
                    ? "pairs-table__detail-cell--multi-a"
                    : "pairs-table__detail-cell--multi-b";

                foreach (var rowEntry in rowEntries)
                {
                    rowEntry.Display.DetailCssClass = detailCssClass;
                }

                multiSizeGroupIndex++;
            }
        }

        private static string GetPairsGroupValue(PairsRowViewModel row, string columnKey)
        {
            return columnKey switch
            {
                "name" => row.ProductName,
                "model" => row.Model,
                "color" => row.Color,
                _ => string.Empty
            };
        }

        public async Task<IActionResult> QRCodesGroupedDetails(string productNumber, string name, string model, string color)
        {
            if (string.IsNullOrWhiteSpace(productNumber) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(model) ||
                string.IsNullOrWhiteSpace(color))
            {
                return BadRequest("Group parameters are required.");
            }

            var products = await _context.Tepisi
                .AsNoTracking()
                .Where(product =>
                    !product.Disabled &&
                    !product.CreatedForDirectSale &&
                    product.ProductNumber == productNumber &&
                    product.Name == name &&
                    product.Model == model &&
                    product.Color == color)
                .OrderByDescending(product => product.Id)
                .ToListAsync();

            if (products.Count == 0)
            {
                _logger.LogWarning(
                    "Grouped QR code details not found for group {ProductNumber}/{Name}/{Model}/{Color}.",
                    productNumber,
                    name,
                    model,
                    color);
                return NotFound("No products were found for the selected group.");
            }

            TextEncodingHelper.DecodeProductsForDisplay(products);
            ViewBag.RemainingLengths = await LoadRemainingPoMjeriLengthsAsync(
                products.Where(product => product.PoMjeri).Select(product => product.Id));

            var firstProduct = products[0];
            return View(new GroupedQrCodeDetailsViewModel
            {
                ProductNumber = firstProduct.ProductNumber ?? string.Empty,
                Name = firstProduct.Name ?? string.Empty,
                Model = firstProduct.Model ?? string.Empty,
                Color = firstProduct.Color ?? string.Empty,
                Products = products
            });
        }

        [Authorize(Roles = "admin,superadmin,employee")]
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

            TextEncodingHelper.DecodeProductForDisplay(tepih);

            return View(tepih);
        }

        [HttpGet]
        [Authorize(Roles = "admin,superadmin")]
        public IActionResult Create()
        {
            if (TempData[CreateFormStateTempDataKey] is string serializedDraft &&
                !string.IsNullOrWhiteSpace(serializedDraft))
            {
                try
                {
                    var draft = JsonConvert.DeserializeObject<CreateProductFormState>(serializedDraft);
                    if (draft != null)
                    {
                        return View(BuildCreateFormModel(draft));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore InventoryItem/Create form state from TempData.");
                }
            }

            return View(new Tepih
            {
                IsPublished = false,
                Price = 0m
            });
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> Create(Tepih tepih)
        {
            if (ModelState.IsValid)
            {
                NormalizeProductTextFields(tepih);
                tepih.IsPublished = false;
                tepih.BroaderCategory = ProductCategoryHelper.Normalize(tepih.BroaderCategory);
                tepih.NarrowerCategory = ProductCategoryHelper.Normalize(tepih.NarrowerCategory);
                if (tepih.PoMjeri)
                {
                    tepih.PerM2 = true;
                }

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
                if (tepih.PoMjeri && tepih.Quantity < 1)
                {
                    TempData["MissingLengthWidth"] = "Po mjeri proizvod mora imati količinu najmanje 1.";
                    return View(tepih);
                }

                var time = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                try
                {
                    NormalizeProductIdentity(tepih);

                    if (tepih.PoMjeri)
                    {
                        var createdProducts = await CreatePoMjeriProductsAsync(tepih, time);
                        if (createdProducts.Count == 0)
                        {
                            return StatusCode(500, "Po mjeri proizvodi nisu kreirani.");
                        }

                        TempData["CreateSuccessMessage"] = $"Uspješno kreirano {createdProducts.Count} po mjeri proizvoda.";
                        TempData[CreateFormStateTempDataKey] = JsonConvert.SerializeObject(CaptureCreateFormState(tepih));
                        TempData["CreateQrPdfUrl"] = Url.Action(
                            "GenerateCloudinaryImagePdfBatch",
                            "Pdf",
                            new { ids = string.Join(",", createdProducts.Select(product => product.Id)) });

                        return RedirectToAction(nameof(Create));
                    }

                    var qrCodeUrl = await GenerateQrCodeUrlAsync(BuildQrCodeData(tepih));

                    var istiProizvod = await _context.Tepisi
                        .Where(c => c.Name == tepih.Name &&
                                    c.Model == tepih.Model &&
                                    c.ProductNumber == tepih.ProductNumber &&
                                    c.Length == tepih.Length &&
                                    c.Width == tepih.Width &&
                                    c.Color == tepih.Color &&
                                    c.PerM2 == tepih.PerM2 &&
                                    !c.PoMjeri &&
                                    c.Disabled == false)
                        .ToListAsync();

                    if (istiProizvod.Count == 1)
                    {
                        istiProizvod[0].Quantity += tepih.Quantity;
                        istiProizvod[0].Price = tepih.Price;
                        if (ProductCategoryHelper.ShouldUpgradePlaceholder(istiProizvod[0].BroaderCategory, tepih.BroaderCategory))
                        {
                            istiProizvod[0].BroaderCategory = tepih.BroaderCategory;
                        }

                        if (ProductCategoryHelper.ShouldUpgradePlaceholder(istiProizvod[0].NarrowerCategory, tepih.NarrowerCategory))
                        {
                            istiProizvod[0].NarrowerCategory = tepih.NarrowerCategory;
                        }
                        istiProizvod[0].OnlinePrice ??= istiProizvod[0].Price;
                        if (string.IsNullOrWhiteSpace(istiProizvod[0].Slug))
                        {
                            istiProizvod[0].Slug = await ProductSlugHelper.GenerateUniqueSlugAsync(
                                _context.Tepisi.AsQueryable(),
                                istiProizvod[0],
                                excludedProductId: istiProizvod[0].Id);
                        }
                        _tepihRepository.Update(istiProizvod[0]);
                        tepih.Id = istiProizvod[0].Id;
                        TempData["CreateSuccessMessage"] = "Postojeći proizvod je pronađen i uspješno ažuriran.";
                    }
                    else
                    {
                        tepih.QRCodeUrl = qrCodeUrl;
                        tepih.DateTime = time;
                        tepih.Disabled = false;
                        tepih.OnlinePrice ??= tepih.Price;
                        tepih.Slug = await ProductSlugHelper.GenerateUniqueSlugAsync(
                            _context.Tepisi.AsQueryable(),
                            tepih);
                        _tepihRepository.Add(tepih);
                        TempData["CreateSuccessMessage"] = "Proizvod je uspješno kreiran.";
                    }

                    await _context.SaveChangesAsync();
                    TempData[CreateFormStateTempDataKey] = JsonConvert.SerializeObject(CaptureCreateFormState(tepih));
                    TempData["CreateQrPdfUrl"] = Url.Action("GenerateCloudinaryImagePdf", "Pdf", new { id = tepih.Id });
                    return RedirectToAction(nameof(Create));
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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> GenerateQRCode(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                _logger.LogError("Data for writting QR code is missing");
                return BadRequest("QR code data must not be empty.");
            }

            try
            {
                var qrCodeUrl = await GenerateQrCodeUrlAsync(data);
                return Ok(new { url = qrCodeUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QR code generation/upload failed.");
                return StatusCode(500, "An error occurred while generating the QR code.");
            }
        }

        private async Task<string> GenerateQrCodeUrlAsync(string data)
        {
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

            using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
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

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            var qrCodeBytes = stream.ToArray();
            var fileName = $"{Guid.NewGuid():N}.png";

            try
            {
                using var uploadStream = new MemoryStream(qrCodeBytes);
                var uploadResult = await _photoService.UploadToCloudinary(fileName, uploadStream, "TepisiQRCodes");

                if (uploadResult?.SecureUrl != null && uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return uploadResult.SecureUrl.ToString();
                }

                _logger.LogWarning(
                    "QR code upload returned an unexpected response for file {FileName}. Status: {StatusCode}, SecureUrl present: {HasSecureUrl}. Falling back to local storage.",
                    fileName,
                    uploadResult?.StatusCode,
                    uploadResult?.SecureUrl != null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QR code upload failed for file {FileName}. Falling back to local storage.", fileName);
            }

            return await SaveQrCodeLocallyAsync(fileName, qrCodeBytes);
        }

        private async Task<string> SaveQrCodeLocallyAsync(string fileName, byte[] qrCodeBytes)
        {
            if (string.IsNullOrWhiteSpace(_env.WebRootPath))
            {
                throw new InvalidOperationException("QR code save failed: web root path is not configured.");
            }

            var directoryPath = QrCodeStorageHelper.EnsureLocalDirectory(_env.WebRootPath);
            var filePath = System.IO.Path.Combine(directoryPath, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, qrCodeBytes);
            return QrCodeStorageHelper.BuildLocalUrl(fileName);
        }

        private static void NormalizeProductIdentity(Tepih tepih)
        {
            tepih.Name = (TextEncodingHelper.NormalizeInput(tepih.Name) ?? string.Empty).ToUpperInvariant();
            tepih.Model = (TextEncodingHelper.NormalizeInput(tepih.Model) ?? string.Empty).ToUpperInvariant();
            tepih.ProductNumber = (TextEncodingHelper.NormalizeInput(tepih.ProductNumber) ?? string.Empty).ToUpperInvariant();
            tepih.Color = (TextEncodingHelper.NormalizeInput(tepih.Color) ?? string.Empty).ToUpperInvariant();
        }

        private static CreateProductFormState CaptureCreateFormState(Tepih tepih)
        {
            return new CreateProductFormState
            {
                ProductNumber = tepih.ProductNumber,
                Name = tepih.Name,
                Model = tepih.Model,
                BroaderCategory = tepih.BroaderCategory,
                NarrowerCategory = tepih.NarrowerCategory,
                Color = tepih.Color,
                Length = tepih.Length,
                Width = tepih.Width,
                Quantity = tepih.Quantity,
                Price = tepih.Price,
                Description = tepih.Description,
                PerM2 = tepih.PerM2,
                PoMjeri = tepih.PoMjeri
            };
        }

        private static Tepih BuildCreateFormModel(CreateProductFormState draft)
        {
            return new Tepih
            {
                ProductNumber = draft.ProductNumber ?? string.Empty,
                Name = draft.Name ?? string.Empty,
                Model = draft.Model ?? string.Empty,
                BroaderCategory = draft.BroaderCategory ?? string.Empty,
                NarrowerCategory = draft.NarrowerCategory ?? string.Empty,
                Color = draft.Color ?? string.Empty,
                Length = draft.Length,
                Width = draft.Width,
                Quantity = draft.Quantity,
                Price = draft.Price,
                Description = draft.Description,
                PerM2 = draft.PerM2,
                PoMjeri = draft.PoMjeri,
                IsPublished = false
            };
        }

        private sealed class CreateProductFormState
        {
            public string? ProductNumber { get; set; }
            public string? Name { get; set; }
            public string? Model { get; set; }
            public string? BroaderCategory { get; set; }
            public string? NarrowerCategory { get; set; }
            public string? Color { get; set; }
            public int? Length { get; set; }
            public int? Width { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public string? Description { get; set; }
            public bool PerM2 { get; set; }
            public bool PoMjeri { get; set; }
        }

        private static string BuildQrCodeData(Tepih tepih)
        {
            var baseData = $"{tepih.Name}/{tepih.Model}/{tepih.ProductNumber}/{tepih.Width}/{tepih.Length}/{tepih.Color}/{tepih.PerM2}";

            return tepih.PoMjeri && !string.IsNullOrWhiteSpace(tepih.UnID)
                ? $"{baseData}/{tepih.UnID}"
                : baseData;
        }

        private static string BuildPoMjeriSlug(Tepih tepih)
        {
            return ProductSlugHelper.BuildDefaultSlug(tepih);
        }

        private async Task<string> GenerateUniqueUnIdAsync(ISet<string> reservedCodes)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var code = PoMjeriHelper.GenerateCandidateUnId();
                if (reservedCodes.Contains(code))
                {
                    continue;
                }

                var exists = await _context.Tepisi.AnyAsync(product => product.UnID == code);
                if (exists)
                {
                    continue;
                }

                reservedCodes.Add(code);
                return code;
            }

            throw new InvalidOperationException("Unable to generate a unique code for po mjeri product.");
        }

        private async Task<List<Tepih>> CreatePoMjeriProductsAsync(Tepih template, string createdAt)
        {
            var quantityToCreate = Math.Max(template.Quantity, 0);
            var reservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reservedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var imageSource = await _context.Tepisi
                .Include(product => product.ProductImages)
                .Where(product =>
                    !product.Disabled &&
                    product.PoMjeri &&
                    product.Name == template.Name &&
                    product.Model == template.Model &&
                    product.ProductNumber == template.ProductNumber &&
                    product.Width == template.Width &&
                    product.Length == template.Length &&
                    product.Color == template.Color)
                .OrderBy(product => product.Id)
                .FirstOrDefaultAsync(product => product.ProductImages.Any(image => !image.Disabled));

            var createdProducts = new List<Tepih>(quantityToCreate);

            for (var index = 0; index < quantityToCreate; index++)
            {
                var unId = await GenerateUniqueUnIdAsync(reservedCodes);
                var product = new Tepih
                {
                    Name = template.Name,
                    ProductNumber = template.ProductNumber,
                    Model = template.Model,
                    BroaderCategory = template.BroaderCategory,
                    NarrowerCategory = template.NarrowerCategory,
                    DateTime = createdAt,
                    Quantity = 1,
                    Length = template.Length,
                    Width = template.Width,
                    Color = template.Color,
                    Price = template.Price,
                    OnlinePrice = template.OnlinePrice ?? template.Price,
                    PerM2 = true,
                    PoMjeri = true,
                    UnID = unId,
                    Description = template.Description,
                    ShortDescription = template.ShortDescription,
                    IsPublished = template.IsPublished,
                    Disabled = false
                };

                product.QRCodeUrl = await GenerateQrCodeUrlAsync(BuildQrCodeData(product));
                product.Slug = await ProductSlugHelper.GenerateUniqueSlugAsync(
                    _context.Tepisi.AsQueryable(),
                    product,
                    reservedSlugs: reservedSlugs);

                createdProducts.Add(product);
            }

            _context.Tepisi.AddRange(createdProducts);
            await _context.SaveChangesAsync();

            if (imageSource != null)
            {
                var sourceImages = imageSource.ProductImages
                    .Where(image => !image.Disabled)
                    .OrderBy(image => image.SortOrder)
                    .ToList();

                foreach (var targetProduct in createdProducts)
                {
                    foreach (var sourceImage in sourceImages)
                    {
                        _context.ProductImages.Add(new ProductImage
                        {
                            TepihId = targetProduct.Id,
                            CloudinaryPublicId = sourceImage.CloudinaryPublicId,
                            Url = sourceImage.Url,
                            ThumbnailUrl = sourceImage.ThumbnailUrl,
                            AltText = sourceImage.AltText,
                            MediaType = sourceImage.MediaType,
                            IsPrimary = sourceImage.IsPrimary,
                            SortOrder = sourceImage.SortOrder,
                            Disabled = false,
                            CreatedUtc = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }

            return createdProducts;
        }


        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> QRCodeScanning(int? id)
        {
            List<ScannedProductViewModel> scannedProds = GetScannedProducts();
            scannedProds.Reverse();
            return View(scannedProds);
        }

        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> QRCodeScanning2()
        {
            return View("QRCodeScanning");
        }

        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
        public IActionResult SimilarProducts()
        {
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> SimilarProductsLookup(string data)
        {
            try
            {
                var model = await BuildSimilarProductsLookupResultAsync(data);
                return PartialView("_SimilarProductsLookupResult", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loading similar products failed for QR data {QrData}.", data);
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                return PartialView("_SimilarProductsLookupResult", new SimilarProductsLookupResultViewModel
                {
                    ErrorMessage = @Inventar.Resources.Resource.ErrorLoadingSimilarProducts
                });
            }
        }

        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> SimilarProductsLookupById(int id)
        {
            try
            {
                var matchedProduct = await _context.Tepisi
                    .AsNoTracking()
                    .FirstOrDefaultAsync(product => product.Id == id && !product.Disabled && !product.CreatedForDirectSale);

                if (matchedProduct == null)
                {
                    return PartialView("_SimilarProductsLookupResult", new SimilarProductsLookupResultViewModel
                    {
                        ErrorMessage = @Inventar.Resources.Resource.ProductSearchFailed
                    });
                }

                var model = await BuildSimilarProductsLookupResultAsync(matchedProduct);
                return PartialView("_SimilarProductsLookupResult", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loading similar products failed for product id {ProductId}.", id);
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                return PartialView("_SimilarProductsLookupResult", new SimilarProductsLookupResultViewModel
                {
                    ErrorMessage = @Inventar.Resources.Resource.ErrorLoadingSimilarProducts
                });
            }
        }

        private void AddOrIncrementRegularScannedProduct(List<ScannedProductViewModel> scannedProducts, Tepih item)
        {
            var existingLine = scannedProducts.FirstOrDefault(product => !product.PoMjeri && product.Id == item.Id);
            if (existingLine != null)
            {
                existingLine.Quantity += 1;
                RecalculateScannedProductTotals(existingLine);
                return;
            }

            scannedProducts.Add(BuildRegularScannedProduct(item));
        }

        private async Task<SimilarProductsLookupResultViewModel> BuildSimilarProductsLookupResultAsync(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return new SimilarProductsLookupResultViewModel
                {
                    ErrorMessage = @Inventar.Resources.Resource.NoQRData
                };
            }

            var extractData = data.Split("/");
            var matchedProduct = await FindProductByQrDataAsync(extractData);
            if (matchedProduct == null)
            {
                _logger.LogWarning("SimilarProductsLookup: Couldn't find a product with properties matching QR code data: {QrData}", data);
                return new SimilarProductsLookupResultViewModel
                {
                    ErrorMessage = @Inventar.Resources.Resource.ProductNotFound
                };
            }

            return await BuildSimilarProductsLookupResultAsync(matchedProduct);
        }

        private async Task<SimilarProductsLookupResultViewModel> BuildSimilarProductsLookupResultAsync(Tepih matchedProduct)
        {
            var relatedProducts = await _context.Tepisi
                .AsNoTracking()
                .Where(product =>
                    !product.Disabled &&
                    !product.CreatedForDirectSale &&
                    product.Name == matchedProduct.Name &&
                    product.Model == matchedProduct.Model)
                .OrderBy(product => product.Color)
                .ThenBy(product => product.ProductNumber)
                .ThenBy(product => product.Width)
                .ThenBy(product => product.Length)
                .ThenBy(product => product.Id)
                .ToListAsync();

            if (relatedProducts.Count == 0)
            {
                return new SimilarProductsLookupResultViewModel
                {
                    ErrorMessage = @Inventar.Resources.Resource.NoSimilarProductFound
                };
            }

            TextEncodingHelper.DecodeProductsForDisplay(relatedProducts);

            var remainingLengths = await LoadRemainingPoMjeriLengthsAsync(
                relatedProducts.Where(product => product.PoMjeri).Select(product => product.Id));

            var selectedProduct = relatedProducts.FirstOrDefault(product => product.Id == matchedProduct.Id) ?? relatedProducts[0];

            var groupedRows = relatedProducts
                .Select(product =>
                {
                    var displayLength = ResolveDisplayLength(product, remainingLengths);
                    var size = product.PoMjeri
                        ? PoMjeriHelper.FormatRemainingSize(product.Width, displayLength ?? 0)
                        : PoMjeriHelper.FormatSize(product.Width, product.Length);
                    var m2 = CalculateInventoryDisplayM2(product.Width, displayLength);

                    return new
                    {
                        ProductNumber = string.IsNullOrWhiteSpace(product.ProductNumber) ? "-" : product.ProductNumber,
                        Price = Math.Round(product.Price, 2, MidpointRounding.AwayFromZero),
                        Color = string.IsNullOrWhiteSpace(product.Color) ? "-" : product.Color,
                        Size = string.IsNullOrWhiteSpace(size) ? "-" : size,
                        M2 = m2,
                        Quantity = product.Quantity,
                        M2Total = m2.HasValue
                            ? Math.Round(m2.Value * product.Quantity, 2, MidpointRounding.AwayFromZero)
                            : (decimal?)null,
                        SortWidth = product.Width ?? 0,
                        SortLength = displayLength ?? 0
                    };
                })
                .OrderBy(row => row.Color, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.ProductNumber, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.SortWidth)
                .ThenBy(row => row.SortLength)
                .ToList();

            var rows = new List<SimilarProductsDisplayRowViewModel>();

            foreach (var colorGroup in groupedRows.GroupBy(row => row.Color, StringComparer.CurrentCultureIgnoreCase))
            {
                var groupRows = colorGroup.ToList();

                for (var index = 0; index < groupRows.Count; index++)
                {
                    var row = groupRows[index];
                    rows.Add(new SimilarProductsDisplayRowViewModel
                    {
                        ProductNumber = row.ProductNumber,
                        Price = row.Price,
                        Color = row.Color,
                        ShowColor = index == 0,
                        ColorRowSpan = index == 0 ? groupRows.Count : 0,
                        Size = row.Size,
                        M2 = row.M2,
                        Quantity = row.Quantity,
                        M2Total = row.M2Total,
                        IsGroupStart = index == 0
                    });
                }
            }

            var selectedDisplayLength = ResolveDisplayLength(selectedProduct, remainingLengths);
            var selectedSize = selectedProduct.PoMjeri
                ? PoMjeriHelper.FormatRemainingSize(selectedProduct.Width, selectedDisplayLength ?? 0)
                : PoMjeriHelper.FormatSize(selectedProduct.Width, selectedProduct.Length);
            var selectedM2 = CalculateInventoryDisplayM2(selectedProduct.Width, selectedDisplayLength);

            return new SimilarProductsLookupResultViewModel
            {
                Summary = new SimilarProductSummaryViewModel
                {
                    ProductNumber = string.IsNullOrWhiteSpace(selectedProduct.ProductNumber) ? "-" : selectedProduct.ProductNumber,
                    ProductName = string.IsNullOrWhiteSpace(selectedProduct.Name) ? "-" : selectedProduct.Name,
                    Price = Math.Round(selectedProduct.Price, 2, MidpointRounding.AwayFromZero),
                    Model = string.IsNullOrWhiteSpace(selectedProduct.Model) ? "-" : selectedProduct.Model,
                    Color = string.IsNullOrWhiteSpace(selectedProduct.Color) ? "-" : selectedProduct.Color,
                    Size = string.IsNullOrWhiteSpace(selectedSize) ? "-" : selectedSize,
                    M2 = selectedM2,
                    Quantity = selectedProduct.Quantity,
                    M2Total = selectedM2.HasValue
                        ? Math.Round(selectedM2.Value * selectedProduct.Quantity, 2, MidpointRounding.AwayFromZero)
                        : null
                },
                Rows = rows
            };
        }

        private static int? ResolveDisplayLength(Tepih product, IReadOnlyDictionary<int, int> remainingLengths)
        {
            return PoMjeriHelper.GetInventoryDisplayLength(product, remainingLengths);
        }

        private static decimal? CalculateInventoryDisplayM2(int? width, int? length)
        {
            return PoMjeriHelper.CalculateM2PerUnit(true, width, length);
        }

        private async Task<object> BuildPoMjeriPromptResponseAsync(Tepih product)
        {
            var remainingLength = await GetRemainingPoMjeriLengthAsync(product);
            var availableRemainingLength = await GetSessionAwareRemainingLengthAsync(product);

            return new
            {
                success = true,
                requiresCustomSize = true,
                productId = product.Id,
                name = TextEncodingHelper.Decode(product.Name),
                model = TextEncodingHelper.Decode(product.Model),
                productNumber = TextEncodingHelper.Decode(product.ProductNumber),
                color = TextEncodingHelper.Decode(product.Color),
                unId = TextEncodingHelper.Decode(product.UnID),
                fixedWidth = product.Width,
                originalWidth = product.Width,
                originalLength = product.Length,
                originalSize = PoMjeriHelper.FormatSize(product.Width, product.Length),
                remainingWidth = product.Width,
                remainingLength,
                availableRemainingLength,
                remainingSize = PoMjeriHelper.FormatRemainingSize(product.Width, remainingLength)
            };
        }

        private async Task<Tepih?> FindProductByQrDataAsync(string[] extractData)
        {
            if (extractData.Length == 8)
            {
                return await _context.Tepisi.FirstOrDefaultAsync(product =>
                    product.Name == extractData[0].Trim() &&
                    product.Model == extractData[1].Trim() &&
                    product.ProductNumber == extractData[2].Trim() &&
                    product.Width.ToString() == extractData[3].Trim() &&
                    product.Length.ToString() == extractData[4].Trim() &&
                    product.Color == extractData[5].Trim() &&
                    product.PerM2.ToString() == extractData[6].Trim() &&
                    product.UnID == extractData[7].Trim() &&
                    product.Disabled != true &&
                    !product.CreatedForDirectSale);
            }

            if (extractData.Length == 7)
            {
                if (string.IsNullOrEmpty(extractData[3]) && string.IsNullOrEmpty(extractData[4]))
                {
                    return await _context.Tepisi.FirstOrDefaultAsync(product =>
                        product.Name == extractData[0].Trim() &&
                        product.Model == extractData[1].Trim() &&
                        product.ProductNumber == extractData[2].Trim() &&
                        product.Color == extractData[5].Trim() &&
                        product.PerM2.ToString() == extractData[6].Trim() &&
                        product.Disabled != true &&
                        !product.CreatedForDirectSale);
                }

                return await _context.Tepisi.FirstOrDefaultAsync(product =>
                    product.Name == extractData[0].Trim() &&
                    product.Model == extractData[1].Trim() &&
                    product.ProductNumber == extractData[2].Trim() &&
                    product.Width.ToString() == extractData[3].Trim() &&
                    product.Length.ToString() == extractData[4].Trim() &&
                    product.Color == extractData[5].Trim() &&
                    product.PerM2.ToString() == extractData[6].Trim() &&
                    product.Disabled != true &&
                    !product.CreatedForDirectSale);
            }

            if (extractData.Length == 5)
            {
                if (string.IsNullOrEmpty(extractData[2]) && string.IsNullOrEmpty(extractData[3]))
                {
                    return await _context.Tepisi.FirstOrDefaultAsync(product =>
                        product.Name == extractData[0].Trim() &&
                        product.Model == extractData[1].Trim() &&
                        product.Color == extractData[4].Trim() &&
                        product.Disabled != true &&
                        !product.CreatedForDirectSale);
                }

                return await _context.Tepisi.FirstOrDefaultAsync(product =>
                    product.Name == extractData[0].Trim() &&
                    product.Model == extractData[1].Trim() &&
                    product.Width.ToString() == extractData[2].Trim() &&
                    product.Length.ToString() == extractData[3].Trim() &&
                    product.Color == extractData[4].Trim() &&
                    product.Disabled != true &&
                    !product.CreatedForDirectSale);
            }

            return null;
        }

        private static bool TryParseLastScanTimestamp(string? rawValue, out long timestampMs)
        {
            timestampMs = 0;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTimestampMs))
            {
                timestampMs = unixTimestampMs;
                return true;
            }

            if (DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var roundtripTimestamp))
            {
                timestampMs = roundtripTimestamp.ToUnixTimeMilliseconds();
                return true;
            }

            foreach (var culture in LocalizationSettings.SupportedCultures)
            {
                if (DateTime.TryParse(rawValue, culture, DateTimeStyles.AssumeLocal, out var localizedTimestamp))
                {
                    timestampMs = new DateTimeOffset(DateTime.SpecifyKind(localizedTimestamp, DateTimeKind.Local)).ToUnixTimeMilliseconds();
                    return true;
                }
            }

            return false;
        }

        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> ProcessQRCode(string data)
        {
            try
            {
                // ------------------------------
                // DUPLICATE FAST-SCAN PROTECTION
                // ------------------------------
                var lastScan = HttpContext.Session.GetString(LastScanValueSessionKey);
                var lastScanTimeString = HttpContext.Session.GetString(LastScanTimeSessionKey);
                var currentScanTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (lastScan == data && TryParseLastScanTimestamp(lastScanTimeString, out var lastScanTimestampMs))
                {
                    // If scanned again within 800 milliseconds → IGNORE
                    if (currentScanTimestampMs - lastScanTimestampMs < DuplicateFastScanWindowMs)
                    {
                        return Json(new { success = false, message = Inventar.Resources.Resource.DuplicateFastScanIgnored });
                    }
                }

                // Save current scan as last scan
                HttpContext.Session.SetString(LastScanValueSessionKey, data);
                HttpContext.Session.SetString(LastScanTimeSessionKey, currentScanTimestampMs.ToString(CultureInfo.InvariantCulture));

                var extractData = data.Split("/");
                var item = await FindProductByQrDataAsync(extractData);
                if (item == null)
                {
                    _logger.LogWarning("ProcessQRCode: Couldn't find a product with properties matching QR Code data: {data}", data);
                    return Json(new { success = false, message = Inventar.Resources.Resource.ProductNotFound });
                }

                var scannedProducts = GetScannedProducts();

                bool isPageReload = Request.Headers["Cache-Control"].ToString().Contains("max-age=0");
                if (isPageReload)
                {
                    return View("QRCodeScanning", scannedProducts);
                }

                if (item.PoMjeri)
                {
                    return Json(await BuildPoMjeriPromptResponseAsync(item));
                }

                AddOrIncrementRegularScannedProduct(scannedProducts, item);
                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProducts));

                return Json(new { success = true });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "QR code processing went wrong for QR code with data: {data}.",data);
                return StatusCode(500, Inventar.Resources.Resource.ErrorProcessingQR);
            }
        }

        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> PrepareProductSelection(int id)
        {
            try
            {
                var item = await _context.Tepisi.FirstOrDefaultAsync(product => product.Id == id && !product.Disabled && !product.CreatedForDirectSale);
                if (item == null)
                {
                    return Json(new { success = false, message = Inventar.Resources.Resource.ProductNotFound });
                }

                if (item.PoMjeri)
                {
                    return Json(await BuildPoMjeriPromptResponseAsync(item));
                }

                var scannedProducts = GetScannedProducts();
                AddOrIncrementRegularScannedProduct(scannedProducts, item);
                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProducts));

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PrepareProductSelection failed for product {ProductId}.", id);
                return StatusCode(500, Inventar.Resources.Resource.ErrorPreparingProduct);
            }
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> CreateDirectSaleProduct([FromBody] CreateDirectSaleProductRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = Inventar.Resources.Resource.InvalidProductData });
            }

            try
            {
                var normalizedName = TextEncodingHelper.NormalizeInput(model.Name)?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    return Json(new { success = false, message = Inventar.Resources.Resource.ProductNameRequired });
                }

                if (!TryParseDirectSaleProductType(model.ProductType, out var perM2, out var poMjeri))
                {
                    return Json(new { success = false, message = Inventar.Resources.Resource.SelectValidProductType });
                }

                var width = model.Width;
                var length = model.Length;

                if (perM2 || poMjeri)
                {
                    if (!width.HasValue || !length.HasValue)
                    {
                        return Json(new { success = false, message = Inventar.Resources.Resource.WidthAndLengthRequiredForSelectedProductType });
                    }
                }
                else if (width.HasValue != length.HasValue)
                {
                    return Json(new { success = false, message = Inventar.Resources.Resource.EnterBothWidthAndLength });
                }

                var product = new Tepih
                {
                    Name = TruncateValue(normalizedName, 50),
                    ProductNumber = "DEF",
                    Model = "DEF",
                    Color = "DEF",
                    BroaderCategory = "DEF",
                    NarrowerCategory = "DEF",
                    Quantity = model.Quantity,
                    Price = Math.Round(model.Price, 4, MidpointRounding.AwayFromZero),
                    ReservedQuantity = 0,
                    PerM2 = perM2,
                    PoMjeri = poMjeri,
                    IsPublished = false,
                    Disabled = false,
                    CreatedForDirectSale = true,
                    DateTime = null,
                    QRCodeUrl = null,
                    Length = length,
                    Width = width,
                    UnID = null,
                    Description = null,
                    Slug = null,
                    OnlinePrice = null,
                    ShortDescription = null,
                    SeoTitle = null,
                    SeoDescription = null
                };

                _context.Tepisi.Add(product);
                await _context.SaveChangesAsync();

                var scannedProducts = GetScannedProducts();
                scannedProducts.Add(BuildDirectSaleScannedProduct(product, model.Quantity));
                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProducts));

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create a direct-sale placeholder product for QR checkout.");
                return StatusCode(500, Inventar.Resources.Resource.ErrorCreatingTemporaryProduct);
            }
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> AddPoMjeriScannedProduct([FromBody] PoMjeriSelectionRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = Inventar.Resources.Resource.InvalidDimensions });
            }

            try
            {
                var product = await _context.Tepisi.FirstOrDefaultAsync(item => item.Id == model.ProductId && !item.Disabled);
                if (product == null)
                {
                    return Json(new { success = false, message = Inventar.Resources.Resource.ProductNotFound });
                }

                if (!product.PoMjeri || !product.Width.HasValue || !product.Length.HasValue)
                {
                    return Json(new { success = false, message = Inventar.Resources.Resource.SelectedProductIsNotPerMeasure });
                }

                var remainingLength = await GetSessionAwareRemainingLengthAsync(product);
                var remainingWidth = product.Width.Value;
                var fixedWidth = remainingWidth;

                if (model.CustomLength > remainingLength)
                {
                    return Json(new { success = false, message = $"{Inventar.Resources.Resource.TheEnteredLengthCannotBeGreaterThanTheRemainingLength}." });
                }

                var scannedProduct = await BuildPoMjeriScannedProductAsync(product, fixedWidth, model.CustomLength);
                var scannedProducts = GetScannedProducts();

                var matchingLine = scannedProducts.FirstOrDefault(item =>
                    item.PoMjeri &&
                    item.Id == product.Id &&
                    item.Width == scannedProduct.Width &&
                    item.Length == scannedProduct.Length &&
                    item.ConsumedLengthPerUnit == scannedProduct.ConsumedLengthPerUnit);

                if (matchingLine != null)
                {
                    var availableLengthExcludingCurrent = await GetSessionAwareRemainingLengthAsync(product, matchingLine.LineId);
                    var maxAvailableQuantity = PoMjeriHelper.CalculateMaxAvailableQuantity(
                        product.Width ?? 0,
                        availableLengthExcludingCurrent,
                        matchingLine.Width ?? 0,
                        matchingLine.Length ?? 0);

                    if (matchingLine.Quantity >= maxAvailableQuantity)
                    {
                        return Json(new
                        {
                            success = false,
                            message = string.Format(CultureInfo.CurrentCulture, Inventar.Resources.Resource.MaxPiecesForGivenProduct, maxAvailableQuantity)
                        });
                    }

                    matchingLine.Quantity += 1;
                    matchingLine.RemainingLength = scannedProduct.RemainingLength;
                    matchingLine.MaxAvailableQuantity = maxAvailableQuantity;
                    RecalculateScannedProductTotals(matchingLine);
                }
                else
                {
                    scannedProducts.Add(scannedProduct);
                }

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProducts));
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddPoMjeriScannedProduct failed for product {ProductId}.", model.ProductId);
                return StatusCode(500, Inventar.Resources.Resource.ErrorAddingPerMeasureProduct);
            }
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> Update([FromBody] ScannedProductViewModel modell)
        {
            try
            {
                if (modell.Price < 0)
                {
                    _logger.LogWarning("Invalid price submitted for product ID {Id}. Price: {Price}", modell.Id, modell.Price);
                    return Json(new { success = false, message = Inventar.Resources.Resource.InvalidPrice });
                }

                List<ScannedProductViewModel> scannedProds = GetScannedProducts();
                var matchingvalue = scannedProds.FirstOrDefault(i => i.LineId == modell.LineId);
                if (matchingvalue == null) {
                    _logger.LogError("Update price for scanned product: Product line was not found in scanned products: {lineId}. Full model: {model} ", modell.LineId, modell);
                    return Json(new { success = false });
                }
                if (matchingvalue != null)
                {
                    matchingvalue.Price = modell.Price;
                    matchingvalue.Rabat = modell.Rabat;
                    matchingvalue.PriceTotal = InventoryPricingHelper.CalculateDiscountedLineTotal(
                        matchingvalue.PerM2,
                        matchingvalue.PoMjeri,
                        matchingvalue.Price,
                        matchingvalue.Width,
                        matchingvalue.Length,
                        matchingvalue.Quantity,
                        matchingvalue.Rabat);

                    if (matchingvalue.IsDirectSaleProduct)
                    {
                        matchingvalue.DirectSaleOriginalTotal = matchingvalue.PriceTotal;
                    }

                    HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                }

                return Json(new { success = true });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Applying discount or changing price mannualy went wrong.");
                return StatusCode(500, Inventar.Resources.Resource.ErrorUpdatingProductPrice);
            }
        }

        [Authorize(Roles = "admin,superadmin")]
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
        [Authorize(Roles = "admin,superadmin")]
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
                try
                {
                    if (CloudinaryHelper.TryGetPublicIdFromUrlFromFolder(tepih.QRCodeUrl, out var publicId))
                    {
                        await _photoService.DeletePhotoAsync(publicId);
                    }
                    else if (QrCodeStorageHelper.TryMapLocalUrlToFilePath(_env.WebRootPath, tepih.QRCodeUrl, out var localFilePath) &&
                             System.IO.File.Exists(localFilePath))
                    {
                        System.IO.File.Delete(localFilePath);
                    }
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

        [Authorize(Roles = "admin,superadmin")]
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
            TextEncodingHelper.DecodeProductForDisplay(tepih);
            var tepihVM = new EditTepihViewModel
            {
                Id = tepih.Id,
                Name = tepih.Name,
                ProductNumber = tepih.ProductNumber,
                Model = tepih.Model,
                BroaderCategory = tepih.BroaderCategory,
                NarrowerCategory = tepih.NarrowerCategory,
                DateTime = tepih.DateTime,
                Quantity = tepih.Quantity,
                QRCodeUrl = tepih.QRCodeUrl,
                Length = tepih.Length,
                Width = tepih.Width,
                Color = tepih.Color,
                Price = tepih.Price,
                OnlinePrice = tepih.OnlinePrice,
                PerM2 = tepih.PerM2,
                PoMjeri = tepih.PoMjeri,
                UnID = tepih.UnID,
                RemainingSize = tepih.PoMjeri ? PoMjeriHelper.FormatRemainingSize(tepih.Width, await GetRemainingPoMjeriLengthAsync(tepih)) : null,
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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> Edit(int id, EditTepihViewModel tepihVM)
        {
            NormalizeProductTextFields(tepihVM);
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje tepiha nije uspjelo");
                _logger.LogWarning("EditTepih post: ModelState is Invalid!");
                tepihVM.ProductImages = await LoadProductImagesAsync(id);
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
                BroaderCategory = ProductCategoryHelper.Normalize(tepihVM.BroaderCategory),
                NarrowerCategory = ProductCategoryHelper.Normalize(tepihVM.NarrowerCategory),
                DateTime = tepihVM.DateTime,
                Quantity = tepihVM.Quantity,
                QRCodeUrl = tepihVM.QRCodeUrl,
                Length = proizvod.Length,
                Width = proizvod.Width,
                Color = tepihVM.Color,
                Price = tepihVM.Price,
                OnlinePrice = proizvod.OnlinePrice ?? tepihVM.Price,
                PerM2 = proizvod.PoMjeri ? true : tepihVM.PerM2,
                PoMjeri = proizvod.PoMjeri,
                UnID = proizvod.UnID,
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

            if (proizvod.PoMjeri)
            {
                tepihEdit.Quantity = proizvod.Quantity;
            }

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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> UploadInventoryGalleryMedia(
            int id,
            List<IFormFile> files,
            string? altText,
            bool reuseForGroup,
            bool reuseForColorGroup)
        {
            var tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .FirstOrDefaultAsync(t => t.Id == id && !t.Disabled);

            if (tepih == null)
            {
                return NotFound(AppResource.ProductNotFound);
            }

            if (reuseForGroup && reuseForColorGroup)
            {
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGalleryOnlyOneReuseOption;
                return RedirectToAction(nameof(Edit), new { id });
            }

            if (files == null || files.Count == 0)
            {
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGallerySelectAtLeastOneFile;
                return RedirectToAction(nameof(Edit), new { id });
            }

            var activeImages = tepih.ProductImages
                .Where(image => !image.Disabled && ProductMediaFolders.IsInventoryGalleryMedia(image.CloudinaryPublicId))
                .ToList();
            var hasPrimary = activeImages.Any(image => image.IsPrimary && !IsVideoMediaType(image.MediaType));
            var nextSortOrder = activeImages.Any() ? activeImages.Max(image => image.SortOrder) + 1 : 1;
            var successfulUploads = 0;
            var uploadedImageSeeds = new List<ProductImageSeed>();

            foreach (var file in files.Where(file => file.Length > 0))
            {
                var mediaType = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                    ? "video"
                    : "image";

                if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                    !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["InventoryGalleryErrorMessage"] = string.Format(AppResource.InventoryGalleryUnsupportedFile, file.FileName);
                    continue;
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                (string PublicId, string SecureUrl, string MediaType) uploadResult;
                try
                {
                    uploadResult = await _photoService.UploadStorefrontMediaToCloudinary(
                        file.FileName,
                        stream,
                        ProductMediaFolders.InventoryGalleryFolder,
                        mediaType);
                }
                catch (ApplicationException ex)
                {
                    _logger.LogWarning(ex, "Inventory gallery media upload failed for product {ProductId} and file {FileName}.", id, file.FileName);
                    TempData["InventoryGalleryErrorMessage"] = BuildInventoryGalleryUploadErrorMessage(ex);
                    break;
                }

                if (string.IsNullOrWhiteSpace(uploadResult.SecureUrl) || string.IsNullOrWhiteSpace(uploadResult.PublicId))
                {
                    TempData["InventoryGalleryErrorMessage"] = string.Format(AppResource.InventoryGalleryUploadFailedForFile, file.FileName);
                    continue;
                }

                var imageSeed = new ProductImageSeed
                {
                    CloudinaryPublicId = uploadResult.PublicId,
                    Url = uploadResult.SecureUrl,
                    ThumbnailUrl = mediaType == "video" ? null : uploadResult.SecureUrl,
                    AltText = string.IsNullOrWhiteSpace(altText) ? tepih.Name : (TextEncodingHelper.NormalizeInput(altText) ?? altText.Trim()),
                    MediaType = NormalizeMediaType(uploadResult.MediaType),
                    IsPrimary = !hasPrimary && mediaType == "image"
                };

                var createdImage = new ProductImage
                {
                    TepihId = tepih.Id,
                    CloudinaryPublicId = imageSeed.CloudinaryPublicId,
                    Url = imageSeed.Url,
                    ThumbnailUrl = imageSeed.ThumbnailUrl,
                    AltText = imageSeed.AltText,
                    MediaType = imageSeed.MediaType,
                    IsPrimary = imageSeed.IsPrimary,
                    SortOrder = nextSortOrder++,
                    Disabled = false,
                    CreatedUtc = DateTime.UtcNow
                };

                _context.ProductImages.Add(createdImage);
                tepih.ProductImages.Add(createdImage);
                uploadedImageSeeds.Add(imageSeed);
                hasPrimary = hasPrimary || imageSeed.IsPrimary;
                successfulUploads++;
            }

            var reusedImages = 0;
            var reuseScope = NormalizeInventoryGallerySyncScope(reuseForColorGroup ? "colorGroup" : reuseForGroup ? "group" : null);
            if (successfulUploads > 0)
            {
                await _context.SaveChangesAsync();
            }

            if ((reuseForGroup || reuseForColorGroup) && uploadedImageSeeds.Count > 0)
            {
                reusedImages = await CopyMissingInventoryGalleryToGroupMembersAsync(
                    tepih,
                    uploadedImageSeeds,
                    reuseScope == "colorGroup");

                if (reusedImages > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }

            if (successfulUploads > 0)
            {
                var uploadMessage = successfulUploads == 1
                    ? AppResource.InventoryGalleryOneFileUploaded
                    : string.Format(AppResource.InventoryGalleryManyFilesUploaded, successfulUploads);

                TempData["InventoryGallerySuccessMessage"] = reusedImages > 0
                    ? $"{uploadMessage} {string.Format(AppResource.InventoryGalleryCopiedToRelatedProducts, reusedImages, DescribeInventoryGalleryScope(reuseScope))}"
                    : uploadMessage;
            }
            else if (TempData["InventoryGalleryErrorMessage"] == null)
            {
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGalleryNoFilesUploaded;
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        private static string BuildInventoryGalleryUploadErrorMessage(Exception ex)
        {
            var detail = ExtractUploadFailureDetail(ex);

            if (ex.Message.Contains("Cloudinary is not configured", StringComparison.OrdinalIgnoreCase) ||
                ex.InnerException?.Message.Contains("Cloudinary is not configured", StringComparison.OrdinalIgnoreCase) == true)
            {
                return AppResource.InventoryGalleryCloudinaryNotConfigured;
            }

            return string.IsNullOrWhiteSpace(detail)
                ? AppResource.InventoryGalleryUploadError
                : string.Format(AppResource.InventoryGalleryUploadErrorWithDetail, detail);
        }

        private static string? ExtractUploadFailureDetail(Exception ex)
        {
            var candidates = new[]
            {
                ex.Message,
                ex.InnerException?.Message,
                ex.GetBaseException().Message
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (candidate.Contains("Failed to upload image to Cloudinary.", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains("Failed to upload media to Cloudinary.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate.Trim();
            }

            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> SyncInventoryGalleryMedia(int id, List<int> selectedImageIds, string? scope)
        {
            var tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .FirstOrDefaultAsync(t => t.Id == id && !t.Disabled);

            if (tepih == null)
            {
                return NotFound(AppResource.ProductNotFound);
            }

            if (selectedImageIds == null || selectedImageIds.Count == 0)
            {
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGalleryNoCheckedItems;
                return RedirectToAction(nameof(Edit), new { id });
            }

            var normalizedScope = NormalizeInventoryGallerySyncScope(scope);
            var groupProducts = await LoadInventoryGalleryGroupProductsAsync(tepih, normalizedScope == "colorGroup");
            if (groupProducts.Count < 2)
            {
                TempData["InventoryGalleryErrorMessage"] = string.Format(
                    AppResource.InventoryGalleryNoOtherProductsInScope,
                    DescribeInventoryGalleryScope(normalizedScope));
                return RedirectToAction(nameof(Edit), new { id });
            }

            var selectedIds = selectedImageIds.Distinct().ToHashSet();
            var sourceImages = tepih.ProductImages
                .Where(image =>
                    selectedIds.Contains(image.Id) &&
                    !image.Disabled &&
                    ProductMediaFolders.IsInventoryGalleryMedia(image.CloudinaryPublicId))
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(BuildImageSeed)
                .GroupBy(BuildImageIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (sourceImages.Count == 0)
            {
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGalleryNoActiveCheckedItems;
                return RedirectToAction(nameof(Edit), new { id });
            }

            var copiedImages = await CopyMissingInventoryGalleryToGroupMembersAsync(
                tepih,
                sourceImages,
                normalizedScope == "colorGroup");

            await _context.SaveChangesAsync();

            TempData["InventoryGallerySuccessMessage"] = copiedImages > 0
                ? string.Format(AppResource.InventoryGallerySyncedItems, copiedImages, DescribeInventoryGalleryScope(normalizedScope))
                : string.Format(AppResource.InventoryGalleryAlreadySynced, DescribeInventoryGalleryScope(normalizedScope));

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> SetPrimaryInventoryGalleryMedia(int id, int imageId)
        {
            var images = await _context.ProductImages
                .Where(image =>
                    image.TepihId == id &&
                    !image.Disabled &&
                    image.CloudinaryPublicId != null &&
                    image.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
                .ToListAsync();

            if (images.Count == 0)
            {
                return NotFound("No gallery media found.");
            }

            if (!images.Any(image => image.Id == imageId))
            {
                return NotFound("Gallery media not found.");
            }

            var selectedImage = images.First(image => image.Id == imageId);
            if (IsVideoMediaType(selectedImage.MediaType))
            {
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGalleryVideoCannotBePrimary;
                return RedirectToAction(nameof(Edit), new { id });
            }

            foreach (var image in images)
            {
                image.IsPrimary = image.Id == imageId;
            }

            await _context.SaveChangesAsync();
            TempData["InventoryGallerySuccessMessage"] = AppResource.InventoryGalleryPrimaryUpdated;
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> DeleteInventoryGalleryMedia(int id, int imageId, string? scope)
        {
            var tepih = await _context.Tepisi
                .AsNoTracking()
                .FirstOrDefaultAsync(product => product.Id == id && !product.Disabled);

            if (tepih == null)
            {
                return NotFound(AppResource.ProductNotFound);
            }

            var image = await _context.ProductImages
                .FirstOrDefaultAsync(productImage =>
                    productImage.Id == imageId &&
                    productImage.TepihId == id &&
                    !productImage.Disabled &&
                    productImage.CloudinaryPublicId != null &&
                    productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix));

            if (image == null)
            {
                return NotFound("Gallery media not found.");
            }

            var normalizedScope = NormalizeInventoryGalleryDeleteScope(scope);
            var normalizedMediaType = NormalizeMediaType(image.MediaType);
            var imagesToDisable = new List<ProductImage> { image };

            if (normalizedScope != "single")
            {
                var targetProductIds = await _context.Tepisi
                    .Where(product =>
                        !product.Disabled &&
                        product.Name == tepih.Name &&
                        product.Model == tepih.Model &&
                        (normalizedScope != "colorGroup" || product.Color == tepih.Color))
                    .Select(product => product.Id)
                    .ToListAsync();

                imagesToDisable = await _context.ProductImages
                    .Where(productImage =>
                        !productImage.Disabled &&
                        targetProductIds.Contains(productImage.TepihId) &&
                        productImage.CloudinaryPublicId != null &&
                        productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix) &&
                        productImage.CloudinaryPublicId == image.CloudinaryPublicId &&
                        productImage.MediaType == normalizedMediaType)
                    .ToListAsync();
            }

            var imageIdsToDisable = imagesToDisable
                .Select(productImage => productImage.Id)
                .Distinct()
                .ToList();

            var affectedProductIds = imagesToDisable
                .Select(productImage => productImage.TepihId)
                .Distinct()
                .ToList();

            var remainingImages = await _context.ProductImages
                .Where(productImage =>
                    affectedProductIds.Contains(productImage.TepihId) &&
                    !productImage.Disabled &&
                    !imageIdsToDisable.Contains(productImage.Id) &&
                    productImage.CloudinaryPublicId != null &&
                    productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
                .OrderBy(productImage => productImage.TepihId)
                .ThenByDescending(productImage => productImage.IsPrimary)
                .ThenBy(productImage => productImage.SortOrder)
                .ToListAsync();

            try
            {
                var isSharedByOtherProducts = await _context.ProductImages
                    .AnyAsync(productImage =>
                        !productImage.Disabled &&
                        !imageIdsToDisable.Contains(productImage.Id) &&
                        productImage.CloudinaryPublicId != null &&
                        productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix) &&
                        productImage.CloudinaryPublicId == image.CloudinaryPublicId &&
                        productImage.MediaType == normalizedMediaType);

                if (!isSharedByOtherProducts)
                {
                    await _photoService.DeleteMediaAsync(image.CloudinaryPublicId, normalizedMediaType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete inventory gallery media {ImageId} from Cloudinary.", imageId);
                TempData["InventoryGalleryErrorMessage"] = AppResource.InventoryGalleryCloudinaryDeleteFailed;
            }

            foreach (var galleryImage in imagesToDisable)
            {
                galleryImage.Disabled = true;
                galleryImage.IsPrimary = false;
            }

            foreach (var affectedProductId in affectedProductIds)
            {
                var productImages = remainingImages
                    .Where(productImage => productImage.TepihId == affectedProductId)
                    .ToList();

                var hasPrimaryImage = productImages.Any(productImage =>
                    productImage.IsPrimary &&
                    !IsVideoMediaType(productImage.MediaType));

                if (hasPrimaryImage)
                {
                    continue;
                }

                var replacementPrimary = productImages
                    .FirstOrDefault(productImage => !IsVideoMediaType(productImage.MediaType));

                if (replacementPrimary != null)
                {
                    replacementPrimary.IsPrimary = true;
                }
            }

            await _context.SaveChangesAsync();

            TempData["InventoryGallerySuccessMessage"] = normalizedScope switch
            {
                "group" => string.Format(AppResource.InventoryGalleryDeletedGroup, affectedProductIds.Count),
                "colorGroup" => string.Format(AppResource.InventoryGalleryDeletedColorGroup, affectedProductIds.Count),
                _ => AppResource.InventoryGalleryDeletedSingle
            };

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> InventoryGalleryMedia(int id)
        {
            var tepih = await _context.Tepisi
                .Include(product => product.ProductImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(product => product.Id == id && !product.Disabled);

            if (tepih == null)
            {
                return NotFound();
            }

            TextEncodingHelper.DecodeProductForDisplay(tepih);

            var galleryItems = tepih.ProductImages
                .Where(image => !image.Disabled && ProductMediaFolders.IsInventoryGalleryMedia(image.CloudinaryPublicId))
                .OrderByDescending(image => image.IsPrimary && !IsVideoMediaType(image.MediaType))
                .ThenBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(image => new
                {
                    id = image.Id,
                    url = image.Url,
                    thumbnailUrl = image.ThumbnailUrl,
                    altText = image.AltText,
                    mediaType = NormalizeMediaType(image.MediaType),
                    isPrimary = image.IsPrimary,
                    sortOrder = image.SortOrder
                })
                .ToList();

            return Json(new
            {
                productId = tepih.Id,
                name = tepih.Name,
                model = tepih.Model,
                items = galleryItems
            });
        }

        private static int CalculateAvailableQuantity(Tepih tepih)
        {
            return Math.Max(tepih.Quantity - tepih.ReservedQuantity, 0);
        }

        private async Task<int> GetRemainingPoMjeriLengthAsync(Tepih product)
        {
            if (!product.PoMjeri || !product.Length.HasValue)
            {
                return product.Length ?? 0;
            }

            var sales = await _context.Prodaje
                .Where(sale => sale.TepihId == product.Id && !sale.Disabled)
                .AsNoTracking()
                .ToListAsync();

            return PoMjeriHelper.CalculateRemainingLength(product, sales);
        }

        private async Task<Dictionary<int, int>> LoadRemainingPoMjeriLengthsAsync(IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var products = await _context.Tepisi
                .Where(product => ids.Contains(product.Id))
                .Select(product => new { product.Id, product.PoMjeri, product.Length })
                .ToListAsync();

            var consumedByProduct = await _context.Prodaje
                .Where(sale => ids.Contains(sale.TepihId) && !sale.Disabled)
                .GroupBy(sale => sale.TepihId)
                .Select(group => new
                {
                    TepihId = group.Key,
                    ConsumedLength = group.Sum(sale => (sale.ConsumedLength ?? sale.CustomLength ?? 0) * sale.Quantity)
                })
                .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength);

            return products.ToDictionary(
                product => product.Id,
                product =>
                {
                    if (!product.PoMjeri || !product.Length.HasValue)
                    {
                        return product.Length ?? 0;
                    }

                    consumedByProduct.TryGetValue(product.Id, out var consumedLength);
                    return Math.Max(product.Length.Value - consumedLength, 0);
                });
        }

        private int GetSessionConsumedLength(int productId, string? excludeLineId = null)
        {
            return GetScannedProducts()
                .Where(item => item.PoMjeri && item.Id == productId && item.LineId != excludeLineId)
                .Sum(item => (item.ConsumedLengthPerUnit ?? 0) * item.Quantity);
        }

        private async Task<int> GetSessionAwareRemainingLengthAsync(Tepih product, string? excludeLineId = null)
        {
            var remainingLength = await GetRemainingPoMjeriLengthAsync(product);
            var sessionConsumedLength = GetSessionConsumedLength(product.Id, excludeLineId);
            return Math.Max(remainingLength - sessionConsumedLength, 0);
        }

        private static void RecalculateScannedProductTotals(ScannedProductViewModel item)
        {
            item.M2Total = item.M2PerUnit.HasValue
                ? Math.Round(item.M2PerUnit.Value * item.Quantity, 2)
                : null;

            item.PriceTotal = InventoryPricingHelper.CalculateLineTotal(
                item.PerM2,
                item.PoMjeri,
                item.Price,
                item.Width,
                item.Length,
                item.Quantity);
        }

        private static bool TryParseDirectSaleProductType(string? value, out bool perM2, out bool poMjeri)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case DirectSaleTypePerUnit:
                case "unit":
                    perM2 = false;
                    poMjeri = false;
                    return true;
                case DirectSaleTypePerM2:
                case "m2":
                    perM2 = true;
                    poMjeri = false;
                    return true;
                case DirectSaleTypePerMeasure:
                case "pomjeri":
                    perM2 = true;
                    poMjeri = true;
                    return true;
                default:
                    perM2 = false;
                    poMjeri = false;
                    return false;
            }
        }

        private static ScannedProductViewModel BuildDirectSaleScannedProduct(
            Tepih item,
            int quantity)
        {
            var scannedProduct = new ScannedProductViewModel
            {
                LineId = Guid.NewGuid().ToString("N"),
                Id = item.Id,
                ProductNumber = item.ProductNumber,
                Model = item.Model,
                Name = item.Name,
                Quantity = quantity,
                Length = item.Length,
                Width = item.Width,
                OriginalLength = item.Length,
                OriginalWidth = item.Width,
                RemainingLength = item.PoMjeri ? item.Length : null,
                RemainingWidth = item.PoMjeri ? item.Width : null,
                ConsumedLengthPerUnit = item.PoMjeri && item.Width.HasValue && item.Length.HasValue
                    ? PoMjeriHelper.CalculateConsumedLengthPerUnit(item.Width.Value, item.Width.Value, item.Length.Value)
                    : null,
                MaxAvailableQuantity = quantity,
                M2PerUnit = PoMjeriHelper.CalculateM2PerUnit(item.PerM2, item.Width, item.Length),
                Color = item.Color,
                Price = item.Price,
                PerM2 = item.PerM2,
                PoMjeri = item.PoMjeri,
                IsDirectSaleProduct = true,
                UnID = item.UnID
            };

            RecalculateScannedProductTotals(scannedProduct);
            scannedProduct.DirectSaleOriginalTotal = scannedProduct.PriceTotal;
            return scannedProduct;
        }

        private ScannedProductViewModel BuildRegularScannedProduct(Tepih item)
        {
            var scannedProduct = new ScannedProductViewModel
            {
                LineId = Guid.NewGuid().ToString("N"),
                Id = item.Id,
                ProductNumber = item.ProductNumber,
                Model = item.Model,
                Name = item.Name,
                Quantity = 1,
                Length = item.Length,
                Width = item.Width,
                OriginalLength = item.Length,
                OriginalWidth = item.Width,
                M2PerUnit = PoMjeriHelper.CalculateM2PerUnit(item.PerM2, item.Width, item.Length),
                Color = item.Color,
                Price = item.Price,
                PerM2 = item.PerM2,
                PoMjeri = item.PoMjeri,
                UnID = item.UnID
            };

            RecalculateScannedProductTotals(scannedProduct);
            return scannedProduct;
        }

        private async Task<ScannedProductViewModel> BuildPoMjeriScannedProductAsync(Tepih product, int customWidth, int customLength)
        {
            var remainingLength = await GetRemainingPoMjeriLengthAsync(product);
            var sessionAwareRemainingLength = await GetSessionAwareRemainingLengthAsync(product);
            var remainingWidth = product.Width ?? 0;
            var consumedLengthPerUnit = PoMjeriHelper.CalculateConsumedLengthPerUnit(remainingWidth, customWidth, customLength);
            var maxAvailableQuantity = PoMjeriHelper.CalculateMaxAvailableQuantity(remainingWidth, sessionAwareRemainingLength, customWidth, customLength);

            if (maxAvailableQuantity < 1)
            {
                throw new InvalidOperationException("Nema dovoljno preostale dužine za traženi komad.");
            }

            var resolvedPrice = await ResolvePoMjeriRateAsync(product, customWidth);

            var scannedProduct = new ScannedProductViewModel
            {
                LineId = Guid.NewGuid().ToString("N"),
                Id = product.Id,
                ProductNumber = product.ProductNumber,
                Model = product.Model,
                Name = product.Name,
                Quantity = 1,
                Length = customLength,
                Width = customWidth,
                OriginalLength = product.Length,
                OriginalWidth = product.Width,
                RemainingLength = remainingLength,
                RemainingWidth = product.Width,
                ConsumedLengthPerUnit = consumedLengthPerUnit,
                MaxAvailableQuantity = maxAvailableQuantity,
                M2PerUnit = PoMjeriHelper.CalculateM2PerUnit(true, customWidth, customLength),
                Color = product.Color,
                Price = resolvedPrice,
                PerM2 = true,
                PoMjeri = true,
                UnID = product.UnID
            };

            RecalculateScannedProductTotals(scannedProduct);
            return scannedProduct;
        }

        private async Task<decimal> ResolvePoMjeriRateAsync(Tepih product, int customWidth)
        {
            if (!product.PoMjeri)
            {
                return product.Price;
            }

            var resolvedPrice = await _context.Tepisi
                .AsNoTracking()
                .Where(candidate =>
                    !candidate.Disabled &&
                    candidate.PoMjeri &&
                    candidate.Name == product.Name &&
                    candidate.ProductNumber == product.ProductNumber &&
                    candidate.Model == product.Model &&
                    candidate.Color == product.Color &&
                    candidate.Width.HasValue &&
                    candidate.Width.Value >= customWidth)
                .OrderBy(candidate => candidate.Width)
                .ThenBy(candidate => candidate.Id)
                .Select(candidate => candidate.Price)
                .FirstOrDefaultAsync();

            return resolvedPrice > 0 ? resolvedPrice : product.Price;
        }

        private async Task<List<Tepih>> LoadInventoryGalleryGroupProductsAsync(Tepih product, bool sameColor = false)
        {
            return await _context.Tepisi
                .Where(tepih =>
                    !tepih.Disabled &&
                    tepih.Name == product.Name &&
                    tepih.Model == product.Model &&
                    (!sameColor || tepih.Color == product.Color))
                .OrderBy(tepih => tepih.Id)
                .ToListAsync();
        }

        private async Task<int> CopyMissingInventoryGalleryToGroupMembersAsync(
            Tepih sourceProduct,
            IReadOnlyCollection<ProductImageSeed> sourceImages,
            bool sameColor = false)
        {
            if (sourceImages.Count == 0)
            {
                return 0;
            }

            var targetProducts = await _context.Tepisi
                .AsNoTracking()
                .Where(product =>
                    !product.Disabled &&
                    product.Id != sourceProduct.Id &&
                    product.Name == sourceProduct.Name &&
                    product.Model == sourceProduct.Model &&
                    (!sameColor || product.Color == sourceProduct.Color))
                .Select(product => new
                {
                    product.Id,
                    product.Name
                })
                .ToListAsync();

            if (targetProducts.Count == 0)
            {
                return 0;
            }

            var orderedImages = sourceImages
                .Where(image => !string.IsNullOrWhiteSpace(image.Url))
                .OrderByDescending(image => image.IsPrimary && !IsVideoMediaType(image.MediaType))
                .ThenBy(image => image.CloudinaryPublicId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var copiedImages = 0;

            foreach (var targetProduct in targetProducts)
            {
                var activeImages = await _context.ProductImages
                    .Where(image =>
                        !image.Disabled &&
                        image.TepihId == targetProduct.Id &&
                        image.CloudinaryPublicId != null &&
                        image.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
                    .ToListAsync();

                var existingKeys = new HashSet<string>(
                    activeImages.Select(BuildImageIdentity),
                    StringComparer.OrdinalIgnoreCase);

                var hasPrimary = activeImages.Any(image => image.IsPrimary && !IsVideoMediaType(image.MediaType));
                var nextSortOrder = activeImages.Any() ? activeImages.Max(image => image.SortOrder) + 1 : 1;

                foreach (var sourceImage in orderedImages)
                {
                    var imageKey = BuildImageIdentity(sourceImage);
                    if (string.IsNullOrWhiteSpace(imageKey) || existingKeys.Contains(imageKey))
                    {
                        continue;
                    }

                    var copiedImage = new ProductImage
                    {
                        TepihId = targetProduct.Id,
                        CloudinaryPublicId = sourceImage.CloudinaryPublicId,
                        Url = sourceImage.Url,
                        ThumbnailUrl = sourceImage.ThumbnailUrl,
                        AltText = string.IsNullOrWhiteSpace(sourceImage.AltText) ? targetProduct.Name : sourceImage.AltText,
                        IsPrimary = !hasPrimary && !IsVideoMediaType(sourceImage.MediaType),
                        MediaType = NormalizeMediaType(sourceImage.MediaType),
                        SortOrder = nextSortOrder++,
                        Disabled = false,
                        CreatedUtc = DateTime.UtcNow
                    };

                    _context.ProductImages.Add(copiedImage);
                    existingKeys.Add(imageKey);
                    hasPrimary = hasPrimary || copiedImage.IsPrimary;
                    copiedImages++;
                }
            }

            return copiedImages;
        }

        private static string NormalizeInventoryGallerySyncScope(string? scope)
        {
            return string.Equals(scope, "colorGroup", StringComparison.OrdinalIgnoreCase)
                ? "colorGroup"
                : "group";
        }

        private static string NormalizeInventoryGalleryDeleteScope(string? scope)
        {
            if (string.Equals(scope, "group", StringComparison.OrdinalIgnoreCase))
            {
                return "group";
            }

            if (string.Equals(scope, "colorGroup", StringComparison.OrdinalIgnoreCase))
            {
                return "colorGroup";
            }

            return "single";
        }

        private static string DescribeInventoryGalleryScope(string scope)
        {
            return string.Equals(scope, "colorGroup", StringComparison.OrdinalIgnoreCase)
                ? AppResource.InventoryGalleryScopeNameModelColor
                : AppResource.InventoryGalleryScopeNameModel;
        }

        private static ProductImageSeed BuildImageSeed(ProductImage image)
        {
            return new ProductImageSeed
            {
                CloudinaryPublicId = image.CloudinaryPublicId,
                Url = image.Url,
                ThumbnailUrl = image.ThumbnailUrl,
                AltText = image.AltText,
                IsPrimary = image.IsPrimary,
                MediaType = NormalizeMediaType(image.MediaType)
            };
        }

        private static string BuildImageIdentity(ProductImage image)
        {
            var mediaType = NormalizeMediaType(image.MediaType);
            return !string.IsNullOrWhiteSpace(image.CloudinaryPublicId)
                ? $"{mediaType}:{image.CloudinaryPublicId.Trim()}"
                : $"{mediaType}:{image.Url.Trim()}";
        }

        private static string BuildImageIdentity(ProductImageSeed image)
        {
            var mediaType = NormalizeMediaType(image.MediaType);
            return !string.IsNullOrWhiteSpace(image.CloudinaryPublicId)
                ? $"{mediaType}:{image.CloudinaryPublicId.Trim()}"
                : $"{mediaType}:{image.Url.Trim()}";
        }

        private static bool IsVideoMediaType(string? mediaType)
        {
            return string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMediaType(string? mediaType)
        {
            return IsVideoMediaType(mediaType) ? "video" : "image";
        }

        private static List<StorefrontProductImageViewModel> MapProductImages(IEnumerable<ProductImage>? productImages)
        {
            return productImages?
                .Where(image => !image.Disabled && ProductMediaFolders.IsInventoryGalleryMedia(image.CloudinaryPublicId))
                .OrderBy(image => image.SortOrder)
                .Select(image => new StorefrontProductImageViewModel
                {
                    Id = image.Id,
                    Url = image.Url,
                    ThumbnailUrl = image.ThumbnailUrl,
                    AltText = image.AltText,
                    MediaType = NormalizeMediaType(image.MediaType),
                    IsPrimary = image.IsPrimary,
                    SortOrder = image.SortOrder
                })
                .ToList() ?? new List<StorefrontProductImageViewModel>();
        }

        private async Task<List<StorefrontProductImageViewModel>> LoadProductImagesAsync(int productId)
        {
            var productImages = await _context.ProductImages
                .Where(image =>
                    image.TepihId == productId &&
                    !image.Disabled &&
                    image.CloudinaryPublicId != null &&
                    image.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
                .OrderBy(image => image.SortOrder)
                .AsNoTracking()
                .ToListAsync();

            return MapProductImages(productImages);
        }

        private async Task<string?> ResolveSlugAsync(int productId, EditTepihViewModel tepihVM, Tepih existingProduct)
        {
            var isCustomSlug = !string.IsNullOrWhiteSpace(tepihVM.Slug);
            var requestedSlug = isCustomSlug
                ? ProductSlugHelper.NormalizeSlug(tepihVM.Slug)
                : await ProductSlugHelper.GenerateUniqueSlugAsync(
                    _context.Tepisi.AsQueryable(),
                    new Tepih
                    {
                        Id = productId,
                        Name = tepihVM.Name,
                        ProductNumber = tepihVM.ProductNumber,
                        Model = tepihVM.Model,
                        Width = tepihVM.Width,
                        Length = tepihVM.Length,
                        Color = tepihVM.Color,
                        UnID = existingProduct.UnID
                    },
                    excludedProductId: productId);

            if (string.IsNullOrWhiteSpace(requestedSlug))
            {
                requestedSlug = await ProductSlugHelper.GenerateUniqueSlugAsync(
                    _context.Tepisi.AsQueryable(),
                    existingProduct,
                    excludedProductId: productId);
            }

            if (!isCustomSlug)
            {
                return requestedSlug;
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

        private sealed class ProductImageSeed
        {
            public string CloudinaryPublicId { get; init; } = string.Empty;
            public string Url { get; init; } = string.Empty;
            public string? ThumbnailUrl { get; init; }
            public string? AltText { get; init; }
            public bool IsPrimary { get; init; }
            public string MediaType { get; init; } = "image";
        }

        private const string ScannedProductsSessionKey = "scannedProducts";

        public List<ScannedProductViewModel> GetScannedProducts()
        {
            var serialized = HttpContext.Session.GetString(ScannedProductsSessionKey);

            if (string.IsNullOrWhiteSpace(serialized))
                return new List<ScannedProductViewModel>();

            try
            {
                var items = JsonConvert.DeserializeObject<List<ScannedProductViewModel>>(serialized)
                    ?? new List<ScannedProductViewModel>();

                foreach (var item in items)
                {
                    item.LineId ??= Guid.NewGuid().ToString("N");
                    item.OriginalWidth ??= item.Width;
                    item.OriginalLength ??= item.Length;
                    if (item.PoMjeri)
                    {
                        item.MaxAvailableQuantity = Math.Max(item.MaxAvailableQuantity, item.Quantity);
                    }
                }

                return items;
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

        private static string BuildSafeExportFileName(string baseName, string extension)
        {
            var normalizedBaseName = string.IsNullOrWhiteSpace(baseName) ? "export" : baseName.Trim();

            foreach (var invalidChar in System.IO.Path.GetInvalidFileNameChars())
            {
                normalizedBaseName = normalizedBaseName.Replace(invalidChar, '-');
            }

            return $"{normalizedBaseName}.{extension}";
        }

        private static bool HasScannedTableExportData(ScannedTableExportRequest? request)
        {
            return request != null &&
                   request.ColumnHeaders != null &&
                   request.ColumnHeaders.Count > 0 &&
                   request.Rows != null &&
                   request.Rows.Count > 0;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return AppResource.ResourceManager.GetString(key) ?? fallback;
        }

        private async Task DeleteDirectSaleProductsIfUnusedAsync(
            IEnumerable<int> productIds,
            IEnumerable<ScannedProductViewModel>? remainingSessionProducts = null)
        {
            var distinctIds = productIds
                .Distinct()
                .ToList();

            if (distinctIds.Count == 0)
            {
                return;
            }

            var remainingIds = remainingSessionProducts?
                .Where(item => item.IsDirectSaleProduct)
                .Select(item => item.Id)
                .ToHashSet() ?? new HashSet<int>();

            var candidates = await _context.Tepisi
                .Where(product => distinctIds.Contains(product.Id) && product.CreatedForDirectSale)
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                if (remainingIds.Contains(candidate.Id))
                {
                    continue;
                }

                var hasSales = await _context.Prodaje
                    .AnyAsync(sale => sale.TepihId == candidate.Id && !sale.Disabled);

                if (!hasSales)
                {
                    _context.Tepisi.Remove(candidate);
                }
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }
        }


        [HttpGet]
        [Authorize(Roles = "admin,superadmin,employee")]
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
        [Authorize(Roles = "admin,superadmin,employee")]
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

                if (spovm.Products.Any(product =>
                    product.PoMjeri &&
                    (!product.Width.HasValue ||
                     !product.Length.HasValue ||
                     !product.ConsumedLengthPerUnit.HasValue ||
                     product.ConsumedLengthPerUnit.Value <= 0)))
                {
                    TempData["ErrorMessage"] = "Po mjeri proizvod nema ispravne dimenzije za prodaju.";
                    return RedirectToAction(nameof(ScannedProductsToBePurchased));
                }

                var sellerName = BuildSellerName();
                var customerFullName = TruncateValue(spovm.FullName.ToUpper().Trim(), 50);
                var plannedPaymentType = TruncateValue(
                    string.IsNullOrWhiteSpace(spovm.PlannedPaymentType) ? "OSTALO" : spovm.PlannedPaymentType.Trim(),
                    20);

                var requestedQuantities = spovm.Products
                    .Where(product => !product.PoMjeri)
                    .GroupBy(product => product.Id)
                    .ToDictionary(group => group.Key, group => group.Sum(product => product.Quantity));

                var poMjeriRequests = spovm.Products
                    .Where(product => product.PoMjeri)
                    .GroupBy(product => product.Id)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(product => (product.ConsumedLengthPerUnit ?? 0) * product.Quantity));

                var productIds = requestedQuantities.Keys
                    .Union(poMjeriRequests.Keys)
                    .Distinct()
                    .ToList();
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

                    // Regular and per-m2 products may be sold even if the resulting quantity drops below zero.
                    // Po mjeri products keep their dedicated remaining-length validation below.
                }

                foreach (var poMjeriRequest in poMjeriRequests)
                {
                    if (!productsById.TryGetValue(poMjeriRequest.Key, out var product) || product.Disabled)
                    {
                        TempData["ErrorMessage"] = $"Proizvod sa ID {poMjeriRequest.Key} nije pronađen.";
                        return RedirectToAction(nameof(ScannedProductsToBePurchased));
                    }

                    if (product.CreatedForDirectSale)
                    {
                        continue;
                    }

                    var remainingLength = await GetRemainingPoMjeriLengthAsync(product);
                    if (poMjeriRequest.Value > remainingLength)
                    {
                        TempData["ErrorMessage"] = $"Nema dovoljno preostale dužine za proizvod {product.Name} ({product.ProductNumber}). Dostupno: {remainingLength} cm.";
                        return RedirectToAction(nameof(ScannedProductsToBePurchased));
                    }
                }

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
                        CustomWidth = scannedProduct.PoMjeri ? scannedProduct.Width : null,
                        CustomLength = scannedProduct.PoMjeri ? scannedProduct.Length : null,
                        ConsumedLength = scannedProduct.PoMjeri ? scannedProduct.ConsumedLengthPerUnit : null,
                        DirectSaleOriginalTotal = scannedProduct.IsDirectSaleProduct ? scannedProduct.DirectSaleOriginalTotal : null,
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

                // A single SaveChanges call already runs in an EF-managed transaction.
                await _context.SaveChangesAsync();

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

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public IActionResult ExportScannedProductsToPdf([FromBody] ScannedProductsOverviewViewModel spovm)
        {
            if (spovm?.Products == null || !spovm.Products.Any())
            {
                return BadRequest(GetLocalizedText("NoProductsToExport", "No products to export."));
            }

            try
            {
                spovm.FullName ??= string.Empty;
                if (spovm.PurchaseTime == default)
                {
                    spovm.PurchaseTime = DateTime.Now;
                }

                var sellerName = BuildSellerName();
                var pdfBytes = GeneratePurchasePdf(spovm, sellerName);

                var customerFullName = spovm.FullName.ToUpperInvariant().Trim();
                foreach (var invalidChar in System.IO.Path.GetInvalidFileNameChars())
                {
                    customerFullName = customerFullName.Replace(invalidChar, '-');
                }

                var fileName = $"{spovm.PurchaseTime:dd-MM-yyyy HH.mm} {customerFullName}.pdf".Trim();
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export scanned products to PDF before purchase confirmation.");
                return StatusCode(500, GetLocalizedText("ErrorExportingPdf", "An error occurred while exporting PDF."));
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
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> DeleteScannedProduct(string lineId)
        {
            try
            {
                var scannedProds = GetScannedProducts();

                var item = scannedProds.FirstOrDefault(i => i.LineId == lineId);
                if (item != null)
                {
                    scannedProds.Remove(item);
                    if (item.IsDirectSaleProduct)
                    {
                        await DeleteDirectSaleProductsIfUnusedAsync(new[] { item.Id }, scannedProds);
                    }
                    HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));
                }

                return View("QRCodeScanning", scannedProds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting scanned product line {LineId}", lineId);
                return StatusCode(500, "An error occurred while removing the product.");
            }
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> UpdateQuantity(string lineId, string action)
        {
            try
            {
                var scannedProds = GetScannedProducts();
                var item = scannedProds.FirstOrDefault(i => i.LineId == lineId);

                if (item == null)
                {
                    _logger.LogWarning("UpdateQuantity: No scanned product found with line ID {LineId}", lineId);
                    return NotFound(Inventar.Resources.Resource.ProductNotFound);
                }

                if (item.IsDirectSaleProduct)
                {
                    return Json(new
                    {
                        qty = item.Quantity,
                        m2Total = item.M2Total,
                        priceTotal = item.PriceTotal,
                        message = Inventar.Resources.Resource.FixedQuantityForDirectSaleProduct
                    });
                }

                if (action == "increase")
                {
                    if (item.PoMjeri)
                    {
                        var product = await _context.Tepisi.FirstOrDefaultAsync(p => p.Id == item.Id && !p.Disabled);
                        if (product == null)
                        {
                            return NotFound(Inventar.Resources.Resource.ProductNotFound);
                        }

                        var availableLength = await GetSessionAwareRemainingLengthAsync(product, item.LineId);
                        item.MaxAvailableQuantity = PoMjeriHelper.CalculateMaxAvailableQuantity(
                            product.Width ?? 0,
                            availableLength,
                            item.Width ?? 0,
                            item.Length ?? 0);

                        if (item.Quantity >= item.MaxAvailableQuantity)
                        {
                            return Json(new
                            {
                                qty = item.Quantity,
                                m2Total = item.M2Total,
                                message = string.Format(CultureInfo.CurrentCulture, Inventar.Resources.Resource.MaxPiecesForGivenProduct, item.MaxAvailableQuantity)
                            });
                        }
                    }

                    item.Quantity += 1;
                }
                else if (action == "decrease" && item.Quantity > 1)
                {
                    item.Quantity -= 1;
                }

                RecalculateScannedProductTotals(item);

                HttpContext.Session.SetString("scannedProducts", JsonConvert.SerializeObject(scannedProds));

                var response = new
                {
                    qty = item.Quantity,
                    m2Total = item.M2Total,
                    priceTotal = item.PriceTotal,
                    maxQty = item.MaxAvailableQuantity
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating quantity for scanned product line {LineId}", lineId);
                return StatusCode(500, Inventar.Resources.Resource.ErrorUpdatingQuantity);
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

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public IActionResult ExportScannedProductsTableToPdf([FromBody] ScannedTableExportRequest request)
        {
            if (!HasScannedTableExportData(request))
            {
                return BadRequest(GetLocalizedText("NoProductsToExport", "No products to export."));
            }

            try
            {
                byte[] pdfBytes;
                using (var stream = new MemoryStream())
                {
                    using (var writer = new PdfWriter(stream))
                    using (var pdf = new PdfDocument(writer))
                    using (var document = new Document(pdf, PageSize.A4.Rotate()))
                    {
                        document.SetMargins(24, 20, 24, 20);

                        var fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                        if (System.IO.File.Exists(fontPath))
                        {
                            var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                            document.SetFont(font);
                        }

                        if (!string.IsNullOrWhiteSpace(request.Heading))
                        {
                            document.Add(new Paragraph(request.Heading)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetFontSize(14)
                                .SimulateBold()
                                .SetMarginBottom(12));
                        }

                        var columnCount = request.ColumnHeaders.Count;
                        var widths = Enumerable.Repeat(1f, columnCount).ToArray();
                        var table = new Table(UnitValue.CreatePercentArray(widths)).UseAllAvailableWidth();

                        foreach (var header in request.ColumnHeaders)
                        {
                            table.AddHeaderCell(new Cell()
                                .Add(new Paragraph(header ?? string.Empty).SetFontSize(9).SimulateBold())
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                                .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                                .SetPadding(4));
                        }

                        foreach (var row in request.Rows)
                        {
                            for (var columnIndex = 0; columnIndex < request.ColumnHeaders.Count; columnIndex++)
                            {
                                var cellValue = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;
                                table.AddCell(new Cell()
                                    .Add(new Paragraph(cellValue).SetFontSize(8))
                                    .SetTextAlignment(TextAlignment.CENTER)
                                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                                    .SetPadding(4));
                            }
                        }

                        document.Add(table);
                        document.Close();
                    }

                    pdfBytes = stream.ToArray();
                }

                var fileName = BuildSafeExportFileName(request.FileNameBase, "pdf");
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export scanned products table to PDF.");
                return StatusCode(500, GetLocalizedText("ErrorExportingPdf", "An error occurred while exporting PDF."));
            }
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public IActionResult ExportScannedProductsTableToExcel([FromBody] ScannedTableExportRequest request)
        {
            if (!HasScannedTableExportData(request))
            {
                return BadRequest(GetLocalizedText("NoProductsToExport", "No products to export."));
            }

            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Products");
                var columnCount = request.ColumnHeaders.Count;

                var currentRow = 1;
                if (!string.IsNullOrWhiteSpace(request.Heading))
                {
                    worksheet.Range(currentRow, 1, currentRow, columnCount).Merge().Value = request.Heading;
                    worksheet.Range(currentRow, 1, currentRow, columnCount).Style.Font.Bold = true;
                    worksheet.Range(currentRow, 1, currentRow, columnCount).Style.Font.FontSize = 14;
                    worksheet.Range(currentRow, 1, currentRow, columnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    currentRow += 2;
                }

                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var headerCell = worksheet.Cell(currentRow, columnIndex + 1);
                    headerCell.Value = request.ColumnHeaders[columnIndex];
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

                var dataStartRow = currentRow + 1;
                var rowIndex = dataStartRow;

                foreach (var row in request.Rows)
                {
                    for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    {
                        var cell = worksheet.Cell(rowIndex, columnIndex + 1);
                        cell.Value = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        cell.Style.Alignment.WrapText = true;
                    }

                    rowIndex++;
                }

                var dataEndRow = rowIndex - 1;
                var tableRange = worksheet.Range(currentRow, 1, dataEndRow, columnCount);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                worksheet.Columns(1, columnCount).AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                var fileName = BuildSafeExportFileName(request.FileNameBase, "xlsx");
                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export scanned products table to Excel.");
                return StatusCode(500, GetLocalizedText("ErrorExportingExcel", "An error occurred while exporting Excel."));
            }
        }

        [Authorize(Roles = "admin,superadmin,employee")]
        public IActionResult ManuallyAddProduct(int id)
        {
            try
            {
                var item = _context.Tepisi.FirstOrDefault(i => i.Id == id);
                if (item == null || item.Disabled == true || item.CreatedForDirectSale)
                {
                    TempData["ProductNotFound"] = Inventar.Resources.Resource.ProductNotFound;
                    _logger.LogWarning("ManuallyAddProduct: product with id {ProductId} was not found!", id);
                    return RedirectToAction("QRCodeScanning");
                }

                if (item.PoMjeri)
                {
                    TempData["ProductNotFound"] = Inventar.Resources.Resource.FirstEnterTheCorrectDimensions;
                    return RedirectToAction("QRCodeScanning");
                }

                List<ScannedProductViewModel> scannedProds = GetScannedProducts();
                AddOrIncrementRegularScannedProduct(scannedProds, item);

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
        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> ClearSession()
        {
            try
            {
                var scannedProducts = GetScannedProducts();
                var directSaleIds = scannedProducts
                    .Where(item => item.IsDirectSaleProduct)
                    .Select(item => item.Id)
                    .ToList();

                if (directSaleIds.Count > 0)
                {
                    await DeleteDirectSaleProductsIfUnusedAsync(directSaleIds);
                }

                _sessionService.ClearScannedProducts(HttpContext.Session);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear 'scannedProducts' from session.");
                return StatusCode(500, "An error occurred while clearing session data.");
            }
        }

        [Authorize(Roles = "admin,superadmin,employee")]
        public async Task<IActionResult> SearchTepisi(
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
                .Where(t => !t.Disabled && !t.CreatedForDirectSale)
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Model)
                .ThenBy(t => t.Color)
                .ThenBy(t => t.Width)
                .ThenBy(t => t.Length)
                .ThenBy(t => t.Id)
                .Take(30)
                .Select(t => new
                {
                    id = t.Id,
                    productNumber = t.ProductNumber,
                    name = t.Name,
                    model = t.Model,
                    color = t.Color,
                    width = t.Width,
                    length = t.Length,
                    poMjeri = t.PoMjeri,
                    unId = t.UnID
                })
                .ToList();

            var remainingLengths = await LoadRemainingPoMjeriLengthsAsync(results.Where(result => result.poMjeri).Select(result => result.id));

            var payload = results.Select(result => new
            {
                result.id,
                productNumber = TextEncodingHelper.Decode(result.productNumber),
                name = TextEncodingHelper.Decode(result.name),
                model = TextEncodingHelper.Decode(result.model),
                color = TextEncodingHelper.Decode(result.color),
                result.width,
                result.length,
                result.poMjeri,
                unId = TextEncodingHelper.Decode(result.unId),
                remainingWidth = result.poMjeri ? result.width : null,
                remainingLength = result.poMjeri && remainingLengths.TryGetValue(result.id, out var remainingLength) ? remainingLength : (int?)null
            });

            return Json(payload);
        }


        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
        public IActionResult AddProductById(int id)
        {
            return ManuallyAddProduct(id);
        }

        private static void NormalizeProductTextFields(Tepih tepih)
        {
            tepih.Name = TextEncodingHelper.NormalizeInput(tepih.Name) ?? string.Empty;
            tepih.ProductNumber = TextEncodingHelper.NormalizeInput(tepih.ProductNumber) ?? string.Empty;
            tepih.Model = TextEncodingHelper.NormalizeInput(tepih.Model) ?? string.Empty;
            tepih.BroaderCategory = TextEncodingHelper.NormalizeInput(tepih.BroaderCategory);
            tepih.NarrowerCategory = TextEncodingHelper.NormalizeInput(tepih.NarrowerCategory);
            tepih.Color = TextEncodingHelper.NormalizeInput(tepih.Color) ?? string.Empty;
            tepih.Description = TextEncodingHelper.NormalizeInput(tepih.Description);
            tepih.ShortDescription = TextEncodingHelper.NormalizeInput(tepih.ShortDescription);
            tepih.SeoTitle = TextEncodingHelper.NormalizeInput(tepih.SeoTitle);
            tepih.SeoDescription = TextEncodingHelper.NormalizeInput(tepih.SeoDescription);
            tepih.Slug = TextEncodingHelper.NormalizeInput(tepih.Slug);
            tepih.UnID = TextEncodingHelper.NormalizeInput(tepih.UnID);
        }

        private static void NormalizeProductTextFields(EditTepihViewModel tepih)
        {
            tepih.Name = TextEncodingHelper.NormalizeInput(tepih.Name) ?? string.Empty;
            tepih.ProductNumber = TextEncodingHelper.NormalizeInput(tepih.ProductNumber) ?? string.Empty;
            tepih.Model = TextEncodingHelper.NormalizeInput(tepih.Model) ?? string.Empty;
            tepih.BroaderCategory = TextEncodingHelper.NormalizeInput(tepih.BroaderCategory);
            tepih.NarrowerCategory = TextEncodingHelper.NormalizeInput(tepih.NarrowerCategory);
            tepih.Color = TextEncodingHelper.NormalizeInput(tepih.Color) ?? string.Empty;
            tepih.Description = TextEncodingHelper.NormalizeInput(tepih.Description);
            tepih.ShortDescription = TextEncodingHelper.NormalizeInput(tepih.ShortDescription);
            tepih.SeoTitle = TextEncodingHelper.NormalizeInput(tepih.SeoTitle);
            tepih.SeoDescription = TextEncodingHelper.NormalizeInput(tepih.SeoDescription);
            tepih.Slug = TextEncodingHelper.NormalizeInput(tepih.Slug);
            tepih.UnID = TextEncodingHelper.NormalizeInput(tepih.UnID);
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin,employee")]
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
