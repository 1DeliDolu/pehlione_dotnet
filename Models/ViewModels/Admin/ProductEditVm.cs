using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductEditVm
{
    [Required]
    public int Id { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Kategori secmelisin.")]
    [Display(Name = "Kategori")]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(160)]
    [Display(Name = "Urun adi")]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(64)]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = "";

    [Required]
    [Range(typeof(decimal), "0.01", "9999999", ErrorMessage = "Fiyat 0.01 ve uzeri olmali.")]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Display(Name = "Aktif mi?")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<ProductCategoryOptionVm> CategoryOptions { get; set; } = Array.Empty<ProductCategoryOptionVm>();
}
