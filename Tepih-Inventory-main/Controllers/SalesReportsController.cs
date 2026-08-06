using ClosedXML.Excel;
using Inventar.Data;
using Inventar.Utils;
using Inventar.ViewModels.SalesReports;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout.Layout;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Renderer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Path = System.IO.Path;
using static iText.Kernel.Font.PdfFontFactory;

namespace Inventar.Controllers
{
    [Authorize(Roles = "admin,superadmin")]
    public class SalesReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SalesReportsController> _logger;

        public SalesReportsController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            ILogger<SalesReportsController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ByCode(DateTime? startDate, DateTime? endDate)
        {
            var groups = await BuildCodeReportGroupsAsync(startDate: startDate, endDate: endDate);

            return View(new SalesPrimaryReportPageViewModel
            {
                Groups = groups,
                Options = groups
                    .Select(group => group.KeyLabel)
                    .OrderBy(value => value)
                    .ToList(),
                StartDate = startDate,
                EndDate = endDate
            });
        }

        [HttpGet]
        public async Task<IActionResult> BySize(DateTime? startDate, DateTime? endDate)
        {
            var groups = await BuildSizeReportGroupsAsync(startDate: startDate, endDate: endDate);

            return View(new SalesPrimaryReportPageViewModel
            {
                Groups = groups,
                Options = groups
                    .Select(group => group.KeyLabel)
                    .OrderBy(value => value)
                    .ToList(),
                StartDate = startDate,
                EndDate = endDate
            });
        }

        [HttpGet]
        public async Task<IActionResult> ByColor(DateTime? startDate, DateTime? endDate)
        {
            var groups = await BuildColorReportGroupsAsync(startDate: startDate, endDate: endDate);

            return View(new SalesColorReportPageViewModel
            {
                Groups = groups,
                ColorOptions = groups
                    .Select(group => group.Color)
                    .OrderBy(value => value)
                    .ToList(),
                StartDate = startDate,
                EndDate = endDate
            });
        }

        [HttpPost]
        public async Task<IActionResult> ExportCodeReportPdf([FromBody] SalesPrimaryReportExportRequest request)
        {
            if (request == null || request.Keys.Count == 0)
            {
                return BadRequest("No product numbers were provided for PDF export.");
            }

            try
            {
                var groups = await BuildCodeReportGroupsAsync(request.Keys, request.StartDate, request.EndDate);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = BuildHeadingWithPeriod(
                    @Inventar.Resources.Resource.ReportByProductNumber,
                    request.StartDate,
                    request.EndDate);
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_sifri.pdf"
                    : "Izvjestaj_po_sifri.pdf";

                return File(GeneratePrimaryReportPdf(groups, heading, @Inventar.Resources.Resource.ProductNumber), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales code report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportCodeReportExcel([FromBody] SalesPrimaryReportExportRequest request)
        {
            if (request == null || request.Keys.Count == 0)
            {
                return BadRequest("No product numbers were provided for Excel export.");
            }

            try
            {
                var groups = await BuildCodeReportGroupsAsync(request.Keys, request.StartDate, request.EndDate);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = BuildHeadingWithPeriod(
                    @Inventar.Resources.Resource.ReportByProductNumber,
                    request.StartDate,
                    request.EndDate);
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_sifri.xlsx"
                    : "Izvjestaj_po_sifri.xlsx";

                return File(
                    GeneratePrimaryReportExcel(groups, heading, @Inventar.Resources.Resource.ProductNumber, "Izvjestaj po sifri"),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales code report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportSizeReportPdf([FromBody] SalesPrimaryReportExportRequest request)
        {
            if (request == null || request.Keys.Count == 0)
            {
                return BadRequest("No sizes were provided for PDF export.");
            }

            try
            {
                var groups = await BuildSizeReportGroupsAsync(request.Keys, request.StartDate, request.EndDate);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = BuildHeadingWithPeriod(
                    @Inventar.Resources.Resource.ReportBySize,
                    request.StartDate,
                    request.EndDate);
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_velicini_prodaje.pdf"
                    : "Izvjestaj_po_velicini_prodaje.pdf";

                return File(GeneratePrimaryReportPdf(groups, heading, @Inventar.Resources.Resource.Size), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales size report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportSizeReportExcel([FromBody] SalesPrimaryReportExportRequest request)
        {
            if (request == null || request.Keys.Count == 0)
            {
                return BadRequest("No sizes were provided for Excel export.");
            }

            try
            {
                var groups = await BuildSizeReportGroupsAsync(request.Keys, request.StartDate, request.EndDate);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = BuildHeadingWithPeriod(
                    @Inventar.Resources.Resource.ReportBySize,
                    request.StartDate,
                    request.EndDate);
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_velicini_prodaje.xlsx"
                    : "Izvjestaj_po_velicini_prodaje.xlsx";

                return File(
                    GeneratePrimaryReportExcel(groups, heading, @Inventar.Resources.Resource.Size, "Izvjestaj po velicini"),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales size report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportColorReportPdf([FromBody] SalesColorReportExportRequest request)
        {
            if (request == null || request.Colors.Count == 0)
            {
                return BadRequest("No colors were provided for PDF export.");
            }

            try
            {
                var groups = await BuildColorReportGroupsAsync(request.Colors, request.StartDate, request.EndDate);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = BuildHeadingWithPeriod(
                    @Inventar.Resources.Resource.ReportByColor,
                    request.StartDate,
                    request.EndDate);
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_boji_prodaje.pdf"
                    : "Izvjestaj_po_boji_prodaje.pdf";

                return File(GenerateColorReportPdf(groups, heading), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales color report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportColorReportExcel([FromBody] SalesColorReportExportRequest request)
        {
            if (request == null || request.Colors.Count == 0)
            {
                return BadRequest("No colors were provided for Excel export.");
            }

            try
            {
                var groups = await BuildColorReportGroupsAsync(request.Colors, request.StartDate, request.EndDate);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = BuildHeadingWithPeriod(
                    @Inventar.Resources.Resource.ReportByColor,
                    request.StartDate,
                    request.EndDate);
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_boji_prodaje.xlsx"
                    : "Izvjestaj_po_boji_prodaje.xlsx";

                return File(
                    GenerateColorReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales color report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        private async Task<List<SalesPrimaryReportGroupViewModel>> BuildCodeReportGroupsAsync(
            IReadOnlyCollection<string>? selectedProductNumbers = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var rows = await GetActiveSaleRowsAsync(startDate, endDate);
            var selected = NormalizeSelection(selectedProductNumbers);

            return rows
                .GroupBy(item => BuildProductNumberLabel(item.ProductNumber), StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new SalesPrimaryReportGroupViewModel
                {
                    KeyLabel = group.Key,
                    ProductRows = group
                        .GroupBy(item => item.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(nameGroup => nameGroup.Key)
                        .Select(nameGroup => new SalesProductSummaryViewModel
                        {
                            ProductName = nameGroup.Key,
                            TotalM2 = Math.Round(nameGroup.Sum(CalculateItemM2Total), 2),
                            TotalQuantity = nameGroup.Sum(item => item.Quantity)
                        })
                        .ToList(),
                    TotalM2 = Math.Round(group.Sum(CalculateItemM2Total), 2),
                    TotalQuantity = group.Sum(item => item.Quantity)
                })
                .ToList();
        }

        private async Task<List<SalesPrimaryReportGroupViewModel>> BuildSizeReportGroupsAsync(
            IReadOnlyCollection<string>? selectedSizes = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var rows = await GetActiveSaleRowsAsync(startDate, endDate);
            var selected = NormalizeSelection(selectedSizes);

            return rows
                .GroupBy(item => BuildSizeLabel(item.Width, item.Length), StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new SalesPrimaryReportGroupViewModel
                {
                    KeyLabel = group.Key,
                    ProductRows = group
                        .GroupBy(item => item.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(nameGroup => nameGroup.Key)
                        .Select(nameGroup => new SalesProductSummaryViewModel
                        {
                            ProductName = nameGroup.Key,
                            TotalM2 = Math.Round(nameGroup.Sum(CalculateItemM2Total), 2),
                            TotalQuantity = nameGroup.Sum(item => item.Quantity)
                        })
                        .ToList(),
                    TotalM2 = Math.Round(group.Sum(CalculateItemM2Total), 2),
                    TotalQuantity = group.Sum(item => item.Quantity)
                })
                .ToList();
        }

        private async Task<List<SalesColorReportGroupViewModel>> BuildColorReportGroupsAsync(
            IReadOnlyCollection<string>? selectedColors = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var rows = await GetActiveSaleRowsAsync(startDate, endDate);
            var selected = NormalizeSelection(selectedColors);

            return rows
                .GroupBy(item => BuildColorLabel(item.Color), StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new SalesColorReportGroupViewModel
                {
                    Color = group.Key,
                    ProductGroups = group
                        .GroupBy(item => item.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(nameGroup => nameGroup.Key)
                        .Select(nameGroup => new SalesColorReportProductGroupViewModel
                        {
                            ProductName = nameGroup.Key,
                            SizeRows = nameGroup
                                .GroupBy(item => new { item.Width, item.Length })
                                .OrderBy(sizeGroup => sizeGroup.Key.Width ?? int.MaxValue)
                                .ThenBy(sizeGroup => sizeGroup.Key.Length ?? int.MaxValue)
                                .Select(sizeGroup => new SalesColorReportSizeSummaryViewModel
                                {
                                    SizeLabel = BuildSizeLabel(sizeGroup.Key.Width, sizeGroup.Key.Length),
                                    TotalM2 = Math.Round(sizeGroup.Sum(CalculateItemM2Total), 2),
                                    TotalQuantity = sizeGroup.Sum(item => item.Quantity)
                                })
                                .ToList(),
                            TotalM2 = Math.Round(nameGroup.Sum(CalculateItemM2Total), 2),
                            TotalQuantity = nameGroup.Sum(item => item.Quantity)
                        })
                        .ToList(),
                    TotalM2 = Math.Round(group.Sum(CalculateItemM2Total), 2),
                    TotalQuantity = group.Sum(item => item.Quantity)
                })
                .ToList();
        }

        private async Task<List<SaleReportRow>> GetActiveSaleRowsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var normalizedStartDate = startDate?.Date;
            var normalizedEndDateExclusive = endDate?.Date.AddDays(1);

            return await (from sale in _context.Prodaje.AsNoTracking()
                          join product in _context.Tepisi.AsNoTracking() on sale.TepihId equals product.Id
                          where !sale.Disabled
                                && !product.Disabled
                                && !string.IsNullOrWhiteSpace(product.Name)
                                && (!normalizedStartDate.HasValue || sale.VrijemeProdaje >= normalizedStartDate.Value)
                                && (!normalizedEndDateExclusive.HasValue || sale.VrijemeProdaje < normalizedEndDateExclusive.Value)
                          select new SaleReportRow
                          {
                              ProductNumber = product.ProductNumber,
                              ProductName = product.Name,
                              Width = sale.CustomWidth ?? product.Width,
                              Length = sale.CustomLength ?? product.Length,
                              Color = product.Color,
                              Quantity = sale.Quantity,
                              PerM2 = product.PerM2
                          })
                .ToListAsync();
        }

        private byte[] GeneratePrimaryReportPdf(
            IReadOnlyList<SalesPrimaryReportGroupViewModel> groups,
            string heading,
            string firstColumnHeader)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            var pageSize = PageSize.A4.Rotate();
            using var document = new Document(pdf, pageSize);
            var pdfFont = GetPdfFont();

            document.SetMargins(20, 20, 20, 20);
            document.SetFont(pdfFont);
            document.SetFontSize(10);
            AddPdfHeading(document, heading);

            if (groups.Count == 0)
            {
                document.Add(new Paragraph("Nema podataka za prikaz."));
                document.Close();
                return ms.ToArray();
            }

            var rows = BuildPrimaryReportPdfRows(groups);
            RenderPaginatedFourColumnReportPdf(
                document,
                pageSize,
                pdfFont,
                heading,
                rows,
                new float[] { 2.5f, 4.3f, 1.6f, 1.6f },
                firstColumnHeader,
                @Inventar.Resources.Resource.Name,
                @Inventar.Resources.Resource.Quantity,
                @Inventar.Resources.Resource.M2Total);

            document.Close();
            return ms.ToArray();
        }

        private byte[] GenerateColorReportPdf(
            IReadOnlyList<SalesColorReportGroupViewModel> groups,
            string heading)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            var pageSize = PageSize.A4.Rotate();
            using var document = new Document(pdf, pageSize);
            var pdfFont = GetPdfFont();

            document.SetMargins(20, 20, 20, 20);
            document.SetFont(pdfFont);
            document.SetFontSize(10);
            AddPdfHeading(document, heading);

            if (groups.Count == 0)
            {
                document.Add(new Paragraph("Nema podataka za prikaz."));
                document.Close();
                return ms.ToArray();
            }

            var rows = BuildColorReportPdfRows(groups);
            RenderPaginatedFiveColumnReportPdf(
                document,
                pageSize,
                pdfFont,
                heading,
                rows,
                new float[] { 2.2f, 3.8f, 2.0f, 1.5f, 1.5f },
                @Inventar.Resources.Resource.Color,
                @Inventar.Resources.Resource.Name,
                @Inventar.Resources.Resource.Size,
                @Inventar.Resources.Resource.Quantity,
                @Inventar.Resources.Resource.M2Total);

            document.Close();
            return ms.ToArray();
        }

        private byte[] GeneratePrimaryReportExcel(
            IReadOnlyList<SalesPrimaryReportGroupViewModel> groups,
            string heading,
            string firstColumnHeader,
            string sheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            worksheet.Range("A1:D1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:D1"));

            var headerRow = 3;
            SetSheetHeaders(worksheet, headerRow, firstColumnHeader, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Quantity, @Inventar.Resources.Resource.M2Total);
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            var currentRow = headerRow + 1;

            foreach (var group in groups)
            {
                var dataRows = group.ProductRows.Count == 0
                    ? new List<SalesProductSummaryViewModel>
                    {
                        new()
                        {
                            ProductName = "-",
                            TotalM2 = group.TotalM2,
                            TotalQuantity = group.TotalQuantity
                        }
                    }
                    : group.ProductRows;

                var groupStartRow = currentRow;

                foreach (var productRow in dataRows)
                {
                    worksheet.Cell(currentRow, 2).Value = productRow.ProductName;
                    worksheet.Cell(currentRow, 3).Value = productRow.TotalQuantity;
                    worksheet.Cell(currentRow, 4).Value = productRow.TotalM2;
                    currentRow++;
                }

                worksheet.Cell(currentRow, 2).Value = @Inventar.Resources.Resource.Total;
                worksheet.Cell(currentRow, 3).Value = group.TotalQuantity;
                worksheet.Cell(currentRow, 4).Value = group.TotalM2;
                StyleTotalRow(worksheet.Range(currentRow, 2, currentRow, 4));

                worksheet.Range(groupStartRow, 1, currentRow, 1).Merge().Value = group.KeyLabel;
                StyleMergedLabelCell(worksheet.Range(groupStartRow, 1, currentRow, 1));
                currentRow++;
            }

            worksheet.Range(currentRow, 1, currentRow, 2).Merge().Value = @Inventar.Resources.Resource.OverallTotal;
            worksheet.Cell(currentRow, 3).Value = groups.Sum(group => group.TotalQuantity);
            worksheet.Cell(currentRow, 4).Value = groups.Sum(group => group.TotalM2);
            StyleGrandTotalRow(worksheet.Range(currentRow, 1, currentRow, 4));

            FinalizeSimpleWorksheet(worksheet, Math.Max(currentRow, headerRow), 4);
            worksheet.Column(1).Width = 20;
            worksheet.Column(2).Width = 28;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 14;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GenerateColorReportExcel(
            IReadOnlyList<SalesColorReportGroupViewModel> groups,
            string heading)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(@Inventar.Resources.Resource.ReportByColor);

            worksheet.Range("A1:E1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:E1"));

            var headerRow = 3;
            SetSheetHeaders(worksheet, headerRow, @Inventar.Resources.Resource.Color, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Size, @Inventar.Resources.Resource.Quantity, @Inventar.Resources.Resource.M2Total);
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            var currentRow = headerRow + 1;

            foreach (var colorGroup in groups)
            {
                var colorStartRow = currentRow;

                foreach (var productGroup in colorGroup.ProductGroups)
                {
                    var sizeRows = productGroup.SizeRows.Count == 0
                        ? new List<SalesColorReportSizeSummaryViewModel>
                        {
                            new()
                            {
                                SizeLabel = "-",
                                TotalM2 = productGroup.TotalM2,
                                TotalQuantity = productGroup.TotalQuantity
                            }
                        }
                        : productGroup.SizeRows;

                    var productStartRow = currentRow;

                    foreach (var sizeRow in sizeRows)
                    {
                        worksheet.Cell(currentRow, 3).Value = sizeRow.SizeLabel;
                        worksheet.Cell(currentRow, 4).Value = sizeRow.TotalQuantity;
                        worksheet.Cell(currentRow, 5).Value = sizeRow.TotalM2;
                        currentRow++;
                    }

                    worksheet.Cell(currentRow, 3).Value = @Inventar.Resources.Resource.Total;
                    worksheet.Cell(currentRow, 4).Value = productGroup.TotalQuantity;
                    worksheet.Cell(currentRow, 5).Value = productGroup.TotalM2;
                    StyleSubtotalRow(worksheet.Range(currentRow, 3, currentRow, 5));

                    worksheet.Range(productStartRow, 2, currentRow, 2).Merge().Value = productGroup.ProductName;
                    StyleMergedLabelCell(worksheet.Range(productStartRow, 2, currentRow, 2));
                    currentRow++;
                }

                worksheet.Cell(currentRow, 2).Value = @Inventar.Resources.Resource.OverallTotal;
                worksheet.Cell(currentRow, 4).Value = colorGroup.TotalQuantity;
                worksheet.Cell(currentRow, 5).Value = colorGroup.TotalM2;
                StyleTotalRow(worksheet.Range(currentRow, 2, currentRow, 5));

                worksheet.Range(colorStartRow, 1, currentRow, 1).Merge().Value = colorGroup.Color;
                StyleMergedLabelCell(worksheet.Range(colorStartRow, 1, currentRow, 1));
                currentRow++;
            }

            worksheet.Range(currentRow, 1, currentRow, 3).Merge().Value = @Inventar.Resources.Resource.OverallTotal;
            worksheet.Cell(currentRow, 4).Value = groups.Sum(group => group.TotalQuantity);
            worksheet.Cell(currentRow, 5).Value = groups.Sum(group => group.TotalM2);
            StyleGrandTotalRow(worksheet.Range(currentRow, 1, currentRow, 5));

            FinalizeSimpleWorksheet(worksheet, Math.Max(currentRow, headerRow), 5);
            worksheet.Column(1).Width = 18;
            worksheet.Column(2).Width = 24;
            worksheet.Column(3).Width = 18;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 14;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private void AddPrimaryGroupToPdfTable(Table table, SalesPrimaryReportGroupViewModel group)
        {
            var productRows = group.ProductRows.Count == 0
                ? new List<SalesProductSummaryViewModel>
                {
                    new()
                    {
                        ProductName = "-",
                        TotalM2 = group.TotalM2,
                        TotalQuantity = group.TotalQuantity
                    }
                }
                : group.ProductRows;

            foreach (var productRow in productRows)
            {
                table.AddCell(CreatePdfBodyCell(group.KeyLabel));
                table.AddCell(CreatePdfBodyCell(productRow.ProductName));
                table.AddCell(CreatePdfBodyCell(productRow.TotalQuantity.ToString()));
                table.AddCell(CreatePdfBodyCell(productRow.TotalM2.ToString("0.00")));
            }

            table.AddCell(CreatePdfTotalCell(group.KeyLabel));
            table.AddCell(CreatePdfTotalCell(@Inventar.Resources.Resource.Total));
            table.AddCell(CreatePdfTotalCell(group.TotalQuantity.ToString()));
            table.AddCell(CreatePdfTotalCell(group.TotalM2.ToString("0.00")));
        }

        private void AddColorGroupToPdfTable(Table table, SalesColorReportGroupViewModel group)
        {
            foreach (var productGroup in group.ProductGroups)
            {
                var sizeRows = GetSafeColorSizeRows(productGroup);

                foreach (var sizeRow in sizeRows)
                {
                    table.AddCell(CreatePdfBodyCell(group.Color));
                    table.AddCell(CreatePdfBodyCell(productGroup.ProductName));
                    table.AddCell(CreatePdfBodyCell(sizeRow.SizeLabel));
                    table.AddCell(CreatePdfBodyCell(sizeRow.TotalQuantity.ToString()));
                    table.AddCell(CreatePdfBodyCell(sizeRow.TotalM2.ToString("0.00")));
                }

                table.AddCell(CreatePdfSubtotalCell(group.Color));
                table.AddCell(CreatePdfSubtotalCell(productGroup.ProductName));
                table.AddCell(CreatePdfSubtotalCell(@Inventar.Resources.Resource.Total));
                table.AddCell(CreatePdfSubtotalCell(productGroup.TotalQuantity.ToString()));
                table.AddCell(CreatePdfSubtotalCell(productGroup.TotalM2.ToString("0.00")));
            }

            table.AddCell(CreatePdfTotalCell(group.Color));
            table.AddCell(CreatePdfTotalCell(@Inventar.Resources.Resource.Total, 2));
            table.AddCell(CreatePdfTotalCell(group.TotalQuantity.ToString()));
            table.AddCell(CreatePdfTotalCell(group.TotalM2.ToString("0.00")));
        }

        private List<FourColumnPdfRow> BuildPrimaryReportPdfRows(IReadOnlyList<SalesPrimaryReportGroupViewModel> groups)
        {
            var rows = new List<FourColumnPdfRow>();

            foreach (var group in groups)
            {
                var productRows = group.ProductRows.Count == 0
                    ? new List<SalesProductSummaryViewModel>
                    {
                        new()
                        {
                            ProductName = "-",
                            TotalM2 = group.TotalM2,
                            TotalQuantity = group.TotalQuantity
                        }
                    }
                    : group.ProductRows;

                rows.AddRange(productRows.Select(productRow => new FourColumnPdfRow
                {
                    GroupLabel = group.KeyLabel,
                    SecondColumnText = productRow.ProductName,
                    QuantityText = productRow.TotalQuantity.ToString(),
                    M2Text = productRow.TotalM2.ToString("0.00"),
                    Kind = FourColumnPdfRowKind.Data
                }));

                rows.Add(new FourColumnPdfRow
                {
                    GroupLabel = group.KeyLabel,
                    SecondColumnText = @Inventar.Resources.Resource.Total,
                    QuantityText = group.TotalQuantity.ToString(),
                    M2Text = group.TotalM2.ToString("0.00"),
                    Kind = FourColumnPdfRowKind.Total
                });
            }

            rows.Add(new FourColumnPdfRow
            {
                GroupLabel = @Inventar.Resources.Resource.OverallTotal,
                QuantityText = groups.Sum(group => group.TotalQuantity).ToString(),
                M2Text = groups.Sum(group => group.TotalM2).ToString("0.00"),
                Kind = FourColumnPdfRowKind.GrandTotal
            });

            return rows;
        }

        private List<FiveColumnPdfRow> BuildColorReportPdfRows(IReadOnlyList<SalesColorReportGroupViewModel> groups)
        {
            var rows = new List<FiveColumnPdfRow>();

            foreach (var colorGroup in groups)
            {
                foreach (var productGroup in colorGroup.ProductGroups)
                {
                    var sizeRows = GetSafeColorSizeRows(productGroup);

                    rows.AddRange(sizeRows.Select(sizeRow => new FiveColumnPdfRow
                    {
                        FirstColumnText = colorGroup.Color,
                        SecondColumnText = productGroup.ProductName,
                        ThirdColumnText = sizeRow.SizeLabel,
                        QuantityText = sizeRow.TotalQuantity.ToString(),
                        M2Text = sizeRow.TotalM2.ToString("0.00"),
                        Kind = FiveColumnPdfRowKind.Data
                    }));

                    rows.Add(new FiveColumnPdfRow
                    {
                        FirstColumnText = colorGroup.Color,
                        SecondColumnText = productGroup.ProductName,
                        ThirdColumnText = @Inventar.Resources.Resource.Total,
                        QuantityText = productGroup.TotalQuantity.ToString(),
                        M2Text = productGroup.TotalM2.ToString("0.00"),
                        Kind = FiveColumnPdfRowKind.ProductSubtotal
                    });
                }

                rows.Add(new FiveColumnPdfRow
                {
                    FirstColumnText = colorGroup.Color,
                    ThirdColumnText = @Inventar.Resources.Resource.Total,
                    QuantityText = colorGroup.TotalQuantity.ToString(),
                    M2Text = colorGroup.TotalM2.ToString("0.00"),
                    Kind = FiveColumnPdfRowKind.GroupTotal
                });
            }

            rows.Add(new FiveColumnPdfRow
            {
                ThirdColumnText = @Inventar.Resources.Resource.OverallTotal,
                QuantityText = groups.Sum(group => group.TotalQuantity).ToString(),
                M2Text = groups.Sum(group => group.TotalM2).ToString("0.00"),
                Kind = FiveColumnPdfRowKind.GrandTotal
            });

            return rows;
        }

        private void RenderPaginatedFourColumnReportPdf(
            Document document,
            PageSize pageSize,
            PdfFont font,
            string heading,
            IReadOnlyList<FourColumnPdfRow> rows,
            float[] widths,
            params string[] headers)
        {
            var usableWidth = pageSize.GetWidth() - document.GetLeftMargin() - document.GetRightMargin();
            var fullPageHeight = pageSize.GetHeight() - document.GetTopMargin() - document.GetBottomMargin();
            var firstPageHeight = Math.Max(80f, fullPageHeight - MeasureHeadingHeight(document, font, heading, usableWidth, fullPageHeight) - 6f);
            var rowIndex = 0;
            var isFirstPage = true;

            while (rowIndex < rows.Count)
            {
                var availableHeight = isFirstPage ? firstPageHeight : fullPageHeight;
                var rowsToTake = FindMaxFourColumnRowsThatFit(document, font, rows, rowIndex, usableWidth, availableHeight, widths, headers);

                if (rowsToTake <= 0)
                {
                    if (isFirstPage)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                        isFirstPage = false;
                        continue;
                    }

                    rowsToTake = 1;
                }

                document.Add(BuildFourColumnChunkTable(font, rows, rowIndex, rowsToTake, widths, headers));
                rowIndex += rowsToTake;

                if (rowIndex < rows.Count)
                {
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    isFirstPage = false;
                }
            }
        }

        private void RenderPaginatedFiveColumnReportPdf(
            Document document,
            PageSize pageSize,
            PdfFont font,
            string heading,
            IReadOnlyList<FiveColumnPdfRow> rows,
            float[] widths,
            params string[] headers)
        {
            var usableWidth = pageSize.GetWidth() - document.GetLeftMargin() - document.GetRightMargin();
            var fullPageHeight = pageSize.GetHeight() - document.GetTopMargin() - document.GetBottomMargin();
            var firstPageHeight = Math.Max(80f, fullPageHeight - MeasureHeadingHeight(document, font, heading, usableWidth, fullPageHeight) - 6f);
            var rowIndex = 0;
            var isFirstPage = true;

            while (rowIndex < rows.Count)
            {
                var availableHeight = isFirstPage ? firstPageHeight : fullPageHeight;
                var rowsToTake = FindMaxFiveColumnRowsThatFit(document, font, rows, rowIndex, usableWidth, availableHeight, widths, headers);

                if (rowsToTake <= 0)
                {
                    if (isFirstPage)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                        isFirstPage = false;
                        continue;
                    }

                    rowsToTake = 1;
                }

                document.Add(BuildFiveColumnChunkTable(font, rows, rowIndex, rowsToTake, widths, headers));
                rowIndex += rowsToTake;

                if (rowIndex < rows.Count)
                {
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    isFirstPage = false;
                }
            }
        }

        private int FindMaxFourColumnRowsThatFit(
            Document document,
            PdfFont font,
            IReadOnlyList<FourColumnPdfRow> rows,
            int startIndex,
            float usableWidth,
            float availableHeight,
            float[] widths,
            string[] headers)
        {
            var low = 1;
            var high = rows.Count - startIndex;
            var best = 0;

            while (low <= high)
            {
                var mid = (low + high) / 2;
                var table = BuildFourColumnChunkTable(font, rows, startIndex, mid, widths, headers);

                if (DoesTableFit(document, table, usableWidth, availableHeight))
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best;
        }

        private int FindMaxFiveColumnRowsThatFit(
            Document document,
            PdfFont font,
            IReadOnlyList<FiveColumnPdfRow> rows,
            int startIndex,
            float usableWidth,
            float availableHeight,
            float[] widths,
            string[] headers)
        {
            var low = 1;
            var high = rows.Count - startIndex;
            var best = 0;

            while (low <= high)
            {
                var mid = (low + high) / 2;
                var table = BuildFiveColumnChunkTable(font, rows, startIndex, mid, widths, headers);

                if (DoesTableFit(document, table, usableWidth, availableHeight))
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best;
        }

        private Table BuildFourColumnChunkTable(
            PdfFont font,
            IReadOnlyList<FourColumnPdfRow> rows,
            int startIndex,
            int count,
            float[] widths,
            params string[] headers)
        {
            var endIndex = Math.Min(startIndex + count, rows.Count);
            var table = CreatePdfTable(widths, headers)
                .SetFont(font)
                .SetFontSize(10);

            var index = startIndex;
            while (index < endIndex)
            {
                var row = rows[index];
                if (row.Kind == FourColumnPdfRowKind.GrandTotal)
                {
                    table.AddCell(CreatePdfGrandTotalCell(row.GroupLabel, 2));
                    table.AddCell(CreatePdfGrandTotalCell(row.QuantityText));
                    table.AddCell(CreatePdfGrandTotalCell(row.M2Text));
                    index++;
                    continue;
                }

                var rowspan = 1;
                while (index + rowspan < endIndex
                    && rows[index + rowspan].Kind != FourColumnPdfRowKind.GrandTotal
                    && string.Equals(rows[index + rowspan].GroupLabel, row.GroupLabel, StringComparison.OrdinalIgnoreCase))
                {
                    rowspan++;
                }

                table.AddCell(CreatePdfBodyCell(row.GroupLabel, rowspan));

                for (var offset = 0; offset < rowspan; offset++)
                {
                    AddFourColumnChunkRowCells(table, rows[index + offset]);
                }

                index += rowspan;
            }

            return table;
        }

        private void AddFourColumnChunkRowCells(Table table, FourColumnPdfRow row)
        {
            if (row.Kind == FourColumnPdfRowKind.Total)
            {
                table.AddCell(CreatePdfTotalCell(row.SecondColumnText));
                table.AddCell(CreatePdfTotalCell(row.QuantityText));
                table.AddCell(CreatePdfTotalCell(row.M2Text));
                return;
            }

            table.AddCell(CreatePdfBodyCell(row.SecondColumnText));
            table.AddCell(CreatePdfBodyCell(row.QuantityText));
            table.AddCell(CreatePdfBodyCell(row.M2Text));
        }

        private Table BuildFiveColumnChunkTable(
            PdfFont font,
            IReadOnlyList<FiveColumnPdfRow> rows,
            int startIndex,
            int count,
            float[] widths,
            params string[] headers)
        {
            var endIndex = Math.Min(startIndex + count, rows.Count);
            var table = CreatePdfTable(widths, headers)
                .SetFont(font)
                .SetFontSize(10);

            var index = startIndex;
            while (index < endIndex)
            {
                var row = rows[index];
                if (row.Kind == FiveColumnPdfRowKind.GrandTotal)
                {
                    table.AddCell(CreatePdfGrandTotalCell(row.ThirdColumnText, 3));
                    table.AddCell(CreatePdfGrandTotalCell(row.QuantityText));
                    table.AddCell(CreatePdfGrandTotalCell(row.M2Text));
                    index++;
                    continue;
                }

                var colorRowspan = 1;
                while (index + colorRowspan < endIndex
                    && rows[index + colorRowspan].Kind != FiveColumnPdfRowKind.GrandTotal
                    && string.Equals(rows[index + colorRowspan].FirstColumnText, row.FirstColumnText, StringComparison.OrdinalIgnoreCase))
                {
                    colorRowspan++;
                }

                table.AddCell(CreatePdfBodyCell(row.FirstColumnText, colorRowspan));

                var colorBlockEnd = index + colorRowspan;
                var innerIndex = index;
                while (innerIndex < colorBlockEnd)
                {
                    var innerRow = rows[innerIndex];
                    if (innerRow.Kind == FiveColumnPdfRowKind.GroupTotal)
                    {
                        table.AddCell(CreatePdfTotalCell(innerRow.ThirdColumnText, 2));
                        table.AddCell(CreatePdfTotalCell(innerRow.QuantityText));
                        table.AddCell(CreatePdfTotalCell(innerRow.M2Text));
                        innerIndex++;
                        continue;
                    }

                    var productRowspan = 1;
                    while (innerIndex + productRowspan < colorBlockEnd
                        && rows[innerIndex + productRowspan].Kind != FiveColumnPdfRowKind.GroupTotal
                        && string.Equals(rows[innerIndex + productRowspan].SecondColumnText, innerRow.SecondColumnText, StringComparison.OrdinalIgnoreCase))
                    {
                        productRowspan++;
                    }

                    table.AddCell(CreatePdfBodyCell(innerRow.SecondColumnText ?? "-", productRowspan));

                    for (var offset = 0; offset < productRowspan; offset++)
                    {
                        AddFiveColumnChunkRowCells(table, rows[innerIndex + offset]);
                    }

                    innerIndex += productRowspan;
                }

                index = colorBlockEnd;
            }

            return table;
        }

        private void AddFiveColumnChunkRowCells(Table table, FiveColumnPdfRow row)
        {
            if (row.Kind == FiveColumnPdfRowKind.ProductSubtotal)
            {
                table.AddCell(CreatePdfSubtotalCell(row.ThirdColumnText));
                table.AddCell(CreatePdfSubtotalCell(row.QuantityText));
                table.AddCell(CreatePdfSubtotalCell(row.M2Text));
                return;
            }

            table.AddCell(CreatePdfBodyCell(row.ThirdColumnText));
            table.AddCell(CreatePdfBodyCell(row.QuantityText));
            table.AddCell(CreatePdfBodyCell(row.M2Text));
        }

        private bool DoesTableFit(Document document, Table table, float usableWidth, float availableHeight)
        {
            var renderer = table.CreateRendererSubTree();
            renderer.SetParent(new DocumentRenderer(document));

            var layoutResult = renderer.Layout(new LayoutContext(
                new LayoutArea(1, new Rectangle(0, 0, usableWidth, availableHeight))));

            return layoutResult.GetStatus() == LayoutResult.FULL;
        }

        private float MeasureHeadingHeight(Document document, PdfFont font, string heading, float usableWidth, float availableHeight)
        {
            var paragraph = new Paragraph(heading)
                .SetFont(font)
                .SetTextAlignment(TextAlignment.CENTER)
                .SimulateBold()
                .SetFontSize(13)
                .SetMarginBottom(12);

            var renderer = paragraph.CreateRendererSubTree();
            renderer.SetParent(new DocumentRenderer(document));

            var layoutResult = renderer.Layout(new LayoutContext(
                new LayoutArea(1, new Rectangle(0, 0, usableWidth, availableHeight))));

            return layoutResult.GetOccupiedArea()?.GetBBox().GetHeight() ?? 0f;
        }

        private sealed class FourColumnPdfRow
        {
            public string GroupLabel { get; set; } = string.Empty;
            public string SecondColumnText { get; set; } = string.Empty;
            public string QuantityText { get; set; } = string.Empty;
            public string M2Text { get; set; } = string.Empty;
            public FourColumnPdfRowKind Kind { get; set; }
        }

        private enum FourColumnPdfRowKind
        {
            Data,
            Total,
            GrandTotal
        }

        private sealed class FiveColumnPdfRow
        {
            public string FirstColumnText { get; set; } = string.Empty;
            public string? SecondColumnText { get; set; }
            public string ThirdColumnText { get; set; } = string.Empty;
            public string QuantityText { get; set; } = string.Empty;
            public string M2Text { get; set; } = string.Empty;
            public FiveColumnPdfRowKind Kind { get; set; }
        }

        private enum FiveColumnPdfRowKind
        {
            Data,
            ProductSubtotal,
            GroupTotal,
            GrandTotal
        }

        private void AddPdfGrandTotalRow(Table table, string label, int labelColSpan, string primaryValue, string secondaryValue)
        {
            table.AddCell(CreatePdfGrandTotalCell(label, labelColSpan));
            table.AddCell(CreatePdfGrandTotalCell(primaryValue));
            table.AddCell(CreatePdfGrandTotalCell(secondaryValue));
        }

        private static List<SalesColorReportSizeSummaryViewModel> GetSafeColorSizeRows(SalesColorReportProductGroupViewModel productGroup)
        {
            return productGroup.SizeRows.Count == 0
                ? new List<SalesColorReportSizeSummaryViewModel>
                {
                    new()
                    {
                        SizeLabel = "-",
                        TotalM2 = productGroup.TotalM2,
                        TotalQuantity = productGroup.TotalQuantity
                    }
                }
                : productGroup.SizeRows;
        }

        private void AddPdfHeading(Document document, string heading)
        {
            document.Add(new Paragraph(heading)
                .SetTextAlignment(TextAlignment.CENTER)
                .SimulateBold()
                .SetFontSize(13)
                .SetMarginBottom(12));
        }

        private Table CreatePdfTable(float[] widths, params string[] headers)
        {
            var table = new Table(UnitValue.CreatePercentArray(widths))
                .UseAllAvailableWidth()
                .SetKeepTogether(false);

            foreach (var header in headers)
            {
                table.AddHeaderCell(CreatePdfHeaderCell(header));
            }

            return table;
        }

        private Cell CreatePdfHeaderCell(string text)
        {
            return new Cell()
                .Add(new Paragraph(text))
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SimulateBold()
                .SetPadding(5);
        }

        private Cell CreatePdfBodyCell(string text, int rowSpan = 1, int colSpan = 1)
        {
            return new Cell(rowSpan, colSpan)
                .Add(new Paragraph(text))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetPadding(5);
        }

        private Cell CreatePdfTotalCell(string text, int colSpan = 1)
        {
            return new Cell(1, colSpan)
                .Add(new Paragraph(text))
                .SetBackgroundColor(new DeviceRgb(255, 243, 176))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SimulateBold()
                .SetPadding(5);
        }

        private Cell CreatePdfSubtotalCell(string text, int colSpan = 1)
        {
            return new Cell(1, colSpan)
                .Add(new Paragraph(text))
                .SetBackgroundColor(new DeviceRgb(216, 236, 255))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SimulateBold()
                .SetPadding(5);
        }

        private Cell CreatePdfGrandTotalCell(string text, int colSpan = 1)
        {
            return new Cell(1, colSpan)
                .Add(new Paragraph(text))
                .SetBackgroundColor(new DeviceRgb(215, 239, 198))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SimulateBold()
                .SetPadding(5);
        }

        private PdfFont GetPdfFont()
        {
            var fontPath = Path.Combine(_env.WebRootPath, "fonts", "arial.ttf");
            if (!System.IO.File.Exists(fontPath))
            {
                throw new FileNotFoundException("Font file not found for PDF generation.", fontPath);
            }

            return CreateFont(fontPath, PdfEncodings.IDENTITY_H, EmbeddingStrategy.PREFER_EMBEDDED);
        }

        private static void SetSheetHeaders(IXLWorksheet worksheet, int row, params string[] headers)
        {
            for (var index = 0; index < headers.Length; index++)
            {
                worksheet.Cell(row, index + 1).Value = headers[index];
            }

            var headerRange = worksheet.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void StyleSheetHeading(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 14;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void StyleMergedLabelCell(IXLRange range)
        {
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Font.Bold = true;
        }

        private static void StyleTotalRow(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#fff3b0");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void StyleSubtotalRow(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#d8ecff");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void StyleGrandTotalRow(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#d7efc6");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void FinalizeSimpleWorksheet(IXLWorksheet worksheet, int lastRow, int columnCount)
        {
            var usedRange = worksheet.Range(1, 1, lastRow, columnCount);
            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static HashSet<string>? NormalizeSelection(IEnumerable<string>? selectedValues)
        {
            var values = selectedValues?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return values is { Count: > 0 } ? values : null;
        }

        private static bool IsSelected(string value, HashSet<string>? selectedValues)
        {
            return selectedValues == null || selectedValues.Contains(value.Trim());
        }

        private static decimal CalculateItemM2PerUnit(SaleReportRow item)
        {
            return PoMjeriHelper.CalculateM2PerUnit(item.PerM2, item.Width, item.Length) ?? 0m;
        }

        private static decimal CalculateItemM2Total(SaleReportRow item)
        {
            return PoMjeriHelper.CalculateM2Total(item.PerM2, item.Width, item.Length, item.Quantity) ?? 0m;
        }

        private static string BuildSizeLabel(int? width, int? length)
        {
            return width.HasValue && length.HasValue
                ? $"{width.Value}x{length.Value}"
                : "-";
        }

        private static string BuildColorLabel(string? color)
        {
            return string.IsNullOrWhiteSpace(color) ? "-" : color.Trim();
        }

        private static string BuildProductNumberLabel(string? productNumber)
        {
            return string.IsNullOrWhiteSpace(productNumber) ? "-" : productNumber.Trim();
        }

        private static string BuildHeadingWithPeriod(string heading, DateTime? startDate, DateTime? endDate)
        {
            var periodLabel = BuildActivePeriodLabel(startDate, endDate);
            return string.IsNullOrWhiteSpace(periodLabel)
                ? heading
                : $"{heading} ({periodLabel})";
        }

        private static string BuildActivePeriodLabel(DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                return $"{startDate.Value:dd-MM-yyyy} - {endDate.Value:dd-MM-yyyy}";
            }

            if (startDate.HasValue)
            {
                return $"od {startDate.Value:dd-MM-yyyy}";
            }

            if (endDate.HasValue)
            {
                return $"do {endDate.Value:dd-MM-yyyy}";
            }

            return string.Empty;
        }

        private sealed class SaleReportRow
        {
            public string? ProductNumber { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public int? Width { get; set; }
            public int? Length { get; set; }
            public string? Color { get; set; }
            public int Quantity { get; set; }
            public bool PerM2 { get; set; }
        }
    }
}
