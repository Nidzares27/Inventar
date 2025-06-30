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

        public PhotoService(IOptions<CloudinarySettings> config, ILogger<PhotoService> logger)
        {
            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
                );
            _cloudinary = new Cloudinary(acc);
            this._logger = logger;
        }
        public async Task<ImageUploadResult> UploadToCloudinary(string filePath, MemoryStream stream)
        {
            try
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(filePath, stream),
                    Folder = "TepisiQRCodes"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                return uploadResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary upload failed for file: {filePath}", filePath);
                throw new ApplicationException("Failed to upload image to Cloudinary.", ex);
            }
        }
        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            try
            {
                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary deletion failed for publicId: {publicId}", publicId);
                throw new ApplicationException("Failed to delete image from Cloudinary.", ex);
            }
        }
    }
}
