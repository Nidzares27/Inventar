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

        //[HttpGet("generate-cloudinary-image-pdf")]
        //public async Task<IActionResult> GenerateCloudinaryImagePdf(int id)
        //{
        //    var tepih = await _context.Tepisi.FindAsync(id);
        //    if (tepih == null)
        //        return NotFound();

        //    var cloudinaryImageUrl = tepih.QRCodeUrl;

        //    // Download image
        //    byte[] imageBytes;
        //    using (var response = await _httpClient.GetAsync(cloudinaryImageUrl))
        //    {
        //        if (!response.IsSuccessStatusCode)
        //            return BadRequest("Could not retrieve the image from Cloudinary");

        //        imageBytes = await response.Content.ReadAsByteArrayAsync();
        //    }

        //    // Generate PDF
        //    using (var memoryStream = new MemoryStream())
        //    {
        //        var writer = new PdfWriter(memoryStream);
        //        var pdf = new PdfDocument(writer);
        //        var document = new Document(pdf);

        //        document.Add(new Paragraph()
        //            .Add(new Text(tepih.Name)).Add(new Text("/"))
        //            .Add(new Text(tepih.Model)).Add(new Text("/"))
        //            .Add(new Text(tepih.Length?.ToString() ?? "")).Add(new Text("/"))
        //            .Add(new Text(tepih.Width?.ToString() ?? "")).Add(new Text("/"))
        //            .Add(new Text(tepih.Color))
        //            .SetFontSize(12).SetMarginBottom(5).SetBold().SetTextAlignment(TextAlignment.CENTER));

        //        var img = ImageDataFactory.Create(imageBytes);
        //        var image = new Image(img)
        //            .ScaleToFit(200, 200)
        //            .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);

        //        document.Add(image);
        //        document.Close();

        //        byte[] pdfBytes = memoryStream.ToArray();
        //        return File(pdfBytes, "application/pdf", $"CloudinaryImage_{tepih.Id}.pdf");
        //    }
        //}

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
                              $"{tepih.Length?.ToString("0.##")}/{tepih.Width?.ToString("0.##")}/" +
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
                .GroupBy(p => new { p.VrijemeProdaje, p.Prodavac })
                .Select(g => new BuyerActivityItem
                {
                    ActivityTime = g.Key.VrijemeProdaje,
                    Type = "Kupovina",
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

            var activities = groupedSales
                .Concat(paymentItems)
                .OrderBy(a => a.ActivityTime)
                .ToList();

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
                table.AddHeaderCell("Vrijeme").SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell("Tip").SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell("Iznos").SetBackgroundColor(ColorConstants.CYAN);
                table.AddHeaderCell("Prodavac/tip placanja").SetBackgroundColor(ColorConstants.CYAN);

                table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph("Prethodni dug:")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph($"{Math.Round(pastTotalDebt, 2)}€")).SetBackgroundColor(ColorConstants.PINK));
                table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.PINK));

                if (pageIndex > 0)
                {
                    table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph("Dug do sad")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph($"{Math.Round(runningDebt, 2)}€")).SetBackgroundColor(ColorConstants.YELLOW));
                    table.AddCell(new Cell().Add(new Paragraph("")).SetBackgroundColor(ColorConstants.YELLOW));
                }

                int rowCounter = i + 1; // to match global row count
                foreach (var item in pageActivities)
                {
                    runningDebt += item.Type == "Kupovina" ? item.Amount : -item.Amount;

                    var bgColor = item.Type == "Kupovina" ? ColorConstants.LIGHT_GRAY : ColorConstants.WHITE;

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

            var totalParagraph = new Paragraph($"Dug: {Math.Round(totalDebtUndisabled, 2)}€ | Prethodni dug: {Math.Round(pastTotalDebt/*totalDebtDisabled*/, 2)}€ | Ukupan dug: {Math.Round(totalDebt, 2)}€")
                .SetBold()
                //.SetFont(boldFont)
                .SetFontSize(12)
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetMarginTop(20);
            document.Add(totalParagraph);
            document.Close();

            return File(stream.ToArray(), "application/pdf", $"Aktivnost_kupca_{buyer.CustomerFullName}_{DateTime.Now:yyyyMMddHHmm}.pdf");
        }

        //[HttpPost]
        //public IActionResult GenerateBuysPDF([FromBody] BuysPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4.Rotate());
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(10);
        //        document.Add(heading);

        //        int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            if (i == 0 && !string.IsNullOrEmpty(request.MinDate) && !string.IsNullOrEmpty(request.MaxDate))
        //            {
        //                if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
        //                {
        //                    filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
        //                }
        //            }

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Kupovine.pdf");
        //    }
        //}

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
                string totalsText = $"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€";
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


        //[HttpPost]
        //public IActionResult GenerateDetailsPDF([FromBody] DetailsPdfRequest request)
        //{
        //    using var ms = new MemoryStream();
        //    var writer = new PdfWriter(ms);
        //    var pdf = new PdfDocument(writer);
        //    var document = new Document(pdf, PageSize.A4.Rotate());
        //    document.SetMargins(20, 20, 20, 20);

        //    string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //    PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //    document.SetFont(font).SetFontSize(10);

        //    // Create header paragraph with h2 left and h4 right on same line
        //    var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
        //    headerTable.SetBorder(Border.NO_BORDER);

        //    headerTable.AddCell(new Cell()
        //        .Add(new Paragraph(request.HeadingLeft).SetBold())
        //        .SetBorder(Border.NO_BORDER)
        //        .SetTextAlignment(TextAlignment.LEFT));

        //    headerTable.AddCell(new Cell()
        //        .Add(new Paragraph(request.HeadingRight))
        //        .SetBorder(Border.NO_BORDER)
        //        .SetTextAlignment(TextAlignment.RIGHT));

        //    document.Add(headerTable);
        //    document.Add(new Paragraph("\n"));

        //    int numCols = request.ColumnHeaders.Count;

        //    var table = new Table(numCols).UseAllAvailableWidth();

        //    // Colors for styling
        //    Color headerBg = ColorConstants.LIGHT_GRAY;
        //    Color filterBg = new DeviceRgb(255, 255, 200);
        //    Color totalsBg = new DeviceRgb(220, 220, 220);

        //    // Add first header row - column names
        //    foreach (var header in request.ColumnHeaders)
        //    {
        //        table.AddHeaderCell(new Cell()
        //            .Add(new Paragraph(header).SetBold())
        //            .SetBackgroundColor(headerBg)
        //            .SetTextAlignment(TextAlignment.CENTER));
        //    }

        //    // Add second header row - filters - ALSO use AddHeaderCell to mark as header
        //    foreach (var filterText in request.Filters)
        //    {
        //        table.AddHeaderCell(new Cell()
        //            .Add(new Paragraph(filterText ?? ""))
        //            .SetBackgroundColor(filterBg)
        //            .SetTextAlignment(TextAlignment.CENTER));
        //    }

        //    // Add data rows (centered, word wrapped)
        //    foreach (var row in request.Data)
        //    {
        //        for (int i = 0; i < numCols; i++)
        //        {
        //            var cellText = i < row.Length ? row[i] : "";
        //            table.AddCell(new Cell()
        //                .Add(new Paragraph(cellText)
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetMultipliedLeading(1.2f))
        //                .SetTextAlignment(TextAlignment.CENTER));
        //        }
        //    }

        //    // Add totals row spanning all columns
        //    var totalsText = $"Količina ukupno: {request.TotalQuantity}   |   Ukupno m²: {request.TotalM2:F2}   |   Cijena ukupno: {request.TotalPrice:F2}€";
        //    table.AddCell(new Cell(1, numCols)
        //        .Add(new Paragraph(totalsText).SetBold())
        //        .SetBackgroundColor(totalsBg)
        //        .SetTextAlignment(TextAlignment.CENTER));

        //    // Prevent splitting rows across pages
        //    table.SetKeepTogether(true);

        //    // Add the table
        //    document.Add(table);

        //    document.Close();

        //    return File(ms.ToArray(), "application/pdf", "Detaljne_prodaje/kupovine.pdf");
        //}

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
                string totalsText = $"Količina ukupno: {request.TotalQuantity}   |   Ukupno m²: {request.TotalM2:F2}   |   Cijena ukupno: {request.TotalPrice:F2}€";

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


        //[HttpPost]
        //public IActionResult GenerateAllSalesPDF([FromBody] AllSalesPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4.Rotate());
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(8);

        //        // Heading
        //        Paragraph heading = new Paragraph(request.Heading)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(14)
        //            .SetMarginBottom(15);
        //        document.Add(heading);

        //        int numColumns = request.ColumnHeaders?.Count ?? 0;
        //        float[] columnWidths = new float[numColumns];
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            if (i == 1 || i == 4)
        //                columnWidths[i] = 3f;
        //            else if (i == 6 || i == numColumns - 1)
        //                columnWidths[i] = 2.5f;
        //            else
        //                columnWidths[i] = 1.4f;
        //        }

        //        Table table = new Table(UnitValue.CreatePercentArray(columnWidths)).UseAllAvailableWidth();
        //        table.SetKeepTogether(true);

        //        // Header row
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBold()
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetPadding(5));
        //        }

        //        // Filter row
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = "";
        //            if (i == 0 && !string.IsNullOrWhiteSpace(request.MinDate) && !string.IsNullOrWhiteSpace(request.MaxDate))
        //            {
        //                if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
        //                {
        //                    filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
        //                }
        //            }
        //            else if (request.Filters != null && request.Filters.TryGetValue(i, out string val))
        //            {
        //                filterValue = val;
        //            }

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetPadding(5));
        //        }

        //        // Data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                string cellValue = row.Length > i ? row[i] ?? "" : "";
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(cellValue))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Totals row
        //        string totalsText = $"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2:F2} | Cijena ukupno: {request.TotalPrice:F2}€";
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph(totalsText))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Prodaje.pdf");
        //    }
        //}

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
                string totalsText = $"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2:F2} | Cijena ukupno: {request.TotalPrice:F2}€";
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


        //[HttpPost]
        //public IActionResult GenerateSalesPDF([FromBody] BuysPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4.Rotate());
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(10);
        //        document.Add(heading);


        //        int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            if (i == 0 && !string.IsNullOrEmpty(request.MinDate) && !string.IsNullOrEmpty(request.MaxDate))
        //            {
        //                if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
        //                {
        //                    filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
        //                }
        //            }

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Grupisane_prodaje.pdf");
        //    }
        //}

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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
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

        //[HttpPost]
        //public IActionResult GeneratePerDayPDF([FromBody] PerDayPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A5);
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading1 + request.Heading2)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(10);
        //        document.Add(heading);

        //        int numColumns = request.Data[0].Length;

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Po_danu.pdf");
        //    }
        //}

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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
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


        //[HttpPost]
        //public IActionResult GeneratePerProductsUngroupedPDF([FromBody] PerProductsPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4.Rotate());
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading1)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(5);
        //        document.Add(heading);

        //        // Create header paragraph with h2 left and h4 right on same line
        //        var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
        //        headerTable.SetBorder(Border.NO_BORDER);

        //        headerTable.AddCell(new Cell()
        //            .Add(new Paragraph(request.Heading2).SetBold())
        //            .SetBorder(Border.NO_BORDER)
        //            .SetTextAlignment(TextAlignment.LEFT));

        //        headerTable.AddCell(new Cell()
        //            .Add(new Paragraph(request.Heading3))
        //            .SetBorder(Border.NO_BORDER)
        //            .SetTextAlignment(TextAlignment.RIGHT));

        //        document.Add(headerTable);
        //        document.Add(new Paragraph("\n")); // small gap

        //        int numColumns = request.Data[0].Length;

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Po_proizvodima_(Negrupisano)_" + request.CustomerName + ".pdf");
        //    }
        //}

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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
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


        //[HttpPost]
        //public IActionResult GeneratePerProductsGroupedPDF([FromBody] PerProductsPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4);
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading1)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(5);
        //        document.Add(heading);

        //        // Create header paragraph with h2 left and h4 right on same line
        //        var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
        //        headerTable.SetBorder(Border.NO_BORDER);

        //        headerTable.AddCell(new Cell()
        //            .Add(new Paragraph(request.Heading2).SetBold())
        //            .SetBorder(Border.NO_BORDER)
        //            .SetTextAlignment(TextAlignment.LEFT));

        //        headerTable.AddCell(new Cell()
        //            .Add(new Paragraph(request.Heading3))
        //            .SetBorder(Border.NO_BORDER)
        //            .SetTextAlignment(TextAlignment.RIGHT));

        //        document.Add(headerTable);
        //        document.Add(new Paragraph("\n"));

        //        int numColumns = request.Data[0].Length;

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Po_proizvodima_(Grupisano)_" + request.CustomerName + ".pdf");
        //    }
        //}

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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
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


        //[HttpPost]
        //public IActionResult GenerateDetailsGroupedPdf([FromBody] DetailsGroupedPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4);
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading1 = new Paragraph(request.Heading1)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(5);
        //        document.Add(heading1);

        //        Paragraph heading2 = new Paragraph(request.Heading2 + request.Heading3)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(5);
        //        document.Add(heading2);

        //        var labeleTable = new Table(new float[] { 1f, 2f }).UseAllAvailableWidth();
        //        labeleTable.SetMarginBottom(10);
        //        labeleTable.SetFontSize(8);

        //        void AddRow(string label, string value)
        //        {
        //            labeleTable.AddCell(new Cell().Add(new Paragraph(label).SetBold()).SetBackgroundColor(ColorConstants.CYAN));
        //            labeleTable.AddCell(new Cell().Add(new Paragraph(value ?? "")).SetTextAlignment(TextAlignment.LEFT));
        //        }

        //        // These should match the values from your Model.Labels object
        //        AddRow("Šifra", request.ProductNumber);
        //        AddRow("Ime", request.Name);
        //        AddRow("Veličina", request.Size);
        //        AddRow("M²", request.M2PerProduct?.ToString());

        //        document.Add(labeleTable);

        //        int numColumns = request.Data[0].Length;

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            if (i == 0 && !string.IsNullOrEmpty(request.MinDate))
        //            {
        //                if (DateTime.TryParse(request.MinDate, out var minDate))
        //                {
        //                    filterValue = $"{minDate:dd-MM-yyyy}";
        //                }
        //            }

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Po_proizvodima_detaljno_(Grupisano)_" + request.CustomerName + ".pdf");
        //    }
        //}

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
                var document = new Document(pdf, PageSize.A4);
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

                AddRow("Šifra", request.ProductNumber);
                AddRow("Ime", request.Name);
                AddRow("Veličina", request.Size);
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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
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


        //[HttpPost]
        //public IActionResult GenerateDetailsUngroupedPdf([FromBody] DetailsUngroupedPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4);
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading1 = new Paragraph(request.Heading1)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(5);
        //        document.Add(heading1);

        //        Paragraph heading2 = new Paragraph(request.Heading2 + request.Heading3)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(5);
        //        document.Add(heading2);

        //        // Add "labele" table before main data table
        //        var labeleTable = new Table(new float[] { 1f, 2f }).UseAllAvailableWidth();
        //        labeleTable.SetMarginBottom(10);
        //        labeleTable.SetFontSize(8);

        //        void AddRow(string label, string value)
        //        {
        //            labeleTable.AddCell(new Cell().Add(new Paragraph(label).SetBold()).SetBackgroundColor(ColorConstants.CYAN));
        //            labeleTable.AddCell(new Cell().Add(new Paragraph(value ?? "")).SetTextAlignment(TextAlignment.LEFT));
        //        }

        //        // These should match the values from your Model.Labels object
        //        AddRow("Proizvod ID", request.ProductId);
        //        AddRow("Šifra", request.ProductNumber);
        //        AddRow("Ime", request.Name);
        //        AddRow("Model", request.Model);
        //        AddRow("Boja", request.Color);
        //        AddRow("Veličina", request.Size);
        //        AddRow("M²", request.M2PerProduct?.ToString());

        //        document.Add(labeleTable);

        //        int numColumns = request.Data[0].Length;

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            if (i == 0 && !string.IsNullOrEmpty(request.MinDate))
        //            {
        //                if (DateTime.TryParse(request.MinDate, out var minDate))
        //                {
        //                    filterValue = $"{minDate:dd-MM-yyyy}";
        //                }
        //            }

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Po_proizvodima_detaljno_(Negrupisano)_" + request.CustomerName + ".pdf");
        //    }
        //}

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

                AddRow("Proizvod ID", request.ProductId);
                AddRow("Šifra", request.ProductNumber);
                AddRow("Ime", request.Name);
                AddRow("Model", request.Model);
                AddRow("Boja", request.Color);
                AddRow("Veličina", request.Size);
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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
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


        //[HttpPost]
        //public IActionResult GenerateInventoryPDF([FromBody] InventoryPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A4.Rotate());
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(10);
        //        document.Add(heading);

        //        int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2}"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Inventar.pdf");
        //    }
        //}

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
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2}"))
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

        //[HttpPost]
        //public IActionResult GeneratePaymentHistoryPDF([FromBody] PaymentPdfRequest request)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        PdfWriter writer = new PdfWriter(ms);
        //        PdfDocument pdf = new PdfDocument(writer);
        //        Document document = new Document(pdf, PageSize.A5);
        //        document.SetMargins(20, 20, 20, 20);

        //        string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
        //        PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        //        document.SetFont(font);
        //        document.SetFontSize(10);

        //        // Add heading centered at the top
        //        Paragraph heading = new Paragraph(request.Heading)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetFontSize(12)
        //            .SetMarginBottom(10);
        //        document.Add(heading);

        //        int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

        //        Table table = new Table(numColumns).UseAllAvailableWidth();

        //        // Add first header row: column names
        //        foreach (var header in request.ColumnHeaders)
        //        {
        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(header))
        //                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add second header row: filters (text only, no inputs)
        //        for (int i = 0; i < numColumns; i++)
        //        {
        //            string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

        //            if (i == 1 && !string.IsNullOrEmpty(request.MinDate) && !string.IsNullOrEmpty(request.MaxDate))
        //            {
        //                if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
        //                {
        //                    filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
        //                }
        //            }

        //            table.AddHeaderCell(new Cell()
        //                .Add(new Paragraph(filterValue))
        //                .SetBackgroundColor(ColorConstants.YELLOW)
        //                .SetTextAlignment(TextAlignment.CENTER)
        //                .SetBold());
        //        }

        //        // Add data rows
        //        foreach (var row in request.Data)
        //        {
        //            for (int i = 0; i < numColumns; i++)
        //            {
        //                // Word wrap will happen automatically in Paragraph
        //                table.AddCell(new Cell()
        //                    .Add(new Paragraph(row[i] ?? ""))
        //                    .SetTextAlignment(TextAlignment.CENTER)
        //                    .SetPadding(5));
        //            }
        //        }

        //        // Add totals row spanning all columns
        //        table.AddCell(new Cell(1, numColumns)
        //            .Add(new Paragraph($"Ukupno: {request.TotalPrice}€"))
        //            .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
        //            .SetTextAlignment(TextAlignment.CENTER)
        //            .SetBold()
        //            .SetPadding(5));

        //        // Add the table to the document
        //        document.Add(table);
        //        document.Close();

        //        return File(ms.ToArray(), "application/pdf", "Istorija_plaćanja.pdf");
        //    }
        //}
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
                    .Add(new Paragraph($"Ukupno: {request.TotalPrice}€"))
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

        public FileContentResult /*byte[]*/ Faktura(string custName, string vrijemeProdaje, List<SaleDetailsViewModel> model)
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
                                      group p by new { p.Name, p.Length, p.Width, p.M2PerUnit, p.ProductNumber, p.Price, p.Seller } into g
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
                                          Seller = g.Key.Seller
                                      };

                document.Add(new Paragraph($"Prodavac: {model[0].Seller}").SetMarginBottom(1));
                document.Add(new Paragraph($"Kupac: {custName.ToUpper().Trim()}").SetMarginBottom(1));
                document.Add(new Paragraph($"Datum: {vrijemeProdaje}").SetMarginBottom(1));
                document.Add(new Paragraph("\n"));


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

                    decimal? totalPrice = 0;
                    decimal? totalM2 = 0;
                    int totalQuantity = 0;

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
            return new Cell()
                .Add(new iText.Layout.Element.Paragraph(text))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                    .SetPadding(1)
                    .SetHeight(10)
                    .SetBold();
        }
    }
}