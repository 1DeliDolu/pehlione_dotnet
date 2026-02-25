using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryCreateVm
{
    [Required]
    [MaxLength(120)]
    [Display(Name = "Kategori adi")]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(160)]
    [Display(Name = "Slug (orn: erkek-ayakkabi)")]
    public string Slug { get; set; } = "";

    [Display(Name = "Aktif mi?")]
    public bool IsActive { get; set; } = true;
}
