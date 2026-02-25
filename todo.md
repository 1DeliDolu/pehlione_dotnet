A) **Hedef (Türkçe)**
Admin/Staff/Customer için **gerçek kullanıcı + rol** altyapısını kuracağız: ASP.NET Core **Identity + EF Core** ile roller tanımlanacak ve dev ortamında her rol için **1’er demo kullanıcı** otomatik seed edilecek (bir sonraki adımda migration ile tabloyu DB’ye basacağız). Identity; kullanıcı/şifre/rol/claim/token gibi kimlik işlerini yönetmek için standart çözümdür. ([Microsoft Learn][1])

B) **CLI Komutu (maksimum 1 komut; gerekiyorsa sonraki komut sonraki adıma)**

```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Models/Identity/ApplicationUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace Pehlione.Models.Identity;

// İleride "MustChangePassword" gibi alanlar eklemek için baştan custom user ile başlıyoruz. :contentReference[oaicite:1]{index=1}
public sealed class ApplicationUser : IdentityUser
{
}
```

`./Data/PehlioneDbContext.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public sealed class PehlioneDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    // İleride e-ticaret domain DbSet'leri buraya gelecek (Products, Orders, vb.)
}
```

`./Data/IdentitySeed.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public static class IdentitySeed
{
    public const string RoleAdmin = "Admin";
    public const string RoleStaff = "Staff";
    public const string RoleCustomer = "Customer";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        // 1) Roller
        await EnsureRoleAsync(roleManager, RoleAdmin);
        await EnsureRoleAsync(roleManager, RoleStaff);
        await EnsureRoleAsync(roleManager, RoleCustomer);

        // 2) Demo kullanıcılar (DEV)
        // Parolaları appsettings.Development.json içindeki Seed bölümünden alıyoruz.
        await EnsureUserAsync(
            userManager,
            email: config["Seed:AdminEmail"] ?? "admin@pehlione.local",
            password: config["Seed:AdminPassword"] ?? "",
            role: RoleAdmin);

        await EnsureUserAsync(
            userManager,
            email: config["Seed:StaffEmail"] ?? "staff@pehlione.local",
            password: config["Seed:StaffPassword"] ?? "",
            role: RoleStaff);

        await EnsureUserAsync(
            userManager,
            email: config["Seed:CustomerEmail"] ?? "customer@pehlione.local",
            password: config["Seed:CustomerPassword"] ?? "",
            role: RoleCustomer);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
            return;

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
            throw new InvalidOperationException($"Role create failed: {roleName} - {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                // Güvenli varsayılan yok: parola verilmediyse kullanıcı oluşturmayalım.
                // (Parolayı appsettings.Development.json içinde veriyoruz.)
                return;
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
                throw new InvalidOperationException($"User create failed: {email} - {string.Join("; ", create.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addRole = await userManager.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
                throw new InvalidOperationException($"AddToRole failed: {email} -> {role} - {string.Join("; ", addRole.Errors.Select(e => e.Description))}");
        }
    }
}
```

`./Program.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;

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

        // Dev için: mail confirm zorunlu değil (ileride açarız)
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<PehlioneDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
});

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

// Authentication/Authorization sırası önemli: UseAuthentication önce gelmeli. :contentReference[oaicite:2]{index=2}
app.UseAuthentication();
app.UseAuthorization();

// DEV: Migration varsa uygula + demo kullanıcıları seed et.
// (Henüz migration yoksa sessizce geçer; bir sonraki adımda migration üreteceğiz.)
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

// Areas routing (Admin / Staff / Customer)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

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

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Identity; kullanıcı, parola, **roller**, claim’ler ve token işleri için standart altyapıdır. ([Microsoft Learn][1])
* Custom `ApplicationUser` ile başlamamız, Identity modelini ileride ihtiyaçlara göre genişletmeyi kolaylaştırır. ([Microsoft Learn][2])
* Middleware sırası kritik: `UseRouting` → `UseAuthentication` → `UseAuthorization` olmalı. ([Microsoft Learn][3])
* Seed parolalarını sadece `appsettings.Development.json` içine koyduk (zaten `.gitignore` ile commit’lenmemeli).
* Bu adım **tabloları DB’ye basmaz**; bir sonraki adımda migration üretip MySQL’e uygulayacağız.

E) **Git Commit**

* Commit mesajı: `Add ASP.NET Core Identity with roles and dev seeding`
* Komut:

```bash
git add -A && git commit -m "Add ASP.NET Core Identity with roles and dev seeding"
```

Bu adımı yaptıktan sonra **“bitti”** yaz. Sonraki adımda (Microsoft Learn migration akışına uygun) `dotnet-ef` ile **ilk migration + database update** yapıp MySQL’de Identity tablolarını oluşturacağız.

[1]: https://learn.microsoft.com/de-de/aspnet/core/security/authentication/identity?view=aspnetcore-10.0&utm_source=chatgpt.com "Einführung in Identity in ASP.NET Core"
[2]: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0&utm_source=chatgpt.com "Identity model customization in ASP.NET Core"
[3]: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0&utm_source=chatgpt.com "Overview of ASP.NET Core Authentication"
