using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryMoveProductsVm
{
    [Required]
    public int SourceCategoryId { get; set; }

    public string SourceName { get; set; } = "";

    public int ProductCount { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Hedef kategori secmelisin.")]
    [Display(Name = "Hedef kategori")]
    public int TargetCategoryId { get; set; }

    public IReadOnlyList<CategoryOptionVm> TargetOptions { get; set; } = Array.Empty<CategoryOptionVm>();
}

public sealed class CategoryOptionVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
