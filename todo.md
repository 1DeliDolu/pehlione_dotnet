A) **Hedef (Türkçe)**
Microsoft’un “First Web API” eğitimindeki gibi, mevcut MVC uygulamanın içine **controller-based Web API** ekleyip `TodoItem` tablosunu EF Core ile yöneteceğiz: `/api/todoitems` altında **CRUD** endpoint’leri çalışacak (DB kullanır). ([Microsoft Learn][1])

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Controllers/TodoItemsController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/[controller]")]
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

`./Models/TodoItem.cs`

```csharp
namespace Pehlione.Models;

public sealed class TodoItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsComplete { get; set; }
}
```

`./Data/PehlioneDbContext.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public sealed class PehlioneDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    // İleride e-ticaret domain DbSet'leri buraya gelecek (Products, Orders, vb.)
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

* Microsoft’un tutorial’ı, controller tabanlı Web API’de tipik CRUD yüzeyini `GET/POST/PUT/DELETE` olarak kurar; biz aynı deseni `/api/todoitems` altında uyguladık. ([Microsoft Learn][1])
* `[ApiController]` + `[Route("api/[controller]")]` ile route ve model binding “API modu”nda standartlaşır. ([Microsoft Learn][2])
* `AsNoTracking()` sadece okuma için daha hafif sorgu üretir.
* `CreatedAtAction(...)` POST sonrası resource URL’ini doğru şekilde döndürür (REST pratiği). ([Microsoft Learn][1])
* `app.MapControllers()` attribute routing’i aktif eder; MVC route’larıyla çakışmadan API endpoint’lerini ekler. ([Microsoft Learn][2])

E) **Git Commit**

* Commit mesajı: `Add TodoItems Web API (CRUD) backed by EF Core`
* Komut:

```bash
git add -A && git commit -m "Add TodoItems Web API (CRUD) backed by EF Core"
```

`dotnet run` sonrası hızlı test:

* `GET /api/todoitems` (şimdilik boş döner; migration sonra)
* `POST /api/todoitems` body: `{ "name": "ilk", "isComplete": false }`

Bunu uyguladıktan sonra **“bitti”** yaz; sonraki adımda **migration** ile hem Identity hem TodoItems tablolarını MySQL’e basacağız (`dotnet ef migrations add ...` + `dotnet ef database update`).

