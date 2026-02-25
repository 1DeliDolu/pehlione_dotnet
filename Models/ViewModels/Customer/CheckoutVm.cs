using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Customer;

public sealed class CheckoutVm
{
    public int Step { get; set; } = 1;
    public CartVm Cart { get; set; } = new();
    public CheckoutUserStepVm User { get; set; } = new();
    public CheckoutAddressStepVm Address { get; set; } = new();
    public CheckoutPaymentStepVm Payment { get; set; } = new();
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
    [Required]
    [Display(Name = "Adres Basligi")]
    public string Title { get; set; } = "";

    [Required]
    [Display(Name = "Adres Satiri 1")]
    public string Line1 { get; set; } = "";

    [Display(Name = "Adres Satiri 2")]
    public string? Line2 { get; set; }

    [Required]
    [Display(Name = "Sehir")]
    public string City { get; set; } = "";

    [Display(Name = "Posta Kodu")]
    public string? PostalCode { get; set; }

    [Required]
    [Display(Name = "Ulke")]
    public string Country { get; set; } = "TR";
}

public sealed class CheckoutPaymentStepVm
{
    [Required]
    [Display(Name = "Odeme Yontemi")]
    public string Method { get; set; } = "Card";

    [Display(Name = "Kart Uzerindeki Isim")]
    public string? CardHolder { get; set; }

    [Display(Name = "Kart Son 4 Hane")]
    [StringLength(4, MinimumLength = 4)]
    public string? CardLast4 { get; set; }
}
