using ClosedXML.Excel;
using Inventar.Data;
using Inventar.Models;
using Inventar.Utils;
using Inventar.ViewModels.InventoryReports;
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
    public class InventoryReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<InventoryReportsController> _logger;

        public InventoryReportsController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            ILogger<InventoryReportsController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ByName()
        {
            var groups = await BuildNameReportGroupsAsync();

            return View(new InventoryNameReportPageViewModel
            {
                Groups = groups,
                ProductNameOptions = groups
                    .Select(group => group.ProductName)
                    .OrderBy(name => name)
                    .ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> ByM2()
        {
            var groups = await BuildNameReportGroupsAsync(onlyPerM2: true);

            return View(new InventoryNameReportPageViewModel
            {
                Groups = groups,
                ProductNameOptions = groups
                    .Select(group => group.ProductName)
                    .OrderBy(name => name)
                    .ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> ByPiece()
        {
            var groups = await BuildPieceReportGroupsAsync();

            return View(new InventoryPieceReportPageViewModel
            {
                Groups = groups,
                ProductNameOptions = groups
                    .Select(group => group.ProductName)
                    .OrderBy(name => name)
                    .ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> BySize()
        {
            var groups = await BuildSizeReportGroupsAsync();

            return View(new InventorySizeReportPageViewModel
            {
                Groups = groups,
                SizeOptions = groups
                    .Select(group => group.SizeLabel)
                    .OrderBy(size => size)
                    .ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> ByColor()
        {
            var groups = await BuildColorReportGroupsAsync();

            return View(new InventoryColorReportPageViewModel
            {
                Groups = groups,
                ColorOptions = groups
                    .Select(group => group.Color)
                    .OrderBy(color => color)
                    .ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> ByErrors()
        {
            var groups = await BuildErrorReportGroupsAsync();
            return View(new InventoryErrorReportPageViewModel
            {
                Groups = groups
            });
        }

        [HttpGet]
        public async Task<IActionResult> ByCode()
        {
            var groups = await BuildCodeReportGroupsAsync();

            return View(new InventoryPrimaryReportPageViewModel
            {
                Groups = groups,
                Options = groups
                    .Select(group => group.KeyLabel)
                    .OrderBy(value => value)
                    .ToList()
            });
        }

        [HttpGet]
        public IActionResult ByProductName()
        {
            return RedirectToAction(nameof(ByCode));
        }

        [HttpPost]
        public async Task<IActionResult> ExportCodeReportPdf([FromBody] InventoryPrimaryReportExportRequest request)
        {
            if (request == null || request.Keys.Count == 0)
            {
                return BadRequest("No product numbers were provided for PDF export.");
            }

            try
            {
                var groups = await BuildCodeReportGroupsAsync(request.Keys);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByProductNumber;
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_sifri_inventar.pdf"
                    : "Izvjestaj_po_sifri_inventar.pdf";

                return File(
                    GeneratePrimaryReportPdf(groups, heading, @Inventar.Resources.Resource.ProductNumber),
                    "application/pdf",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory code report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportCodeReportExcel([FromBody] InventoryPrimaryReportExportRequest request)
        {
            if (request == null || request.Keys.Count == 0)
            {
                return BadRequest("No product numbers were provided for Excel export.");
            }

            try
            {
                var groups = await BuildCodeReportGroupsAsync(request.Keys);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByProductNumber;
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_sifri_inventar.xlsx"
                    : "Izvjestaj_po_sifri_inventar.xlsx";

                return File(
                    GeneratePrimaryReportExcel(
                        groups,
                        heading,
                        @Inventar.Resources.Resource.ProductNumber,
                        "Izvjestaj po sifri inventar"),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory code report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportNameReportPdf([FromBody] InventoryNameReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for PDF export.");
            }

            try
            {
                var groups = await BuildNameReportGroupsAsync(request.ProductNames);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByName; /*"Izvjestaj po Imenu"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_Imenu.pdf"
                    : "Izvjestaj_po_Imenu.pdf";

                return File(GenerateNameReportPdf(groups, heading), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory name report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportNameReportExcel([FromBody] InventoryNameReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for Excel export.");
            }

            try
            {
                var groups = await BuildNameReportGroupsAsync(request.ProductNames);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByName; /*"Izvjestaj po Imenu"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_Imenu.xlsx"
                    : "Izvjestaj_po_Imenu.xlsx";

                return File(
                    GenerateNameReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory name report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportM2ReportPdf([FromBody] InventoryNameReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for PDF export.");
            }

            try
            {
                var groups = await BuildNameReportGroupsAsync(request.ProductNames, onlyPerM2: true);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByM2; /*"Izvjestaj po m2"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_m2.pdf"
                    : "Izvjestaj_po_m2.pdf";

                return File(GenerateNameReportPdf(groups, heading), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory m2 report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportM2ReportExcel([FromBody] InventoryNameReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for Excel export.");
            }

            try
            {
                var groups = await BuildNameReportGroupsAsync(request.ProductNames, onlyPerM2: true);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByM2 ; /*"Izvjestaj po m2"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_m2.xlsx"
                    : "Izvjestaj_po_m2.xlsx";

                return File(
                    GenerateNameReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory m2 report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPieceReportPdf([FromBody] InventoryNameReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for PDF export.");
            }

            try
            {
                var groups = await BuildPieceReportGroupsAsync(request.ProductNames);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportPerUnit; /*"Izvjestaj po komadu"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_komadu.pdf"
                    : "Izvjestaj_po_komadu.pdf";

                return File(GeneratePieceReportPdf(groups, heading), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory piece report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPieceReportExcel([FromBody] InventoryNameReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for Excel export.");
            }

            try
            {
                var groups = await BuildPieceReportGroupsAsync(request.ProductNames);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportPerUnit; /*"Izvjestaj po komadu"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_komadu.xlsx"
                    : "Izvjestaj_po_komadu.xlsx";

                return File(
                    GeneratePieceReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory piece report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportSizeReportPdf([FromBody] InventorySizeReportExportRequest request)
        {
            if (request == null || request.Sizes.Count == 0)
            {
                return BadRequest("No sizes were provided for PDF export.");
            }

            try
            {
                var groups = await BuildSizeReportGroupsAsync(request.Sizes);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportBySize; /*"Izvjestaj po velicini"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_velicini.pdf"
                    : "Izvjestaj_po_velicini.pdf";

                return File(GenerateSizeReportPdf(groups, heading), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory size report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportSizeReportExcel([FromBody] InventorySizeReportExportRequest request)
        {
            if (request == null || request.Sizes.Count == 0)
            {
                return BadRequest("No sizes were provided for Excel export.");
            }

            try
            {
                var groups = await BuildSizeReportGroupsAsync(request.Sizes);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportBySize; /*"Izvjestaj po velicini"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_velicini.xlsx"
                    : "Izvjestaj_po_velicini.xlsx";

                return File(
                    GenerateSizeReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory size report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportColorReportPdf([FromBody] InventoryColorReportExportRequest request)
        {
            if (request == null || request.Colors.Count == 0)
            {
                return BadRequest("No colors were provided for PDF export.");
            }

            try
            {
                var groups = await BuildColorReportGroupsAsync(request.Colors);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByColor; /*"Izvjestaj po boji"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_boji.pdf"
                    : "Izvjestaj_po_boji.pdf";

                return File(GenerateColorReportPdf(groups, heading), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory color report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportColorReportExcel([FromBody] InventoryColorReportExportRequest request)
        {
            if (request == null || request.Colors.Count == 0)
            {
                return BadRequest("No colors were provided for Excel export.");
            }

            try
            {
                var groups = await BuildColorReportGroupsAsync(request.Colors);
                if (groups.Count == 0)
                {
                    return BadRequest("No grouped rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByColor; /*"Izvjestaj po boji"*/
                var fileName = request.UseCustomTable
                    ? "NovaTabela_Izvjestaj_po_boji.xlsx"
                    : "Izvjestaj_po_boji.xlsx";

                return File(
                    GenerateColorReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory color report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportErrorReportPdf([FromBody] InventoryErrorReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for PDF export.");
            }

            try
            {
                var groups = await BuildErrorReportGroupsAsync(request.ProductNames);
                if (groups.Count == 0)
                {
                    return BadRequest("No error-report rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByErrors; /*"Izvjestaj po greskama"*/
                return File(GenerateErrorReportPdf(groups, heading), "application/pdf", "Izvjestaj_po_greskama.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory error report PDF.");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportErrorReportExcel([FromBody] InventoryErrorReportExportRequest request)
        {
            if (request == null || request.ProductNames.Count == 0)
            {
                return BadRequest("No product names were provided for Excel export.");
            }

            try
            {
                var groups = await BuildErrorReportGroupsAsync(request.ProductNames);
                if (groups.Count == 0)
                {
                    return BadRequest("No error-report rows were found for the requested export.");
                }

                var heading = @Inventar.Resources.Resource.ReportByErrors; /*"Izvjestaj po greskama"*/
                return File(
                    GenerateErrorReportExcel(groups, heading),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Izvjestaj_po_greskama.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory error report Excel.");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        private async Task<List<InventoryNameReportGroupViewModel>> BuildNameReportGroupsAsync(
            IReadOnlyCollection<string>? selectedNames = null,
            bool onlyPerM2 = false)
        {
            var products = await GetActiveInventoryReportRowsAsync(onlyPerM2);
            var selected = NormalizeSelection(selectedNames);

            return products
                .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new InventoryNameReportGroupViewModel
                {
                    ProductName = group.Key,
                    SizeRows = group
                        .GroupBy(item => new
                        {
                            item.EffectiveWidth,
                            item.EffectiveLength,
                            item.OriginalWidth,
                            item.OriginalLength,
                            item.PoMjeri,
                            SizeLabel = BuildInventorySizeLabel(item)
                        })
                        .OrderBy(sizeGroup => sizeGroup.Key.EffectiveWidth ?? int.MaxValue)
                        .ThenBy(sizeGroup => sizeGroup.Key.EffectiveLength ?? int.MaxValue)
                        .ThenBy(sizeGroup => sizeGroup.Key.OriginalWidth ?? int.MaxValue)
                        .ThenBy(sizeGroup => sizeGroup.Key.OriginalLength ?? int.MaxValue)
                        .Select(sizeGroup => new InventoryNameReportSizeSummaryViewModel
                        {
                            SizeLabel = sizeGroup.Key.SizeLabel,
                            TotalM2 = Math.Round(sizeGroup.Sum(CalculateItemM2Total), 2),
                            TotalQuantity = sizeGroup.Sum(item => item.Quantity)
                        })
                        .ToList(),
                    TotalM2 = Math.Round(group.Sum(CalculateItemM2Total), 2),
                    TotalQuantity = group.Sum(item => item.Quantity)
                })
                .ToList();
        }

        private async Task<List<InventoryPrimaryReportGroupViewModel>> BuildCodeReportGroupsAsync(
            IReadOnlyCollection<string>? selectedProductNumbers = null)
        {
            var rows = await GetActiveInventoryReportRowsAsync();
            var selected = NormalizeSelection(selectedProductNumbers);

            return rows
                .GroupBy(item => BuildProductNumberLabel(item.ProductNumber), StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new InventoryPrimaryReportGroupViewModel
                {
                    KeyLabel = group.Key,
                    ProductRows = group
                        .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(nameGroup => nameGroup.Key)
                        .Select(nameGroup => new InventoryProductSummaryViewModel
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

        private async Task<List<InventoryPieceReportGroupViewModel>> BuildPieceReportGroupsAsync(
            IReadOnlyCollection<string>? selectedNames = null)
        {
            var products = await GetActiveProductsQuery()
                .Where(item => !item.PerM2)
                .ToListAsync();

            var selected = NormalizeSelection(selectedNames);

            return products
                .GroupBy(item => BuildProductNameLabel(item.Name), StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new InventoryPieceReportGroupViewModel
                {
                    ProductName = group.Key,
                    TotalQuantity = group.Sum(item => item.Quantity)
                })
                .ToList();
        }

        private async Task<List<InventorySizeReportGroupViewModel>> BuildSizeReportGroupsAsync(
            IReadOnlyCollection<string>? selectedSizes = null)
        {
            var products = await GetActiveInventoryReportRowsAsync();
            var selected = NormalizeSelection(selectedSizes);

            return products
                .GroupBy(item => BuildInventorySizeLabel(item), StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    SizeLabel = group.Key,
                    SortWidth = group.Min(item => item.EffectiveWidth ?? item.OriginalWidth ?? int.MaxValue),
                    SortLength = group.Min(item => item.EffectiveLength ?? item.OriginalLength ?? int.MaxValue),
                    Items = group.ToList()
                })
                .OrderBy(group => group.SortWidth)
                .ThenBy(group => group.SortLength)
                .ThenBy(group => group.SizeLabel)
                .Select(group => new InventorySizeReportGroupViewModel
                {
                    SizeLabel = group.SizeLabel,
                    ProductRows = group.Items
                        .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(nameGroup => nameGroup.Key)
                        .Select(nameGroup => new InventorySizeReportProductSummaryViewModel
                        {
                            ProductName = nameGroup.Key,
                            TotalM2 = Math.Round(nameGroup.Sum(CalculateItemM2Total), 2),
                            TotalQuantity = nameGroup.Sum(item => item.Quantity)
                        })
                        .ToList(),
                    TotalM2 = Math.Round(group.Items.Sum(CalculateItemM2Total), 2),
                    TotalQuantity = group.Items.Sum(item => item.Quantity)
                })
                .Where(group => IsSelected(group.SizeLabel, selected))
                .ToList();
        }

        private async Task<List<InventoryColorReportGroupViewModel>> BuildColorReportGroupsAsync(
            IReadOnlyCollection<string>? selectedColors = null)
        {
            var products = await GetActiveInventoryReportRowsAsync();
            var selected = NormalizeSelection(selectedColors);

            return products
                .GroupBy(item => BuildColorLabel(item.Color), StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new InventoryColorReportGroupViewModel
                {
                    Color = group.Key,
                    ProductGroups = group
                        .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(nameGroup => nameGroup.Key)
                        .Select(nameGroup => new InventoryColorReportProductGroupViewModel
                        {
                            ProductName = nameGroup.Key,
                            SizeRows = nameGroup
                                .GroupBy(item => new
                                {
                                    item.EffectiveWidth,
                                    item.EffectiveLength,
                                    item.OriginalWidth,
                                    item.OriginalLength,
                                    item.PoMjeri,
                                    SizeLabel = BuildInventorySizeLabel(item)
                                })
                                .OrderBy(sizeGroup => sizeGroup.Key.EffectiveWidth ?? int.MaxValue)
                                .ThenBy(sizeGroup => sizeGroup.Key.EffectiveLength ?? int.MaxValue)
                                .ThenBy(sizeGroup => sizeGroup.Key.OriginalWidth ?? int.MaxValue)
                                .ThenBy(sizeGroup => sizeGroup.Key.OriginalLength ?? int.MaxValue)
                                .Select(sizeGroup => new InventoryColorReportSizeSummaryViewModel
                                {
                                    SizeLabel = sizeGroup.Key.SizeLabel,
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

        private async Task<List<InventoryErrorReportGroupViewModel>> BuildErrorReportGroupsAsync(
            IReadOnlyCollection<string>? selectedNames = null)
        {
            var products = await GetActiveInventoryReportRowsAsync();
            var selected = NormalizeSelection(selectedNames);

            var errorNames = products
                .Where(item => item.Quantity < 0)
                .Select(item => item.ProductName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return products
                .Where(item => errorNames.Contains(item.ProductName))
                .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .Where(group => IsSelected(group.Key, selected))
                .OrderBy(group => group.Key)
                .Select(group => new InventoryErrorReportGroupViewModel
                {
                    ProductName = group.Key,
                    Items = group
                        .OrderBy(item => item.ProductNumber)
                        .ThenBy(item => item.Model)
                        .ThenBy(item => item.Color)
                        .ThenBy(item => item.EffectiveWidth ?? int.MaxValue)
                        .ThenBy(item => item.EffectiveLength ?? int.MaxValue)
                        .ThenBy(item => item.OriginalWidth ?? int.MaxValue)
                        .ThenBy(item => item.OriginalLength ?? int.MaxValue)
                        .Select(item => new InventoryErrorReportItemViewModel
                        {
                            ProductNumber = item.ProductNumber ?? string.Empty,
                            Model = item.Model ?? string.Empty,
                            Color = BuildColorLabel(item.Color),
                            SizeLabel = BuildInventorySizeLabel(item),
                            M2 = Math.Round(CalculateItemM2PerUnit(item), 2),
                            Quantity = item.Quantity,
                            M2Total = Math.Round(CalculateItemM2Total(item), 2)
                        })
                        .ToList(),
                    TotalM2 = Math.Round(group.Sum(CalculateItemM2Total), 2),
                    TotalQuantity = group.Sum(item => item.Quantity)
                })
                .ToList();
        }

        private byte[] GenerateNameReportPdf(
            IReadOnlyList<InventoryNameReportGroupViewModel> groups,
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

            var rows = BuildNameReportPdfRows(groups);
            RenderPaginatedFourColumnReportPdf(
                document,
                pageSize,
                pdfFont,
                heading,
                rows,
                new float[] { 4.6f, 2.2f, 1.6f, 1.6f },
                @Inventar.Resources.Resource.Name,
                @Inventar.Resources.Resource.Size,
                @Inventar.Resources.Resource.Quantity,
                @Inventar.Resources.Resource.M2Total);

            document.Close();
            return ms.ToArray();
        }

        private byte[] GenerateSizeReportPdf(
            IReadOnlyList<InventorySizeReportGroupViewModel> groups,
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

            var rows = BuildSizeReportPdfRows(groups);
            RenderPaginatedFourColumnReportPdf(
                document,
                pageSize,
                pdfFont,
                heading,
                rows,
                new float[] { 2.5f, 4.3f, 1.6f, 1.6f },
                @Inventar.Resources.Resource.Size,
                @Inventar.Resources.Resource.Name,
                @Inventar.Resources.Resource.Quantity,
                @Inventar.Resources.Resource.M2Total);

            document.Close();
            return ms.ToArray();
        }

        private byte[] GenerateColorReportPdf(
            IReadOnlyList<InventoryColorReportGroupViewModel> groups,
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

        private byte[] GenerateErrorReportPdf(
            IReadOnlyList<InventoryErrorReportGroupViewModel> groups,
            string heading)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());

            document.SetMargins(20, 20, 20, 20);
            document.SetFont(GetPdfFont());
            document.SetFontSize(10);
            AddPdfHeading(document, heading);

            if (groups.Count == 0)
            {
                document.Add(new Paragraph("Nema podataka za prikaz."));
                document.Close();
                return ms.ToArray();
            }

            foreach (var group in groups)
            {
                var table = new Table(UnitValue.CreatePercentArray(new float[] { 2.0f, 2.6f, 2.2f, 1.8f, 1.2f, 1.4f, 1.6f }))
                    .UseAllAvailableWidth()
                    .SetKeepTogether(false);
                table.AddHeaderCell(new Cell(1, 7)
                    .Add(new Paragraph(group.ProductName))
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SimulateBold()
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetPadding(6));

                table.AddHeaderCell(CreatePdfHeaderCell(@Inventar.Resources.Resource.ProductNumber));
                table.AddHeaderCell(CreatePdfHeaderCell("Model"));
                table.AddHeaderCell(CreatePdfHeaderCell(@Inventar.Resources.Resource.Color));
                table.AddHeaderCell(CreatePdfHeaderCell(@Inventar.Resources.Resource.Size));
                table.AddHeaderCell(CreatePdfHeaderCell("m2"));
                table.AddHeaderCell(CreatePdfHeaderCell(@Inventar.Resources.Resource.Quantity));
                table.AddHeaderCell(CreatePdfHeaderCell(@Inventar.Resources.Resource.M2Total));

                foreach (var item in group.Items)
                {
                    table.AddCell(CreatePdfBodyCell(item.ProductNumber));
                    table.AddCell(CreatePdfBodyCell(item.Model));
                    table.AddCell(CreatePdfBodyCell(item.Color));
                    table.AddCell(CreatePdfBodyCell(item.SizeLabel));
                    table.AddCell(CreatePdfBodyCell(item.M2.ToString("0.00")));
                    table.AddCell(CreatePdfBodyCell(item.Quantity.ToString()));
                    table.AddCell(CreatePdfBodyCell(item.M2Total.ToString("0.00")));
                }

                table.AddCell(CreatePdfTotalCell(@Inventar.Resources.Resource.Total, 5));
                table.AddCell(CreatePdfTotalCell(group.TotalQuantity.ToString()));
                table.AddCell(CreatePdfTotalCell(group.TotalM2.ToString("0.00")));

                document.Add(table);
                document.Add(new Paragraph(" ").SetMarginBottom(8));
            }

            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 2.0f, 2.6f, 2.2f, 1.8f, 1.2f, 1.4f, 1.6f }))
                .UseAllAvailableWidth();
            summaryTable.AddCell(CreatePdfGrandTotalCell(@Inventar.Resources.Resource.OverallTotal, 5));
            summaryTable.AddCell(CreatePdfGrandTotalCell(groups.Sum(group => group.TotalQuantity).ToString()));
            summaryTable.AddCell(CreatePdfGrandTotalCell(groups.Sum(group => group.TotalM2).ToString("0.00")));
            document.Add(summaryTable);

            document.Close();
            return ms.ToArray();
        }

        private byte[] GeneratePieceReportPdf(
            IReadOnlyList<InventoryPieceReportGroupViewModel> groups,
            string heading)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());

            document.SetMargins(20, 20, 20, 20);
            document.SetFont(GetPdfFont());
            document.SetFontSize(10);
            AddPdfHeading(document, heading);

            if (groups.Count == 0)
            {
                document.Add(new Paragraph("Nema podataka za prikaz."));
                document.Close();
                return ms.ToArray();
            }

            var table = CreatePdfTable(new float[] { 6.0f, 2.0f }, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Quantity);

            foreach (var group in groups)
            {
                table.AddCell(CreatePdfBodyCell(group.ProductName));
                table.AddCell(CreatePdfBodyCell(group.TotalQuantity.ToString()));
            }

            table.AddCell(CreatePdfGrandTotalCell(@Inventar.Resources.Resource.OverallTotal));
            table.AddCell(CreatePdfGrandTotalCell(groups.Sum(group => group.TotalQuantity).ToString()));
            document.Add(table);

            document.Close();
            return ms.ToArray();
        }

        private byte[] GenerateNameReportExcel(
            IReadOnlyList<InventoryNameReportGroupViewModel> groups,
            string heading)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(@Inventar.Resources.Resource.ReportByName);

            worksheet.Range("A1:D1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:D1"));

            var headerRow = 3;
            SetSheetHeaders(worksheet, headerRow, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Size, @Inventar.Resources.Resource.Quantity, @Inventar.Resources.Resource.M2Total);
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            var currentRow = headerRow + 1;

            foreach (var group in groups)
            {
                var dataRows = group.SizeRows.Count == 0
                    ? new List<InventoryNameReportSizeSummaryViewModel>
                    {
                        new()
                        {
                            SizeLabel = "-",
                            TotalM2 = group.TotalM2,
                            TotalQuantity = group.TotalQuantity
                        }
                    }
                    : group.SizeRows;

                var groupStartRow = currentRow;

                foreach (var sizeRow in dataRows)
                {
                    worksheet.Cell(currentRow, 2).Value = sizeRow.SizeLabel;
                    worksheet.Cell(currentRow, 3).Value = sizeRow.TotalQuantity;
                    worksheet.Cell(currentRow, 4).Value = sizeRow.TotalM2;
                    currentRow++;
                }

                worksheet.Cell(currentRow, 2).Value = @Inventar.Resources.Resource.Total;
                worksheet.Cell(currentRow, 3).Value = group.TotalQuantity;
                worksheet.Cell(currentRow, 4).Value = group.TotalM2;
                StyleTotalRow(worksheet.Range(currentRow, 2, currentRow, 4));

                worksheet.Range(groupStartRow, 1, currentRow, 1).Merge().Value = group.ProductName;
                StyleMergedLabelCell(worksheet.Range(groupStartRow, 1, currentRow, 1));
                currentRow++;
            }

            worksheet.Range(currentRow, 1, currentRow, 2).Merge().Value = @Inventar.Resources.Resource.OverallTotal;
            worksheet.Cell(currentRow, 3).Value = groups.Sum(group => group.TotalQuantity);
            worksheet.Cell(currentRow, 4).Value = groups.Sum(group => group.TotalM2);
            StyleGrandTotalRow(worksheet.Range(currentRow, 1, currentRow, 4));

            FinalizeSimpleWorksheet(worksheet, Math.Max(currentRow, headerRow), 4);
            worksheet.Column(1).Width = 26;
            worksheet.Column(2).Width = 18;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 14;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GenerateSizeReportExcel(
            IReadOnlyList<InventorySizeReportGroupViewModel> groups,
            string heading)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(@Inventar.Resources.Resource.ReportBySize);

            worksheet.Range("A1:D1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:D1"));

            var headerRow = 3;
            SetSheetHeaders(worksheet, headerRow, @Inventar.Resources.Resource.Size, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Quantity, @Inventar.Resources.Resource.M2Total);
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            var currentRow = headerRow + 1;

            foreach (var group in groups)
            {
                var dataRows = group.ProductRows.Count == 0
                    ? new List<InventorySizeReportProductSummaryViewModel>
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

                worksheet.Range(groupStartRow, 1, currentRow, 1).Merge().Value = group.SizeLabel;
                StyleMergedLabelCell(worksheet.Range(groupStartRow, 1, currentRow, 1));
                currentRow++;
            }

            worksheet.Range(currentRow, 1, currentRow, 2).Merge().Value = @Inventar.Resources.Resource.OverallTotal;
            worksheet.Cell(currentRow, 3).Value = groups.Sum(group => group.TotalQuantity);
            worksheet.Cell(currentRow, 4).Value = groups.Sum(group => group.TotalM2);
            StyleGrandTotalRow(worksheet.Range(currentRow, 1, currentRow, 4));

            FinalizeSimpleWorksheet(worksheet, Math.Max(currentRow, headerRow), 4);
            worksheet.Column(1).Width = 18;
            worksheet.Column(2).Width = 26;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 14;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GenerateColorReportExcel(
            IReadOnlyList<InventoryColorReportGroupViewModel> groups,
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
                        ? new List<InventoryColorReportSizeSummaryViewModel>
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

                worksheet.Cell(currentRow, 2).Value = @Inventar.Resources.Resource.Total;
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

        private byte[] GenerateErrorReportExcel(
            IReadOnlyList<InventoryErrorReportGroupViewModel> groups,
            string heading)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(@Inventar.Resources.Resource.ReportByErrors);

            worksheet.Range("A1:G1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:G1"));

            var currentRow = 3;

            foreach (var group in groups)
            {
                worksheet.Range(currentRow, 1, currentRow, 7).Merge().Value = group.ProductName;
                worksheet.Range(currentRow, 1, currentRow, 7).Style.Font.Bold = true;
                worksheet.Range(currentRow, 1, currentRow, 7).Style.Fill.BackgroundColor = XLColor.LightGray;
                worksheet.Range(currentRow, 1, currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow++;

                SetSheetHeaders(worksheet, currentRow, @Inventar.Resources.Resource.ProductNumber, "Model", @Inventar.Resources.Resource.Color, @Inventar.Resources.Resource.Size, "m2", @Inventar.Resources.Resource.Quantity, @Inventar.Resources.Resource.M2Total);
                currentRow++;

                foreach (var item in group.Items)
                {
                    worksheet.Cell(currentRow, 1).Value = item.ProductNumber;
                    worksheet.Cell(currentRow, 2).Value = item.Model;
                    worksheet.Cell(currentRow, 3).Value = item.Color;
                    worksheet.Cell(currentRow, 4).Value = item.SizeLabel;
                    worksheet.Cell(currentRow, 5).Value = item.M2;
                    worksheet.Cell(currentRow, 6).Value = item.Quantity;
                    worksheet.Cell(currentRow, 7).Value = item.M2Total;
                    currentRow++;
                }

                worksheet.Cell(currentRow, 1).Value = @Inventar.Resources.Resource.Total;
                worksheet.Range(currentRow, 1, currentRow, 5).Merge();
                worksheet.Cell(currentRow, 6).Value = group.TotalQuantity;
                worksheet.Cell(currentRow, 7).Value = group.TotalM2;
                StyleTotalRow(worksheet.Range(currentRow, 1, currentRow, 7));
                currentRow += 2;
            }

            currentRow--;
            worksheet.Range(currentRow, 1, currentRow, 5).Merge().Value = @Inventar.Resources.Resource.OverallTotal;
            worksheet.Cell(currentRow, 6).Value = groups.Sum(group => group.TotalQuantity);
            worksheet.Cell(currentRow, 7).Value = groups.Sum(group => group.TotalM2);
            StyleGrandTotalRow(worksheet.Range(currentRow, 1, currentRow, 7));

            FinalizeSimpleWorksheet(worksheet, Math.Max(currentRow, 3), 7);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            worksheet.Column(1).Width = 16;
            worksheet.Column(2).Width = 18;
            worksheet.Column(3).Width = 18;
            worksheet.Column(4).Width = 16;
            worksheet.Column(5).Width = 10;
            worksheet.Column(6).Width = 10;
            worksheet.Column(7).Width = 12;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GeneratePieceReportExcel(
            IReadOnlyList<InventoryPieceReportGroupViewModel> groups,
            string heading)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(@Inventar.Resources.Resource.ReportPerUnit);

            worksheet.Range("A1:B1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:B1"));

            var headerRow = 3;
            SetSheetHeaders(worksheet, headerRow, @Inventar.Resources.Resource.Name, @Inventar.Resources.Resource.Quantity);
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            var currentRow = headerRow + 1;

            foreach (var group in groups)
            {
                worksheet.Cell(currentRow, 1).Value = group.ProductName;
                worksheet.Cell(currentRow, 2).Value = group.TotalQuantity;
                currentRow++;
            }

            worksheet.Cell(currentRow, 1).Value = @Inventar.Resources.Resource.OverallTotal;
            worksheet.Cell(currentRow, 2).Value = groups.Sum(group => group.TotalQuantity);
            StyleGrandTotalRow(worksheet.Range(currentRow, 1, currentRow, 2));

            FinalizeSimpleWorksheet(worksheet, Math.Max(currentRow, headerRow), 2);
            worksheet.Column(1).Width = 32;
            worksheet.Column(2).Width = 14;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GeneratePrimaryReportPdf(
            IReadOnlyList<InventoryPrimaryReportGroupViewModel> groups,
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

        private byte[] GeneratePrimaryReportExcel(
            IReadOnlyList<InventoryPrimaryReportGroupViewModel> groups,
            string heading,
            string firstColumnHeader,
            string worksheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(worksheetName);

            worksheet.Range("A1:D1").Merge().Value = heading;
            StyleSheetHeading(worksheet.Range("A1:D1"));

            var headerRow = 3;
            SetSheetHeaders(
                worksheet,
                headerRow,
                firstColumnHeader,
                @Inventar.Resources.Resource.Name,
                @Inventar.Resources.Resource.Quantity,
                @Inventar.Resources.Resource.M2Total);

            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            var currentRow = headerRow + 1;

            foreach (var group in groups)
            {
                var groupStartRow = currentRow;
                var productRows = group.ProductRows.Count == 0
                    ? new List<InventoryProductSummaryViewModel>
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

        private void AddNameGroupToPdfTable(Table table, InventoryNameReportGroupViewModel group)
        {
            var sizeRows = group.SizeRows.Count == 0
                ? new List<InventoryNameReportSizeSummaryViewModel>
                {
                    new()
                    {
                        SizeLabel = "-",
                        TotalM2 = group.TotalM2,
                        TotalQuantity = group.TotalQuantity
                    }
                }
                : group.SizeRows;

            foreach (var sizeRow in sizeRows)
            {
                table.AddCell(CreatePdfBodyCell(group.ProductName));
                table.AddCell(CreatePdfBodyCell(sizeRow.SizeLabel));
                table.AddCell(CreatePdfBodyCell(sizeRow.TotalQuantity.ToString()));
                table.AddCell(CreatePdfBodyCell(sizeRow.TotalM2.ToString("0.00")));
            }

            table.AddCell(CreatePdfTotalCell(group.ProductName));
            table.AddCell(CreatePdfTotalCell(@Inventar.Resources.Resource.Total));
            table.AddCell(CreatePdfTotalCell(group.TotalQuantity.ToString()));
            table.AddCell(CreatePdfTotalCell(group.TotalM2.ToString("0.00")));
        }

        private void AddPrimaryGroupToPdfTable(Table table, InventoryPrimaryReportGroupViewModel group)
        {
            var productRows = group.ProductRows.Count == 0
                ? new List<InventoryProductSummaryViewModel>
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

        private void AddSizeGroupToPdfTable(Table table, InventorySizeReportGroupViewModel group)
        {
            var productRows = group.ProductRows.Count == 0
                ? new List<InventorySizeReportProductSummaryViewModel>
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
                table.AddCell(CreatePdfBodyCell(group.SizeLabel));
                table.AddCell(CreatePdfBodyCell(productRow.ProductName));
                table.AddCell(CreatePdfBodyCell(productRow.TotalQuantity.ToString()));
                table.AddCell(CreatePdfBodyCell(productRow.TotalM2.ToString("0.00")));
            }

            table.AddCell(CreatePdfTotalCell(group.SizeLabel));
            table.AddCell(CreatePdfTotalCell(@Inventar.Resources.Resource.Total));
            table.AddCell(CreatePdfTotalCell(group.TotalQuantity.ToString()));
            table.AddCell(CreatePdfTotalCell(group.TotalM2.ToString("0.00")));
        }

        private void AddColorGroupToPdfTable(Table table, InventoryColorReportGroupViewModel group)
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

        private List<FourColumnPdfRow> BuildNameReportPdfRows(IReadOnlyList<InventoryNameReportGroupViewModel> groups)
        {
            var rows = new List<FourColumnPdfRow>();

            foreach (var group in groups)
            {
                var sizeRows = group.SizeRows.Count == 0
                    ? new List<InventoryNameReportSizeSummaryViewModel>
                    {
                        new()
                        {
                            SizeLabel = "-",
                            TotalM2 = group.TotalM2,
                            TotalQuantity = group.TotalQuantity
                        }
                    }
                    : group.SizeRows;

                rows.AddRange(sizeRows.Select(sizeRow => new FourColumnPdfRow
                {
                    GroupLabel = group.ProductName,
                    SecondColumnText = sizeRow.SizeLabel,
                    QuantityText = sizeRow.TotalQuantity.ToString(),
                    M2Text = sizeRow.TotalM2.ToString("0.00"),
                    Kind = FourColumnPdfRowKind.Data
                }));

                rows.Add(new FourColumnPdfRow
                {
                    GroupLabel = group.ProductName,
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

        private List<FourColumnPdfRow> BuildSizeReportPdfRows(IReadOnlyList<InventorySizeReportGroupViewModel> groups)
        {
            var rows = new List<FourColumnPdfRow>();

            foreach (var group in groups)
            {
                var productRows = group.ProductRows.Count == 0
                    ? new List<InventorySizeReportProductSummaryViewModel>
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
                    GroupLabel = group.SizeLabel,
                    SecondColumnText = productRow.ProductName,
                    QuantityText = productRow.TotalQuantity.ToString(),
                    M2Text = productRow.TotalM2.ToString("0.00"),
                    Kind = FourColumnPdfRowKind.Data
                }));

                rows.Add(new FourColumnPdfRow
                {
                    GroupLabel = group.SizeLabel,
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

        private List<FourColumnPdfRow> BuildPrimaryReportPdfRows(IReadOnlyList<InventoryPrimaryReportGroupViewModel> groups)
        {
            var rows = new List<FourColumnPdfRow>();

            foreach (var group in groups)
            {
                var productRows = group.ProductRows.Count == 0
                    ? new List<InventoryProductSummaryViewModel>
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

        private List<FiveColumnPdfRow> BuildColorReportPdfRows(IReadOnlyList<InventoryColorReportGroupViewModel> groups)
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

        private static List<InventoryColorReportSizeSummaryViewModel> GetSafeColorSizeRows(InventoryColorReportProductGroupViewModel productGroup)
        {
            return productGroup.SizeRows.Count == 0
                ? new List<InventoryColorReportSizeSummaryViewModel>
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

        private IQueryable<Tepih> GetActiveProductsQuery()
        {
            return _context.Tepisi
                .AsNoTracking()
                .Where(item => !item.Disabled && !item.CreatedForDirectSale);
        }

        private async Task<List<InventoryReportRow>> GetActiveInventoryReportRowsAsync(bool onlyPerM2 = false)
        {
            var query = GetActiveProductsQuery();
            if (onlyPerM2)
            {
                query = query.Where(item => item.PerM2);
            }

            var products = await query.ToListAsync();
            if (products.Count == 0)
            {
                return new List<InventoryReportRow>();
            }

            var poMjeriIds = products
                .Where(product => product.PoMjeri && product.Length.HasValue)
                .Select(product => product.Id)
                .ToList();

            var consumedLengthsByProductId = poMjeriIds.Count == 0
                ? new Dictionary<int, int>()
                : await _context.Prodaje
                    .AsNoTracking()
                    .Where(sale => !sale.Disabled && poMjeriIds.Contains(sale.TepihId))
                    .GroupBy(sale => sale.TepihId)
                    .Select(group => new
                    {
                        TepihId = group.Key,
                        ConsumedLength = group.Sum(sale => (sale.ConsumedLength ?? sale.CustomLength ?? 0) * sale.Quantity)
                    })
                    .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength);

            return products
                .Select(product =>
                {
                    int? effectiveLength = product.Length;
                    if (product.PoMjeri && product.Length.HasValue)
                    {
                        effectiveLength = Math.Max(
                            product.Length.Value - consumedLengthsByProductId.GetValueOrDefault(product.Id),
                            0);
                    }

                    return new InventoryReportRow
                    {
                        ProductId = product.Id,
                        ProductNumber = product.ProductNumber,
                        ProductName = BuildProductNameLabel(product.Name),
                        Model = product.Model,
                        Color = product.Color,
                        Quantity = product.Quantity,
                        Price = product.Price,
                        PerM2 = product.PerM2,
                        PoMjeri = product.PoMjeri,
                        EffectiveWidth = product.Width,
                        EffectiveLength = effectiveLength,
                        OriginalWidth = product.Width,
                        OriginalLength = product.Length
                    };
                })
                .ToList();
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

        private static decimal CalculateItemM2PerUnit(Tepih item)
        {
            return PoMjeriHelper.CalculateM2PerUnit(item.PerM2, item.Width, item.Length) ?? 0m;
        }

        private static decimal CalculateItemM2Total(Tepih item)
        {
            return PoMjeriHelper.CalculateM2Total(item.PerM2, item.Width, item.Length, item.Quantity) ?? 0m;
        }

        private static decimal CalculateItemM2PerUnit(InventoryReportRow item)
        {
            return PoMjeriHelper.CalculateM2PerUnit(item.PerM2, item.EffectiveWidth, item.EffectiveLength) ?? 0m;
        }

        private static decimal CalculateItemM2Total(InventoryReportRow item)
        {
            return PoMjeriHelper.CalculateM2Total(item.PerM2, item.EffectiveWidth, item.EffectiveLength, item.Quantity) ?? 0m;
        }

        private static string BuildSizeLabel(int? width, int? length)
        {
            return width.HasValue && length.HasValue
                ? $"{width.Value}x{length.Value}"
                : "-";
        }

        private static string BuildInventorySizeLabel(InventoryReportRow item)
        {
            return BuildInventorySizeLabel(
                item.EffectiveWidth,
                item.EffectiveLength,
                item.OriginalWidth,
                item.OriginalLength,
                item.PoMjeri);
        }

        private static string BuildInventorySizeLabel(
            int? effectiveWidth,
            int? effectiveLength,
            int? originalWidth,
            int? originalLength,
            bool poMjeri)
        {
            var effectiveLabel = BuildSizeLabel(effectiveWidth, effectiveLength);
            if (!poMjeri)
            {
                return effectiveLabel;
            }

            var originalLabel = BuildSizeLabel(originalWidth, originalLength);
            if (effectiveLabel == "-" || string.Equals(effectiveLabel, originalLabel, StringComparison.OrdinalIgnoreCase))
            {
                return effectiveLabel == "-" ? originalLabel : effectiveLabel;
            }

            return $"{effectiveLabel} (orig. {originalLabel})";
        }

        private static string BuildColorLabel(string? color)
        {
            return string.IsNullOrWhiteSpace(color) ? "-" : color.Trim();
        }

        private static string BuildProductNameLabel(string? productName)
        {
            return string.IsNullOrWhiteSpace(productName) ? "-" : productName.Trim();
        }

        private static string BuildProductNumberLabel(string? productNumber)
        {
            return string.IsNullOrWhiteSpace(productNumber) ? "-" : productNumber.Trim();
        }

        private static decimal CalculateInventoryRowTotalPrice(InventoryReportRow item)
        {
            decimal totalPrice;
            if (item.PoMjeri)
            {
                totalPrice = item.Price * InventoryPricingHelper.CalculatePoMjeriLengthFactor(item.EffectiveLength) * item.Quantity;
            }
            else if (item.PerM2)
            {
                totalPrice = item.Price * (PoMjeriHelper.CalculateM2Total(true, item.EffectiveWidth, item.EffectiveLength, item.Quantity) ?? 0m);
            }
            else
            {
                totalPrice = item.Price * item.Quantity;
            }

            return Math.Round(totalPrice, 2);
        }

        private sealed class InventoryReportRow
        {
            public int ProductId { get; set; }
            public string? ProductNumber { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string? Model { get; set; }
            public string? Color { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public bool PerM2 { get; set; }
            public bool PoMjeri { get; set; }
            public int? EffectiveWidth { get; set; }
            public int? EffectiveLength { get; set; }
            public int? OriginalWidth { get; set; }
            public int? OriginalLength { get; set; }
        }
    }
}
