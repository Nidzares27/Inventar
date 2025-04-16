using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Image;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Inventar.Models;
using iText.Layout.Properties;

namespace Inventar.Controllers
{
    public class PdfController : Controller
    {
        private readonly HttpClient _httpClient;

        public PdfController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpGet("generate-cloudinary-image-pdf")]
        public async Task<IActionResult> GenerateCloudinaryImagePdf(Tepih tepih)
        {
            string cloudinaryImageUrl = tepih.QRCodeUrl;

            // Download the image from Cloudinary
            byte[] imageBytes;
            using (var response = await _httpClient.GetAsync(cloudinaryImageUrl))
            {
                if (!response.IsSuccessStatusCode)
                    return BadRequest("Could not retrieve the image from Cloudinary");

                imageBytes = await response.Content.ReadAsByteArrayAsync();
            }

            // Create a memory stream to save the PDF
            using (var memoryStream = new MemoryStream())
            {
                // Initialize PDF writer and document
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                // Load the image from byte array and add it to the document
                var img = ImageDataFactory.Create(imageBytes);
                var image = new Image(img);

                // Configure image dimensions (e.g., 100x100 pixels)
                float imageWidth = 200;
                float imageHeight = 200;
                image.ScaleToFit(imageWidth, imageHeight);

                float pageHeight = pdf.GetDefaultPageSize().GetHeight();

                document.Add(new Paragraph("Name: " + tepih.Name)
                    .SetFontSize(16)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20)); // Add space before images
                document.Add(new Paragraph("Model: " + tepih.Model)
                    .SetFontSize(16)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20)); // Add space before images
                document.Add(new Paragraph("Size: " + tepih.Length + "X" + tepih.Width)
                    .SetFontSize(16)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20)); // Add space before images
                document.Add(new Paragraph("Color: " + tepih.Color)
                    .SetFontSize(16)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20)); // Add space before images
                document.Add(new Paragraph("Quantity: " + tepih.Quantity)
                    .SetFontSize(16)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20)); // Add space before images
                //document.Add(new Paragraph("Price: " + tepih.PriceM2)
                //    .SetFontSize(16)
                //    .SetBold()
                //    .SetTextAlignment(TextAlignment.CENTER)
                //    .SetMarginBottom(120)); // Add space before images
                // Loop to add the image multiple times in a grid format
                //for (int row = 0; row < 4; row++)
                //{
                //    for (int col = 0; col < 3; col++)
                //    {
                //        // Clone the image to avoid modifying the original
                //        var clonedImage = new Image(img).ScaleToFit(imageWidth, imageHeight);
                //        clonedImage.SetFixedPosition(col * imageWidth, pageHeight - ((row + 1) * imageHeight));
                //        document.Add(clonedImage);
                //    }
                //}

                // Optionally, set image size and alignment
                image.ScaleToFit(500, 700); // Scale image to fit within bounds
                image.SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);

                // Add the image to the document
                document.Add(image);

                // Close the document
                document.Close();

                // Prepare the PDF to be returned as a downloadable file
                byte[] pdfBytes = memoryStream.ToArray();
                return File(pdfBytes, "application/pdf", "CloudinaryImage" + tepih.Id +".pdf");
            }
        }
    }
}
