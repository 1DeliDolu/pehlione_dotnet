using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.ViewModels.Customer;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize(Roles = IdentitySeed.RoleCustomer)]
public sealed class CartController : Controller
{
    private const string CartCookieKey = "pehlione.cart";
    private const int MaxDistinctItems = 50;
    private const int MaxQtyPerItem = 99;

    private readonly PehlioneDbContext _db;

    public CartController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cart = ReadCart();

        if (cart.Count == 0)
            return View(new CartVm());

        var ids = cart.Select(x => x.ProductId).Distinct().ToArray();

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive && p.Category != null && p.Category.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Price
            })
            .ToListAsync(ct);

        var productMap = products.ToDictionary(x => x.Id, x => x);

        var lines = new List<CartLineVm>();
        decimal total = 0;

        foreach (var ci in cart)
        {
            if (!productMap.TryGetValue(ci.ProductId, out var p))
                continue;

            var qty = Math.Clamp(ci.Qty, 1, MaxQtyPerItem);
            var sub = p.Price * qty;

            lines.Add(new CartLineVm
            {
                ProductId = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                UnitPrice = p.Price,
                Quantity = qty,
                Subtotal = sub
            });

            total += sub;
        }

        return View(new CartVm
        {
            Lines = lines,
            Total = total
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int qty = 1, string? returnUrl = null, CancellationToken ct = default)
    {
        qty = Math.Clamp(qty, 1, MaxQtyPerItem);

        var exists = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId && p.IsActive && p.Category != null && p.Category.IsActive, ct);

        if (!exists)
            return NotFound();

        var cart = ReadCart();

        var item = cart.FirstOrDefault(x => x.ProductId == productId);
        if (item is null)
        {
            if (cart.Count >= MaxDistinctItems)
                return RedirectToAction(nameof(Index));

            cart.Add(new CartCookieItem { ProductId = productId, Qty = qty });
        }
        else
        {
            item.Qty = Math.Clamp(item.Qty + qty, 1, MaxQtyPerItem);
        }

        WriteCart(cart);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private List<CartCookieItem> ReadCart()
    {
        if (!Request.Cookies.TryGetValue(CartCookieKey, out var json) || string.IsNullOrWhiteSpace(json))
            return new List<CartCookieItem>();

        try
        {
            var items = JsonSerializer.Deserialize<List<CartCookieItem>>(json) ?? new List<CartCookieItem>();
            items.RemoveAll(x => x.ProductId <= 0 || x.Qty <= 0);
            return items;
        }
        catch
        {
            return new List<CartCookieItem>();
        }
    }

    private void WriteCart(List<CartCookieItem> items)
    {
        var json = JsonSerializer.Serialize(items);

        Response.Cookies.Append(
            CartCookieKey,
            json,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
    }

    private sealed class CartCookieItem
    {
        public int ProductId { get; set; }
        public int Qty { get; set; }
    }
}
