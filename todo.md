### A) Hedef

Kullanıcının **ödeme yöntemlerini** (4 tip: **Klarna, PayPal, Visa, Mastercard**) güvenli olacak şekilde (kart için **sadece brand/last4/son kullanım**, **CVV/kart no yok**) saklayacağımız veri modelini ekleyip, EF Core ile MySQL tarafında tabloyu oluşturacak migration’ı hazırlayacağız.

### B) CLI Komutu (maksimum 1 komut)

```bash
dotnet ef migrations add AddUserPaymentMethods -c UserDataDbContext
```

### C) Dosya Değişiklikleri (sadece değişen/eklenen dosyalar)

#### 1) `./Models/PaymentMethodType.cs`

```csharp
namespace Pehlione.Models;

public enum PaymentMethodType
{
    Klarna = 1,
    PayPal = 2,
    Visa = 3,
    Mastercard = 4
}
```

#### 2) `./Models/UserPaymentMethod.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models;

public class UserPaymentMethod
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Ödeme Tipi")]
    public PaymentMethodType Type { get; set; }

    // UI'da görünen ad: "PayPal (m***@mail.com)" / "Visa •••• 1234" gibi
    [Required, StringLength(80)]
    [Display(Name = "Görünen Ad")]
    public string DisplayName { get; set; } = string.Empty;

    // Klarna/PayPal için: provider tarafı referansı (token/id) — hassas olmayan
    [StringLength(120)]
    [Display(Name = "Provider Referansı (opsiyonel)")]
    public string? ProviderReference { get; set; }

    // Kartlar için: sadece last4 + son kullanım (CVV / tam kart no ASLA)
    [StringLength(4)]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Son 4 hane 4 rakam olmalı.")]
    [Display(Name = "Son 4 Hane")]
    public string? CardLast4 { get; set; }

    [Range(1, 12)]
    [Display(Name = "Son Kullanım Ay")]
    public int? ExpMonth { get; set; }

    [Range(2020, 2100)]
    [Display(Name = "Son Kullanım Yıl")]
    public int? ExpYear { get; set; }

    [Display(Name = "Varsayılan")]
    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
```

#### 3) `./Data/UserDataDbContext.cs`  *(güncellendi)*

```csharp
using Microsoft.EntityFrameworkCore;
using Pehlione.Models;

namespace Pehlione.Data;

public class UserDataDbContext : DbContext
{
    public UserDataDbContext(DbContextOptions<UserDataDbContext> options) : base(options)
    {
    }

    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<UserPaymentMethod> UserPaymentMethods => Set<UserPaymentMethod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Type, x.IsDefault });
            entity.Property(x => x.CountryCode).HasMaxLength(2);
        });

        modelBuilder.Entity<UserPaymentMethod>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Type, x.IsDefault });
        });
    }
}
```

### D) Kısa Açıklama (en fazla 5 madde)

* **Kart verisini güvenli tuttuk:** tam kart numarası / CVV **saklanmıyor**, sadece `CardLast4 + ExpMonth/ExpYear`.
* Klarna/PayPal gibi provider’lar için `ProviderReference` alanı **opsiyonel** ve “token/id” gibi hassas olmayan referanslar için.
* `DisplayName` ile UI’da kullanıcıya gösterilecek maskelemiş ifade tutulacak.
* `IsDefault` ile varsayılan ödeme yöntemi seçimi altyapısı hazır (kuralı UI/Controller’da enforce edeceğiz).
* Bir sonraki adımda **Controller + View (Ödeme Yöntemlerim CRUD)** ve sadece **Customer** rolünün erişimi eklenecek.

### E) Git Commit

**Commit mesajı:** `Add user payment methods (Klarna/PayPal/Visa/Mastercard)`

```bash
git add -A && git commit -m "Add user payment methods (Klarna/PayPal/Visa/Mastercard)"
```

Migration başarıyla oluşunca `bitti` yaz. Sonraki adımda **Ödeme Yöntemlerim** ekranlarını (listele/ekle/sil/varsayılan yap) ve yetkilendirmeyi ekleyeceğiz.
