using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Pehlione.Models.ViewModels.Staff;

public sealed class ReceiveStockVm
{
    [Required]
    [Display(Name = "Urun")]
    public int ProductId { get; set; }

    [Range(1, 100000, ErrorMessage = "Adet 1 veya daha buyuk olmali.")]
    [Display(Name = "Adet")]
    public int Quantity { get; set; } = 1;

    [MaxLength(500)]
    [Display(Name = "Aciklama")]
    public string? Note { get; set; }

    public IReadOnlyList<SelectListItem> ProductOptions { get; set; } = Array.Empty<SelectListItem>();
}
