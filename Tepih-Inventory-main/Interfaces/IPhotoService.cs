using CloudinaryDotNet.Actions;

namespace Inventar.Interfaces
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> UploadToCloudinary(string filePath, MemoryStream stream);
        Task<DeletionResult> DeletePhotoAsync(string publicId);
    }
}
