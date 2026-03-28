using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Utils;
using Inventar.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Controllers
{
    [Authorize(Roles = "admin,superadmin")]
    public class StorefrontCatalogController : Controller
    {
        private readonly ITepihRepository _tepihRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly ILogger<StorefrontCatalogController> _logger;

        public StorefrontCatalogController(
            ITepihRepository tepihRepository,
            ApplicationDbContext context,
            IPhotoService photoService,
            ILogger<StorefrontCatalogController> logger)
        {
            _tepihRepository = tepihRepository;
            _context = context;
            _photoService = photoService;
            _logger = logger;
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
                _logger.LogError(ex, "Error loading storefront catalog admin.");
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tepih == null)
            {
                _logger.LogError("Storefront catalog details: no product was found matching ID {Id}.", id);
                return NotFound("No product was found with this ID.");
            }

            return View(tepih);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            if (tepih == null)
            {
                _logger.LogError("Storefront catalog delete: no product was found matching ID {Id}.", id);
                return NotFound("No product was found for deleting.");
            }

            return View("~/Views/InventoryItem/Delete.cshtml", tepih);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteTepih(int id)
        {
            var tepih = await _tepihRepository.GetByIdAsync(id);
            if (tepih == null)
            {
                _logger.LogWarning("Storefront catalog delete: Tepih with ID {Id} not found.", id);
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
                    _logger.LogWarning(ex, "Failed to delete QR code image for product {Id} with URL {Url}.", id, tepih.QRCodeUrl);
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
                _logger.LogError(ex, "Error while disabling Tepih with ID {Id}.", id);
                return StatusCode(500, "An error occurred while deleting the product.");
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var tepih = await _context.Tepisi
                .Include(t => t.ProductImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tepih == null)
            {
                _logger.LogWarning("Storefront catalog edit: Tepih with ID {Id} not found.", id);
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
                _logger.LogWarning("Storefront catalog edit post: ModelState is invalid.");
                tepihVM.ProductImages = await LoadProductImagesAsync(id);
                return View("Edit", tepihVM);
            }

            var existingProduct = await _tepihRepository.GetByIdAsyncNoTracking(id);
            if (existingProduct == null)
            {
                _logger.LogWarning("Storefront catalog edit post: Tepih with ID {Id} not found.", id);
                return NotFound("Product not found.");
            }

            var normalizedSlug = await ResolveSlugAsync(id, tepihVM, existingProduct);
            if (normalizedSlug == null)
            {
                tepihVM.ProductImages = await LoadProductImagesAsync(id);
                tepihVM.ReservedQuantity = existingProduct.ReservedQuantity;
                tepihVM.AvailableQuantity = CalculateAvailableQuantity(existingProduct);
                return View("Edit", tepihVM);
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
                OnlinePrice = tepihVM.OnlinePrice ?? tepihVM.Price,
                PerM2 = tepihVM.PerM2,
                Description = tepihVM.Description,
                ShortDescription = tepihVM.ShortDescription,
                SeoTitle = tepihVM.SeoTitle,
                SeoDescription = tepihVM.SeoDescription,
                Slug = normalizedSlug,
                IsPublished = tepihVM.IsPublished,
                ReservedQuantity = existingProduct.ReservedQuantity,
                RowVersion = existingProduct.RowVersion,
                Disabled = tepihVM.Disabled
            };

            try
            {
                _tepihRepository.Update(tepihEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storefront catalog edit post: editing product with ID {Id} failed.", id);
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
    }
}
