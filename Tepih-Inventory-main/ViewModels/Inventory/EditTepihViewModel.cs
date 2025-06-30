using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Inventory
{
    public class EditTepihViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ProductNumber { get; set; }
        public string Model { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public string? DateTime { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        public string? QRCodeUrl { get; set; }//provjeriti da li je obavezno
        [Range(0, int.MaxValue)]
        public int? Length { get; set; }
        public int? Width { get; set; }
        public string Color { get; set; }
        [Range(0, int.MaxValue)]
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        public string? Description { get; set; }
        public bool Disabled { get; set; }
    }
}