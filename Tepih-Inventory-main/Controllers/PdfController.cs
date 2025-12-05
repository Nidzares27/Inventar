using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Image;
using Microsoft.AspNetCore.Mvc;
using iText.Layout.Properties;
using iText.Layout.Borders;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using Inventar.Data;
using Microsoft.EntityFrameworkCore;
using Inventar.ViewModels.Shared;
using iText.Kernel.Geom;
using Inventar.ViewModels.Buyer.DTO;
using Inventar.ViewModels.Sales.DTO;
using Inventar.ViewModels.Inventory.DTO;
using iText.IO.Font;
using static iText.Kernel.Font.PdfFontFactory;
using Path = System.IO.Path;
using Inventar.ViewModels.Inventory;
using Inventar.ViewModels.Pdf;
using System.Diagnostics;

namespace Inventar.Controllers
{
    public class PdfController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfController> _logger;

        public PdfController(HttpClient httpClient, ApplicationDbContext context, IWebHostEnvironment env, ILogger<PdfController> logger)
        {
            _httpClient = httpClient;
            this._context = context;
            this._env = env;
            this._logger = logger;
        }

        [HttpGet("generate-cloudinary-image-pdf")]
        public async Task<IActionResult> GenerateCloudinaryImagePdf(int id)
        {
            var tepih = await _context.Tepisi.FindAsync(id);
            if (tepih == null)
            {
                _logger.LogError("Couldn't find a product with an ID: {id} for generating Cloudinary image!", id);
                return NotFound("Product not found!");
            }

            if (string.IsNullOrWhiteSpace(tepih.QRCodeUrl))
            {
                _logger.LogError("QR code URL is missing for a product with an ID: {id} for generating Cloudinary image!", id);
                return BadRequest("QR code URL is missing!");
            }

            byte[] imageBytes;
            try
            {
                using var response = await _httpClient.GetAsync(tepih.QRCodeUrl);
                if (!response.IsSuccessStatusCode)
                    return BadRequest("Could not retrieve the image from Cloudinary.");

                imageBytes = await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving image from Cloudinary.");
                return StatusCode(500, $"Error retrieving image: {ex.Message}");
            }

            using var memoryStream = new MemoryStream();
            using var writer = new PdfWriter(memoryStream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
            if (!System.IO.File.Exists(fontPath))
            {
                _logger.LogError("Font file missing at {Path}", fontPath);
                return StatusCode(500, "Font file not found.");
            }

            PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
            document.SetFont(font);

            // Build description line
            var description = $"{tepih.Name.ToUpper().Trim() ?? ""}/{tepih.Model.ToUpper().Trim() ?? ""}/" +
                              $"{tepih.Width?.ToString("0.##")}/{tepih.Length?.ToString("0.##")}/" +
                              $"{tepih.Color.ToUpper().Trim() ?? ""}";

            var paragraph = new Paragraph(description)
                .SetFontSize(12)
                .SetBold()
                .SetMarginBottom(10)
                .SetTextAlignment(TextAlignment.CENTER);

            document.Add(paragraph);

            try
            {
                var imgData = ImageDataFactory.Create(imageBytes);
                var image = new Image(imgData)
                    .ScaleToFit(200, 200)
                    .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);

                document.Add(image);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering QR code image!");
                return StatusCode(500, $"Image rendering error: {ex.Message}");
            }

            document.Close();

            return File(memoryStream.ToArray(), "application/pdf", $"CloudinaryImage_{tepih.Id}.pdf");
        }

        public async Task<IActionResult> ExportBuyerActivity(int buyerId, DateTime? startDate, DateTime? endDate)
        {
            var buyer = await _context.Kupci.FirstOrDefaultAsync(k => k.Id == buyerId);
            if (buyer == null) {
                _logger.LogError("Couldn't find a buyer with provided id {buyerId}", buyerId);
                return NotFound("Couldn't find a buyer with provided id!");
            } 

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
                .GroupBy(p => new { p.VrijemeProdaje, p.Prodavac })
                .Select(g => new BuyerActivityItem
                {
                    ActivityTime = g.Key.VrijemeProdaje,
                    Type = "Sale",
                    Amount = g.Sum(prodaja =>
                        prodaja.Tepih.PerM2
                            ? prodaja.Rabat != null || prodaja.Rabat > 0 ? (prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)) - (((decimal)prodaja.Rabat / 100) * ((prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)))) : prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)
                            : prodaja.Rabat != null || prodaja.Rabat > 0 ? (prodaja.Price * prodaja.Quantity) - (((decimal)prodaja.Rabat / 100) * (prodaja.Price * prodaja.Quantity)) : prodaja.Price * prodaja.Quantity
                        ),
                    Info = g.Key.Prodavac
                });

            var paymentItems = payments.Select(p => new BuyerActivityItem
            {
                ActivityTime = p.PaymentTime,
                Type = "Payment",
                Amount = p.Amount,
                Info = p.PaymentType ?? "N/A"
            });

            var debtItems = debts.Select(p => new BuyerActivityItem
            {
                ActivityTime = p.DebtTime,
                Type = "Debt",
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
                        Type = "Sale",
                        Amount = g.Sum(prodaja =>
                            prodaja.Tepih.PerM2
                                ? prodaja.Rabat != null || prodaja.Rabat > 0 ? (prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)) - (((decimal)prodaja.Rabat / 100) * ((prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)))) : prodaja.Price * ((((decimal)prodaja.Tepih.Length * (decimal)prodaja.Tepih.Width) / 10000m) * prodaja.Quantity)
                                : prodaja.Rabat != null || prodaja.Rabat > 0 ? (prodaja.Price * prodaja.Quantity) - (((decimal)prodaja.Rabat / 100) * (prodaja.Price * prodaja.Quantity)) : prodaja.Price * prodaja.Quantity
                        ),
                        Info = g.Key.Prodavac,
                        Disabled = g.Key.Disabled
                    });

                pastPaymentItems = pastPayments.Select(p => new BuyerActivityItem
                {
                    ActivityTime = p.PaymentTime,
                    Type = "Payment",
                    Amount = p.Amount,
                    Info = p.PaymentType ?? "N/A",
                    Disabled = p.Disabled
                });

                pastDebtItems = pastDebts.Select(p => new BuyerActivityItem
                {
                    ActivityTime = p.DebtTime,
                    Type = "Debt",
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

            var activities = groupedSales
                .Concat(paymentItems)
                .Concat(debtItems)
                .OrderBy(a => a.ActivityTime)
                .ToList();

            var salesUndisabled = groupedSales.Where(s => s.Disabled != true);
            var paymentsUndisabled = paymentItems.Where(s => s.Disabled != true);

            //NOVO
            var pastTotalSales = pastGroupedSales.Sum(s => s.Amount);
            var pastTotalPayments = pastPaymentItems.Sum(p => p.Amount);
            var pastTotalDugovanja = pastDebtItems.Sum(p => p.Amount);
            var pastTotalDebt = pastTotalSales + pastTotalDugovanja - pastTotalPayments;

            var totalSalesUndisabled = salesUndisabled.Sum(s => s.Amount);
            var totalPaymentsUndisabled = paymentsUndisabled.Sum(p => p.Amount);
            var totalDugovanjaUndisabled = debtItems.Sum(p => p.Amount);
            var totalDebtUndisabled = totalSalesUndisabled + totalDugovanjaUndisabled - totalPaymentsUndisabled;

            var totalDebt = totalDebtUndisabled + pastTotalDebt;

            // Generate PDF with iText7
            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
            if (!System.IO.File.Exists(fontPath))
            {
                _logger.LogError("Font file missing at {Path}", fontPath);
                return StatusCode(500, "Font file not found.");
            }

            PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
            document.SetFont(font);

            //var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            //var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            var header = new Paragraph(buyer.CustomerFullName)
                .SetBold()
                //.SetFont(boldFont)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(16);
            document.Add(header);

            if (startDate.HasValue && endDate.HasValue)
            {
                var dateInfo = new Paragraph($"Period: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}")
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetFontSize(10);
                document.Add(dateInfo);
            }

            decimal runningDebt = 0;
            int rowsPerPage = 30;
            int totalRows = activities.Count;
            int pageIndex = 0;

            for (int i = 0; i < totalRows; i += rowsPerPage)
            {
                var pageActivities = activities.Skip(i).Take(rowsPerPage).ToList();

                if (pageIndex > 0)
                {
                    // Force new page for clean layout
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                }

                var table = new Table(5).UseAllAvailableWidth();
                table.AddHeaderCell("#").SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell(@Inventar.Resources.Resource.Time).SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell(@Inventar.Resources.Resource.Type).SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell(@Inventar.Resources.Resource.Amount).SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell(@Inventar.Resources.Resource.SellerPaymentType).SetBackgroundColor(ColorConstants.CYAN);

                table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph(@Inventar.Resources.Resource.PreviousDebt)).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph($"{Math.Round(pastTotalDebt, 2)}€")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.PINK));

                if (pageIndex > 0)
                {
                    table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph(@Inventar.Resources.Resource.DebtSoFar)).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph($"{Math.Round(runningDebt, 2)}€")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.YELLOW));
                }

                int rowCounter = i + 1; // to match global row count
                foreach (var item in pageActivities)
                {
                    runningDebt += item.Type == "Sale" ? item.Amount : -item.Amount;

                    //var bgColor = item.Type == "Kupovina" ? ColorConstants.LIGHT_GRAY : ColorConstants.WHITE;
                    var bgColor = item.Type == "Sale" ? ColorConstants.LIGHT_GRAY : item.Type == "Payment" ? ColorConstants.GREEN : ColorConstants.WHITE;


                    table.AddCell(new Cell().Add(new Paragraph(rowCounter.ToString())).SetBackgroundColor(bgColor));
                    table.AddCell(new Cell().Add(new Paragraph(item.ActivityTime.ToString("dd-MM-yyyy HH:mm"))).SetBackgroundColor(bgColor));
                    table.AddCell(new Cell().Add(new Paragraph(item.Type)).SetBackgroundColor(bgColor));
                    table.AddCell(new Cell().Add(new Paragraph($"{Math.Round(item.Amount, 2)}€")).SetBackgroundColor(bgColor));
                    table.AddCell(new Cell().Add(new Paragraph(item.Info)).SetBackgroundColor(bgColor));

                    rowCounter++;
                }

                document.Add(table);
                pageIndex++;
            }

            var totalParagraph = new Paragraph($"{@Inventar.Resources.Resource.Debt}: {Math.Round(totalDebtUndisabled, 2)}€ | {@Inventar.Resources.Resource.PreviousDebt}: {Math.Round(pastTotalDebt/*totalDebtDisabled*/, 2)}€ | {@Inventar.Resources.Resource.TotalDebt}: {Math.Round(totalDebt, 2)}€")
                .SetBold()
                //.SetFont(boldFont)
                .SetFontSize(12)
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetMarginTop(20);
            document.Add(totalParagraph);
            document.Close();

            return File(stream.ToArray(), "application/pdf", $"Aktivnost_kupca_{buyer.CustomerFullName}_{DateTime.Now:yyyyMMddHHmm}.pdf");
        }

        [HttpPost]
        public IActionResult GenerateBuysPDF([FromBody] BuysPdfRequest request)
        {
            if (request == null)
            {
                _logger.LogError("GenerateBuysPDF:  missing data or headers {req}", request);
                return BadRequest("Error occured while requesting data!");
            }

            using var ms = new MemoryStream();
            try
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                // Load font with error check
                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font file missing at {Path}", fontPath);
                    return StatusCode(500, "Font file not found.");
                }

                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font).SetFontSize(10);

                // Add heading centered at the top
                var heading = new Paragraph(request.Heading ?? "")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                // Determine number of columns safely
                int numColumns = (request.Data?.Count > 0) ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);
                if (numColumns == 0)
                    return BadRequest("No columns available to create the table.");

                var table = new Table(numColumns).UseAllAvailableWidth();

                // Add header row (column names)
                if (request.ColumnHeaders != null)
                {
                    foreach (var header in request.ColumnHeaders)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new Paragraph(header ?? ""))
                            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetBold());
                    }
                }

                // Add filters row
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = "";

                    if (request.Filters != null && request.Filters.TryGetValue(i, out string filterText))
                        filterValue = filterText;

                    if (i == 1 &&
                        !string.IsNullOrEmpty(request.MinDate) &&
                        !string.IsNullOrEmpty(request.MaxDate) &&
                        DateTime.TryParse(request.MinDate, out var minDate) &&
                        DateTime.TryParse(request.MaxDate, out var maxDate))
                    {
                        filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                if (request.Data != null)
                {
                    foreach (var row in request.Data)
                    {
                        for (int i = 0; i < numColumns; i++)
                        {
                            string cellText = (row != null && row.Length > i) ? row[i] ?? "" : "";
                            table.AddCell(new Cell()
                                .Add(new Paragraph(cellText))
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetPadding(5));
                        }
                    }
                }

                // Add totals row spanning all columns
                string totalsText = $"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€";

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph(totalsText))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                // Return PDF file
                return File(ms.ToArray(), "application/pdf", "Kupovine.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Buys PDF");
                return StatusCode(500, "Internal server error while generating PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateDetailsPDF([FromBody] DetailsPdfRequest request)
        {
            try
            {
                if (request == null || request.ColumnHeaders == null || request.Data == null || request.Filters == null)
                {
                    _logger.LogError("GenerateDetailsPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request data.");
                }

                int numCols = request.ColumnHeaders.Count;
                if (numCols == 0 || request.Filters.Count != numCols)
                {
                    _logger.LogError("GenerateDetailsPDF: No columns provided {cols}.",numCols);
                    return BadRequest("Column headers and filters count mismatch.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                // Font setup
                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font file missing at {Path}", fontPath);
                    return StatusCode(500, "Missing font file.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font).SetFontSize(10);

                // Header: Left and Right aligned titles
                var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth().SetBorder(Border.NO_BORDER);

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph(request.HeadingLeft ?? "").SetBold())
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.LEFT));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph(request.HeadingRight ?? ""))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                var table = new Table(numCols).UseAllAvailableWidth();
                table.SetKeepTogether(true);

                var headerBg = ColorConstants.LIGHT_GRAY;
                var filterBg = new DeviceRgb(255, 255, 200);
                var totalsBg = new DeviceRgb(220, 220, 220);

                // Column Headers
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header ?? "").SetBold())
                        .SetBackgroundColor(headerBg)
                        .SetTextAlignment(TextAlignment.CENTER));
                }

                // Filter Headers
                foreach (var filter in request.Filters)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filter ?? ""))
                        .SetBackgroundColor(filterBg)
                        .SetTextAlignment(TextAlignment.CENTER));
                }

                // Data Rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numCols; i++)
                    {
                        string value = row.ElementAtOrDefault(i) ?? "";
                        table.AddCell(new Cell()
                            .Add(new Paragraph(value)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetMultipliedLeading(1.2f))
                            .SetTextAlignment(TextAlignment.CENTER));
                    }
                }

                // Totals Row
                string totalsText = $"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity}   |   {@Inventar.Resources.Resource.M2Total}: {request.TotalM2:F2}   |   {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice:F2}€";

                table.AddCell(new Cell(1, numCols)
                    .Add(new Paragraph(totalsText).SetBold())
                    .SetBackgroundColor(totalsBg)
                    .SetTextAlignment(TextAlignment.CENTER));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Detaljne_prodaje_kupovine.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating detailed PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateAllSalesPDF([FromBody] AllSalesPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GenerateAllSalesPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request: missing data or column headers.");
                }

                int numColumns = request.ColumnHeaders.Count;

                if (numColumns == 0)
                {
                    _logger.LogError("GenerateAllSalesPDF: No columns provided.");
                    return BadRequest("No columns provided.");
                }

                float[] columnWidths = new float[numColumns];
                for (int i = 0; i < numColumns; i++)
                {
                    columnWidths[i] = (i == 1 || i == 4 || i == numColumns - 1) ? 3f : (i == 6 /*|| i == numColumns - 1*/) ? 2.5f : (i == 0 || i == 2 || i == 3) ? 1.0f : 1.4f;
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font not found at path: {FontPath}", fontPath);
                    return StatusCode(500, "Font file is missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font).SetFontSize(8);

                // Heading
                document.Add(new Paragraph(request.Heading ?? "")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(14)
                    .SetMarginBottom(15));

                var table = new Table(UnitValue.CreatePercentArray(columnWidths)).UseAllAvailableWidth();
                table.SetKeepTogether(true);

                // Header
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBold()
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetPadding(5));
                }

                // Filters
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = "";

                    if (i == 1 && DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                        filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                    else if (request.Filters?.TryGetValue(i, out var val) == true)
                        filterValue = val;

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetPadding(5));
                }

                // Data Rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        string value = row.ElementAtOrDefault(i) ?? "";
                        table.AddCell(new Cell()
                            .Add(new Paragraph(value))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Totals row
                string totalsText = $"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2:F2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice:F2}€";

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph(totalsText))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Prodaje.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AllSales PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateSalesPDF([FromBody] BuysPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GenerateSalesPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request: missing data or column headers.");
                }

                int numColumns = request.Data[0].Length - 1;
                if (numColumns <= 0 || request.ColumnHeaders.Count < numColumns)
                {
                    _logger.LogError("GenerateSalesPDF: Column header count mismatch or insufficient data: {numCols}.", numColumns);
                    return BadRequest("Column header count mismatch or insufficient data.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font not found at path: {FontPath}", fontPath);
                    return StatusCode(500, "Font file is missing.");
                }

                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Heading
                document.Add(new Paragraph(request.Heading ?? "")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10));

                var table = new Table(numColumns).UseAllAvailableWidth();

                // Column Headers
                foreach (var header in request.ColumnHeaders.Take(numColumns))
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Filters row
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";

                    if (i == 1 && DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                    {
                        filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        string cellValue = row.ElementAtOrDefault(i) ?? "";
                        table.AddCell(new Cell()
                            .Add(new Paragraph(cellValue))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Totals row
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Grupisane_prodaje.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GeneratePerDayPDF([FromBody] PerDayPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GeneratePerDayPDF: missing data or column headers. {req}", request);
                    return BadRequest("Invalid request: missing data or column headers.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A5);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font not found at path: {FontPath}", fontPath);
                    return StatusCode(500, "Font file is missing.");
                }

                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Heading
                document.Add(new Paragraph((request.Heading1 ?? "") + (request.Heading2 ?? ""))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10));

                int numColumns = request.Data[0].Length;
                var table = new Table(numColumns).UseAllAvailableWidth();

                // Column headers
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Filters
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Totals row
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Po_danu.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating per-day PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GeneratePerProductsUngroupedPDF([FromBody] PerProductsPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GeneratePerProductsUngroupedPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request: missing data or headers.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font file not found at {FontPath}", fontPath);
                    return StatusCode(500, "Font file not found.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Heading
                document.Add(new Paragraph(request.Heading1 ?? "")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5));

                var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth().SetBorder(Border.NO_BORDER);
                headerTable.AddCell(new Cell().Add(new Paragraph(request.Heading2 ?? "").SetBold())
                    .SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.LEFT));
                headerTable.AddCell(new Cell().Add(new Paragraph(request.Heading3 ?? ""))
                    .SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT));
                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                int numColumns = request.Data[0].Length;
                var table = new Table(numColumns).UseAllAvailableWidth();

                // Column headers
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Filters
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Totals
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                string sanitizedCustomerName = string.Join("_", request.CustomerName.Split(Path.GetInvalidFileNameChars()));
                string fileName = $"Po_proizvodima_(Negrupisano)_{sanitizedCustomerName}.pdf";

                return File(ms.ToArray(), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ungrouped per-products PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GeneratePerProductsGroupedPDF([FromBody] PerProductsPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GeneratePerProductsGroupedPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request: missing data or headers.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogError("Font file missing: {FontPath}", fontPath);
                    return StatusCode(500, "Required font file is missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Heading
                document.Add(new Paragraph(request.Heading1 ?? "")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5));

                var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                headerTable.SetBorder(Border.NO_BORDER);
                headerTable.AddCell(new Cell().Add(new Paragraph(request.Heading2 ?? "").SetBold())
                    .SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.LEFT));
                headerTable.AddCell(new Cell().Add(new Paragraph(request.Heading3 ?? ""))
                    .SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT));
                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                int numColumns = request.Data[0].Length;

                var table = new Table(numColumns).UseAllAvailableWidth();

                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                string sanitizedName = string.Join("_", request.CustomerName.Split(Path.GetInvalidFileNameChars()));
                string filename = $"Po_proizvodima_(Grupisano)_{sanitizedName}.pdf";

                return File(ms.ToArray(), "application/pdf", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating grouped products PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateDetailsGroupedPdf([FromBody] DetailsGroupedPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GenerateDetailsGroupedPdf:  missing data or headers {req}", request);
                    return BadRequest("Invalid request: missing data or headers.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogWarning("Font file not found at path: {Path}", fontPath);
                    return StatusCode(500, "Required font file is missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Headings
                document.Add(new Paragraph(request.Heading1 ?? "")
                    .SetTextAlignment(TextAlignment.CENTER).SetBold().SetFontSize(12).SetMarginBottom(5));

                document.Add(new Paragraph($"{request.Heading2}{request.Heading3}")
                    .SetTextAlignment(TextAlignment.CENTER).SetBold().SetFontSize(12).SetMarginBottom(5));

                // Labels table
                var labeleTable = new Table(new float[] { 1f, 2f }).UseAllAvailableWidth()
                    .SetMarginBottom(10).SetFontSize(8);

                void AddRow(string label, string value)
                {
                    labeleTable.AddCell(new Cell().Add(new Paragraph(label).SetBold()).SetBackgroundColor(ColorConstants.CYAN));
                    labeleTable.AddCell(new Cell().Add(new Paragraph(value ?? "")).SetTextAlignment(TextAlignment.LEFT));
                }

                AddRow(@Inventar.Resources.Resource.ProductNumber, request.ProductNumber);
                AddRow(@Inventar.Resources.Resource.Name, request.Name);
                AddRow(@Inventar.Resources.Resource.Size, request.Size);
                AddRow("M²", request.M2PerProduct);

                document.Add(labeleTable);

                int numColumns = request.Data[0].Length;
                var table = new Table(numColumns).UseAllAvailableWidth();

                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";

                    if (i == 1 && DateTime.TryParse(request.MinDate, out var minDate))
                    {
                        filterValue = minDate.ToString("dd-MM-yyyy");
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                string sanitizedCustomerName = string.Join("_", request.CustomerName.Split(Path.GetInvalidFileNameChars()));
                string filename = $"Po_proizvodima_detaljno_(Grupisano)_{sanitizedCustomerName}.pdf";

                return File(ms.ToArray(), "application/pdf", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating grouped details PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateDetailsUngroupedPdf([FromBody] DetailsUngroupedPdfRequest request)
        {
            try
            {
                if (request == null || request.Data == null || request.Data.Count == 0 || request.ColumnHeaders == null)
                {
                    _logger.LogError("GenerateDetailsUngroupedPdf:  missing data or headers {req}", request);
                    return BadRequest("Invalid PDF request.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogWarning("Font not found at path: {Path}", fontPath);
                    return StatusCode(500, "Font file is missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                var heading1 = new Paragraph(request.Heading1 ?? "")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading1);

                var heading2 = new Paragraph($"{request.Heading2}{request.Heading3}")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading2);

                var labeleTable = new Table(new float[] { 1f, 2f }).UseAllAvailableWidth();
                labeleTable.SetMarginBottom(10).SetFontSize(8);

                void AddRow(string label, string value)
                {
                    labeleTable.AddCell(new Cell().Add(new Paragraph(label).SetBold()).SetBackgroundColor(ColorConstants.CYAN));
                    labeleTable.AddCell(new Cell().Add(new Paragraph(value ?? "")).SetTextAlignment(TextAlignment.LEFT));
                }

                AddRow(@Inventar.Resources.Resource.ProductID, request.ProductId);
                AddRow(@Inventar.Resources.Resource.ProductNumber, request.ProductNumber);
                AddRow(@Inventar.Resources.Resource.Name, request.Name);
                AddRow("Model", request.Model);
                AddRow(@Inventar.Resources.Resource.Color, request.Color);
                AddRow(@Inventar.Resources.Resource.Size, request.Size);
                AddRow("M²", request.M2PerProduct);

                document.Add(labeleTable);

                int numColumns = request.Data[0].Length;
                var table = new Table(numColumns).UseAllAvailableWidth();

                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";

                    if (i == 1 && DateTime.TryParse(request.MinDate, out var minDate))
                    {
                        filterValue = minDate.ToString("dd-MM-yyyy");
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2} | {@Inventar.Resources.Resource.PriceTotal}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                // Sanitize filename to remove invalid characters
                string sanitizedCustomerName = string.Join("_", request.CustomerName.Split(Path.GetInvalidFileNameChars()));
                string fileName = $"Po_proizvodima_detaljno_(Negrupisano)_{sanitizedCustomerName}.pdf";

                return File(ms.ToArray(), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating details ungrouped PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateInventoryPDF([FromBody] InventoryPdfRequest request)
        {
            try
            {
                if (request == null || request.ColumnHeaders == null || request.Data == null)
                {
                    _logger.LogError("GenerateInventoryPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request data.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");

                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogWarning("Font file not found at {Path}", fontPath);
                    return StatusCode(500, "Font file is missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                var heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : request.ColumnHeaders.Count;
                var table = new Table(numColumns).UseAllAvailableWidth();

                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.TotalQuantity}: {request.TotalQuantity} | {@Inventar.Resources.Resource.M2Total}: {request.TotalM2}"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Inventar.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory PDF.");
                return StatusCode(500, "An error occurred while generating the inventory PDF.");
            }
        }

        [HttpPost]
        public IActionResult GeneratePaymentHistoryPDF([FromBody] PaymentPdfRequest request)
        {
            try
            {
                if (request == null || request.ColumnHeaders == null || request.Data == null)
                {
                    _logger.LogError("GeneratePaymentHistoryPDF:  missing data or headers {req}", request);
                    return BadRequest("Invalid request data.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A5);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogWarning("Font file not found: {FontPath}", fontPath);
                    return StatusCode(500, "Font file missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                var heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : request.ColumnHeaders.Count;
                var table = new Table(numColumns).UseAllAvailableWidth();

                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";
                    if (i == 2 && DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                    {
                        filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.Total}: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Istorija_plaćanja.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for payment history.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public IActionResult GenerateDebtHistoryPdf([FromBody] DebtHistoryRequest request)
        {
            try
            {
                if (request == null || request.ColumnHeaders == null || request.Data == null)
                {
                    _logger.LogError("DebtHistoryRequest:  missing data or headers {req}", request);
                    return BadRequest("Invalid request data.");
                }

                using var ms = new MemoryStream();
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A5);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                if (!System.IO.File.Exists(fontPath))
                {
                    _logger.LogWarning("Font file not found: {FontPath}", fontPath);
                    return StatusCode(500, "Font file missing.");
                }

                var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                var heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : request.ColumnHeaders.Count;
                var table = new Table(numColumns).UseAllAvailableWidth();

                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters?.GetValueOrDefault(i) ?? "";
                    if (i == 1 && DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                    {
                        filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row.ElementAtOrDefault(i) ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.Total}: {request.TotalDebt}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Istorija_dugovanja.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for debt history.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        public FileContentResult Faktura(string custName, string vrijemeProdaje, List<SaleDetailsViewModel> model)
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

                var groupedProducts = from p in model
                                      group p by new { p.Name, p.Length, p.Width, p.M2PerUnit, p.ProductNumber, p.Price, p.Rabat ,p.Seller } into g
                                      select new ReceiptWithSellerViewModel
                                      {
                                          ProductNumber = g.Key.ProductNumber,
                                          Name = g.Key.Name,
                                          Price = g.Average(p => p.Price),
                                          Size = $"{g.Key.Width}X{g.Key.Length}",
                                          M2PerUnit = g.Key.M2PerUnit,
                                          M2Total = g.Sum(p => p.M2Total),
                                          Quantity = g.Sum(p => p.Quantity),
                                          PriceTotal = g.Sum(p => p.PriceTotal),
                                          Seller = g.Key.Seller,
                                          Rabat = (int?)g.Average(p => p.Rabat)
                                      };

                var headerTable = new Table(3)
                                    .UseAllAvailableWidth()
                                    .SetMarginBottom(5);

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.Seller}: {model[0].Seller}"))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.LEFT));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"{custName.ToUpper().Trim()}").SetFontSize(10).SetBold())
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.CENTER));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"{@Inventar.Resources.Resource.Time}: {vrijemeProdaje}"))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                document.Add(headerTable);
                document.Add(new Paragraph("\n"));


                var table = new Table(9).UseAllAvailableWidth();

                if (User.Identity.IsAuthenticated && (User.IsInRole("admin") || User.IsInRole("superadmin")))
                {
                    string[] headers = { @Inventar.Resources.Resource.ProductNumber, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Price, "Rabat %" ,@Inventar.Resources.Resource.Size, @Inventar.Resources.Resource.Quantity, "m²", @Inventar.Resources.Resource.M2Total, @Inventar.Resources.Resource.Amount };

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

                    decimal? totalPrice = 0;
                    decimal? totalM2 = 0;
                    int totalQuantity = 0;

                    foreach (var item in groupedProducts)
                    {
                        table.AddCell(CreateCenteredCell(item.ProductNumber));
                        table.AddCell(CreateCenteredCell(item.Name));
                        table.AddCell(CreateCenteredCell($"{Math.Round(item.Price, 2)}€"));
                        table.AddCell(CreateCenteredCell(item.Rabat?.ToString() ?? ""));
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
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell(@Inventar.Resources.Resource.Total + ":"));
                    table.AddCell(CreateCenteredBoldCell(totalQuantity.ToString()));
                    table.AddCell(CreateCenteredBoldCell(""));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalM2 ?? 0, 2)}"));
                    table.AddCell(CreateCenteredBoldCell($"{Math.Round(totalPrice ?? 0, 2)}€"));
                }

                document.Add(table);
                document.Close();
                return File(stream.ToArray(), "application/pdf", "OrderDetails.pdf");

                //return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF generation failed for purchase: {Customer}", custName);
                throw;
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
                .SetBold();
            return new Cell()
                .Add(paragraph)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(1);
        }
    }
}