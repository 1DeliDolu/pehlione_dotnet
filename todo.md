A) **Hedef (Türkçe)**
Çalışanlar (Staff), Müşteri (Customer) ve Admin için ayrı “bölüm” iskeletini **MVC Areas** ile kurup her bölümde birer `HomeController/Index` action + view oluşturacağız; böylece ileride JWT + RBAC geldiğinde her rolün sadece kendi alanına erişmesini net biçimde bağlayacağız.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

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

// Areas routing (Admin / Staff / Customer)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

`./Areas/Admin/Controllers/HomeController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

`./Areas/Admin/Views/Home/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Admin";
}

<div class="text-center">
    <h1 class="display-4">Admin Bölümü</h1>
    <p>Bu alan ileride <strong>Admin</strong> rolü ile korunacak (JWT + RBAC).</p>
</div>
```

`./Areas/Staff/Controllers/HomeController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

`./Areas/Staff/Views/Home/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Çalışanlar";
}

<div class="text-center">
    <h1 class="display-4">Çalışanlar (Staff) Bölümü</h1>
    <p>Bu alan ileride <strong>Staff</strong> rolü ile korunacak (JWT + RBAC).</p>
</div>
```

`./Areas/Customer/Controllers/HomeController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

`./Areas/Customer/Views/Home/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Müşteri";
}

<div class="text-center">
    <h1 class="display-4">Müşteri (Customer) Bölümü</h1>
    <p>Bu alan ileride <strong>Customer</strong> rolü ile korunacak (JWT + RBAC).</p>
</div>
```

`./Views/Home/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Home Page";
}

<div class="text-center">
    <h1 class="display-4">Pehlione</h1>
    <p>Prototip alışveriş sitesi - bölüm iskeletleri</p>

    <hr />

    <h2 class="h4">Bölümler</h2>
    <div class="d-flex justify-content-center gap-3 flex-wrap">
        <a class="btn btn-outline-primary" asp-area="Customer" asp-controller="Home" asp-action="Index">
            Müşteri
        </a>
        <a class="btn btn-outline-secondary" asp-area="Staff" asp-controller="Home" asp-action="Index">
            Çalışanlar
        </a>
        <a class="btn btn-outline-danger" asp-area="Admin" asp-controller="Home" asp-action="Index">
            Admin
        </a>
    </div>

    <p class="mt-3 text-muted">
        Not: Şimdilik herkes linkleri görebilir; bir sonraki güvenlik adımlarında (JWT + RBAC) her rol sadece kendi alanına erişebilecek.
    </p>
</div>
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Controller’lar istekleri “action” metodlarıyla karşılar; action’lar çoğunlukla `IActionResult` döndürür ve `View()` gibi helper’larla response seçilir. ([Microsoft Learn][1])
* “Area” kullanımı, uygulamayı **bölümlere ayırmanın** (Admin/Staff/Customer) en temiz yollarından biri; RBAC geldiğinde her area’ya `[Authorize(Roles="...")]` ile net kapı koyacağız. ([Microsoft Learn][1])
* `MapControllerRoute` içinde `{area:exists}` pattern’i olmadan `/Admin` gibi area URL’leri çözülmez; bu yüzden Program.cs’e area route ekledik.
* Her bölüm için `HomeController.Index` + `Views/Home/Index.cshtml` ile en minimal “çalışıyor mu?” sayfasını verdik.
* Bir sonraki adımda bu alanlara **rol bazlı erişim** ekleyeceğiz (JWT + policy/roles) ve ana sayfadaki butonları role göre göstereceğiz. ([Microsoft Learn][1])

E) **Git Commit**

* Commit mesajı: `Add Admin/Staff/Customer areas with Home/Index pages`
* Komut:

```bash
git add -A && git commit -m "Add Admin/Staff/Customer areas with Home/Index pages"
```

Bu adımı yaptıktan sonra tarayıcıda şu URL’leri açıp çalıştığını doğrula: `/Admin`, `/Staff`, `/Customer`. Sonra **“bitti”** yaz.

[1]: https://learn.microsoft.com/de-de/aspnet/core/mvc/controllers/actions?view=aspnetcore-10.0 "Verarbeiten von Anforderungen mit Controllern in ASP.NET Core MVC | Microsoft Learn"
