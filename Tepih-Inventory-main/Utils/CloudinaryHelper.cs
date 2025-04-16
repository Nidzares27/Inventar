namespace Inventar.Utils
{
    public class CloudinaryHelper
    {
        public static string GetPublicIdFromUrlFromFolder(string url)
        {
            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments;

                if (segments.Length >= 3)
                {
                    var publicIdWithExtension = string.Join("", segments.Skip(segments.Length - 2));  // Skip the version segment
                    var publicId = System.IO.Path.GetFileNameWithoutExtension(publicIdWithExtension);
                    var exstension = System.IO.Path.GetExtension(publicIdWithExtension);
                    var publicIdWithFolderName = publicIdWithExtension.Remove((publicIdWithExtension.Length - exstension.Length), exstension.Length);
                    return publicIdWithFolderName;
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                throw new Exception("Invalid URL format", ex);
            }

            return null;
        }
    }
}
