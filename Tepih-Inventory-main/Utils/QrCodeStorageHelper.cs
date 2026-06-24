using Path = System.IO.Path;

namespace Inventar.Utils
{
    public static class QrCodeStorageHelper
    {
        private static readonly string[] FolderSegments = ["generated", "qrcodes"];

        public static string EnsureLocalDirectory(string webRootPath)
        {
            var directoryPath = Path.Combine(new[] { webRootPath }.Concat(FolderSegments).ToArray());
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        public static string BuildLocalUrl(string fileName)
        {
            return "/" + string.Join("/", FolderSegments) + "/" + fileName;
        }

        public static bool TryMapLocalUrlToFilePath(string webRootPath, string? url, out string filePath)
        {
            filePath = string.Empty;

            if (string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return false;
            }

            var relativePath = url
                .Split(['?', '#'], 2)[0]
                .Trim()
                .TrimStart('~')
                .TrimStart('/');

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var webRootFullPath = Path.GetFullPath(webRootPath);
            var candidatePath = Path.GetFullPath(
                Path.Combine(webRootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!candidatePath.StartsWith(webRootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            filePath = candidatePath;
            return true;
        }
    }
}
