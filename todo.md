A) **Hedef (Türkçe)**
“Birimler arası haberleşme” için **uygulama içi event bus** (in-process) kurup, **Customer** tarafında basit bir “Sipariş ver” akışı ekleyeceğiz. Sipariş verildiğinde **OrderPlaced** eventi yayınlanacak ve ayrı bir handler bu eventi dinleyip **pickup directory’e sipariş onay e-postası** bırakacak.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Services/Events/IEvent.cs`

```csharp
namespace Pehlione.Services.Events;

public interface IEvent
{
}
```

`./Services/Events/IEventHandler.cs`

```csharp
namespace Pehlione.Services.Events;

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}
```

`./Services/Events/IEventBus.cs`

```csharp
namespace Pehlione.Services.Events;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct) where TEvent : IEvent;
}
```

`./Services/Events/InProcessEventBus.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Pehlione.Services.Events;

public sealed class InProcessEventBus : IEventBus
{
    private readonly IServiceProvider _services;

    public InProcessEventBus(IServiceProvider services)
    {
        _services = services;
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct) where TEvent : IEvent
    {
        var handlers = _services.GetServices<IEventHandler<TEvent>>().ToArray();
        foreach (var h in handlers)
        {
            await h.HandleAsync(evt, ct);
        }
    }
}
```

`./Events/OrderPlacedEvent.cs`

```csharp
using Pehlione.Services.Events;

namespace Pehlione.Events;

public sealed class OrderPlacedEvent : IEvent
{
    public string OrderNo { get; init; } = "";
    public string CustomerEmail { get; init; } = "";
    public decimal Total { get; init; }
    public string ItemName { get; init; } = "";
    public DateTime PlacedAtUtc { get; init; }
}
```

`./Services/Events/Handlers/SendOrderConfirmationEmailHandler.cs`

```csharp
using Pehlione.Events;

namespace Pehlione.Services.Events.Handlers;

public sealed class SendOrderConfirmationEmailHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly IAppEmailSender _email;

    public SendOrderConfirmationEmailHandler(IAppEmailSender email)
    {
        _email = email;
    }

    public async Task HandleAsync(OrderPlacedEvent evt, CancellationToken ct)
    {
        var subject = $"Pehlione - Sipariş Onayı ({evt.OrderNo})";
        var body = $@"
            <p>Merhaba,</p>
            <p>Siparişin alındı.</p>
            <ul>
                <li><strong>Sipariş No:</strong> {System.Net.WebUtility.HtmlEncode(evt.OrderNo)}</li>
                <li><strong>Ürün:</strong> {System.Net.WebUtility.HtmlEncode(evt.ItemName)}</li>
                <li><strong>Tutar:</strong> {evt.Total:0.00}</li>
                <li><strong>Tarih (UTC):</strong> {evt.PlacedAtUtc:u}</li>
            </ul>
            <p>Teşekkürler.</p>
        ";

        await _email.SendAsync(evt.CustomerEmail, subject, body, ct);
    }
}
```

`./Models/ViewModels/Customer/PlaceOrderVm.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Customer;

public sealed class PlaceOrderVm
{
    [Required]
    [Display(Name = "Ürün adı")]
    public string ItemName { get; set; } = "";

    [Required]
    [Range(typeof(decimal), "0.01", "9999999")]
    [Display(Name = "Tutar")]
    public decimal Total { get; set; }
}
```

`./Models/ViewModels/Customer/OrderSuccessVm.cs`

```csharp
namespace Pehlione.Models.ViewModels.Customer;

public sealed class OrderSuccessVm
{
    public string OrderNo { get; set; } = "";
    public string Email { get; set; } = "";
}
```

`./Areas/Customer/Controllers/OrdersController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;
using Pehlione.Events;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Customer;
using Pehlione.Services.Events;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize(Roles = IdentitySeed.RoleCustomer)]
public sealed class OrdersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEventBus _bus;

    public OrdersController(UserManager<ApplicationUser> userManager, IEventBus bus)
    {
        _userManager = userManager;
        _bus = bus;
    }

    [HttpGet]
    public IActionResult Checkout()
    {
        return View(new PlaceOrderVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(PlaceOrderVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var email = user.Email ?? user.UserName ?? "unknown@local";

        var orderNo = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 26);

        await _bus.PublishAsync(new OrderPlacedEvent
        {
            OrderNo = orderNo,
            CustomerEmail = email,
            ItemName = model.ItemName,
            Total = model.Total,
            PlacedAtUtc = DateTime.UtcNow
        }, ct);

        return View("Success", new OrderSuccessVm
        {
            OrderNo = orderNo,
            Email = email
        });
    }
}
```

`./Areas/Customer/Views/Orders/Checkout.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Customer.PlaceOrderVm
@{
    ViewData["Title"] = "Checkout";
}

<div class="container" style="max-width: 680px;">
    <h1 class="h3 mb-3">Sipariş Ver (Demo)</h1>

    <form asp-area="Customer" asp-controller="Orders" asp-action="Checkout" method="post">
        @Html.AntiForgeryToken()
        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="ItemName" class="form-label"></label>
            <input asp-for="ItemName" class="form-control" />
            <span asp-validation-for="ItemName" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Total" class="form-label"></label>
            <input asp-for="Total" class="form-control" />
            <span asp-validation-for="Total" class="text-danger"></span>
        </div>

        <button type="submit" class="btn btn-primary">Siparişi ver</button>
        <a class="btn btn-outline-secondary" asp-area="Customer" asp-controller="Home" asp-action="Index">Geri</a>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

`./Areas/Customer/Views/Orders/Success.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Customer.OrderSuccessVm
@{
    ViewData["Title"] = "Sipariş Alındı";
}

<div class="container" style="max-width: 820px;">
    <h1 class="h3 mb-3">Sipariş alındı</h1>

    <div class="alert alert-success">
        Sipariş No: <strong>@Model.OrderNo</strong>
    </div>

    <p class="text-muted mb-3">
        Sipariş onay e-postası dev ortamında pickup directory’e yazıldı:
        <code>App_Data/MailPickup</code>
    </p>

    <a class="btn btn-outline-primary" asp-area="Customer" asp-controller="Orders" asp-action="Checkout">Yeni sipariş</a>
    <a class="btn btn-outline-secondary" asp-area="Customer" asp-controller="Home" asp-action="Index">Müşteri ana sayfa</a>
</div>
```

`./Areas/Customer/Views/Home/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Müşteri";
}

<div class="text-center">
    <h1 class="display-4">Müşteri (Customer) Bölümü</h1>
    <p>Bu alan <strong>Customer</strong> rolü ile korunur.</p>

    <div class="d-flex justify-content-center gap-2 flex-wrap mt-3">
        <a class="btn btn-primary" asp-area="Customer" asp-controller="Orders" asp-action="Checkout">Sipariş Ver (Demo)</a>
    </div>
</div>
```

`./Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pehlione.Data;
using Pehlione.Events;
using Pehlione.Models.Identity;
using Pehlione.Models.Security;
using Pehlione.Services;
using Pehlione.Services.Events;
using Pehlione.Services.Events.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// EF Core + MySQL
var connectionString = builder.Configuration.GetConnectionString("PehlioneDb")
    ?? throw new InvalidOperationException("Connection string 'PehlioneDb' not found.");

builder.Services.AddDbContext<PehlioneDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Identity (Users + Roles)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<PehlioneDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
});

// JWT options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
          ?? throw new InvalidOperationException("Jwt configuration missing.");

if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be set and at least 32 characters.");

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,

            ValidateAudience = true,
            ValidAudience = jwt.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// DEV e-posta (pickup directory). Prod'da şimdilik "no-op".
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IAppEmailSender, DevPickupEmailSender>();
else
    builder.Services.AddSingleton<IAppEmailSender, NullEmailSender>();

// Event bus + handlers
builder.Services.AddScoped<IEventBus, InProcessEventBus>();
builder.Services.AddScoped<IEventHandler<OrderPlacedEvent>, SendOrderConfirmationEmailHandler>();

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

app.UseAuthentication();
app.UseAuthorization();

// DEV: Migration varsa uygula + demo kullanıcıları seed et.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PehlioneDbContext>();

    var hasMigrations = (await db.Database.GetMigrationsAsync()).Any();
    if (hasMigrations)
    {
        await db.Database.MigrateAsync();
        await IdentitySeed.SeedAsync(scope.ServiceProvider);
    }
}

// Attribute-routed API controller'lar için (api/*)
app.MapControllers();

// Areas routing (Admin / Staff / Customer)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* `OrdersController` siparişi “veriyor” ve **OrderPlacedEvent** yayınlıyor; e-posta göndermek controller’ın işi değil.
* `SendOrderConfirmationEmailHandler` eventi dinleyip maili **pickup directory**’e yazar (birimler arası haberleşme = event).
* Bu adımda siparişi DB’ye kaydetmedik; sadece event + e-posta akışını netleştirdik.
* Test: Customer ile giriş → `/Customer/Orders/Checkout` → sipariş ver → `App_Data/MailPickup` altında `.eml` oluşmalı.
* Sonraki adımda: siparişi **EF Core entity** olarak MySQL’e kaydedip (migration) sonra event’i DB kaydı sonrası yayınlayacağız.

E) **Git Commit**

* Commit mesajı: `Add in-process event bus and order-placed email flow (dev pickup)`
* Komut:

```bash
git add -A && git commit -m "Add in-process event bus and order-placed email flow (dev pickup)"
```

Sipariş verip `App_Data/MailPickup` içinde sipariş onay `.eml` dosyasını gördüysen **“bitti”** yaz. Sonraki adımda siparişi MySQL’e persist edecek EF entity + migration’a geçelim.
