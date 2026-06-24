namespace Inventar.Utils
{
    public static class CloudinaryHelper
    {
        public static bool IsCloudinaryUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Host.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetPublicIdFromUrlFromFolder(string? url, out string publicId)
        {
            publicId = string.Empty;

            if (!IsCloudinaryUrl(url))
            {
                return false;
            }

            try
            {
                var uri = new Uri(url!);
                var segments = uri.Segments;

                if (segments.Length < 3)
                {
                    return false;
                }

                var publicIdWithExtension = string.Join("", segments.Skip(segments.Length - 2));
                var extension = System.IO.Path.GetExtension(publicIdWithExtension);
                publicId = publicIdWithExtension.Remove(publicIdWithExtension.Length - extension.Length, extension.Length);
                return !string.IsNullOrWhiteSpace(publicId);
            }
            catch
            {
                publicId = string.Empty;
                return false;
            }
        }

        public static string GetPublicIdFromUrlFromFolder(string url)
        {
            if (TryGetPublicIdFromUrlFromFolder(url, out var publicId))
            {
                return publicId;
            }

            throw new Exception("Invalid Cloudinary URL format.");
        }
    }
}
