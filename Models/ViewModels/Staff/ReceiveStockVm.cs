using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Pehlione.Models.ViewModels.Staff;

public sealed class ReceiveStockVm
{
    [Required]
    [Display(Name = "Ana Kategori")]
    public int TopCategoryId { get; set; }

    [Display(Name = "Alt Grup")]
    public int? SubCategoryId { get; set; }

    [Display(Name = "Alt Grup 2")]
    public int? SubSubCategoryId { get; set; }

    [Required]
    [Display(Name = "Urun")]
    public int ProductId { get; set; }

    [Range(1, 100000, ErrorMessage = "Adet 1 veya daha buyuk olmali.")]
    [Display(Name = "Adet")]
    public int Quantity { get; set; } = 1;

    public IReadOnlyList<SelectListItem> ProductOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> TopCategoryOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> SubCategoryOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> SubSubCategoryOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<ReceiveCategoryOptionVm> AllCategories { get; set; } = Array.Empty<ReceiveCategoryOptionVm>();
    public IReadOnlyList<ReceiveProductOptionVm> AllProducts { get; set; } = Array.Empty<ReceiveProductOptionVm>();
    public IReadOnlyList<StockSnapshotVm> StockSnapshots { get; set; } = Array.Empty<StockSnapshotVm>();
    public IReadOnlyList<StockMovementListItemVm> RecentMovements { get; set; } = Array.Empty<StockMovementListItemVm>();
}

public sealed class ReceiveCategoryOptionVm
{
    public int CategoryId { get; set; }
    public int? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public string Name { get; set; } = "";
}

public sealed class ReceiveProductOptionVm
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
}

public sealed class StockSnapshotVm
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class StockMovementListItemVm
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Type { get; set; } = "";
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
