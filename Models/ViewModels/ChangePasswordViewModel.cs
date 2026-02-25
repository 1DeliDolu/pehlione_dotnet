using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels;

public sealed class ChangePasswordViewModel
{
    [Required]
    public string? ReturnUrl { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola (tekrar)")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parolalar eslesmiyor.")]
    public string ConfirmNewPassword { get; set; } = "";
}
