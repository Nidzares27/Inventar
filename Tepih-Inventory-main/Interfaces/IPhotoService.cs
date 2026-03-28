using CloudinaryDotNet.Actions;

namespace Inventar.Interfaces
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> UploadToCloudinary(string filePath, Stream stream, string folder);
        Task<DeletionResult> DeletePhotoAsync(string publicId);
    }
}
