using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Inventar.Helpers;
using Inventar.Interfaces;
using Microsoft.Extensions.Options;

namespace Inventar.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;

        public PhotoService(IOptions<CloudinarySettings> config)
        {
            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
                );
            _cloudinary = new Cloudinary(acc);
        }
        public async Task<ImageUploadResult> UploadToCloudinary(string filePath, MemoryStream stream)
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(filePath, stream),
                Folder = "TepisiQRCodes"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult;
        }
        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            return result;
        }
    }
}
