using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleAdmin}")]
public sealed class PurchasingController : Controller
{
    private readonly PehlioneDbContext _db;

    public PurchasingController(PehlioneDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Returns(string? q, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking().AsQueryable();

        query = query.Where(o =>
            o.Status == OrderStatusWorkflow.Cancelled ||
            o.Status == OrderStatusWorkflow.Refunded);

        var normalizedQ = (q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            query = query.Where(o =>
                o.Id.ToString().Contains(normalizedQ) ||
                (o.User != null && o.User.Email != null && o.User.Email.Contains(normalizedQ)));
        }

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(500)
            .Select(o => new OrderListItemVm
            {
                Id = o.Id,
                CreatedAt = o.CreatedAt,
                CustomerEmail = o.User != null ? (o.User.Email ?? o.User.UserName ?? "-") : "-",
                Status = o.Status,
                ShippingCarrier = o.ShippingCarrier,
                TrackingCode = o.TrackingCode,
                ItemCount = o.Items.Count,
                TotalAmount = o.TotalAmount,
                Currency = o.Currency
            })
            .ToListAsync(ct);

        ViewBag.Query = normalizedQ;
        return View(items);
    }
}
