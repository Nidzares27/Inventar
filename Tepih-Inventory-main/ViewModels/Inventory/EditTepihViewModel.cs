using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Inventory
{
    public class EditTepihViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ProductNumber { get; set; }
        public string Model { get; set; }
        [Required]
        [StringLength(100)]
        [Display(Name = "Broader Category")]
        public string BroaderCategory { get; set; } = string.Empty;
        [Required]
        [StringLength(100)]
        [Display(Name = "Narrower Category")]
        public string NarrowerCategory { get; set; } = string.Empty;

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
        [Range(0, int.MaxValue)]
        public decimal? OnlinePrice { get; set; }
        public bool PerM2 { get; set; }
        public bool PoMjeri { get; set; }
        [StringLength(6)]
        public string? UnID { get; set; }
        public string? RemainingSize { get; set; }
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? Slug { get; set; }
        public bool IsPublished { get; set; }
        public bool CopyDescriptionsToGroup { get; set; }
        public bool CopyDescriptionsToIdenticalPoMjeri { get; set; }
        [Range(0, int.MaxValue)]
        public int ReservedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public bool Disabled { get; set; }
        public List<StorefrontProductImageViewModel> ProductImages { get; set; } = new();
    }
}
