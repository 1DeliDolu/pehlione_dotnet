### A) Hedef

**RBAC temelini** kurup yetkileri net ayıracağız:

* **Purchasing**: sadece **stok artırma** (silme yok)
* **IT**: sadece **silme** (stok/product silme)
  Bunu hem **endpoint** seviyesinde (Policy) hem de **UI** seviyesinde (menü/form görünürlüğü) uygulayacağız.

### B) CLI Komutu (maks. 1 komut)

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
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
  },

  "Jwt": {
    "Issuer": "Pehlione",
    "Audience": "Pehlione",
    "Key": "DEV_ONLY_CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_32+CHARS",
    "ExpiresMinutes": 60
  }
}
```

#### ./Models/ApplicationUser.cs

```csharp
using Microsoft.AspNetCore.Identity;

namespace Pehlione.Models;

public sealed class ApplicationUser : IdentityUser
{
    // İleride: ForcePasswordChange, Department vb. alanlar eklenebilir.
}
```

#### ./Data/ApplicationDbContext.cs

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models;

namespace Pehlione.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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

#### ./Program.cs

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pehlione.Data;
using Pehlione.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// EF Core (MySQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Identity (Roles dahil)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// AuthN: Cookie (UI) + JWT (API/endpoint)
builder.Services.AddAuthentication(options =>
{
    // UI tarafı için cookie default kalsın.
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");
    var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// AuthZ: Policy bazlı net yetki ayrımı
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanIncreaseStock", policy =>
        policy.RequireRole("Purchasing", "Admin"));

    options.AddPolicy("CanDeleteStock", policy =>
        policy.RequireRole("IT", "Admin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

#### ./ViewModels/Inventory/IncreaseStockVm.cs

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.ViewModels.Inventory;

public sealed class IncreaseStockVm
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [Range(1, 1_000_000)]
    public int QuantityToAdd { get; set; }
}
```

#### ./ViewModels/Inventory/DeleteProductVm.cs

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.ViewModels.Inventory;

public sealed class DeleteProductVm
{
    [Required]
    public int ProductId { get; set; }
}
```

#### ./Controllers/InventoryController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.ViewModels.Inventory;

namespace Pehlione.Controllers;

[Authorize]
public sealed class InventoryController : Controller
{
    private readonly ApplicationDbContext _db;

    public InventoryController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Basit demo ekranı (UI tarafında role göre buton/form gösteriyoruz)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                StockQty = p.Stock != null ? p.Stock.Quantity : 0
            })
            .ToListAsync();

        return View(products);
    }

    // Purchasing: sadece stok artırır
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanIncreaseStock")]
    public async Task<IActionResult> Increase(IncreaseStockVm vm)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index));

        var productExists = await _db.Products.AnyAsync(p => p.Id == vm.ProductId);
        if (!productExists)
            return NotFound();

        var stock = await _db.Stocks.SingleOrDefaultAsync(s => s.ProductId == vm.ProductId);
        if (stock is null)
        {
            stock = new Pehlione.Models.Stock
            {
                ProductId = vm.ProductId,
                Quantity = vm.QuantityToAdd
            };
            _db.Stocks.Add(stock);
        }
        else
        {
            stock.Quantity += vm.QuantityToAdd;
        }

        await _db.SaveChangesAsync();
        TempData["Msg"] = "Stok artırıldı.";
        return RedirectToAction(nameof(Index));
    }

    // IT: silme yetkisi sadece burada
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanDeleteStock")]
    public async Task<IActionResult> DeleteProduct(DeleteProductVm vm)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index));

        var product = await _db.Products.SingleOrDefaultAsync(p => p.Id == vm.ProductId);
        if (product is null)
            return NotFound();

        _db.Products.Remove(product); // Stock cascade ile gider.
        await _db.SaveChangesAsync();

        TempData["Msg"] = "Ürün silindi (IT yetkisi).";
        return RedirectToAction(nameof(Index));
    }
}
```

#### ./Views/Inventory/Index.cshtml

```cshtml
@model IEnumerable<dynamic>

@{
    ViewData["Title"] = "Inventory";
}

<h1>Inventory</h1>

@if (TempData["Msg"] is string msg)
{
    <div class="alert alert-info">@msg</div>
}

<table class="table table-striped">
    <thead>
        <tr>
            <th>Id</th>
            <th>Ürün</th>
            <th>SKU</th>
            <th>Stok</th>
            <th>İşlem</th>
        </tr>
    </thead>
    <tbody>
@foreach (var p in Model)
{
        <tr>
            <td>@p.Id</td>
            <td>@p.Name</td>
            <td>@p.Sku</td>
            <td>@p.StockQty</td>
            <td>
                @* Purchasing sadece artırır *@
                @if (User.IsInRole("Purchasing") || User.IsInRole("Admin"))
                {
                    <form asp-action="Increase" method="post" class="d-inline">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="ProductId" value="@p.Id" />
                        <input type="number" name="QuantityToAdd" min="1" value="1" style="width:90px" />
                        <button type="submit" class="btn btn-sm btn-success">+ Arttır</button>
                    </form>
                }

                @* IT sadece siler *@
                @if (User.IsInRole("IT") || User.IsInRole("Admin"))
                {
                    <form asp-action="DeleteProduct" method="post" class="d-inline"
                          onsubmit="return confirm('Ürün silinsin mi? (IT)');">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="ProductId" value="@p.Id" />
                        <button type="submit" class="btn btn-sm btn-danger">Sil</button>
                    </form>
                }
            </td>
        </tr>
}
    </tbody>
</table>
```

### D) Kısa Açıklama (en fazla 5 madde)

* **Policy** ile net ayrım yaptık: `CanIncreaseStock` (Purchasing/Admin) ve `CanDeleteStock` (IT/Admin).
* Endpoint seviyesinde `[Authorize(Policy=...)]` ile “silme asla Purchasing’e açılmaz”.
* UI tarafında `User.IsInRole(...)` ile **buton/form görünürlüğü** role göre kısıtlandı.
* Identity altyapısı eklendi (rol/claim tabanlı yetki için gerekli).
* JWT ayarlarını koyduk; token üretimini bir sonraki adımda yapacağız.

### E) Git Commit

**Commit mesajı:** `Add RBAC policies for stock increase vs delete and Inventory UI`

```bash
git add -A && git commit -m "Add RBAC policies for stock increase vs delete and Inventory UI"
```

“**bitti**” yazınca bir sonraki adımda: **Identity tabloları için migration + update** yapacağız ve ardından **rol seed** (Purchasing/IT/Admin) + dev ortamında test kullanıcılarıyla “Purchasing artırır ama silemez, IT siler ama artıramaz” doğrulamasını tamamlayacağız.
