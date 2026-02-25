A) **Hedef (Türkçe)**
Pehlione’a **JWT Bearer authentication** ekleyip, Identity’deki rollerden (Admin/Staff/Customer) JWT içine **role claim** olarak basacağız. Sonuç: API tarafında **JWT + RBAC** çalışacak; ayrıca UI’da giriş yapan kullanıcı için **JWT üretip gösteren** bir sayfa ekleyeceğiz.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Controllers/AuthController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Models.Auth;
using Pehlione.Models.Identity;
using Pehlione.Services;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    // POST: /api/auth/token
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Token([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await _userManager.FindByEmailAsync(request.EmailOrUserName)
                   ?? await _userManager.FindByNameAsync(request.EmailOrUserName);

        if (user is null)
            return Unauthorized(new { message = "Invalid credentials." });

        var ok = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!ok)
            return Unauthorized(new { message = "Invalid credentials." });

        var (token, expiresAtUtc, roles) = await _jwtTokenService.CreateTokenAsync(user, ct);

        return Ok(new TokenResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            Roles = roles
        });
    }
}
```

`./Controllers/SecureController.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/secure")]
public sealed class SecureController : ControllerBase
{
    // GET: /api/secure/me  (any authenticated via JWT)
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult Me()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToArray();

        return Ok(new
        {
            name = User.Identity?.Name,
            roles
        });
    }

    // GET: /api/secure/admin
    [HttpGet("admin")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = IdentitySeed.RoleAdmin)]
    public IActionResult AdminOnly() => Ok(new { ok = true, area = "Admin" });

    // GET: /api/secure/staff
    [HttpGet("staff")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = IdentitySeed.RoleStaff)]
    public IActionResult StaffOnly() => Ok(new { ok = true, area = "Staff" });

    // GET: /api/secure/customer
    [HttpGet("customer")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = IdentitySeed.RoleCustomer)]
    public IActionResult CustomerOnly() => Ok(new { ok = true, area = "Customer" });
}
```

`./Controllers/TokenController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels;
using Pehlione.Services;

namespace Pehlione.Controllers;

[Authorize]
public sealed class TokenController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public TokenController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var (token, expiresAtUtc, roles) = await _jwtTokenService.CreateTokenAsync(user, ct);

        return View(new TokenViewModel
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            Roles = roles
        });
    }
}
```

`./Controllers/TodoItemsController.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = $"{IdentitySeed.RoleStaff},{IdentitySeed.RoleAdmin}")]
public sealed class TodoItemsController : ControllerBase
{
    private readonly PehlioneDbContext _db;

    public TodoItemsController(PehlioneDbContext db)
    {
        _db = db;
    }

    // GET: /api/todoitems
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems(CancellationToken ct)
    {
        return await _db.TodoItems.AsNoTracking().ToListAsync(ct);
    }

    // GET: /api/todoitems/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TodoItem>> GetTodoItem(int id, CancellationToken ct)
    {
        var todoItem = await _db.TodoItems.FindAsync([id], ct);
        if (todoItem is null)
            return NotFound();

        return todoItem;
    }

    // POST: /api/todoitems
    [HttpPost]
    public async Task<ActionResult<TodoItem>> PostTodoItem(TodoItem todoItem, CancellationToken ct)
    {
        _db.TodoItems.Add(todoItem);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTodoItem), new { id = todoItem.Id }, todoItem);
    }

    // PUT: /api/todoitems/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutTodoItem(int id, TodoItem todoItem, CancellationToken ct)
    {
        if (id != todoItem.Id)
            return BadRequest();

        _db.Entry(todoItem).State = EntityState.Modified;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var exists = await _db.TodoItems.AnyAsync(x => x.Id == id, ct);
            if (!exists)
                return NotFound();

            throw;
        }

        return NoContent();
    }

    // DELETE: /api/todoitems/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTodoItem(int id, CancellationToken ct)
    {
        var todoItem = await _db.TodoItems.FindAsync([id], ct);
        if (todoItem is null)
            return NotFound();

        _db.TodoItems.Remove(todoItem);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
```

`./Models/Auth/LoginRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.Auth;

public sealed class LoginRequest
{
    [Required]
    public string EmailOrUserName { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
```

`./Models/Auth/TokenResponse.cs`

```csharp
namespace Pehlione.Models.Auth;

public sealed class TokenResponse
{
    public string AccessToken { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}
```

`./Models/Security/JwtOptions.cs`

```csharp
namespace Pehlione.Models.Security;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";
    public int ExpiresMinutes { get; init; } = 60;
}
```

`./Models/ViewModels/TokenViewModel.cs`

```csharp
namespace Pehlione.Models.ViewModels;

public sealed class TokenViewModel
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}
```

`./Services/IJwtTokenService.cs`

```csharp
using Pehlione.Models.Identity;

namespace Pehlione.Services;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc, string[] Roles)> CreateTokenAsync(ApplicationUser user, CancellationToken ct);
}
```

`./Services/JwtTokenService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pehlione.Models.Identity;
using Pehlione.Models.Security;

namespace Pehlione.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _options;

    public JwtTokenService(UserManager<ApplicationUser> userManager, IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _options = options.Value;
    }

    public async Task<(string Token, DateTime ExpiresAtUtc, string[] Roles)> CreateTokenAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToArray();

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? "")
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, expires, roles);
    }
}
```

`./Views/Token/Index.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.TokenViewModel
@{
    ViewData["Title"] = "JWT Token";
}

<div class="container" style="max-width: 900px;">
    <h1 class="h3 mb-3">JWT (Bearer) Token</h1>

    <div class="alert alert-warning">
        Bu token dev/test içindir. Paylaşma. Süre bitince yeniden üret.
    </div>

    <div class="mb-2">
        <strong>Roller:</strong> @(Model.Roles.Length == 0 ? "-" : string.Join(", ", Model.Roles))
    </div>
    <div class="mb-3">
        <strong>Expires (UTC):</strong> @Model.ExpiresAtUtc.ToString("u")
    </div>

    <div class="mb-2">
        <label class="form-label">Access Token</label>
        <textarea id="tokenBox" class="form-control" rows="6" readonly>@Model.AccessToken</textarea>
    </div>

    <div class="d-flex gap-2">
        <button type="button" class="btn btn-primary" onclick="copyToken()">Kopyala</button>
        <a class="btn btn-outline-secondary" asp-controller="Home" asp-action="Index">Ana sayfa</a>
    </div>

    <hr />

    <p class="mb-1"><strong>Örnek kullanım</strong></p>
    <ul class="mb-0">
        <li><code>Authorization: Bearer &lt;token&gt;</code> header ile çağır</li>
        <li>Test endpoint: <code>/api/secure/me</code>, <code>/api/secure/admin</code>, <code>/api/secure/staff</code>, <code>/api/secure/customer</code></li>
    </ul>
</div>

<script>
    function copyToken() {
        const el = document.getElementById('tokenBox');
        el.select();
        el.setSelectionRange(0, 999999);
        document.execCommand('copy');
    }
</script>
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

    @if (User?.Identity?.IsAuthenticated == true)
    {
        <p class="mb-2">
            Giriş yapıldı: <strong>@User.Identity!.Name</strong>
        </p>

        <div class="d-flex justify-content-center gap-2 flex-wrap mb-3">
            <a class="btn btn-outline-primary" asp-controller="Token" asp-action="Index">JWT Token</a>

            <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
                @Html.AntiForgeryToken()
                <button type="submit" class="btn btn-outline-dark">Çıkış</button>
            </form>
        </div>

        <h2 class="h4">Bölümler (Rolüne göre)</h2>
        <div class="d-flex justify-content-center gap-3 flex-wrap">
            @if (User.IsInRole("Customer"))
            {
                <a class="btn btn-outline-primary" asp-area="Customer" asp-controller="Home" asp-action="Index">Müşteri</a>
            }
            @if (User.IsInRole("Staff"))
            {
                <a class="btn btn-outline-secondary" asp-area="Staff" asp-controller="Home" asp-action="Index">Çalışanlar</a>
            }
            @if (User.IsInRole("Admin"))
            {
                <a class="btn btn-outline-danger" asp-area="Admin" asp-controller="Home" asp-action="Index">Admin</a>
            }
        </div>
    }
    else
    {
        <p class="mb-3">Bölümler rol ile korunuyor. Devam etmek için giriş yap.</p>
        <a class="btn btn-primary" asp-controller="Account" asp-action="Login">Giriş</a>
    }
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

// JWT bearer (API)
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

* MVC UI (cookie) ile API (JWT) aynı projede birlikte yaşayabilir; API controller’larında `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` ile net ayırdık.
* Token içine `ClaimTypes.Role` eklediğimiz için `[Authorize(Roles="Admin")]` hem UI (cookie) hem API (JWT) tarafında rol kontrolü yapabilir.
* UI için `/Token` sayfası, cookie ile giriş yapan kullanıcı adına JWT üretip gösterir; API testini kolaylaştırır.
* `/api/auth/token` endpoint’i ile de programatik olarak token alabilirsin (Postman/curl).
* Microsoft dokümanında dev ortamında JWT yönetimi için `dotnet user-jwts` yaklaşımı da anlatılıyor; istersen sonraki adımda bunu da ekleyip “secrets” tabanlı daha güvenli dev akışına geçebiliriz. ([Microsoft Learn][1])

E) **Git Commit**

* Commit mesajı: `Add JWT bearer auth, role-based API protection, and UI token page`
* Komut:

```bash
git add -A && git commit -m "Add JWT bearer auth, role-based API protection, and UI token page"
```

Bunu uygulayıp:

1. `/Account/Login` ile giriş → `/Token` sayfasında token gör,
2. Token ile `/api/secure/me` çağır,
3. Admin/Staff/Customer endpoint’lerini rolüne göre dene.

