using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Staff;

public sealed class DepartmentConstraintsVm
{
    public List<DepartmentConstraintEditItemVm> Items { get; set; } = new();
}

public sealed class DepartmentConstraintEditItemVm
{
    [Required]
    public string Department { get; set; } = "";

    [Display(Name = "Okuma")]
    public bool CanReadStock { get; set; }

    [Display(Name = "Yazma")]
    public bool CanIncreaseStock { get; set; }

    [Display(Name = "Silme")]
    public bool CanDeleteStock { get; set; }

    [Display(Name = "Maks. Tek Islem Adedi")]
    [Range(1, 100000, ErrorMessage = "Maksimum adet 1 veya daha buyuk olmali.")]
    public int? MaxReceiveQuantity { get; set; }
}
