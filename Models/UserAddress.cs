using System.ComponentModel.DataAnnotations;
using Pehlione.Models.Identity;

namespace Pehlione.Models;

public sealed class UserAddress
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Company { get; set; }

    [Required, StringLength(120)]
    public string Street { get; set; } = string.Empty;

    [Required, StringLength(15)]
    public string HouseNumber { get; set; } = string.Empty;

    [StringLength(120)]
    public string? AddressLine2 { get; set; }

    [Required]
    [RegularExpression("^\\d{5}$")]
    public string PostalCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [StringLength(80)]
    public string? State { get; set; }

    [Required]
    [RegularExpression("^[A-Z]{2}$")]
    public string CountryCode { get; set; } = "DE";

    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Required]
    public AddressType Type { get; set; } = AddressType.Shipping;

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
