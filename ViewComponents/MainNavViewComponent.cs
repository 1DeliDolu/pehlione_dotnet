using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.ViewModels.Navigation;
using System.Text.Json;

namespace Pehlione.ViewComponents;

public sealed class MainNavViewComponent : ViewComponent
{
    private const string CartCookieKey = "pehlione.cart";
    private readonly PehlioneDbContext _db;

    public MainNavViewComponent(PehlioneDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var activeSlug = GetActiveSlug();

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentId == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new MainNavItemVm
            {
                Name = c.Name,
                Slug = c.Slug
            })
            .ToListAsync();

        var vm = new MainNavVm
        {
            ActiveSlug = activeSlug,
            Categories = categories,
            CartItemCount = GetCartItemCount()
        };
        ViewData["MainNavCartItemCount"] = vm.CartItemCount;

        return View(vm);
    }

    private string? GetActiveSlug()
    {
        var keys = new[] { "categorySlug", "slug", "category" };

        foreach (var key in keys)
        {
            if (ViewContext.RouteData.Values.TryGetValue(key, out var val) && val is not null)
            {
                var s = val.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }

        var qs = HttpContext?.Request?.Query;
        if (qs is not null)
        {
            foreach (var key in keys)
            {
                var s = qs[key].ToString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }

        return null;
    }

    private int GetCartItemCount()
    {
        if (HttpContext?.Request?.Cookies is null)
            return 0;

        if (!HttpContext.Request.Cookies.TryGetValue(CartCookieKey, out var json) || string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            var items = JsonSerializer.Deserialize<List<CartCookieItem>>(json);
            if (items is null || items.Count == 0)
                return 0;

            return items.Sum(x => Math.Clamp(x.Qty, 1, 99));
        }
        catch
        {
            return 0;
        }
    }

    private sealed class CartCookieItem
    {
        public int Qty { get; init; }
    }
}
