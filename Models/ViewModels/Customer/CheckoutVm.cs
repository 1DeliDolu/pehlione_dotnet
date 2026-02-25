using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Customer;

public sealed class CheckoutVm
{
    public int Step { get; set; } = 1;
    public CartVm Cart { get; set; } = new();
    public CheckoutUserStepVm User { get; set; } = new();
    public CheckoutAddressStepVm Address { get; set; } = new();
    public CheckoutPaymentStepVm Payment { get; set; } = new();
    public IReadOnlyList<CheckoutAddressOptionVm> SavedAddresses { get; set; } = Array.Empty<CheckoutAddressOptionVm>();
    public IReadOnlyList<CheckoutPaymentMethodOptionVm> SavedPaymentMethods { get; set; } = Array.Empty<CheckoutPaymentMethodOptionVm>();
}

public sealed class CheckoutUserStepVm
{
    [Required]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required]
    [Display(Name = "Telefon")]
    public string Phone { get; set; } = "";
}

public sealed class CheckoutAddressStepVm
{
    public int? SelectedAddressId { get; set; }

    [Required]
    [Display(Name = "Adres Basligi")]
    public string Title { get; set; } = "";

    [Required]
    [Display(Name = "Sokak / Cadde (Strasse)")]
    public string Street { get; set; } = "";

    [Required]
    [Display(Name = "Kapi No (Hausnummer)")]
    public string HouseNumber { get; set; } = "";

    [Display(Name = "Adres Satiri 2")]
    public string? Line2 { get; set; }

    [Required]
    [Display(Name = "Sehir (Ort)")]
    public string City { get; set; } = "";

    [Required]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "PLZ 5 haneli olmali (ornek: 10115).")]
    [Display(Name = "Posta Kodu (PLZ)")]
    public string PostalCode { get; set; } = "";

    [Display(Name = "Eyalet (Bundesland)")]
    public string? State { get; set; }

    [Required]
    [RegularExpression(@"^[A-Z]{2}$", ErrorMessage = "Ulke kodu 2 harf olmali (DE).")]
    [Display(Name = "Ulke Kodu")]
    public string CountryCode { get; set; } = "DE";

    [Display(Name = "Telefon (opsiyonel)")]
    public string? PhoneNumber { get; set; }
}

public sealed class CheckoutPaymentStepVm
{
    public int? SelectedPaymentMethodId { get; set; }

    [Required]
    [Display(Name = "Odeme Yontemi")]
    public string Method { get; set; } = "Visa";

    [Display(Name = "Kart Uzerindeki Isim")]
    public string? CardHolder { get; set; }

    [Display(Name = "Kart Son 4 Hane")]
    [StringLength(4, MinimumLength = 4)]
    public string? CardLast4 { get; set; }
}

public sealed class CheckoutAddressOptionVm
{
    public int Id { get; set; }
    public bool IsDefault { get; set; }
    public string Label { get; set; } = "";
}

public sealed class CheckoutPaymentMethodOptionVm
{
    public int Id { get; set; }
    public bool IsDefault { get; set; }
    public string Label { get; set; } = "";
    public string Type { get; set; } = "";
}
