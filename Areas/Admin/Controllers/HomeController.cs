using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Catalog;
using Pehlione.Models.Commerce;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Security;
using Pehlione.Services;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class HomeController : Controller
{
    private static readonly HashSet<string> AdminActions = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Index),
        nameof(UserForm),
        nameof(ProductForm),
        nameof(StockForm),
        nameof(PersonnelForm)
    };

    private static readonly string[] AllowedRoles =
    [
        IdentitySeed.RoleCustomer,
        IdentitySeed.RoleStaff,
        IdentitySeed.RolePurchasing,
        IdentitySeed.RoleWarehouse,
        IdentitySeed.RoleIt,
        IdentitySeed.RoleHr,
        IdentitySeed.RoleAccounting,
        IdentitySeed.RoleCourier,
        IdentitySeed.RoleAdmin
    ];

    private readonly PehlioneDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IInventoryService _inventoryService;

    public HomeController(PehlioneDbContext db, UserManager<ApplicationUser> userManager, IInventoryService inventoryService)
    {
        _db = db;
        _userManager = userManager;
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await BuildVmAsync(ct);
        return View(vm);
    }

    [HttpGet]
    public IActionResult UserForm()
    {
        return View(new CreateUserVm { Role = IdentitySeed.RoleStaff });
    }

    [HttpGet]
    public async Task<IActionResult> ProductForm(CancellationToken ct)
    {
        return View(new ProductCreateVm
        {
            Price = 1,
            IsActive = true,
            CategoryOptions = await LoadCategoryOptionsAsync(ct)
        });
    }

    [HttpGet]
    public async Task<IActionResult> StockForm(CancellationToken ct)
    {
        var productOptions = await LoadProductOptionsAsync(ct);
        return View(new AdminStockFormVm
        {
            Form = new QuickStockOperationVm
            {
                ProductId = productOptions.Select(x => int.TryParse(x.Value, out var id) ? id : 0).FirstOrDefault(x => x > 0),
                Quantity = 1,
                OperationType = "Increase"
            },
            ProductOptions = productOptions
        });
    }

    [HttpGet]
    public async Task<IActionResult> PersonnelForm(CancellationToken ct)
    {
        var personnelOptions = await LoadPersonnelOptionsAsync(ct);
        return View(new AdminPersonnelFormVm
        {
            Form = new QuickPersonnelUpdateVm
            {
                UserId = personnelOptions.FirstOrDefault()?.Value ?? "",
                Role = IdentitySeed.RoleStaff
            },
            PersonnelOptions = personnelOptions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCreateUser(CreateUserVm model, string? returnTo, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            TempData["AdminError"] = "Gecersiz rol secimi.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        if (!ModelState.IsValid)
        {
            TempData["AdminError"] = "Calisan formu eksik veya gecersiz.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            TempData["AdminError"] = "Bu e-posta zaten kayitli.";
            return RedirectToAction(SafeReturnAction(returnTo));
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
            TempData["AdminError"] = string.Join(" | ", create.Errors.Select(x => x.Description));
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var addRole = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addRole.Succeeded)
        {
            TempData["AdminError"] = string.Join(" | ", addRole.Errors.Select(x => x.Description));
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(PehlioneClaimTypes.MustChangePassword, "true"));

        TempData["AdminSuccess"] = $"Calisan olusturuldu: {model.Email}";
        return RedirectToAction(SafeReturnAction(returnTo));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCreateProduct(ProductCreateVm model, string? returnTo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminError"] = "Urun formu eksik veya gecersiz.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var name = (model.Name ?? "").Trim();
        var sku = (model.Sku ?? "").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku))
        {
            TempData["AdminError"] = "Urun adi ve SKU zorunlu.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.CategoryId, ct);
        if (!categoryExists)
        {
            TempData["AdminError"] = "Gecersiz kategori secimi.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var skuExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku, ct);
        if (skuExists)
        {
            TempData["AdminError"] = "Bu SKU zaten kullaniliyor.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        _db.Products.Add(new Product
        {
            CategoryId = model.CategoryId,
            Name = name,
            Sku = sku,
            Price = model.Price,
            IsActive = model.IsActive
        });
        await _db.SaveChangesAsync(ct);

        TempData["AdminSuccess"] = $"Urun eklendi: {name} ({sku})";
        return RedirectToAction(SafeReturnAction(returnTo));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickStockOperation(QuickStockOperationVm model, string? returnTo, CancellationToken ct)
    {
        if (model.ProductId <= 0 || model.Quantity <= 0)
        {
            TempData["AdminError"] = "Stok formu gecersiz.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var userId = _userManager.GetUserId(User);
        var op = (model.OperationType ?? "").Trim();

        ReceiveStockResult result;
        if (string.Equals(op, "Decrease", StringComparison.OrdinalIgnoreCase))
        {
            result = await _inventoryService.ReduceStockAsync(model.ProductId, model.Quantity, "Admin dashboard stok azaltma", userId, ct);
        }
        else
        {
            result = await _inventoryService.ReceiveStockAsync(model.ProductId, model.Quantity, "Admin dashboard stok artirma", userId, ct);
        }

        if (!result.Success)
        {
            TempData["AdminError"] = result.Error ?? "Stok islemi basarisiz.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        TempData["AdminSuccess"] = $"Stok islemi tamamlandi. Guncel stok: {result.CurrentQuantity}";
        return RedirectToAction(SafeReturnAction(returnTo));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickUpdatePersonnel(QuickPersonnelUpdateVm model, string? returnTo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.UserId) || !AllowedRoles.Contains(model.Role))
        {
            TempData["AdminError"] = "Personel guncelleme formu gecersiz.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == model.UserId, ct);
        if (user is null)
        {
            TempData["AdminError"] = "Personel bulunamadi.";
            return RedirectToAction(SafeReturnAction(returnTo));
        }

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var oldRole in roles.Where(r => AllowedRoles.Contains(r)).ToList())
        {
            if (!string.Equals(oldRole, model.Role, StringComparison.OrdinalIgnoreCase))
                await _userManager.RemoveFromRoleAsync(user, oldRole);
        }

        if (!await _userManager.IsInRoleAsync(user, model.Role))
            await _userManager.AddToRoleAsync(user, model.Role);

        await UpsertClaimAsync(user, PehlioneClaimTypes.Department, model.Department);
        await UpsertClaimAsync(user, PehlioneClaimTypes.Position, model.Position);

        TempData["AdminSuccess"] = "Personel rol/birim bilgisi guncellendi.";
        return RedirectToAction(SafeReturnAction(returnTo));
    }

    private async Task UpsertClaimAsync(ApplicationUser user, string claimType, string? value)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        var existing = claims.Where(x => x.Type == claimType).ToList();
        if (existing.Count > 0)
            await _userManager.RemoveClaimsAsync(user, existing);

        if (!string.IsNullOrWhiteSpace(value))
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(claimType, value.Trim()));
    }

    private async Task<AdminDashboardVm> BuildVmAsync(CancellationToken ct)
    {
        var categories = await LoadCategoryOptionsAsync(ct);
        var productOptions = await LoadProductOptionsAsync(ct);
        var personnelOptions = await LoadPersonnelOptionsAsync(ct);

        var totalUsers = await _userManager.Users.CountAsync(ct);
        var totalProducts = await _db.Products.AsNoTracking().CountAsync(ct);
        var activeProducts = await _db.Products.AsNoTracking().CountAsync(x => x.IsActive, ct);
        var totalCategories = await _db.Categories.AsNoTracking().CountAsync(ct);
        var totalOrders = await _db.Orders.AsNoTracking().CountAsync(ct);
        var ordersRevenue = await _db.Orders.AsNoTracking().SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var totalStockQuantity = await _db.Stocks.AsNoTracking().SumAsync(x => (int?)x.Quantity, ct) ?? 0;
        var lowStockProducts = await (
            from p in _db.Products.AsNoTracking()
            join s in _db.Stocks.AsNoTracking() on p.Id equals s.ProductId into ps
            from s in ps.DefaultIfEmpty()
            let qty = s == null ? 0 : s.Quantity
            where qty <= 5
            select p.Id
        ).CountAsync(ct);

        var categoryStock = await (
            from c in _db.Categories.AsNoTracking()
            join p in _db.Products.AsNoTracking() on c.Id equals p.CategoryId into cp
            from p in cp.DefaultIfEmpty()
            join s in _db.Stocks.AsNoTracking() on p.Id equals s.ProductId into ps
            from s in ps.DefaultIfEmpty()
            group s by c.Name into g
            select new AdminCategoryStockVm
            {
                CategoryName = g.Key,
                Quantity = g.Sum(x => x == null ? 0 : x.Quantity)
            }
        )
        .OrderByDescending(x => x.Quantity)
        .Take(8)
        .ToListAsync(ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);
        var monthlyRaw = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= monthStart)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                OrderCount = g.Count(),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(ct);

        var monthlyOrders = new List<AdminMonthlyOrderVm>(6);
        for (var i = 0; i < 6; i++)
        {
            var current = monthStart.AddMonths(i);
            var row = monthlyRaw.FirstOrDefault(x => x.Year == current.Year && x.Month == current.Month);
            monthlyOrders.Add(new AdminMonthlyOrderVm
            {
                MonthLabel = current.ToString("MMM yyyy"),
                OrderCount = row?.OrderCount ?? 0,
                Revenue = row?.Revenue ?? 0m
            });
        }

        return new AdminDashboardVm
        {
            QuickUser = new CreateUserVm { Role = IdentitySeed.RoleStaff },
            QuickProduct = new ProductCreateVm
            {
                Price = 1,
                IsActive = true,
                CategoryOptions = categories
            },
            QuickStockOperation = new QuickStockOperationVm
            {
                ProductId = productOptions.Select(x => int.TryParse(x.Value, out var id) ? id : 0).FirstOrDefault(x => x > 0),
                Quantity = 1,
                OperationType = "Increase"
            },
            QuickPersonnelUpdate = new QuickPersonnelUpdateVm
            {
                UserId = personnelOptions.FirstOrDefault()?.Value ?? "",
                Role = IdentitySeed.RoleStaff
            },
            ProductOptions = productOptions,
            PersonnelOptions = personnelOptions,
            TotalUsers = totalUsers,
            TotalProducts = totalProducts,
            TotalCategories = totalCategories,
            TotalOrders = totalOrders,
            TotalStockQuantity = totalStockQuantity,
            ActiveProducts = activeProducts,
            InactiveProducts = Math.Max(totalProducts - activeProducts, 0),
            LowStockProducts = lowStockProducts,
            OrdersRevenue = ordersRevenue,
            CategoryStock = categoryStock,
            MonthlyOrders = monthlyOrders
        };
    }

    private string SafeReturnAction(string? returnTo)
    {
        if (!string.IsNullOrWhiteSpace(returnTo) && AdminActions.Contains(returnTo))
            return returnTo;

        return nameof(Index);
    }

    private async Task<List<ProductCategoryOptionVm>> LoadCategoryOptionsAsync(CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryOptionVm
            {
                Id = c.Id,
                Name = c.Parent != null ? c.Parent.Name + " / " + c.Name : c.Name
            })
            .ToListAsync(ct);
    }

    private async Task<List<AdminSelectOptionVm>> LoadProductOptionsAsync(CancellationToken ct)
    {
        return await _db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminSelectOptionVm
            {
                Value = x.Id.ToString(),
                Label = x.Name + " (" + x.Sku + ")"
            })
            .ToListAsync(ct);
    }

    private async Task<List<AdminSelectOptionVm>> LoadPersonnelOptionsAsync(CancellationToken ct)
    {
        var users = await _userManager.Users
            .OrderBy(x => x.Email)
            .ToListAsync(ct);

        return users
            .Select(x => new AdminSelectOptionVm
            {
                Value = x.Id,
                Label = x.Email ?? x.UserName ?? x.Id
            })
            .ToList();
    }
}
