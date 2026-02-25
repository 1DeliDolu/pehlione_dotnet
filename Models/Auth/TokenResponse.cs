namespace Pehlione.Models.Auth;

public sealed class TokenResponse
{
    public string AccessToken { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}
