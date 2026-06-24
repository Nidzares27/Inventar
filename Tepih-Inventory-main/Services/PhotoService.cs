using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Inventar.Controllers;
using Inventar.Helpers;
using Inventar.Interfaces;
using Microsoft.Extensions.Options;

namespace Inventar.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<PhotoService> _logger;
        private readonly bool _isConfigured;

        public PhotoService(IOptions<CloudinarySettings> config, ILogger<PhotoService> logger)
        {
            _isConfigured =
                !string.IsNullOrWhiteSpace(config.Value.CloudName) &&
                !string.IsNullOrWhiteSpace(config.Value.ApiKey) &&
                !string.IsNullOrWhiteSpace(config.Value.ApiSecret);

            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
                );
            _cloudinary = new Cloudinary(acc);
            this._logger = logger;
        }
        public async Task<ImageUploadResult> UploadToCloudinary(string filePath, Stream stream, string folder)
        {
            try
            {
                if (!_isConfigured)
                {
                    throw new InvalidOperationException("Cloudinary is not configured.");
                }

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(filePath, stream),
                    Folder = folder
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                return uploadResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary upload failed for file: {filePath}", filePath);
                if (ex is InvalidOperationException invalidOperationException)
                {
                    throw new ApplicationException(invalidOperationException.Message, ex);
                }

                throw new ApplicationException("Failed to upload image to Cloudinary.", ex);
            }
        }

        public async Task<(string PublicId, string SecureUrl, string MediaType)> UploadStorefrontMediaToCloudinary(
            string filePath,
            Stream stream,
            string folder,
            string mediaType)
        {
            var normalizedMediaType = NormalizeMediaType(mediaType);

            try
            {
                if (!_isConfigured)
                {
                    throw new InvalidOperationException("Cloudinary is not configured.");
                }

                if (normalizedMediaType == "video")
                {
                    var uploadParams = new VideoUploadParams
                    {
                        File = new FileDescription(filePath, stream),
                        Folder = folder
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    return (
                        uploadResult.PublicId ?? string.Empty,
                        uploadResult.SecureUrl?.ToString() ?? string.Empty,
                        normalizedMediaType);
                }

                var imageResult = await UploadToCloudinary(filePath, stream, folder);
                return (
                    imageResult.PublicId ?? string.Empty,
                    imageResult.SecureUrl?.ToString() ?? string.Empty,
                    normalizedMediaType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary storefront media upload failed for file: {filePath}", filePath);
                if (ex is InvalidOperationException invalidOperationException)
                {
                    throw new ApplicationException(invalidOperationException.Message, ex);
                }

                throw new ApplicationException("Failed to upload media to Cloudinary.", ex);
            }
        }

        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            try
            {
                if (!_isConfigured)
                {
                    throw new InvalidOperationException("Cloudinary is not configured.");
                }

                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary deletion failed for publicId: {publicId}", publicId);
                if (ex is InvalidOperationException invalidOperationException)
                {
                    throw new ApplicationException(invalidOperationException.Message, ex);
                }

                throw new ApplicationException("Failed to delete image from Cloudinary.", ex);
            }
        }

        public async Task<DeletionResult> DeleteMediaAsync(string publicId, string mediaType)
        {
            try
            {
                if (!_isConfigured)
                {
                    throw new InvalidOperationException("Cloudinary is not configured.");
                }

                var deleteParams = new DeletionParams(publicId);
                if (NormalizeMediaType(mediaType) == "video")
                {
                    deleteParams.ResourceType = ResourceType.Video;
                }

                return await _cloudinary.DestroyAsync(deleteParams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary media deletion failed for publicId: {publicId}", publicId);
                if (ex is InvalidOperationException invalidOperationException)
                {
                    throw new ApplicationException(invalidOperationException.Message, ex);
                }

                throw new ApplicationException("Failed to delete media from Cloudinary.", ex);
            }
        }

        private static string NormalizeMediaType(string? mediaType)
        {
            return string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase)
                ? "video"
                : "image";
        }
    }
}
