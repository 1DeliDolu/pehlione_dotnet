using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.ViewModels.Navigation;

namespace Pehlione.ViewComponents;

public sealed class MainNavViewComponent : ViewComponent
{
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
            Categories = categories
        };

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
}
