using System.ComponentModel.DataAnnotations;
using Pehlione.Data;

namespace Pehlione.Models.ViewModels.Staff;

public sealed class ItCreatePersonnelVm
{
    [Required]
    [EmailAddress]
    [Display(Name = "Personel E-posta")]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Gecici Sifre")]
    public string Password { get; set; } = "";

    [Required]
    [Display(Name = "Rol")]
    public string Role { get; set; } = IdentitySeed.RoleStaff;
}
