using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Image;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Inventar.Models;
using iText.Layout.Properties;
using iText.Layout.Borders;
using Inventar.ViewModels;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using Inventar.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Inventar.Migrations;
using Inventar.ViewModels.Shared;
using Inventar.ViewModels.Inventory;
using iText.Kernel.Geom;
using System.Drawing.Printing;
using Inventar.ViewModels.Buyer.DTO;
using System.Globalization;
using Inventar.ViewModels.Sales.DTO;
using Inventar.ViewModels.Inventory.DTO;
using Inventar.Utils;
using iText.Kernel.Events;
using iText.IO.Font;
using static iText.Kernel.Font.PdfFontFactory;

namespace Inventar.Controllers
{
    public class PdfController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PdfController(HttpClient httpClient, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _httpClient = httpClient;
            this._context = context;
            this._env = env;
        }

        [HttpGet("generate-cloudinary-image-pdf")]
        public async Task<IActionResult> GenerateCloudinaryImagePdf(int id)
        {
            var tepih = await _context.Tepisi.FindAsync(id);
            if (tepih == null)
                return NotFound();

            var cloudinaryImageUrl = tepih.QRCodeUrl;

            // Download image
            byte[] imageBytes;
            using (var response = await _httpClient.GetAsync(cloudinaryImageUrl))
            {
                if (!response.IsSuccessStatusCode)
                    return BadRequest("Could not retrieve the image from Cloudinary");

                imageBytes = await response.Content.ReadAsByteArrayAsync();
            }

            // Generate PDF
            using (var memoryStream = new MemoryStream())
            {
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                document.Add(new Paragraph()
                    .Add(new Text(tepih.Name)).Add(new Text("/"))
                    .Add(new Text(tepih.Model)).Add(new Text("/"))
                    .Add(new Text(tepih.Length?.ToString() ?? "")).Add(new Text("/"))
                    .Add(new Text(tepih.Width?.ToString() ?? "")).Add(new Text("/"))
                    .Add(new Text(tepih.Color))
                    .SetFontSize(12).SetMarginBottom(5).SetBold().SetTextAlignment(TextAlignment.CENTER));

                var img = ImageDataFactory.Create(imageBytes);
                var image = new Image(img)
                    .ScaleToFit(200, 200)
                    .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);

                document.Add(image);
                document.Close();

                byte[] pdfBytes = memoryStream.ToArray();
                return File(pdfBytes, "application/pdf", $"CloudinaryImage_{tepih.Id}.pdf");
            }
        }

        public async Task<IActionResult> ExportBuyerActivity(int buyerId, DateTime? startDate, DateTime? endDate)
        {
            var buyer = await _context.Kupci.FirstOrDefaultAsync(k => k.Id == buyerId);
            if (buyer == null) return NotFound();

            var paymentsQuery = _context.Placanja
                .Where(p => p.CustomerName == buyer.CustomerFullName);

            var salesQuery = _context.Prodaje
                .Include(p => p.Tepih)
                .Where(p => p.CustomerFullName == buyer.CustomerFullName);

            if (startDate.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentTime >= startDate.Value);
                salesQuery = salesQuery.Where(p => p.VrijemeProdaje >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentTime <= endDate.Value);
                salesQuery = salesQuery.Where(p => p.VrijemeProdaje <= endDate.Value);
            }

            var payments = await paymentsQuery.ToListAsync();
            var sales = await salesQuery.ToListAsync();

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

            var activities = groupedSales
                .Concat(paymentItems)
                .OrderBy(a => a.ActivityTime)
                .ToList();

            var totalSales = groupedSales.Sum(s => s.Amount);
            var totalPayments = paymentItems.Sum(p => p.Amount);
            var totalDebt = totalSales - totalPayments;

            // Generate PDF with iText7
            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            var header = new Paragraph(buyer.CustomerFullName)
                .SetFont(boldFont)
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
                table.AddHeaderCell("#").SetBackgroundColor(ColorConstants.BLUE);
                table.AddHeaderCell("Vrijeme").SetBackgroundColor(ColorConstants.BLUE);
                table.AddHeaderCell("Tip").SetBackgroundColor(ColorConstants.BLUE);
                table.AddHeaderCell("Iznos").SetBackgroundColor(ColorConstants.BLUE);
                table.AddHeaderCell("Prodavac/tip placanja").SetBackgroundColor(ColorConstants.BLUE);

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

            var totalParagraph = new Paragraph($"Dug: {Math.Round(totalDebt, 2)}€")
                .SetFont(boldFont)
                .SetFontSize(12)
                .SetMarginTop(20);
            document.Add(totalParagraph);
            document.Close();

            return File(stream.ToArray(), "application/pdf", $"Aktivnost_kupca_{buyer.CustomerFullName}_{DateTime.Now:yyyyMMddHHmm}.pdf");
        }

        [HttpPost]
        public IActionResult GenerateBuysPDF([FromBody] BuysPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf"); // ili Arial.ttf
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    if (i == 0 && !string.IsNullOrEmpty(request.MinDate) && !string.IsNullOrEmpty(request.MaxDate))
                    {
                        if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                        {
                            filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                        }
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Kupovine.pdf");
            }
        }


        [HttpPost]
        public IActionResult GenerateDetailsPDF([FromBody] DetailsPdfRequest request)
        {
            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, PageSize.A4.Rotate());
            document.SetMargins(20, 20, 20, 20);

            string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
            PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
            document.SetFont(font).SetFontSize(10);

            // Create header paragraph with h2 left and h4 right on same line
            var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
            headerTable.SetBorder(Border.NO_BORDER);

            headerTable.AddCell(new Cell()
                .Add(new Paragraph(request.HeadingLeft).SetBold())
                .SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.LEFT));

            headerTable.AddCell(new Cell()
                .Add(new Paragraph(request.HeadingRight))
                .SetBorder(Border.NO_BORDER)
                .SetTextAlignment(TextAlignment.RIGHT));

            document.Add(headerTable);
            document.Add(new Paragraph("\n"));

            int numCols = request.ColumnHeaders.Count;

            var table = new Table(numCols).UseAllAvailableWidth();

            // Colors for styling
            Color headerBg = ColorConstants.LIGHT_GRAY;
            Color filterBg = new DeviceRgb(255, 255, 200);
            Color totalsBg = new DeviceRgb(220, 220, 220);

            // Add first header row - column names
            foreach (var header in request.ColumnHeaders)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(header).SetBold())
                    .SetBackgroundColor(headerBg)
                    .SetTextAlignment(TextAlignment.CENTER));
            }

            // Add second header row - filters - ALSO use AddHeaderCell to mark as header
            foreach (var filterText in request.Filters)
            {
                table.AddHeaderCell(new Cell()
                    .Add(new Paragraph(filterText ?? ""))
                    .SetBackgroundColor(filterBg)
                    .SetTextAlignment(TextAlignment.CENTER));
            }

            // Add data rows (centered, word wrapped)
            foreach (var row in request.Data)
            {
                for (int i = 0; i < numCols; i++)
                {
                    var cellText = i < row.Length ? row[i] : "";
                    table.AddCell(new Cell()
                        .Add(new Paragraph(cellText)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetMultipliedLeading(1.2f))
                        .SetTextAlignment(TextAlignment.CENTER));
                }
            }

            // Add totals row spanning all columns
            var totalsText = $"Količina ukupno: {request.TotalQuantity}   |   Ukupno m²: {request.TotalM2:F2}   |   Cijena ukupno: {request.TotalPrice:F2}€";
            table.AddCell(new Cell(1, numCols)
                .Add(new Paragraph(totalsText).SetBold())
                .SetBackgroundColor(totalsBg)
                .SetTextAlignment(TextAlignment.CENTER));

            // Prevent splitting rows across pages
            table.SetKeepTogether(true);

            // Add the table
            document.Add(table);

            document.Close();

            return File(ms.ToArray(), "application/pdf", "Detaljne_prodaje/kupovine.pdf");
        }

        [HttpPost]
        public IActionResult GenerateAllSalesPDF([FromBody] AllSalesPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(8);

                // Heading
                Paragraph heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(14)
                    .SetMarginBottom(15);
                document.Add(heading);

                int numColumns = request.ColumnHeaders?.Count ?? 0;
                float[] columnWidths = new float[numColumns];
                for (int i = 0; i < numColumns; i++)
                {
                    if (i == 1 || i == 4)
                        columnWidths[i] = 3f;
                    else if (i == 6 || i == numColumns - 1)
                        columnWidths[i] = 2.5f;
                    else
                        columnWidths[i] = 1.4f;
                }

                Table table = new Table(UnitValue.CreatePercentArray(columnWidths)).UseAllAvailableWidth();
                table.SetKeepTogether(true);

                // Header row
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBold()
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetPadding(5));
                }

                // Filter row
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = "";
                    if (i == 0 && !string.IsNullOrWhiteSpace(request.MinDate) && !string.IsNullOrWhiteSpace(request.MaxDate))
                    {
                        if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                        {
                            filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                        }
                    }
                    else if (request.Filters != null && request.Filters.TryGetValue(i, out string val))
                    {
                        filterValue = val;
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetPadding(5));
                }

                // Data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        string cellValue = row.Length > i ? row[i] ?? "" : "";
                        table.AddCell(new Cell()
                            .Add(new Paragraph(cellValue))
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
        }

        [HttpPost]
        public IActionResult GenerateSalesPDF([FromBody] BuysPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);


                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    if (i == 0 && !string.IsNullOrEmpty(request.MinDate) && !string.IsNullOrEmpty(request.MaxDate))
                    {
                        if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                        {
                            filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                        }
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Grupisane_prodaje.pdf");
            }
        }

        [HttpPost]
        public IActionResult GeneratePerDayPDF([FromBody] PerDayPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A5);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading1 + request.Heading2)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data[0].Length;

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Po_danu.pdf");
            }
        }

        [HttpPost]
        public IActionResult GeneratePerProductsUngroupedPDF([FromBody] PerProductsPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading1)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading);

                // Create header paragraph with h2 left and h4 right on same line
                var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                headerTable.SetBorder(Border.NO_BORDER);

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph(request.Heading2).SetBold())
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.LEFT));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph(request.Heading3))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                document.Add(headerTable);
                document.Add(new Paragraph("\n")); // small gap

                int numColumns = request.Data[0].Length;

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Po_proizvodima_(Negrupisano)_" + request.CustomerName + ".pdf");
            }
        }

        [HttpPost]
        public IActionResult GeneratePerProductsGroupedPDF([FromBody] PerProductsPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading1)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading);

                // Create header paragraph with h2 left and h4 right on same line
                var headerTable = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                headerTable.SetBorder(Border.NO_BORDER);

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph(request.Heading2).SetBold())
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.LEFT));

                headerTable.AddCell(new Cell()
                    .Add(new Paragraph(request.Heading3))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                int numColumns = request.Data[0].Length;

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Po_proizvodima_(Grupisano)_" + request.CustomerName + ".pdf");
            }
        }

        [HttpPost]
        public IActionResult GenerateDetailsGroupedPdf([FromBody] DetailsGroupedPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading1 = new Paragraph(request.Heading1)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading1);

                Paragraph heading2 = new Paragraph(request.Heading2 + request.Heading3)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading2);

                var labeleTable = new Table(new float[] { 1f, 2f }).UseAllAvailableWidth();
                labeleTable.SetMarginBottom(10);
                labeleTable.SetFontSize(8);

                void AddRow(string label, string value)
                {
                    labeleTable.AddCell(new Cell().Add(new Paragraph(label).SetBold()).SetBackgroundColor(ColorConstants.CYAN));
                    labeleTable.AddCell(new Cell().Add(new Paragraph(value ?? "")).SetTextAlignment(TextAlignment.LEFT));
                }

                // These should match the values from your Model.Labels object
                AddRow("Šifra", request.ProductNumber);
                AddRow("Ime", request.Name);
                AddRow("Veličina", request.Size);
                AddRow("M²", request.M2PerProduct?.ToString());

                document.Add(labeleTable);

                int numColumns = request.Data[0].Length;

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    if (i == 0 && !string.IsNullOrEmpty(request.MinDate))
                    {
                        if (DateTime.TryParse(request.MinDate, out var minDate))
                        {
                            filterValue = $"{minDate:dd-MM-yyyy}";
                        }
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Po_proizvodima_detaljno_(Grupisano)_" + request.CustomerName + ".pdf");
            }
        }

        [HttpPost]
        public IActionResult GenerateDetailsUngroupedPdf([FromBody] DetailsUngroupedPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading1 = new Paragraph(request.Heading1)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading1);

                Paragraph heading2 = new Paragraph(request.Heading2 + request.Heading3)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(5);
                document.Add(heading2);

                // Add "labele" table before main data table
                var labeleTable = new Table(new float[] { 1f, 2f }).UseAllAvailableWidth();
                labeleTable.SetMarginBottom(10);
                labeleTable.SetFontSize(8);

                void AddRow(string label, string value)
                {
                    labeleTable.AddCell(new Cell().Add(new Paragraph(label).SetBold()).SetBackgroundColor(ColorConstants.CYAN));
                    labeleTable.AddCell(new Cell().Add(new Paragraph(value ?? "")).SetTextAlignment(TextAlignment.LEFT));
                }

                // These should match the values from your Model.Labels object
                AddRow("Proizvod ID", request.ProductId); 
                AddRow("Šifra", request.ProductNumber);
                AddRow("Ime", request.Name);
                AddRow("Model", request.Model);
                AddRow("Boja", request.Color);
                AddRow("Veličina", request.Size);
                AddRow("M²", request.M2PerProduct?.ToString());

                document.Add(labeleTable);

                int numColumns = request.Data[0].Length;

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    if (i == 0 && !string.IsNullOrEmpty(request.MinDate))
                    {
                        if (DateTime.TryParse(request.MinDate, out var minDate))
                        {
                            filterValue = $"{minDate:dd-MM-yyyy}";
                        }
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2} | Cijena ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Po_proizvodima_detaljno_(Negrupisano)_" + request.CustomerName + ".pdf");
            }
        }

        [HttpPost]
        public IActionResult GenerateInventoryPDF([FromBody] InventoryPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Količina ukupno: {request.TotalQuantity} | Ukupno m²: {request.TotalM2}"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Inventar.pdf");
            }
        }

        [HttpPost]
        public IActionResult GeneratePaymentHistoryPDF([FromBody] PaymentPdfRequest request)
        {
            using (var ms = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(ms);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf, PageSize.A5);
                document.SetMargins(20, 20, 20, 20);

                string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
                document.SetFont(font);
                document.SetFontSize(10);

                // Add heading centered at the top
                Paragraph heading = new Paragraph(request.Heading)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(12)
                    .SetMarginBottom(10);
                document.Add(heading);

                int numColumns = request.Data.Count > 0 ? request.Data[0].Length - 1 : (request.ColumnHeaders?.Count ?? 0);

                Table table = new Table(numColumns).UseAllAvailableWidth();

                // Add first header row: column names
                foreach (var header in request.ColumnHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add second header row: filters (text only, no inputs)
                for (int i = 0; i < numColumns; i++)
                {
                    string filterValue = request.Filters != null && request.Filters.ContainsKey(i) ? request.Filters[i] : "";

                    if (i == 1 && !string.IsNullOrEmpty(request.MinDate) && !string.IsNullOrEmpty(request.MaxDate))
                    {
                        if (DateTime.TryParse(request.MinDate, out var minDate) && DateTime.TryParse(request.MaxDate, out var maxDate))
                        {
                            filterValue = $"{minDate:dd-MM-yyyy} - {maxDate:dd-MM-yyyy}";
                        }
                    }

                    table.AddHeaderCell(new Cell()
                        .Add(new Paragraph(filterValue))
                        .SetBackgroundColor(ColorConstants.YELLOW)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBold());
                }

                // Add data rows
                foreach (var row in request.Data)
                {
                    for (int i = 0; i < numColumns; i++)
                    {
                        // Word wrap will happen automatically in Paragraph
                        table.AddCell(new Cell()
                            .Add(new Paragraph(row[i] ?? ""))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetPadding(5));
                    }
                }

                // Add totals row spanning all columns
                table.AddCell(new Cell(1, numColumns)
                    .Add(new Paragraph($"Ukupno: {request.TotalPrice}€"))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetPadding(5));

                // Add the table to the document
                document.Add(table);
                document.Close();

                return File(ms.ToArray(), "application/pdf", "Istorija_plaćanja.pdf");
            }
        }

    }
}