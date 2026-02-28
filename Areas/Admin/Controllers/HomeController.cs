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
    private static readonly int[] DashboardRangeOptions = [5, 7, 15, 30, 60, 90, 180, 365];
    private static readonly string[] DepartmentOptions =
    [
        "Sales",
        "Purchasing",
        "Warehouse",
        "IT",
        "HR",
        "Accounting",
        "Courier",
        "CustomerRelations"
    ];

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
        IdentitySeed.RoleCustomerRelations,
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
    public async Task<IActionResult> Index(int? days = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var range = ResolveRange(days, startDate, endDate);
        var vm = await BuildVmAsync(range.StartUtc, range.EndUtc, range.RangeDays, range.CustomStartDate, range.CustomEndDate, range.Label, ct);
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
    public async Task<IActionResult> QuickUpdatePersonnel(
        QuickPersonnelUpdateVm model,
        string[]? departments,
        string? department,
        string? returnTo,
        CancellationToken ct)
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

        var selectedDepartments = NormalizeDepartments(departments, department, model.Department);
        await ReplaceDepartmentClaimsAsync(user, selectedDepartments);
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

    private async Task ReplaceDepartmentClaimsAsync(ApplicationUser user, IReadOnlyList<string> departments)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        var existing = claims.Where(x => x.Type == PehlioneClaimTypes.Department).ToList();
        if (existing.Count > 0)
            await _userManager.RemoveClaimsAsync(user, existing);

        foreach (var department in departments)
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(PehlioneClaimTypes.Department, department));
    }

    private static string[] NormalizeDepartments(string[]? departments, string? department, string? modelDepartment)
    {
        return (departments ?? Array.Empty<string>())
            .Concat(string.IsNullOrWhiteSpace(department) ? Array.Empty<string>() : new[] { department })
            .Concat(string.IsNullOrWhiteSpace(modelDepartment) ? Array.Empty<string>() : new[] { modelDepartment })
            .Select(x => (x ?? "").Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => DepartmentOptions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<AdminDashboardVm> BuildVmAsync(
        DateTime startUtc,
        DateTime endUtc,
        int selectedRangeDays,
        DateTime? customStartDate,
        DateTime? customEndDate,
        string rangeLabel,
        CancellationToken ct)
    {
        var categories = await LoadCategoryOptionsAsync(ct);
        var productOptions = await LoadProductOptionsAsync(ct);
        var personnelOptions = await LoadPersonnelOptionsAsync(ct);

        var totalUsers = await _userManager.Users.CountAsync(ct);
        var totalProducts = await _db.Products.AsNoTracking().CountAsync(ct);
        var activeProducts = await _db.Products.AsNoTracking().CountAsync(x => x.IsActive, ct);
        var totalCategories = await _db.Categories.AsNoTracking().CountAsync(ct);
        var orderQueryForRange = _db.Orders
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc);
        var totalOrders = await orderQueryForRange.CountAsync(ct);
        var ordersRevenue = await orderQueryForRange.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
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

        var rangeDays = Math.Max((int)Math.Ceiling((endUtc - startUtc).TotalDays), 1);
        var useDailyBuckets = rangeDays <= 60;
        var monthlyOrders = useDailyBuckets
            ? await BuildDailyOrdersAsync(startUtc, endUtc, ct)
            : await BuildMonthlyOrdersAsync(startUtc, endUtc, ct);

        var orderCreatedMap = await _db.Orders
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
            .Select(x => new { x.Id, x.CreatedAt })
            .ToDictionaryAsync(x => x.Id, x => x.CreatedAt, ct);

        var ordersInRange = _db.Orders
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
            .Select(x => x.Id);

        var statusLogs = await (
            from log in _db.OrderStatusLogs.AsNoTracking()
            join orderId in ordersInRange on log.OrderId equals orderId
            orderby log.ChangedAt
            select new OrderStatusLogRow
            {
                OrderId = log.OrderId,
                ToStatus = log.ToStatus,
                ChangedAt = log.ChangedAt
            })
            .ToListAsync(ct);

        var (orderTimings, transitionTimings) = BuildOrderTimings(orderCreatedMap, statusLogs);

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
            MonthlyOrders = monthlyOrders,
            OrderTimings = orderTimings,
            TransitionTimings = transitionTimings,
            SelectedRangeDays = selectedRangeDays,
            CustomStartDate = customStartDate,
            CustomEndDate = customEndDate,
            RangeLabel = rangeLabel,
            RangeOptions = DashboardRangeOptions
        };
    }

    private async Task<IReadOnlyList<AdminMonthlyOrderVm>> BuildDailyOrdersAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var raw = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Day = g.Key,
                OrderCount = g.Count(),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(ct);

        var map = raw.ToDictionary(x => x.Day, x => x);
        var result = new List<AdminMonthlyOrderVm>();
        for (var day = startUtc.Date; day < endUtc.Date; day = day.AddDays(1))
        {
            map.TryGetValue(day, out var row);
            result.Add(new AdminMonthlyOrderVm
            {
                MonthLabel = day.ToString("dd MMM"),
                OrderCount = row?.OrderCount ?? 0,
                Revenue = row?.Revenue ?? 0m
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<AdminMonthlyOrderVm>> BuildMonthlyOrdersAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var raw = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                OrderCount = g.Count(),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(ct);

        var map = raw.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x);
        var monthStart = new DateTime(startUtc.Year, startUtc.Month, 1);
        var monthEnd = new DateTime(endUtc.Year, endUtc.Month, 1).AddMonths(1);

        var result = new List<AdminMonthlyOrderVm>();
        for (var current = monthStart; current < monthEnd; current = current.AddMonths(1))
        {
            var key = $"{current.Year:D4}-{current.Month:D2}";
            map.TryGetValue(key, out var row);
            result.Add(new AdminMonthlyOrderVm
            {
                MonthLabel = current.ToString("MMM yyyy"),
                OrderCount = row?.OrderCount ?? 0,
                Revenue = row?.Revenue ?? 0m
            });
        }

        return result;
    }

    private static (DateTime StartUtc, DateTime EndUtc, int RangeDays, DateTime? CustomStartDate, DateTime? CustomEndDate, string Label) ResolveRange(
        int? days,
        DateTime? startDate,
        DateTime? endDate)
    {
        var now = DateTime.UtcNow;
        var normalizedDays = DashboardRangeOptions.Contains(days ?? 0) ? days!.Value : 30;

        if (startDate.HasValue && endDate.HasValue)
        {
            var start = startDate.Value.Date;
            var endInclusive = endDate.Value.Date;
            if (endInclusive < start)
                (start, endInclusive) = (endInclusive, start);

            var endExclusive = endInclusive.AddDays(1);
            if (endExclusive <= start)
                endExclusive = start.AddDays(1);

            var spanDays = Math.Max((int)Math.Ceiling((endExclusive - start).TotalDays), 1);
            return (start, endExclusive, spanDays, start, endInclusive, $"Custom: {start:yyyy-MM-dd} - {endInclusive:yyyy-MM-dd}");
        }

        var startUtc = now.AddDays(-normalizedDays);
        return (startUtc, now, normalizedDays, null, null, $"Last {normalizedDays} days");
    }

    private static (AdminOrderTimingSummaryVm summary, IReadOnlyList<AdminOrderTransitionTimingVm> transitions) BuildOrderTimings(
        IReadOnlyDictionary<int, DateTime> orderCreatedMap,
        IReadOnlyList<OrderStatusLogRow> statusLogs)
    {
        var approvalHours = new List<double>();
        var dispatchHours = new List<double>();
        var shippingHours = new List<double>();
        var endToEndHours = new List<double>();

        var transitionConfig = new (string From, string To, string Label)[]
        {
            (OrderStatusWorkflow.Pending, OrderStatusWorkflow.Paid, "Pending -> Paid"),
            (OrderStatusWorkflow.Paid, OrderStatusWorkflow.Processing, "Paid -> Processing"),
            (OrderStatusWorkflow.Processing, OrderStatusWorkflow.Packed, "Processing -> Packed"),
            (OrderStatusWorkflow.Packed, OrderStatusWorkflow.Shipped, "Packed -> Shipped"),
            (OrderStatusWorkflow.Shipped, OrderStatusWorkflow.CourierPickedUp, "Shipped -> Courier Picked Up"),
            (OrderStatusWorkflow.CourierPickedUp, OrderStatusWorkflow.OutForDelivery, "Courier Picked Up -> Out for Delivery"),
            (OrderStatusWorkflow.OutForDelivery, OrderStatusWorkflow.Delivered, "Out for Delivery -> Delivered")
        };

        var transitionBuckets = transitionConfig.ToDictionary(
            x => x.Label,
            _ => new List<double>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in statusLogs.GroupBy(x => x.OrderId))
        {
            var firstStatusTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in group)
            {
                var normalized = OrderStatusWorkflow.Normalize(item.ToStatus);
                if (string.IsNullOrWhiteSpace(normalized))
                    continue;

                if (!firstStatusTimes.ContainsKey(normalized))
                    firstStatusTimes[normalized] = item.ChangedAt;
            }

            DateTime? pendingAt = firstStatusTimes.TryGetValue(OrderStatusWorkflow.Pending, out var pendingTs)
                ? pendingTs
                : orderCreatedMap.TryGetValue(group.Key, out var createdAt) ? createdAt : null;

            DateTime? paidAt = firstStatusTimes.TryGetValue(OrderStatusWorkflow.Paid, out var paidTs) ? paidTs : null;
            DateTime? shippedAt = firstStatusTimes.TryGetValue(OrderStatusWorkflow.Shipped, out var shippedTs) ? shippedTs : null;
            DateTime? deliveredAt = firstStatusTimes.TryGetValue(OrderStatusWorkflow.Delivered, out var deliveredTs) ? deliveredTs : null;

            AddDurationHours(approvalHours, pendingAt, paidAt);
            AddDurationHours(dispatchHours, paidAt, shippedAt);
            AddDurationHours(shippingHours, shippedAt, deliveredAt);
            AddDurationHours(endToEndHours, pendingAt, deliveredAt);

            foreach (var transition in transitionConfig)
            {
                var fromAt = firstStatusTimes.TryGetValue(transition.From, out var fromTs) ? fromTs : (DateTime?)null;
                var toAt = firstStatusTimes.TryGetValue(transition.To, out var toTs) ? toTs : (DateTime?)null;
                AddDurationHours(transitionBuckets[transition.Label], fromAt, toAt);
            }
        }

        var summary = new AdminOrderTimingSummaryVm
        {
            AvgApprovalHours = AverageHours(approvalHours),
            ApprovalSampleCount = approvalHours.Count,
            AvgDispatchHours = AverageHours(dispatchHours),
            DispatchSampleCount = dispatchHours.Count,
            AvgShippingHours = AverageHours(shippingHours),
            ShippingSampleCount = shippingHours.Count,
            AvgEndToEndHours = AverageHours(endToEndHours),
            EndToEndSampleCount = endToEndHours.Count
        };

        var transitions = transitionConfig
            .Select(x => new AdminOrderTransitionTimingVm
            {
                Label = x.Label,
                AvgHours = AverageHours(transitionBuckets[x.Label]),
                SampleCount = transitionBuckets[x.Label].Count
            })
            .ToArray();

        return (summary, transitions);
    }

    private static void AddDurationHours(List<double> bucket, DateTime? start, DateTime? end)
    {
        if (!start.HasValue || !end.HasValue)
            return;

        var hours = (end.Value - start.Value).TotalHours;
        if (hours < 0 || hours > 24 * 365 * 2)
            return;

        bucket.Add(hours);
    }

    private static double AverageHours(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
            return 0;

        return Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private sealed class OrderStatusLogRow
    {
        public int OrderId { get; set; }
        public string ToStatus { get; set; } = "";
        public DateTime ChangedAt { get; set; }
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
