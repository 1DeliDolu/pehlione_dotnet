using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.Auth;

public sealed class LoginRequest
{
    [Required]
    public string EmailOrUserName { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
