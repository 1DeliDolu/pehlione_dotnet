using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Customer;

public sealed class AccountDashboardVm
{
    public ProfileUpdateVm Profile { get; set; } = new();
    public PasswordChangeVm Password { get; set; } = new();
    public AddressEditVm AddressForm { get; set; } = new();
    public PaymentEditVm PaymentForm { get; set; } = new();
    public IReadOnlyList<CustomerOrderHistoryItemVm> Orders { get; set; } = Array.Empty<CustomerOrderHistoryItemVm>();
    public IReadOnlyList<CustomerAddressListItemVm> Addresses { get; set; } = Array.Empty<CustomerAddressListItemVm>();
    public IReadOnlyList<CustomerPaymentListItemVm> Payments { get; set; } = Array.Empty<CustomerPaymentListItemVm>();
}

public sealed class ProfileUpdateVm
{
    [Required]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Display(Name = "Kullanici Adi")]
    public string UserName { get; set; } = "";

    [Display(Name = "Telefon")]
    public string PhoneNumber { get; set; } = "";
}

public sealed class PasswordChangeVm
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut Sifre")]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Sifre")]
    public string NewPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Sifre tekrari ayni olmali.")]
    [Display(Name = "Yeni Sifre Tekrar")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class AddressEditVm
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    [Display(Name = "Sirket")]
    public string? Company { get; set; }

    [Required]
    [Display(Name = "Sokak")]
    public string Street { get; set; } = "";

    [Required]
    [Display(Name = "Kapi No")]
    public string HouseNumber { get; set; } = "";

    [Display(Name = "Adres Satiri 2")]
    public string? AddressLine2 { get; set; }

    [Required]
    [RegularExpression("^\\d{5}$")]
    [Display(Name = "Posta Kodu")]
    public string PostalCode { get; set; } = "";

    [Required]
    [Display(Name = "Sehir")]
    public string City { get; set; } = "";

    [Display(Name = "Eyalet")]
    public string? State { get; set; }

    [Required]
    [RegularExpression("^[A-Z]{2}$")]
    [Display(Name = "Ulke")]
    public string CountryCode { get; set; } = "DE";

    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Varsayilan")]
    public bool IsDefault { get; set; }
}

public sealed class PaymentEditVm
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Odeme Tipi")]
    public int Type { get; set; } = 3;

    [Required]
    [Display(Name = "Banka / Kart Adi")]
    public string DisplayName { get; set; } = "";

    [Display(Name = "IBAN / Referans")]
    public string? ProviderReference { get; set; }

    [Display(Name = "Kart Son 4")]
    [StringLength(4, MinimumLength = 4)]
    public string? CardLast4 { get; set; }

    [Display(Name = "Son Kullanma Ay")]
    public int? ExpMonth { get; set; }

    [Display(Name = "Son Kullanma Yil")]
    public int? ExpYear { get; set; }

    [Display(Name = "Varsayilan")]
    public bool IsDefault { get; set; }
}

public sealed class CustomerOrderHistoryItemVm
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public bool CanCancel { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "";
    public int ItemCount { get; set; }
}

public sealed class CustomerAddressListItemVm
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string AddressLine { get; set; } = "";
    public string CityLine { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class CustomerPaymentListItemVm
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? ProviderReference { get; set; }
    public string? CardLast4 { get; set; }
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }
    public bool IsDefault { get; set; }
}
