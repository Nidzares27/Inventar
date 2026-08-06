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
    [Authorize(Roles = "admin,superadmin,employee")]
    public class StorefrontCatalogController : Controller
    {
        private readonly ITepihRepository _tepihRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly ILogger<StorefrontCatalogController> _logger;
        private readonly IWebHostEnvironment _env;

        public StorefrontCatalogController(
            ITepihRepository tepihRepository,
            ApplicationDbContext context,
            IPhotoService photoService,
            ILogger<StorefrontCatalogController> logger,
            IWebHostEnvironment env)
        {
            _tepihRepository = tepihRepository;
            _context = context;
            _photoService = photoService;
            _logger = logger;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var tepisi = await _tepihRepository.GetAllUndisabledAsync() ?? new List<Tepih>();
                TextEncodingHelper.DecodeProductsForDisplay(tepisi);
                var productIds = tepisi.Select(product => product.Id).ToList();
                var poMjeriProducts = tepisi.Where(product => product.PoMjeri).ToList();
                var activeReservations = await _context.InventoryReservations
                    .AsNoTracking()
                    .Where(reservation =>
                        productIds.Contains(reservation.TepihId) &&
                        reservation.Status == InventoryReservationStatuses.Active)
                    .Select(reservation => new ActiveReservationDisplaySeed
                    {
                        TepihId = reservation.TepihId,
                        Quantity = reservation.Quantity,
                        CutWidth = reservation.CutWidth,
                        CutLength = reservation.CutLength
                    })
                    .ToListAsync();

                var remainingLengths = await BuildRemainingLengthLookupAsync(poMjeriProducts);
                ViewBag.ReservedDisplay = BuildReservedDisplayLookup(tepisi, activeReservations);
                ViewBag.RemainingLengths = remainingLengths;
                ViewBag.RemainingSizeDisplay = BuildRemainingSizeLookup(poMjeriProducts, remainingLengths);
                return View(tepisi);
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

            TextEncodingHelper.DecodeProductForDisplay(tepih);
            tepih.ProductImages = tepih.ProductImages
                .Where(image => !image.Disabled && ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId))
                .OrderBy(image => image.SortOrder)
                .ToList();

            return View(tepih);
        }

        [Authorize(Roles = "admin,superadmin")]
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
        [Authorize(Roles = "admin,superadmin")]
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
                try
                {
                    if (CloudinaryHelper.TryGetPublicIdFromUrlFromFolder(tepih.QRCodeUrl, out var publicId))
                    {
                        await _photoService.DeletePhotoAsync(publicId);
                    }
                    else if (QrCodeStorageHelper.TryMapLocalUrlToFilePath(
                                 _env.WebRootPath,
                                 tepih.QRCodeUrl,
                                 out var localFilePath) &&
                             System.IO.File.Exists(localFilePath))
                    {
                        System.IO.File.Delete(localFilePath);
                    }
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

        [Authorize(Roles = "admin,superadmin")]
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

            TextEncodingHelper.DecodeProductForDisplay(tepih);

            string? remainingSize = null;
            if (tepih.PoMjeri)
            {
                var sales = await _context.Prodaje
                    .Where(sale => sale.TepihId == tepih.Id && !sale.Disabled)
                    .AsNoTracking()
                    .ToListAsync();

                remainingSize = PoMjeriHelper.FormatRemainingSize(tepih.Width, PoMjeriHelper.CalculateRemainingLength(tepih, sales));
            }

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
                RemainingSize = remainingSize,
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
            tepihVM.Name = TextEncodingHelper.NormalizeInput(tepihVM.Name) ?? string.Empty;
            tepihVM.ProductNumber = TextEncodingHelper.NormalizeInput(tepihVM.ProductNumber) ?? string.Empty;
            tepihVM.Model = TextEncodingHelper.NormalizeInput(tepihVM.Model) ?? string.Empty;
            tepihVM.Color = TextEncodingHelper.NormalizeInput(tepihVM.Color) ?? string.Empty;
            tepihVM.BroaderCategory = TextEncodingHelper.NormalizeInput(tepihVM.BroaderCategory);
            tepihVM.NarrowerCategory = TextEncodingHelper.NormalizeInput(tepihVM.NarrowerCategory);
            tepihVM.Description = TextEncodingHelper.NormalizeInput(tepihVM.Description);
            tepihVM.ShortDescription = TextEncodingHelper.NormalizeInput(tepihVM.ShortDescription);
            tepihVM.SeoTitle = TextEncodingHelper.NormalizeInput(tepihVM.SeoTitle);
            tepihVM.SeoDescription = TextEncodingHelper.NormalizeInput(tepihVM.SeoDescription);
            tepihVM.Slug = TextEncodingHelper.NormalizeInput(tepihVM.Slug);

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

            if (User.IsInRole("admin") && !User.IsInRole("superadmin"))
            {
                tepihVM.Slug = existingProduct.Slug;
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
                BroaderCategory = ProductCategoryHelper.Normalize(tepihVM.BroaderCategory),
                NarrowerCategory = ProductCategoryHelper.Normalize(tepihVM.NarrowerCategory),
                DateTime = tepihVM.DateTime,
                Quantity = tepihVM.Quantity,
                QRCodeUrl = tepihVM.QRCodeUrl,
                Length = existingProduct.Length,
                Width = existingProduct.Width,
                Color = tepihVM.Color,
                Price = tepihVM.Price,
                OnlinePrice = tepihVM.OnlinePrice ?? tepihVM.Price,
                PerM2 = existingProduct.PoMjeri ? true : tepihVM.PerM2,
                PoMjeri = existingProduct.PoMjeri,
                UnID = existingProduct.UnID,
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

                var shouldStayOnEdit = tepihVM.CopyDescriptionsToGroup
                    || (tepihVM.CopyDescriptionsToIdenticalPoMjeri && tepihEdit.PoMjeri);
                var messageParts = new List<string> { "Product updated." };

                if (tepihVM.CopyDescriptionsToGroup)
                {
                    var copiedProducts = await CopyDescriptionsToGroupAsync(tepihEdit);
                    messageParts.Add(copiedProducts > 0
                        ? $"Description and short description copied to {copiedProducts} other products in the same Name + Model group."
                        : "There were no other products in this Name + Model group to update.");
                }

                if (tepihVM.CopyDescriptionsToIdenticalPoMjeri && tepihEdit.PoMjeri)
                {
                    var copiedProducts = await CopyDescriptionsToIdenticalPoMjeriAsync(tepihEdit);
                    messageParts.Add(copiedProducts > 0
                        ? $"Description and short description copied to {copiedProducts} identical po mjeri products."
                        : "There were no other identical po mjeri products to update.");
                }

                TempData["StorefrontSuccessMessage"] = string.Join(" ", messageParts);
                return shouldStayOnEdit
                    ? RedirectToAction(nameof(Edit), new { id })
                    : RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storefront catalog edit post: editing product with ID {Id} failed.", id);
                return StatusCode(500, "An error occurred while editing the product.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> UploadStorefrontImages(int id, List<IFormFile> files, string? altText, bool reuseForGroup)
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
                TempData["StorefrontErrorMessage"] = "Select at least one photo or video to upload.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var activeImages = tepih.ProductImages
                .Where(image => !image.Disabled && ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId))
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
                    TempData["StorefrontErrorMessage"] = $"'{file.FileName}' nije podržan fajl.";
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
                        "StorefrontProducts",
                        mediaType);
                }
                catch (ApplicationException ex)
                {
                    _logger.LogWarning(ex, "Storefront catalog media upload failed for product {ProductId} and file {FileName}.", id, file.FileName);
                    TempData["StorefrontErrorMessage"] = BuildDetailedStorefrontImageUploadErrorMessage(ex);
                    break;
                }

                if (string.IsNullOrWhiteSpace(uploadResult.SecureUrl) || string.IsNullOrWhiteSpace(uploadResult.PublicId))
                {
                    TempData["StorefrontErrorMessage"] = $"Upload failed for '{file.FileName}'.";
                    continue;
                }

                var imageSeed = new ProductImageSeed
                {
                    CloudinaryPublicId = uploadResult.PublicId,
                    Url = uploadResult.SecureUrl,
                    ThumbnailUrl = mediaType == "video" ? null : uploadResult.SecureUrl,
                    AltText = string.IsNullOrWhiteSpace(altText) ? tepih.Name : (TextEncodingHelper.NormalizeInput(altText) ?? altText.Trim()),
                    IsPrimary = !hasPrimary && mediaType == "image",
                    MediaType = uploadResult.MediaType
                };

                var createdImage = new ProductImage
                {
                    TepihId = tepih.Id,
                    CloudinaryPublicId = imageSeed.CloudinaryPublicId,
                    Url = imageSeed.Url,
                    ThumbnailUrl = imageSeed.ThumbnailUrl,
                    AltText = imageSeed.AltText,
                    IsPrimary = imageSeed.IsPrimary,
                    MediaType = imageSeed.MediaType,
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
            if (successfulUploads > 0)
            {
                await _context.SaveChangesAsync();
            }

            if (reuseForGroup && uploadedImageSeeds.Count > 0)
            {
                reusedImages = await CopyMissingImagesToGroupMembersAsync(tepih, uploadedImageSeeds);
                if (reusedImages > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }

            if (successfulUploads > 0)
            {
                var uploadMessage = successfulUploads == 1
                    ? "1 media file uploaded."
                    : $"{successfulUploads} media files uploaded.";

                TempData["StorefrontSuccessMessage"] = reusedImages > 0
                    ? $"{uploadMessage} Reused on {reusedImages} additional product image slots in the same group."
                    : uploadMessage;
            }
            else if (TempData["StorefrontErrorMessage"] == null)
            {
                TempData["StorefrontErrorMessage"] = "No media files were uploaded.";
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        private static string BuildStorefrontImageUploadErrorMessage(Exception ex)
        {
            if (ex.Message.Contains("Cloudinary is not configured", StringComparison.OrdinalIgnoreCase) ||
                ex.InnerException?.Message.Contains("Cloudinary is not configured", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "Cloudinary nije konfigurisan. Dodajte ispravan ApiSecret prije otpremanja medija proizvoda.";
            }

            return "Došlo je do greške prilikom otpremanja medija proizvoda.";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> SyncGroupStorefrontImages(int id)
        {
            var tepih = await _context.Tepisi
                .FirstOrDefaultAsync(t => t.Id == id && !t.Disabled);

            if (tepih == null)
            {
                return NotFound("Product not found.");
            }

            var groupProducts = await LoadGroupProductsAsync(tepih);
            if (groupProducts.Count < 2)
            {
                TempData["StorefrontErrorMessage"] = "There are no other products in this Name + Model group yet.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var sourceImages = groupProducts
                .OrderBy(product => product.Id == tepih.Id ? 0 : 1)
                .ThenBy(product => product.Id)
                .SelectMany(product => product.ProductImages
                    .Where(image => !image.Disabled && ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId))
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .ThenBy(image => image.Id))
                .Select(BuildImageSeed)
                .GroupBy(BuildImageIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (sourceImages.Count == 0)
            {
                TempData["StorefrontErrorMessage"] = "This group has no active storefront media to sync.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var copiedImages = await CopyMissingImagesToGroupMembersAsync(tepih, sourceImages);
            await _context.SaveChangesAsync();

            TempData["StorefrontSuccessMessage"] = copiedImages > 0
                ? $"Synced {copiedImages} missing product media entries across this group."
                : "All products in this group already have these media files.";

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> SetPrimaryStorefrontImage(int id, int imageId)
        {
            var images = await _context.ProductImages
                .Where(image =>
                    image.TepihId == id &&
                    !image.Disabled &&
                    !image.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
                .ToListAsync();

            if (images.Count == 0)
            {
                return NotFound("No product images found.");
            }

            if (!images.Any(image => image.Id == imageId))
            {
                return NotFound("Product image not found.");
            }

            var selectedImage = images.First(image => image.Id == imageId);
            if (IsVideoMediaType(selectedImage.MediaType))
            {
                TempData["StorefrontErrorMessage"] = "Video ne može biti postavljen kao glavna fotografija proizvoda.";
                return RedirectToAction(nameof(Edit), new { id });
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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> DeleteStorefrontImage(int id, int imageId)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(productImage =>
                    productImage.Id == imageId &&
                    productImage.TepihId == id &&
                    !productImage.Disabled &&
                    !productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix));

            if (image == null)
            {
                return NotFound("Product image not found.");
            }

            try
            {
                var isSharedByOtherProducts = await _context.ProductImages
                    .AnyAsync(productImage =>
                        !productImage.Disabled &&
                        productImage.Id != image.Id &&
                        !productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix) &&
                        productImage.CloudinaryPublicId == image.CloudinaryPublicId &&
                        productImage.MediaType == image.MediaType);

                if (!isSharedByOtherProducts)
                {
                    await _photoService.DeleteMediaAsync(image.CloudinaryPublicId, image.MediaType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete storefront media {ImageId} from Cloudinary.", imageId);
                TempData["StorefrontErrorMessage"] = "Cloudinary media deletion failed. The record was still removed from the catalog.";
            }

            image.Disabled = true;
            image.IsPrimary = false;

            var replacementPrimary = await _context.ProductImages
                .Where(productImage =>
                    productImage.TepihId == id &&
                    !productImage.Disabled &&
                    productImage.Id != imageId &&
                    !productImage.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix) &&
                    productImage.MediaType != "video")
                .OrderBy(productImage => productImage.SortOrder)
                .FirstOrDefaultAsync();

            if (replacementPrimary != null)
            {
                replacementPrimary.IsPrimary = true;
            }

            await _context.SaveChangesAsync();
            TempData["StorefrontSuccessMessage"] = "Media removed.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        private static int CalculateAvailableQuantity(Tepih tepih)
        {
            return Math.Max(tepih.Quantity - tepih.ReservedQuantity, 0);
        }

        private async Task<Dictionary<int, int>> BuildRemainingLengthLookupAsync(IReadOnlyCollection<Tepih> poMjeriProducts)
        {
            if (poMjeriProducts.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var poMjeriProductIds = poMjeriProducts.Select(product => product.Id).ToList();
            var sales = await _context.Prodaje
                .AsNoTracking()
                .Where(sale => poMjeriProductIds.Contains(sale.TepihId) && !sale.Disabled)
                .ToListAsync();

            var salesLookup = sales
                .GroupBy(sale => sale.TepihId)
                .ToDictionary(group => group.Key, group => (IReadOnlyCollection<Prodaja>)group.ToList());

            return poMjeriProducts.ToDictionary(
                product => product.Id,
                product =>
                {
                    var productSales = salesLookup.TryGetValue(product.Id, out var matchedSales)
                        ? matchedSales
                        : Array.Empty<Prodaja>();

                    return PoMjeriHelper.CalculateRemainingLength(product, productSales);
                });
        }

        private static Dictionary<int, string> BuildRemainingSizeLookup(
            IReadOnlyCollection<Tepih> poMjeriProducts,
            IReadOnlyDictionary<int, int> remainingLengths)
        {
            return poMjeriProducts.ToDictionary(
                product => product.Id,
                product =>
                {
                    var displayLength = remainingLengths.TryGetValue(product.Id, out var remainingLength)
                        ? remainingLength
                        : product.Length ?? 0;

                    return PoMjeriHelper.FormatRemainingSize(product.Width, displayLength) ?? "-";
                });
        }

        private static List<StorefrontProductImageViewModel> MapProductImages(IEnumerable<ProductImage>? productImages)
        {
            return productImages?
                .Where(image => !image.Disabled && ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId))
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
                    !image.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
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

        private async Task<List<Tepih>> LoadGroupProductsAsync(Tepih product)
        {
            return await _context.Tepisi
                .Include(tepih => tepih.ProductImages)
                .Where(tepih =>
                    !tepih.Disabled &&
                    tepih.Name == product.Name &&
                    tepih.Model == product.Model)
                .OrderBy(tepih => tepih.Id)
                .ToListAsync();
        }

        private async Task<int> CopyMissingImagesToGroupMembersAsync(Tepih sourceProduct, IReadOnlyCollection<ProductImageSeed> sourceImages)
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
                    product.Model == sourceProduct.Model)
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
                        !image.CloudinaryPublicId.StartsWith(ProductMediaFolders.InventoryGalleryFolderPrefix))
                    .Where(image => image.TepihId == targetProduct.Id)
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

        private async Task<int> CopyDescriptionsToGroupAsync(Tepih sourceProduct)
        {
            var siblingProducts = await _context.Tepisi
                .Where(product =>
                    !product.Disabled &&
                    product.Id != sourceProduct.Id &&
                    product.Name == sourceProduct.Name &&
                    product.Model == sourceProduct.Model)
                .ToListAsync();

            foreach (var sibling in siblingProducts)
            {
                sibling.Description = sourceProduct.Description;
                sibling.ShortDescription = sourceProduct.ShortDescription;
            }

            if (siblingProducts.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return siblingProducts.Count;
        }

        private async Task<int> CopyDescriptionsToIdenticalPoMjeriAsync(Tepih sourceProduct)
        {
            if (!sourceProduct.PoMjeri)
            {
                return 0;
            }

            var siblingProducts = await _context.Tepisi
                .Where(product =>
                    !product.Disabled &&
                    product.Id != sourceProduct.Id &&
                    product.PoMjeri &&
                    product.Name == sourceProduct.Name &&
                    product.ProductNumber == sourceProduct.ProductNumber &&
                    product.Model == sourceProduct.Model &&
                    product.Color == sourceProduct.Color &&
                    product.Width == sourceProduct.Width &&
                    product.Length == sourceProduct.Length)
                .ToListAsync();

            foreach (var sibling in siblingProducts)
            {
                sibling.Description = sourceProduct.Description;
                sibling.ShortDescription = sourceProduct.ShortDescription;
            }

            if (siblingProducts.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return siblingProducts.Count;
        }

        private static Dictionary<int, string> BuildReservedDisplayLookup(
            IEnumerable<Tepih> products,
            IReadOnlyCollection<ActiveReservationDisplaySeed> reservations)
        {
            var lookup = new Dictionary<int, string>();

            foreach (var product in products)
            {
                if (!product.PoMjeri)
                {
                    lookup[product.Id] = product.ReservedQuantity.ToString();
                    continue;
                }

                var productReservations = reservations
                    .Where(reservation => reservation.TepihId == product.Id)
                    .ToList();

                if (productReservations.Count == 0)
                {
                    lookup[product.Id] = "0";
                    continue;
                }

                var formattedReservations = productReservations
                    .GroupBy(
                        reservation => new { reservation.CutWidth, reservation.CutLength },
                        reservation => reservation.Quantity)
                    .Select(group =>
                    {
                        var totalQuantity = group.Sum();
                        var sizeLabel = group.Key.CutWidth.HasValue && group.Key.CutLength.HasValue
                            ? $"{group.Key.CutWidth}x{group.Key.CutLength}"
                            : "custom";

                        return $"{totalQuantity} x {sizeLabel}";
                    })
                    .ToList();

                lookup[product.Id] = formattedReservations.Count > 0
                    ? string.Join(", ", formattedReservations)
                    : "0";
            }

            return lookup;
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

        private static string BuildDetailedStorefrontImageUploadErrorMessage(Exception ex)
        {
            var detail = ExtractUploadFailureDetail(ex);

            if (ex.Message.Contains("Cloudinary is not configured", StringComparison.OrdinalIgnoreCase) ||
                ex.InnerException?.Message.Contains("Cloudinary is not configured", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "Cloudinary nije konfigurisan. Dodajte ispravan ApiSecret prije otpremanja medija proizvoda.";
            }

            return string.IsNullOrWhiteSpace(detail)
                ? "Došlo je do greške prilikom otpremanja medija proizvoda."
                : $"Došlo je do greške prilikom otpremanja medija proizvoda. Detalj: {detail}";
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

                if (candidate.Contains("Failed to upload media to Cloudinary.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate.Trim();
            }

            return null;
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

        private sealed class ActiveReservationDisplaySeed
        {
            public int TepihId { get; init; }
            public int Quantity { get; init; }
            public int? CutWidth { get; init; }
            public int? CutLength { get; init; }
        }
    }
}
