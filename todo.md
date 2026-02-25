### A) Hedef

Kullanıcıların **Almanya adres formatına uygun** (Straße + Hausnummer, 5 haneli PLZ, Ort, opsiyonel Bundesland, ISO ülke kodu) **birden fazla adres** (Teslimat/Fatura) saklayabilmesi için veri modelini ve EF Core DbContext altyapısını ekleyeceğiz. 

### B) CLI Komutu (maksimum 1)

```bash
dotnet ef migrations add AddUserAddresses -c UserDataDbContext
```

### C) Dosya Değişiklikleri (sadece değişen/eklenen dosyalar)

#### 1) `./Models/AddressType.cs`

```csharp
namespace Pehlione.Models;

public enum AddressType
{
    Shipping = 1, // Teslimat
    Billing = 2   // Fatura
}
```

#### 2) `./Models/UserAddress.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models;

public class UserAddress
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(60)]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Firma (opsiyonel)")]
    public string? Company { get; set; }

    // DE: Straße
    [Required, StringLength(120)]
    [Display(Name = "Sokak / Cadde (Straße)")]
    public string Street { get; set; } = string.Empty;

    // DE: Hausnummer (harfli olabilir: 12a)
    [Required, StringLength(15)]
    [Display(Name = "Kapı No (Hausnummer)")]
    public string HouseNumber { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Adres Ek Bilgi (Kat, Daire, c/o)")]
    public string? AddressLine2 { get; set; }

    // DE: PLZ = 5 hane
    [Required]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "PLZ 5 haneli olmalı (örn: 10115).")]
    [Display(Name = "Posta Kodu (PLZ)")]
    public string PostalCode { get; set; } = string.Empty;

    // DE: Ort
    [Required, StringLength(80)]
    [Display(Name = "Şehir (Ort)")]
    public string City { get; set; } = string.Empty;

    // DE: Bundesland (opsiyonel)
    [StringLength(80)]
    [Display(Name = "Eyalet (Bundesland)")]
    public string? State { get; set; }

    // ISO 3166-1 alpha-2 (DE)
    [Required]
    [RegularExpression(@"^[A-Z]{2}$", ErrorMessage = "Ülke kodu ISO 2 harf olmalı (DE).")]
    [Display(Name = "Ülke Kodu")]
    public string CountryCode { get; set; } = "DE";

    [StringLength(30)]
    [Phone]
    [Display(Name = "Telefon (opsiyonel)")]
    public string? PhoneNumber { get; set; }

    [Required]
    [Display(Name = "Adres Tipi")]
    public AddressType Type { get; set; } = AddressType.Shipping;

    [Display(Name = "Varsayılan")]
    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
```

#### 3) `./Data/UserDataDbContext.cs`

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Type, x.IsDefault });
            entity.Property(x => x.CountryCode).HasMaxLength(2);
        });
    }
}
```

#### 4) `./Data/UserDataDbContextFactory.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Pehlione.Data;

public class UserDataDbContextFactory : IDesignTimeDbContextFactory<UserDataDbContext>
{
    public UserDataDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection bulunamadı (appsettings).");

        var options = new DbContextOptionsBuilder<UserDataDbContext>()
            .UseMySql(cs, ServerVersion.AutoDetect(cs))
            .Options;

        return new UserDataDbContext(options);
    }
}
```

### D) Kısa Açıklama (en fazla 5 madde)

* Alman adresi için **Street + HouseNumber** ayrı tutuldu; **PLZ regex ile 5 hane** zorunlu.
* `CountryCode` default **DE** ve **2 harf ISO** validasyonlu.
* `AddressType` ile **Teslimat/Fatura** ayrımı var; `IsDefault` ile varsayılan adres seçimi altyapısı hazır.
* `UserDataDbContextFactory` sayesinde migration üretimi için Program.cs’ye dokunmadan tasarım-zamanı context oluşturuluyor.
* Sonraki adımda bu context’i DI’a ekleyip **“Adreslerim” CRUD ekranlarını** yapacağız.

### E) Git Commit

**Commit mesajı:** `Add German-format user address entity + UserDataDbContext`

```bash
git add -A && git commit -m "Add German-format user address entity + UserDataDbContext"
```

Bu adımı uygulayıp migration oluşunca `bitti` yaz. Sonraki adımda kullanıcı tarafına **Adreslerim (Listele/Ekle/Düzenle/Sil)** ekranlarını ve yetkilendirmeyi ekleyeceğiz.
