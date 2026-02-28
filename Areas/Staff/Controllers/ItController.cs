using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.Security;
using Pehlione.Models.ViewModels.Staff;
using Pehlione.Security;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleIt},{IdentitySeed.RoleAdmin}")]
public sealed class ItController : Controller
{
    private readonly PehlioneDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ItController(PehlioneDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["CreatePersonnelModel"] = new ItCreatePersonnelVm();
        var vm = await BuildVmAsync(ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DepartmentConstraintsVm model, CancellationToken ct)
    {
        var supported = DepartmentConstraintService.GetSupportedDepartments();
        var items = (model.Items ?? new List<DepartmentConstraintEditItemVm>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Department))
            .Where(x => supported.Contains(x.Department, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.Department, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        foreach (var item in items)
        {
            var context = new ValidationContext(item);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(item, context, results, true))
            {
                foreach (var err in results)
                {
                    ModelState.AddModelError(string.Empty, err.ErrorMessage ?? "Gecersiz alan.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            ViewData["CreatePersonnelModel"] = new ItCreatePersonnelVm();
            return View(new DepartmentConstraintsVm { Items = items });
        }

        var entities = await _db.Set<DepartmentConstraint>()
            .Where(x => supported.Contains(x.Department))
            .ToListAsync(ct);

        var byDepartment = entities.ToDictionary(x => x.Department, StringComparer.OrdinalIgnoreCase);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        foreach (var item in items)
        {
            if (!byDepartment.TryGetValue(item.Department, out var entity))
            {
                entity = new DepartmentConstraint
                {
                    Department = item.Department
                };
                _db.Add(entity);
                byDepartment[item.Department] = entity;
            }

            entity.CanIncreaseStock = item.CanIncreaseStock;
            entity.CanDeleteStock = item.CanDeleteStock;
            entity.MaxReceiveQuantity = item.MaxReceiveQuantity;
            entity.UpdatedByUserId = userId;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        TempData["ItConstraintSaved"] = "Departman kisitlari guncellendi.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePersonnel(ItCreatePersonnelVm model, CancellationToken ct)
    {
        var allowedRoles = new[] { IdentitySeed.RoleStaff, IdentitySeed.RolePurchasing, IdentitySeed.RoleWarehouse, IdentitySeed.RoleIt, IdentitySeed.RoleHr, IdentitySeed.RoleAccounting };
        if (!allowedRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Gecersiz rol secimi.");

        if (!ModelState.IsValid)
        {
            ViewData["CreatePersonnelModel"] = model;
            return View("Index", await BuildVmAsync(ct));
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta zaten kayitli.");
            ViewData["CreatePersonnelModel"] = model;
            return View("Index", await BuildVmAsync(ct));
        }

        var user = new ApplicationUser
        {
            Email = model.Email,
            UserName = model.Email,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var err in create.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            ViewData["CreatePersonnelModel"] = model;
            return View("Index", await BuildVmAsync(ct));
        }

        var addRole = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addRole.Succeeded)
        {
            foreach (var err in addRole.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            ViewData["CreatePersonnelModel"] = model;
            return View("Index", await BuildVmAsync(ct));
        }

        await _userManager.AddClaimAsync(user, new Claim(PehlioneClaimTypes.MustChangePassword, "true"));

        TempData["ItConstraintSaved"] = $"Personel olusturuldu: {model.Email}";
        return RedirectToAction(nameof(Index));
    }

    private async Task<DepartmentConstraintsVm> BuildVmAsync(CancellationToken ct)
    {
        var supported = DepartmentConstraintService.GetSupportedDepartments();
        var saved = await _db.Set<DepartmentConstraint>()
            .AsNoTracking()
            .Where(x => supported.Contains(x.Department))
            .ToListAsync(ct);

        var items = new List<DepartmentConstraintEditItemVm>();
        foreach (var department in supported)
        {
            var item = saved.FirstOrDefault(x => x.Department == department)
                ?? DepartmentConstraintService.GetDefaultConstraint(department);

            items.Add(new DepartmentConstraintEditItemVm
            {
                Department = department,
                CanIncreaseStock = item.CanIncreaseStock,
                CanDeleteStock = item.CanDeleteStock,
                MaxReceiveQuantity = item.MaxReceiveQuantity
            });
        }

        return new DepartmentConstraintsVm
        {
            Items = items
        };
    }
}
