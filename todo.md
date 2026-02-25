A) **Hedef (Türkçe)**
Dev ortamında **gerçek SMTP kullanmadan** e-posta göndermeyi tamamlayacağız: e-postalar **pickup directory** içine `.eml` olarak düşecek. Admin yeni kullanıcı oluşturduğunda o kullanıcıya **“hoş geldin / ilk girişte şifre değiştir”** bilgilendirme e-postası üretilecek (ileride sipariş sonrası e-posta için aynı altyapıyı kullanacağız).

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Services/IAppEmailSender.cs`

```csharp
namespace Pehlione.Services;

public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct);
}
```

`./Services/DevPickupEmailSender.cs`

```csharp
using System.Net.Mail;

namespace Pehlione.Services;

public sealed class DevPickupEmailSender : IAppEmailSender
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<DevPickupEmailSender> _logger;

    public DevPickupEmailSender(
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<DevPickupEmailSender> logger)
    {
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var from = _config["Mail:From"] ?? "no-reply@pehlione.local";
        var pickup = _config["Mail:PickupDirectory"] ?? "App_Data/MailPickup";

        var pickupPath = Path.IsPathRooted(pickup)
            ? pickup
            : Path.Combine(_env.ContentRootPath, pickup);

        Directory.CreateDirectory(pickupPath);

        using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        using var client = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupPath
        };

        // SmtpClient SendMailAsync CancellationToken almaz; ct burada sadece imza için.
        await client.SendMailAsync(message);

        _logger.LogInformation("DEV email queued to pickup directory: {PickupPath} -> {ToEmail}", pickupPath, toEmail);
    }
}
```

`./Services/NullEmailSender.cs`

```csharp
namespace Pehlione.Services;

public sealed class NullEmailSender : IAppEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        => Task.CompletedTask;
}
```

`./Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.Security;
using Pehlione.Services;

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

`./Areas/Admin/Controllers/UsersController.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Security;
using Pehlione.Services;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class UsersController : Controller
{
    private static readonly string[] AllowedRoles =
    [
        IdentitySeed.RoleCustomer,
        IdentitySeed.RoleStaff,
        IdentitySeed.RoleAdmin
    ];

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEmailSender _emailSender;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        IAppEmailSender emailSender,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var items = new List<UserListItemVm>(users.Count);

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new UserListItemVm
            {
                Email = u.Email ?? "",
                UserName = u.UserName ?? "",
                Roles = roles.OrderBy(x => x).ToArray()
            });
        }

        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserVm model, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Geçersiz rol seçimi.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta zaten kayıtlı.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var err in create.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        var addRole = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addRole.Succeeded)
        {
            foreach (var err in addRole.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        // İlk girişte şifre değişimi zorunlu
        var claims = await _userManager.GetClaimsAsync(user);
        var hasFlag = claims.Any(c => c.Type == PehlioneClaimTypes.MustChangePassword);
        if (!hasFlag)
        {
            await _userManager.AddClaimAsync(user, new Claim(PehlioneClaimTypes.MustChangePassword, "true"));
        }

        // DEV email (pickup directory) - hata olsa bile kullanıcı oluştu, akışı bozmayalım.
        try
        {
            var subject = "Pehlione - Hesabın oluşturuldu";
            var body = $@"
                <p>Merhaba,</p>
                <p>Pehlione hesabın oluşturuldu.</p>
                <ul>
                    <li><strong>E-posta/Kullanıcı adı:</strong> {System.Net.WebUtility.HtmlEncode(model.Email)}</li>
                    <li><strong>Rol:</strong> {System.Net.WebUtility.HtmlEncode(model.Role)}</li>
                </ul>
                <p>İlk girişte şifre değiştirmen istenecektir.</p>
                <p>Giriş: <a href=""{Request.Scheme}://{Request.Host}/Account/Login"">/Account/Login</a></p>
            ";

            await _emailSender.SendAsync(model.Email, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DEV email send failed for new user: {Email}", model.Email);
        }

        return RedirectToAction(nameof(Index));
    }
}
```

`./appsettings.json`

```json
{
  "ConnectionStrings": {
    "PehlioneDb": ""
  },
  "Jwt": {
    "Issuer": "pehlione",
    "Audience": "pehlione",
    "SigningKey": "",
    "ExpiresMinutes": 60
  },
  "Mail": {
    "From": "no-reply@pehlione.local",
    "PickupDirectory": "App_Data/MailPickup"
  },
  "Seed": {
    "AdminEmail": "admin@pehlione.local",
    "AdminPassword": "",
    "StaffEmail": "staff@pehlione.local",
    "StaffPassword": "",
    "CustomerEmail": "customer@pehlione.local",
    "CustomerPassword": ""
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
  "Jwt": {
    "Issuer": "pehlione-dev",
    "Audience": "pehlione-dev",
    "SigningKey": "DEV_ONLY_CHANGE_ME_32_CHARS_MINIMUM__________",
    "ExpiresMinutes": 120
  },
  "Mail": {
    "From": "no-reply@pehlione.local",
    "PickupDirectory": "App_Data/MailPickup"
  },
  "Seed": {
    "AdminEmail": "admin@pehlione.local",
    "AdminPassword": "Pehlione!Admin1",
    "StaffEmail": "staff@pehlione.local",
    "StaffPassword": "Pehlione!Staff1",
    "CustomerEmail": "customer@pehlione.local",
    "CustomerPassword": "Pehlione!Cust1"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

`./.gitignore`

```gitignore
# Build outputs
[Bb]in/
[Oo]bj/

# User-specific / IDE
.vs/
.vscode/
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json

.idea/
*.sln.iml

*.user
*.suo
*.userosscache
*.sln.docstates

# OS / tooling noise
.DS_Store
Thumbs.db
*.swp
*.tmp
*.log

# Secrets / local overrides (do NOT commit)
appsettings.Development.json
appsettings.Local.json
.env
.env.*

# Dev email pickup directory
App_Data/MailPickup/
App_Data/MailPickup/**

# Certificates / keys
*.pfx
*.snk
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* `SmtpDeliveryMethod.SpecifiedPickupDirectory`, e-postayı SMTP’ye göndermek yerine belirttiğin klasöre kopyalar. ([Microsoft Learn][1])
* Pickup directory mantığı: uygulama maili bir dizine bırakır; dış bir süreç/insan isterse `.eml` dosyasını işler. ([Microsoft Learn][2])
* Dev’de güvenli: yanlışlıkla gerçek kişilere mail gitmez; klasörden içerikleri görüp test edersin.
* Prod’da SMTP’ye geçince `NullEmailSender` yerine gerçek SMTP sender yazacağız (aynı `IAppEmailSender` ile).
* Test: Admin → `/Admin/Users/Create` ile yeni kullanıcı oluştur → proje kökünde `App_Data/MailPickup` altında `.eml` oluşmalı.

E) **Git Commit**

* Commit mesajı: `Add dev pickup-directory email sender and send welcome email on user creation`
* Komut:

```bash
git add -A && git commit -m "Add dev pickup-directory email sender and send welcome email on user creation"
```

Bunu uygulayıp yeni kullanıcı oluşturduğunda `App_Data/MailPickup` içine `.eml` düştüyse **“bitti”** yaz. Sonraki adımda bunu **sipariş sonrası e-posta** ile bağlayacağız (Order oluşturulunca event → email).

