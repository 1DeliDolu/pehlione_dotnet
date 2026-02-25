using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "E-posta veya kullanici adi")]
    public string EmailOrUserName { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = "";

    [Display(Name = "Beni hatirla")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
