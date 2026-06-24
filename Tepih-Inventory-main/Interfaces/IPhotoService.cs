using CloudinaryDotNet.Actions;

namespace Inventar.Interfaces
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> UploadToCloudinary(string filePath, Stream stream, string folder);
        Task<(string PublicId, string SecureUrl, string MediaType)> UploadStorefrontMediaToCloudinary(
            string filePath,
            Stream stream,
            string folder,
            string mediaType);
        Task<DeletionResult> DeletePhotoAsync(string publicId);
        Task<DeletionResult> DeleteMediaAsync(string publicId, string mediaType);
    }
}
