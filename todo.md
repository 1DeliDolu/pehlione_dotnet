A) **Hedef (Türkçe)**
Önce “catalog” temelini kuruyoruz: **Category** ve **Product** entity’lerini ekleyip `PehlioneDbContext` içine bağlayacağız. UI/CRUD’a geçmeden önce domain + EF mapping doğru otursun. (Sonraki adım: migration + DB update.)

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Models/Catalog/Category.cs`

```csharp
namespace Pehlione.Models.Catalog;

public sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    // URL için (ör: "erkek-ayakkabi")
    public string Slug { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
```

`./Models/Catalog/Product.cs`

```csharp
namespace Pehlione.Models.Catalog;

public sealed class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}
```

`./Data/PehlioneDbContext.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models.Catalog;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public sealed class PehlioneDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();

    // Demo API
    public DbSet<Models.TodoItem> TodoItems => Set<Models.TodoItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            b.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Product>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.Sku).IsUnique();

            b.Property(x => x.Price).HasPrecision(18, 2);

            b.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* `Category (1) -> (N) Product` ilişkisini net tanımladık (`CategoryId` FK).
* `Slug` ve `Sku` için **unique index** koyduk (URL ve stok kodu çakışmasın).
* `Price` için `decimal(18,2)` hassasiyetini ayarladık.
* Bu adım sadece model + mapping; **DB’de tablo henüz yok**.
* Sonraki adım: `dotnet ef migrations add CatalogInit` ile migration üretip ardından DB’ye uygulayacağız.

E) **Git Commit**

* Commit mesajı: `Add catalog entities Category and Product`
* Komut:

```bash
git add -A && git commit -m "Add catalog entities Category and Product"
```

Bunu uygulayıp build geçtiyse **“bitti”** yaz. Sonraki adımda tek komutla **migration** üreteceğiz (`dotnet ef migrations add CatalogInit`).
