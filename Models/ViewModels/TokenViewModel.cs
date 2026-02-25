namespace Pehlione.Models.ViewModels;

public sealed class TokenViewModel
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}
