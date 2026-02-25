### A) Hedef

Header’daki ortadaki “pill” menüyü **DB’deki Category kayıtlarından dinamik üretmek** ve mevcut sayfanın kategori slug’ına göre ilgili menü öğesini **active** göstermek (hardcoded menüyü kaldırmak).

---

### B) CLI Komutu

```bash
dotnet watch run
```

---

### C) Dosya Değişiklikleri

#### 1) `./ViewComponents/MainNavViewComponent.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pehlione.ViewModels.Navigation;

namespace Pehlione.ViewComponents;

public sealed class MainNavViewComponent : ViewComponent
{
    private readonly PehlioneDbContext _db;

    public MainNavViewComponent(PehlioneDbContext db)
    {
        _db = db;
    }

    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var activeSlug = GetActiveSlug();

        var categories = await _db.Set<Category>()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new MainNavItemVm
            {
                Name = c.Name,
                Slug = c.Slug
            })
            .ToListAsync();

        var vm = new MainNavVm
        {
            ActiveSlug = activeSlug,
            Categories = categories
        };

        return View(vm);
    }

    private string? GetActiveSlug()
    {
        // Route -> Query fallback (esnek olsun diye birden fazla anahtar deniyoruz)
        var keys = new[] { "categorySlug", "slug", "category" };

        foreach (var key in keys)
        {
            if (ViewContext.RouteData.Values.TryGetValue(key, out var val) && val is not null)
            {
                var s = val.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }

        var qs = HttpContext?.Request?.Query;
        if (qs is not null)
        {
            foreach (var key in keys)
            {
                var s = qs[key].ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }

        return null;
    }
}
```

#### 2) `./ViewModels/Navigation/MainNavItemVm.cs`

```csharp
namespace Pehlione.ViewModels.Navigation;

public sealed class MainNavItemVm
{
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
}
```

#### 3) `./ViewModels/Navigation/MainNavVm.cs`

```csharp
namespace Pehlione.ViewModels.Navigation;

public sealed class MainNavVm
{
    public string? ActiveSlug { get; init; }
    public IReadOnlyList<MainNavItemVm> Categories { get; init; } = Array.Empty<MainNavItemVm>();
}
```

#### 4) `./Views/Shared/Components/MainNav/Default.cshtml`

```cshtml
@model Pehlione.ViewModels.Navigation.MainNavVm

<div class="ph-navsurface mx-lg-auto my-3 my-lg-0">
    <ul class="navbar-nav ph-navsurface__nav">
        <li class="nav-item">
            <a class="nav-link ph-navsurface__link @(Model.ActiveSlug is null ? "active" : "")"
               asp-controller="Catalog" asp-action="Index">
                Neu &amp; angesagt
            </a>
        </li>

        @foreach (var c in Model.Categories)
        {
            var isActive = string.Equals(Model.ActiveSlug, c.Slug, StringComparison.OrdinalIgnoreCase);

            <li class="nav-item">
                <a class="nav-link ph-navsurface__link @(isActive ? "active" : "")"
                   asp-controller="Catalog" asp-action="Index" asp-route-categorySlug="@c.Slug">
                    @c.Name
                </a>
            </li>
        }
    </ul>
</div>
```

#### 5) `./Views/Shared/_Layout.cshtml`

```cshtml
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - Pehlione</title>

    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/Pehlione.styles.css" asp-append-version="true" />
</head>
<body>
<header class="ph-header">
    <!-- Top announcement bar -->
    <div class="ph-topbar">
        <a class="ph-topbar__link" href="#" aria-label="Kampanya: %10 kupon al">
            Hol dir einen 10 %-Gutschein
        </a>
        <button type="button" class="ph-topbar__plus" aria-label="Kampanya detayını aç">
            +
        </button>
    </div>

    <!-- Main navbar -->
    <nav class="navbar navbar-expand-lg ph-navbar">
        <div class="container-fluid px-3 px-lg-4">
            <a class="navbar-brand ph-brand" asp-controller="Home" asp-action="Index">
                <span class="ph-brand__mark">Pehlione</span>
            </a>

            <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#phMainNav"
                    aria-controls="phMainNav" aria-expanded="false" aria-label="Menüyü aç/kapat">
                <span class="navbar-toggler-icon"></span>
            </button>

            <div class="collapse navbar-collapse" id="phMainNav">
                <!-- Center nav (dynamic categories) -->
                @await Component.InvokeAsync("MainNav")

                <!-- Right icon actions -->
                <ul class="navbar-nav ms-lg-auto align-items-lg-center gap-lg-1">
                    <li class="nav-item">
                        <a class="nav-link ph-iconbtn" href="#" aria-label="Ara">
                            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                                <path fill="currentColor" d="M10 4a6 6 0 1 1 0 12A6 6 0 0 1 10 4m0-2a8 8 0 1 0 4.9 14.3l4.4 4.4 1.4-1.4-4.4-4.4A8 8 0 0 0 10 2z"/>
                            </svg>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link ph-iconbtn" href="#" aria-label="Hesabım">
                            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                                <path fill="currentColor" d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4zm0 2c-4.4 0-8 2.2-8 5v1h16v-1c0-2.8-3.6-5-8-5z"/>
                            </svg>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link ph-iconbtn" href="#" aria-label="Sepet">
                            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                                <path fill="currentColor" d="M7 6h14l-2 8H9L7 6zm2 10h10a2 2 0 0 1 2 2v2H7v-2a2 2 0 0 1 2-2zM6 4l2.2 10.2A3.99 3.99 0 0 0 9 14h10a4 4 0 0 0 3.9-3l1.6-7H7.4L7 2H3v2h3z"/>
                            </svg>
                        </a>
                    </li>
                </ul>
            </div>
        </div>
    </nav>
</header>

<div class="container">
    <main role="main" class="pb-3">
        @RenderBody()
    </main>
</div>

<footer class="border-top footer text-muted">
    <div class="container">
        &copy; @DateTime.UtcNow.Year - Pehlione - <a asp-controller="Home" asp-action="Privacy">Privacy</a>
    </div>
</footer>

<script src="~/lib/jquery/dist/jquery.min.js"></script>
<script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
<script src="~/js/site.js" asp-append-version="true"></script>
@await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

#### 6) `./Views/Shared/_Layout.cshtml.css`

```css
/* Header shell */
.ph-header {
  position: sticky;
  top: 0;
  z-index: 1030; /* above content, below modals */
  background: #fff;
}

/* Top announcement bar */
.ph-topbar {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  height: 42px;
  background: #f2f2f2;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
}

.ph-topbar__link {
  color: #111;
  text-decoration: none;
  font-weight: 600;
  letter-spacing: 0.2px;
  padding: 0 56px; /* space for + button */
}

.ph-topbar__link:hover {
  text-decoration: underline;
}

.ph-topbar__plus {
  position: absolute;
  right: 16px;
  top: 50%;
  transform: translateY(-50%);
  width: 34px;
  height: 34px;
  border-radius: 999px;
  border: 1px solid rgba(0, 0, 0, 0.10);
  background: #fff;
  color: #111;
  font-size: 18px;
  line-height: 1;
}

/* Navbar */
.ph-navbar {
  background: #e9e9e9;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
}

.ph-brand {
  font-weight: 800;
  letter-spacing: 0.4px;
}

.ph-brand__mark {
  display: inline-block;
}

/* Center pill navigation surface */
.ph-navsurface {
  background: #fff;
  border-radius: 14px;
  padding: 8px 10px;
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.06);
}

.ph-navsurface__nav {
  flex-direction: row;
  flex-wrap: wrap;
  gap: 6px;
}

.ph-navsurface__link {
  color: #111;
  font-weight: 600;
  border-radius: 999px;
  padding: 8px 10px;
  text-decoration: none;
}

.ph-navsurface__link:hover {
  background: rgba(0, 0, 0, 0.05);
}

.ph-navsurface__link.active {
  background: rgba(0, 0, 0, 0.08);
}

/* Right icon buttons */
.ph-iconbtn {
  width: 40px;
  height: 40px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #111;
}

.ph-iconbtn:hover {
  background: rgba(255, 255, 255, 0.65);
}

/* Small screens: keep the pill readable */
@media (max-width: 991.98px) {
  .ph-navsurface {
    border-radius: 16px;
  }

  .ph-navsurface__nav {
    justify-content: center;
  }
}
```

---

### D) Kısa Açıklama

* Hardcoded menü yerine **ViewComponent** kullandık: layout sade kalır, menü logic’i tek yerde olur.
* Kategorileri EF Core ile `Set<Category>()` üzerinden çektik; DbSet adından bağımsız çalışır.
* `ActiveSlug` route/query’den okunuyor; kategori sayfasında ilgili link otomatik **active** oluyor.
* Menü linkleri `asp-controller/asp-action` ile üretildi (route-safe).
* Bu adım sadece header menüsünü dinamikleştirir; kategoriye tıklayınca hangi sayfaya gideceğini bir sonraki adımda netleştireceğiz.

---

### E) Git Commit

**Commit mesajı:** `Make header nav dynamic from categories`

```bash
git add -A && git commit -m "Make header nav dynamic from categories"
```
