Aşağıdaki yol haritası, **Satın Alma (Purchasing)** birimi bir ürünü “aldığında/stoğa giriş yaptığında” **MySQL (EF Core)** üzerinde **ürün stok adedini artırmayı** güvenli ve ölçeklenebilir şekilde kurgulamak için. Proje yapın şu an temel MVC template. 

## Yol Haritası: “Satın Alma → Stok Artır” Akışı

### 1) Veri modeli: stok kavramını netleştir

* **Product**: Ürün temel bilgileri (Name, SKU, Price vs.)
* **Stock (veya Inventory)**: Ürünün güncel stok adedi (ProductId, Quantity)
* **StockMovement (Ledger)**: Her stok değişikliğinin kaydı (IN/OUT, Quantity, Reason, CreatedAt, CreatedByUserId)

  * Satın alma “ürün aldığında” aslında **StockMovement = IN** oluşturur.
  * “Sadece Product.Quantity arttır” yerine **ledger** tutmak audit + hata ayıklama için çok daha iyi.

**Hedef çıktı:** Stok artışı hem güncel sayıyı günceller, hem de hareket kaydı bırakır.

---

### 2) Transaction + Concurrency: sayıyı doğru artır (kritik)

Stok artırma sırasında iki kişi aynı anda giriş yaparsa **yarış durumu** olur. Çözüm seçenekleri:

* **DB Transaction**: hareket + stok güncellemesi aynı transaction içinde.
* **Optimistic concurrency**: Stock tablosuna `RowVersion`/`ConcurrencyStamp` benzeri alan ekleyip EF Core concurrency kontrolü.
* **Atomic update** (en sağlam): `UPDATE Stock SET Quantity = Quantity + @delta WHERE ProductId=@id;` (EF Core raw SQL veya ExecuteUpdate)

**Hedef çıktı:** aynı anda 10 satın alma girişi gelse bile stok doğru artsın.

---

### 3) Uygulama katmanı: domain service yaklaşımı

MVC controller içine stok mantığını gömmek yerine:

* `InventoryService` (Application/Domain Service)

  * `ReceiveStock(productId, qty, note, userId)` gibi bir metot
  * İçeride: validation + transaction + movement insert + stock update

**Hedef çıktı:** Controller ince, iş kuralı tek yerde.

---

### 4) RBAC: sadece Satın Alma görsün/işlesin

Senin gereksinimlerin: 3 rol ve her rol kendi bölümünü görsün.

* Örn roller: `Purchasing`, `Sales`, `Admin` (veya senin isimlerin)
* Stok giriş ekranları:

  * Controller/Action: `[Authorize(Roles="Purchasing,Admin")]`
  * Menü/UI: kullanıcı rolüne göre link göster

**Hedef çıktı:** Satın alma dışındakiler stok artırma ekranını görmesin/çağıramasın.

---

### 5) UI akışı: “Stok Giriş (IN)” ekranı

Purchasing tarafında tipik ekranlar:

* **Ürün listesi** (filtre + seç)
* “Stok Girişi Yap” formu:

  * ProductId (hidden / select)
  * Quantity (pozitif integer)
  * Note / InvoiceNo (opsiyonel)
* Submit → `POST /Purchasing/Inventory/Receive`

**Hedef çıktı:** Kullanıcı “aldım, şu kadar geldi” deyince stok artsın.

---

### 6) Sistem içi haberleşme: event/bildirim

Stok girişi sonrası diğer birimler bilgilendirilebilir:

* Basit: `Notification` tablosuna “SKU123 stoğa girdi (50)” kaydı
* Biraz daha temiz: Domain event `StockReceivedEvent`

  * Handler: Notification oluşturur, EmailQueue’ya yazar vs.

**Hedef çıktı:** “Birimler arası haberleşme” requirement’ı somutlaşır.

---

### 7) Sipariş sonrası e-posta ile uyum

Bu konu ayrı ama stok mantığıyla bağlanır:

* Sales sipariş oluşturduğunda stok düşer (OUT movement)
* Sipariş sonrası email dev ortamında “pickup directory/log” ile üretilir
* Stok yetersizse sipariş bloklanır ya da backorder akışı tanımlanır

**Hedef çıktı:** IN/OUT ledger aynı altyapı.

---

### 8) Test stratejisi (minimum)

* Unit test: `ReceiveStock` qty validation, negative qty reject
* Integration test: aynı product için ardışık 2 receive → quantity doğru
* Concurrency test (isteğe bağlı): paralel receive → final qty beklenen

---

## Uygulanabilir “Milestone” Sırası (sende adım adım işleyeceğiz)

1. EF Core + MySQL provider + DbContext + ilk migration (senin protokolün zaten bunu 1. adım yapıyor)
2. Product + Stock + StockMovement modelleri ve migration
3. InventoryService (transaction + atomic update)
4. Purchasing area/controller + Receive view
5. RBAC (JWT + roles) ve UI menü saklama
6. Notification/Event handler (stok girişi sonrası bildirim)
7. OUT akışı (sipariş → stok düşürme) + e-posta dev çözümü

---

“**kodlama yapalım**” dediğinde, protokolüne uygun olarak **1. adımdan** (EF Core + MySQL provider, connection string, migration, bağlantı testi) tek komut + tam dosya içerikleriyle başlayacağım.


### A) Hedef

EF Core + MySQL altyapısını projeye ekleyip `ApplicationDbContext` ve `appsettings.json` connection string’i ile **DB’ye bağlanabilecek temel iskeleti** kuracağız (yetkilendirme adımına geçmeden önce şart).

### B) CLI Komutu (maks. 1)

```bash
dotnet add package Pomelo.EntityFrameworkCore.MySql && dotnet add package Microsoft.EntityFrameworkCore.Design && dotnet add package Microsoft.EntityFrameworkCore.Tools
```

### C) Dosya Değişiklikleri (tam içerik)

#### ./appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",

  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=pehlione_dotnet;User=root;Password=D0cker!;TreatTinyAsBoolean=true;"
  }
}
```

#### ./Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// AuthN/AuthZ adımlarını ileride ekleyeceğiz.
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

#### ./Data/ApplicationDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Pehlione.Models;

namespace Pehlione.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<Stock>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProductId).IsUnique();

            e.Property(x => x.Quantity).IsRequired();

            e.HasOne(x => x.Product)
             .WithOne(p => p.Stock)
             .HasForeignKey<Stock>(x => x.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

#### ./Models/Product.cs

```csharp
namespace Pehlione.Models;

public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public Stock? Stock { get; set; }
}
```

#### ./Models/Stock.cs

```csharp
namespace Pehlione.Models;

public sealed class Stock
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public Product Product { get; set; } = null!;
}
```

### D) Kısa Açıklama (en fazla 5 madde)

* Pomelo MySQL provider ile EF Core’u MySQL’e bağladık.
* Connection string’i `appsettings.json` altında `DefaultConnection` olarak tanımladık.
* `ApplicationDbContext` içine `Product` ve `Stock` tablolarını ekledik (min. model).
* `Sku` için unique index koyduk; stokta `ProductId` de unique (1 ürün = 1 stok satırı).
* RBAC (Purchasing sadece artırır, IT siler) adımını **migration + temel DB kurulumu**ndan sonra ekleyeceğiz.

### E) Git Commit

**Commit mesajı:** `Add EF Core MySQL setup and base DbContext`

```bash
git add -A && git commit -m "Add EF Core MySQL setup and base DbContext"
```

“**bitti**” yazınca bir sonraki adımda **EF migration + DB bağlantı testi** (tek komutla) yapacağız.
