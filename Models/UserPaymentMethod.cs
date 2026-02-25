using System.ComponentModel.DataAnnotations;
using Pehlione.Models.Identity;

namespace Pehlione.Models;

public sealed class UserPaymentMethod
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    public PaymentMethodType Type { get; set; }

    [Required, StringLength(80)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(120)]
    public string? ProviderReference { get; set; }

    [StringLength(4)]
    [RegularExpression("^\\d{4}$")]
    public string? CardLast4 { get; set; }

    [Range(1, 12)]
    public int? ExpMonth { get; set; }

    [Range(2020, 2100)]
    public int? ExpYear { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
