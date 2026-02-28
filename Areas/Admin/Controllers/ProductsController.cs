using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Catalog;
using Pehlione.Models.ViewModels.Admin;
using System.Globalization;
using System.Text;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class ProductsController : Controller
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly PehlioneDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductsController(PehlioneDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int? categoryId, bool? isActive, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        var normalizedQ = (q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            query = query.Where(p => p.Name.Contains(normalizedQ) || p.Sku.Contains(normalizedQ));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var items = await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductListItemVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "",
                Price = p.Price,
                ImageUrls = p.ImageUrls,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        ViewBag.Query = normalizedQ;
        ViewBag.CategoryId = categoryId;
        ViewBag.IsActive = isActive;
        ViewBag.CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: categoryId, ct);

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new ProductCreateVm
        {
            CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: null, ct)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateVm model, List<IFormFile>? uploadedImages, CancellationToken ct)
    {
        model.CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: null, ct);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var name = (model.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(model.Name), "Ürün adı zorunludur.");
            return View(model);
        }

        var sku = (model.Sku ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku))
        {
            ModelState.AddModelError(nameof(model.Sku), "SKU zorunludur.");
            return View(model);
        }

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.CategoryId, ct);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Geçersiz kategori seçimi.");
            return View(model);
        }

        var skuExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku, ct);
        if (skuExists)
        {
            ModelState.AddModelError(nameof(model.Sku), "Bu SKU zaten kullanılıyor.");
            return View(model);
        }

        var imageUrls = ParseImageUrls(model.ImageUrlsText);
        var uploadedImageUrls = await SaveUploadedImagesAsync(uploadedImages, model.CategoryId, name, ct);
        imageUrls.AddRange(uploadedImageUrls);
        imageUrls = imageUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Product
        {
            CategoryId = model.CategoryId,
            Name = name,
            Sku = sku,
            Price = model.Price,
            ImageUrls = imageUrls,
            IsActive = model.IsActive
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entity = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        return View(new ProductEditVm
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId,
            Name = entity.Name,
            Sku = entity.Sku,
            Price = entity.Price,
            ImageUrlsText = string.Join(Environment.NewLine, entity.ImageUrls),
            IsActive = entity.IsActive,
            CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: entity.CategoryId, ct)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductEditVm model, List<IFormFile>? uploadedImages, CancellationToken ct)
    {
        model.CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: model.CategoryId, ct);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == model.Id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var name = (model.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(model.Name), "Ürün adı zorunludur.");
            return View(model);
        }

        var sku = (model.Sku ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku))
        {
            ModelState.AddModelError(nameof(model.Sku), "SKU zorunludur.");
            return View(model);
        }

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.CategoryId, ct);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Geçersiz kategori seçimi.");
            return View(model);
        }

        var skuExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku && p.Id != model.Id, ct);
        if (skuExists)
        {
            ModelState.AddModelError(nameof(model.Sku), "Bu SKU zaten kullanılıyor.");
            return View(model);
        }

        var hasManualImageInput = !string.IsNullOrWhiteSpace(model.ImageUrlsText);
        var imageUrls = hasManualImageInput
            ? ParseImageUrls(model.ImageUrlsText)
            : entity.ImageUrls.ToList();
        var uploadedImageUrls = await SaveUploadedImagesAsync(uploadedImages, model.CategoryId, name, ct);
        imageUrls.AddRange(uploadedImageUrls);
        imageUrls = imageUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entity.CategoryId = model.CategoryId;
        entity.Name = name;
        entity.Sku = sku;
        entity.Price = model.Price;
        entity.ImageUrls = imageUrls;
        entity.IsActive = model.IsActive;

        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Product(int id, CancellationToken ct)
    {
        var item = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetailsVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "",
                Price = p.Price,
                IsActive = p.IsActive,
                ImageUrls = p.ImageUrls
            })
            .FirstOrDefaultAsync(ct);

        if (item is null)
        {
            return NotFound();
        }

        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDeleteVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : ""
            })
            .FirstOrDefaultAsync(ct);

        if (item is null)
        {
            return NotFound();
        }

        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(ProductDeleteVm model, CancellationToken ct)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == model.Id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<ProductCategoryOptionVm>> LoadCategoryOptionsAsync(int? includeCategoryId, CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive || (includeCategoryId.HasValue && c.Id == includeCategoryId.Value))
            .Select(c => new ProductCategoryOptionVm
            {
                Id = c.Id,
                Name = c.Parent != null ? c.Parent.Name + " / " + c.Name : c.Name
            })
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    private List<string> ParseImageUrls(string? raw)
    {
        var normalized = (raw ?? "").Replace("\r", "");
        var parts = normalized
            .Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var url in parts)
        {
            var isValid =
                Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

            if (!isValid)
            {
                ModelState.AddModelError(nameof(ProductCreateVm.ImageUrlsText), $"Gecersiz resim linki: {url}");
            }
        }

        return parts;
    }

    private async Task<List<string>> SaveUploadedImagesAsync(List<IFormFile>? files, int categoryId, string productName, CancellationToken ct)
    {
        var result = new List<string>();
        if (files is null || files.Count == 0)
        {
            return result;
        }

        var (root, relativeRoot) = await GetProductImageDirectoryAsync(categoryId, productName, ct);

        foreach (var file in files.Where(f => f is not null && f.Length > 0))
        {
            var extension = Path.GetExtension(file.FileName ?? "");
            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(ProductCreateVm.ImageUrlsText), $"Desteklenmeyen dosya uzantisi: {file.FileName}");
                continue;
            }

            const long maxSizeBytes = 10 * 1024 * 1024;
            if (file.Length > maxSizeBytes)
            {
                ModelState.AddModelError(nameof(ProductCreateVm.ImageUrlsText), $"Dosya cok buyuk (maks 10MB): {file.FileName}");
                continue;
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(root, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream, ct);

            result.Add($"{relativeRoot}/{fileName}");
        }

        return result;
    }

    private async Task<(string Directory, string RelativeDirectory)> GetProductImageDirectoryAsync(int categoryId, string productName, CancellationToken ct)
    {
        var categorySegments = await BuildCategoryPathSegmentsAsync(categoryId, ct);
        var productSegment = ToPathSegment(productName, "urun");

        var segments = new List<string> { "uploads", "products" };
        segments.AddRange(categorySegments);
        segments.Add(productSegment);

        var directory = Path.Combine(_env.WebRootPath, Path.Combine(segments.ToArray()));
        Directory.CreateDirectory(directory);

        var relativeDirectory = "/" + string.Join('/', segments);
        return (directory, relativeDirectory);
    }

    private async Task<IReadOnlyList<string>> BuildCategoryPathSegmentsAsync(int categoryId, CancellationToken ct)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Select(c => new CategoryPathNode
            {
                Id = c.Id,
                Name = c.Name,
                ParentId = c.ParentId
            })
            .ToDictionaryAsync(c => c.Id, ct);

        var segments = new Stack<string>();
        var visited = new HashSet<int>();
        var currentId = categoryId;

        while (categories.TryGetValue(currentId, out var node) && visited.Add(currentId))
        {
            segments.Push(ToPathSegment(node.Name, $"kategori-{node.Id}"));
            if (!node.ParentId.HasValue)
            {
                break;
            }

            currentId = node.ParentId.Value;
        }

        if (segments.Count == 0)
        {
            segments.Push("kategori");
        }

        return segments.ToList();
    }

    private static string ToPathSegment(string? value, string fallback)
    {
        var normalized = (value ?? "")
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        var previousWasDash = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var isAsciiLetter = ch is >= 'a' and <= 'z';
            var isDigit = ch is >= '0' and <= '9';
            if (isAsciiLetter || isDigit)
            {
                sb.Append(ch);
                previousWasDash = false;
                continue;
            }

            if (previousWasDash)
            {
                continue;
            }

            sb.Append('-');
            previousWasDash = true;
        }

        var result = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private sealed class CategoryPathNode
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int? ParentId { get; init; }
    }
}
