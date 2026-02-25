A) **Hedef (Türkçe)**
Pehlione MVC projesini EF Core üzerinden **yerel MySQL**’e bağlayacak altyapıyı kurup, bağlantıyı hızlı doğrulamak için **/db/ping** endpoint’i ekleyeceğiz (RBAC/JWT’ye geçmeden önce “DB’ye gerçekten erişiyor muyuz?” testini netleştireceğiz).

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet add package Pomelo.EntityFrameworkCore.MySql && dotnet add package Microsoft.EntityFrameworkCore.Design && dotnet add package Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Controllers/DbPingController.cs`

```csharp
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Controllers;

[ApiController]
[Route("db")]
public sealed class DbPingController : ControllerBase
{
    private readonly PehlioneDbContext _db;

    public DbPingController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);

            string? provider = _db.Database.ProviderName;
            string? serverVersion = null;

            if (canConnect)
            {
                await using var conn = _db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync(ct);
                }

                serverVersion = conn.ServerVersion;
            }

            return Ok(new
            {
                canConnect,
                provider,
                serverVersion
            });
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Database connection failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
```

`./Data/PehlioneDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace Pehlione.Data;

public sealed class PehlioneDbContext : DbContext
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    // Şimdilik DbSet yok: sadece bağlantı testi yapıyoruz.
    // İlk entity ile birlikte buraya DbSet<...> ekleyeceğiz.
}
```

`./Program.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// EF Core + MySQL
var connectionString = builder.Configuration.GetConnectionString("PehlioneDb")
    ?? throw new InvalidOperationException("Connection string 'PehlioneDb' not found.");

builder.Services.AddDbContext<PehlioneDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// EF Core hata tanılama (dev)
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

`./appsettings.json`

```json
{
  "ConnectionStrings": {
    "PehlioneDb": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

`./appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "PehlioneDb": "server=localhost;port=3306;database=pehlione_dotnet;user=root;password=D0cker!;SslMode=None;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

D) **Kısa Açıklama (en fazla 5 madde)**

* Microsoft’un EF MVC yaklaşımındaki temel fikir: **provider paketi + DbContext + DI kaydı + connection string**. Biz SQL Server yerine MySQL provider’ı kullandık. ([Microsoft Learn][1])
* Tutorial sayfası .NET 6+ minimal hosting farklarına değiniyor; biz zaten tek `Program.cs` (minimal hosting) ile ilerledik. ([Microsoft Learn][1])
* `GET /db/ping` → `canConnect: true` görürsen MySQL bağlantısı OK; false/500 ise connection string / MySQL servis / DB varlığı kontrol edilir.
* `ServerVersion.AutoDetect(...)` MySQL sürümünü otomatik algılar (ilk bağlantıda doğrulama yapar).
* Bir sonraki adımda ilk entity + migration + `dotnet ef database update` ile DB’ye tablo basacağız.

E) **Git Commit**

* Commit mesajı: `Add EF Core MySQL wiring and /db/ping endpoint`
* Komut:

```bash
git add -A && git commit -m "Add EF Core MySQL wiring and /db/ping endpoint"
```

> Bu adımı uygulayıp projeyi çalıştırdıktan sonra tarayıcıdan **/db/ping**’e gidip sonucu gördüğünde **“bitti”** yaz.

[1]: https://learn.microsoft.com/de-de/aspnet/core/data/ef-mvc/intro?view=aspnetcore-10.0 "Tutorial: Erste Schritte mit EF Core in einer ASP.NET Core MVC-Web-App | Microsoft Learn"
