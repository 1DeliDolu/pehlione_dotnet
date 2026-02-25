using System.ComponentModel.DataAnnotations;
using Pehlione.Data;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CreateUserVm
{
    [Required]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = "";

    [Required]
    [Display(Name = "Rol")]
    public string Role { get; set; } = IdentitySeed.RoleCustomer;
}
