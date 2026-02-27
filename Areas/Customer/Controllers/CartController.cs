using System.Text.Json;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models;
using Pehlione.Models.Catalog;
using Pehlione.Models.Commerce;
using Pehlione.Models.Identity;
using Pehlione.Models.Inventory;
using Pehlione.Models.ViewModels.Customer;
using Pehlione.Services;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[AllowAnonymous]
public sealed class CartController : Controller
{
    private const string CartCookieKey = "pehlione.cart";
    private const string CheckoutDraftCookieKey = "pehlione.checkout";
    private const int MaxDistinctItems = 50;
    private const int MaxQtyPerItem = 99;

    private readonly PehlioneDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CartController> _logger;

    public CartController(
        PehlioneDbContext db,
        UserManager<ApplicationUser> userManager,
        IAppEmailSender emailSender,
        INotificationService notificationService,
        ILogger<CartController> logger)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cart = ReadCart();

        if (cart.Count == 0)
            return View(new CartVm());

        var ids = cart.Select(x => x.ProductId).Distinct().ToArray();
        var productQuery = ApplyProductIdFilter(_db.Products.AsNoTracking(), ids);

        var products = await productQuery
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
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
                Color = ci.Color,
                Size = ci.Size,
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
    public async Task<IActionResult> Add(
        int productId,
        int qty = 1,
        string? color = null,
        string? size = null,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        qty = Math.Clamp(qty, 1, MaxQtyPerItem);
        color = NormalizeOption(color);
        size = NormalizeOption(size);

        var exists = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId && p.IsActive && p.Category != null && p.Category.IsActive, ct);

        if (!exists)
            return NotFound();

        var cart = ReadCart();

        var item = cart.FirstOrDefault(x =>
            x.ProductId == productId &&
            string.Equals(x.Color, color, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Size, size, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            if (cart.Count >= MaxDistinctItems)
                return RedirectToAction(nameof(Index));

            cart.Add(new CartCookieItem
            {
                ProductId = productId,
                Qty = qty,
                Color = color,
                Size = size
            });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        int productId,
        int qty,
        string? color = null,
        string? size = null,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        qty = Math.Clamp(qty, 1, MaxQtyPerItem);
        color = NormalizeOption(color);
        size = NormalizeOption(size);

        var exists = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId && p.IsActive && p.Category != null && p.Category.IsActive, ct);

        var cart = ReadCart();
        var item = cart.FirstOrDefault(x =>
            x.ProductId == productId &&
            string.Equals(x.Color, color, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Size, size, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return RedirectToReturnUrlOrCart(returnUrl);

        if (!exists)
        {
            cart.Remove(item);
            WriteCart(cart);
            return RedirectToReturnUrlOrCart(returnUrl);
        }

        item.Qty = qty;
        WriteCart(cart);

        return RedirectToReturnUrlOrCart(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productId, string? color = null, string? size = null, string? returnUrl = null)
    {
        color = NormalizeOption(color);
        size = NormalizeOption(size);
        var cart = ReadCart();
        cart.RemoveAll(x =>
            x.ProductId == productId &&
            string.Equals(x.Color, color, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Size, size, StringComparison.OrdinalIgnoreCase));
        WriteCart(cart);

        return RedirectToReturnUrlOrCart(returnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int step = 1, CancellationToken ct = default)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            var returnUrl = Url.Action(nameof(Checkout), "Cart", new { area = "Customer" }) ?? "/Customer/Cart/Checkout";
            return RedirectToAction("Login", "Account", new { area = "", returnUrl });
        }

        var cartVm = await BuildCartVmAsync(ct);
        if (cartVm.Lines.Count == 0)
        {
            TempData["CartError"] = "Sepet bos.";
            return RedirectToAction(nameof(Index));
        }

        step = Math.Clamp(step, 1, 4);
        var draft = await GetOrCreateCheckoutDraftAsync(ct);

        if (step > 1 && !IsUserStepComplete(draft))
            return RedirectToAction(nameof(Checkout), new { step = 1 });

        if (step > 2 && !IsAddressStepComplete(draft))
            return RedirectToAction(nameof(Checkout), new { step = 2 });

        if (step > 3 && !IsPaymentStepComplete(draft))
            return RedirectToAction(nameof(Checkout), new { step = 3 });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var (savedAddresses, savedPaymentMethods) = await LoadSavedCheckoutOptionsAsync(userId, ct);
        return View(CreateCheckoutVm(step, cartVm, draft, savedAddresses, savedPaymentMethods));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutUser([Bind(Prefix = "User")] CheckoutUserStepVm model, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/Customer/Cart/Checkout" });

        model.FullName = (model.FullName ?? "").Trim();
        model.Email = (model.Email ?? "").Trim();
        model.Phone = (model.Phone ?? "").Trim();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                if (string.IsNullOrWhiteSpace(model.FullName))
                    model.FullName = BuildDisplayName(user.UserName, user.Email);
                if (string.IsNullOrWhiteSpace(model.Email))
                    model.Email = user.Email ?? "";
                if (string.IsNullOrWhiteSpace(model.Phone))
                    model.Phone = user.PhoneNumber ?? "";
            }
        }

        if (!TryValidateModel(model, nameof(CheckoutVm.User)))
            return await RenderCheckoutStepWithModelErrorsAsync(1, model, null, null, ct);

        var draft = ReadCheckoutDraft();
        draft.FullName = model.FullName.Trim();
        draft.Email = model.Email.Trim();
        draft.Phone = model.Phone.Trim();
        WriteCheckoutDraft(draft);

        return RedirectToAction(nameof(Checkout), new { step = 2 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutAddress([Bind(Prefix = "Address")] CheckoutAddressStepVm model, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/Customer/Cart/Checkout" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (model.SelectedAddressId.HasValue)
        {
            var selectedAddress = await _db.UserAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.SelectedAddressId.Value && x.UserId == userId, ct);

            if (selectedAddress is null)
            {
                ModelState.AddModelError(nameof(CheckoutVm.Address) + "." + nameof(CheckoutAddressStepVm.SelectedAddressId), "Secilen adres bulunamadi.");
                return await RenderCheckoutStepWithModelErrorsAsync(2, null, model, null, ct);
            }

            var draftFromSelection = ReadCheckoutDraft();
            draftFromSelection.AddressTitle = string.IsNullOrWhiteSpace(selectedAddress.Company) ? "Teslimat" : selectedAddress.Company!;
            draftFromSelection.Street = selectedAddress.Street;
            draftFromSelection.HouseNumber = selectedAddress.HouseNumber;
            draftFromSelection.AddressLine2 = selectedAddress.AddressLine2 ?? "";
            draftFromSelection.City = selectedAddress.City;
            draftFromSelection.PostalCode = selectedAddress.PostalCode;
            draftFromSelection.State = selectedAddress.State ?? "";
            draftFromSelection.CountryCode = selectedAddress.CountryCode;
            draftFromSelection.AddressPhone = selectedAddress.PhoneNumber ?? "";
            draftFromSelection.SelectedAddressId = selectedAddress.Id;
            WriteCheckoutDraft(draftFromSelection);

            return RedirectToAction(nameof(Checkout), new { step = 3 });
        }

        model.Title = (model.Title ?? "").Trim();
        model.Street = (model.Street ?? "").Trim();
        model.HouseNumber = (model.HouseNumber ?? "").Trim();
        model.Line2 = (model.Line2 ?? "").Trim();
        model.City = (model.City ?? "").Trim();
        model.PostalCode = (model.PostalCode ?? "").Trim();
        model.State = (model.State ?? "").Trim();
        model.CountryCode = (model.CountryCode ?? "").Trim().ToUpperInvariant();
        model.PhoneNumber = (model.PhoneNumber ?? "").Trim();

        if (!TryValidateModel(model, nameof(CheckoutVm.Address)))
            return await RenderCheckoutStepWithModelErrorsAsync(2, null, model, null, ct);

        var draft = ReadCheckoutDraft();
        draft.AddressTitle = model.Title.Trim();
        draft.Street = model.Street.Trim();
        draft.HouseNumber = model.HouseNumber.Trim();
        draft.AddressLine2 = (model.Line2 ?? "").Trim();
        draft.City = model.City.Trim();
        draft.PostalCode = (model.PostalCode ?? "").Trim();
        draft.State = (model.State ?? "").Trim();
        draft.CountryCode = model.CountryCode.Trim().ToUpperInvariant();
        draft.AddressPhone = (model.PhoneNumber ?? "").Trim();
        WriteCheckoutDraft(draft);

        var (firstName, lastName) = SplitName(draft.FullName);
        var existingDefault = await _db.UserAddresses
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Type == AddressType.Shipping && x.IsDefault, ct);

        if (existingDefault is null)
        {
            existingDefault = new UserAddress
            {
                UserId = userId,
                Type = AddressType.Shipping,
                IsDefault = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.UserAddresses.Add(existingDefault);
        }

        existingDefault.FirstName = firstName;
        existingDefault.LastName = lastName;
        existingDefault.Company = model.Title;
        existingDefault.Street = model.Street;
        existingDefault.HouseNumber = model.HouseNumber;
        existingDefault.AddressLine2 = string.IsNullOrWhiteSpace(model.Line2) ? null : model.Line2;
        existingDefault.PostalCode = model.PostalCode ?? "";
        existingDefault.City = model.City;
        existingDefault.State = string.IsNullOrWhiteSpace(model.State) ? null : model.State;
        existingDefault.CountryCode = model.CountryCode;
        existingDefault.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber;
        existingDefault.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        draft.SelectedAddressId = existingDefault.Id;
        WriteCheckoutDraft(draft);

        return RedirectToAction(nameof(Checkout), new { step = 3 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutPayment([Bind(Prefix = "Payment")] CheckoutPaymentStepVm model, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/Customer/Cart/Checkout" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (model.SelectedPaymentMethodId.HasValue)
        {
            var selectedPaymentMethod = await _db.UserPaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.SelectedPaymentMethodId.Value && x.UserId == userId, ct);

            if (selectedPaymentMethod is null)
            {
                ModelState.AddModelError(nameof(CheckoutVm.Payment) + "." + nameof(CheckoutPaymentStepVm.SelectedPaymentMethodId), "Secilen odeme yontemi bulunamadi.");
                return await RenderCheckoutStepWithModelErrorsAsync(3, null, null, model, ct);
            }

            var draftFromSelection = ReadCheckoutDraft();
            draftFromSelection.PaymentMethod = selectedPaymentMethod.Type.ToString();
            draftFromSelection.CardHolder = selectedPaymentMethod.DisplayName;
            draftFromSelection.CardLast4 = selectedPaymentMethod.CardLast4 ?? "";
            draftFromSelection.SelectedPaymentMethodId = selectedPaymentMethod.Id;
            WriteCheckoutDraft(draftFromSelection);

            return RedirectToAction(nameof(Checkout), new { step = 4 });
        }

        if (!TryValidateModel(model, nameof(CheckoutVm.Payment)))
            return await RenderCheckoutStepWithModelErrorsAsync(3, null, null, model, ct);

        var draft = ReadCheckoutDraft();
        draft.PaymentMethod = model.Method.Trim();
        draft.CardHolder = (model.CardHolder ?? "").Trim();
        draft.CardLast4 = (model.CardLast4 ?? "").Trim();
        draft.SelectedPaymentMethodId = null;
        WriteCheckoutDraft(draft);

        var type = ParsePaymentMethodTypeOrDefault(model.Method);
        var displayName = BuildPaymentDisplayName(type, draft.CardLast4, draft.CardHolder);

        var existingDefaultMethod = await _db.UserPaymentMethods
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Type == type && x.IsDefault, ct);

        if (existingDefaultMethod is null)
        {
            existingDefaultMethod = new UserPaymentMethod
            {
                UserId = userId,
                Type = type,
                IsDefault = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.UserPaymentMethods.Add(existingDefaultMethod);
        }

        existingDefaultMethod.DisplayName = displayName;
        existingDefaultMethod.ProviderReference = null;
        existingDefaultMethod.CardLast4 = string.IsNullOrWhiteSpace(draft.CardLast4) ? null : draft.CardLast4;
        existingDefaultMethod.ExpMonth = null;
        existingDefaultMethod.ExpYear = null;
        existingDefaultMethod.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        draft.SelectedPaymentMethodId = existingDefaultMethod.Id;
        WriteCheckoutDraft(draft);

        return RedirectToAction(nameof(Checkout), new { step = 4 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            var returnUrl = Url.Action(nameof(Index), "Cart", new { area = "Customer" }) ?? "/Customer/Cart";
            return RedirectToAction("Login", "Account", new { area = "", returnUrl });
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var draft = ReadCheckoutDraft();
        if (!IsUserStepComplete(draft))
            return RedirectToAction(nameof(Checkout), new { step = 1 });
        if (!IsAddressStepComplete(draft))
            return RedirectToAction(nameof(Checkout), new { step = 2 });
        if (!IsPaymentStepComplete(draft))
            return RedirectToAction(nameof(Checkout), new { step = 3 });

        var cart = ReadCart();
        if (cart.Count == 0)
        {
            TempData["CartError"] = "Sepet bos.";
            return RedirectToAction(nameof(Index));
        }

        var ids = cart.Select(x => x.ProductId).Distinct().ToArray();
        var productQuery = ApplyProductIdFilter(_db.Products.AsNoTracking(), ids);
        var products = await productQuery
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Price
            })
            .ToListAsync(ct);

        var productMap = products.ToDictionary(x => x.Id, x => x);

        var order = new Order
        {
            UserId = userId,
            Currency = "TRY",
            Status = "Pending"
        };

        decimal total = 0;
        foreach (var ci in cart)
        {
            if (!productMap.TryGetValue(ci.ProductId, out var p))
                continue;

            var qty = Math.Clamp(ci.Qty, 1, MaxQtyPerItem);
            var sub = p.Price * qty;

            order.Items.Add(new OrderItem
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

        if (order.Items.Count == 0)
        {
            TempData["CartError"] = "Sepette satin alinabilir urun bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        order.TotalAmount = total;
        var requiredStocks = order.Items
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct);

            foreach (var required in requiredStocks)
            {
                var updatedRows = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE stocks SET quantity = quantity - {required.Quantity} WHERE product_id = {required.ProductId} AND quantity >= {required.Quantity}",
                    ct);

                if (updatedRows == 0)
                {
                    await tx.RollbackAsync(ct);
                    TempData["CartError"] = "Stok yetersiz. Lutfen sepetinizi guncelleyip tekrar deneyin.";
                    return RedirectToAction(nameof(Index));
                }

                _db.StockMovements.Add(new StockMovement
                {
                    ProductId = required.ProductId,
                    Type = StockMovementType.Out,
                    Quantity = required.Quantity,
                    Reason = $"Order #{order.Id}",
                    CreatedByUserId = userId
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await TrySendOrderEmailAsync(userId, order, draft, ct);
        await _notificationService.CreateAsync(
            department: "Purchasing",
            title: "Stok dusumu gerceklesti",
            message: $"Siparis #{order.Id} ile {requiredStocks.Count} urun kaleminde stok dusumu yapildi.",
            relatedEntityType: "Order",
            relatedEntityId: order.Id.ToString(),
            ct: ct);

        ClearCart();
        ClearCheckoutDraft();
        TempData["CheckoutSuccess"] = $"Siparisiniz olusturuldu. Siparis no: #{order.Id}";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToReturnUrlOrCart(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private static IQueryable<Product> ApplyProductIdFilter(IQueryable<Product> query, IReadOnlyList<int> productIds)
    {
        if (productIds.Count == 0)
            return query.Where(_ => false);

        var parameter = Expression.Parameter(typeof(Product), "p");
        var member = Expression.Property(parameter, nameof(Product.Id));
        Expression body = Expression.Equal(member, Expression.Constant(productIds[0]));

        for (var i = 1; i < productIds.Count; i++)
        {
            var next = Expression.Equal(member, Expression.Constant(productIds[i]));
            body = Expression.OrElse(body, next);
        }

        var lambda = Expression.Lambda<Func<Product, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private async Task<CartVm> BuildCartVmAsync(CancellationToken ct)
    {
        var cart = ReadCart();
        if (cart.Count == 0)
            return new CartVm();

        var ids = cart.Select(x => x.ProductId).Distinct().ToArray();
        var productQuery = ApplyProductIdFilter(_db.Products.AsNoTracking(), ids);
        var products = await productQuery
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
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
                Color = ci.Color,
                Size = ci.Size,
                UnitPrice = p.Price,
                Quantity = qty,
                Subtotal = sub
            });
            total += sub;
        }

        return new CartVm
        {
            Lines = lines,
            Total = total
        };
    }

    private async Task<CheckoutDraft> GetOrCreateCheckoutDraftAsync(CancellationToken ct)
    {
        var draft = ReadCheckoutDraft();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return draft;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            if (string.IsNullOrWhiteSpace(draft.FullName))
                draft.FullName = BuildDisplayName(user.UserName, user.Email);
            if (string.IsNullOrWhiteSpace(draft.Email))
                draft.Email = user.Email ?? "";
            if (string.IsNullOrWhiteSpace(draft.Phone))
                draft.Phone = user.PhoneNumber ?? "";
        }

        var defaultShipping = await _db.UserAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == AddressType.Shipping && x.IsDefault)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (defaultShipping is not null)
        {
            if (string.IsNullOrWhiteSpace(draft.AddressTitle))
                draft.AddressTitle = defaultShipping.Company ?? "Teslimat";
            if (string.IsNullOrWhiteSpace(draft.Street))
                draft.Street = defaultShipping.Street;
            if (string.IsNullOrWhiteSpace(draft.HouseNumber))
                draft.HouseNumber = defaultShipping.HouseNumber;
            if (string.IsNullOrWhiteSpace(draft.AddressLine2))
                draft.AddressLine2 = defaultShipping.AddressLine2 ?? "";
            if (string.IsNullOrWhiteSpace(draft.City))
                draft.City = defaultShipping.City;
            if (string.IsNullOrWhiteSpace(draft.PostalCode))
                draft.PostalCode = defaultShipping.PostalCode;
            if (string.IsNullOrWhiteSpace(draft.State))
                draft.State = defaultShipping.State ?? "";
            if (string.IsNullOrWhiteSpace(draft.CountryCode))
                draft.CountryCode = defaultShipping.CountryCode;
            if (string.IsNullOrWhiteSpace(draft.AddressPhone))
                draft.AddressPhone = defaultShipping.PhoneNumber ?? "";
            if (!draft.SelectedAddressId.HasValue)
                draft.SelectedAddressId = defaultShipping.Id;
        }

        var defaultPaymentMethod = await _db.UserPaymentMethods
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsDefault)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (defaultPaymentMethod is not null)
        {
            if (string.IsNullOrWhiteSpace(draft.PaymentMethod))
                draft.PaymentMethod = defaultPaymentMethod.Type.ToString();
            if (string.IsNullOrWhiteSpace(draft.CardHolder))
                draft.CardHolder = defaultPaymentMethod.DisplayName;
            if (string.IsNullOrWhiteSpace(draft.CardLast4))
                draft.CardLast4 = defaultPaymentMethod.CardLast4 ?? "";
            if (!draft.SelectedPaymentMethodId.HasValue)
                draft.SelectedPaymentMethodId = defaultPaymentMethod.Id;
        }

        WriteCheckoutDraft(draft);
        return draft;
    }

    private static bool IsUserStepComplete(CheckoutDraft draft)
    {
        return !string.IsNullOrWhiteSpace(draft.FullName)
            && !string.IsNullOrWhiteSpace(draft.Email)
            && !string.IsNullOrWhiteSpace(draft.Phone);
    }

    private static bool IsAddressStepComplete(CheckoutDraft draft)
    {
        return !string.IsNullOrWhiteSpace(draft.AddressTitle)
            && !string.IsNullOrWhiteSpace(draft.Street)
            && !string.IsNullOrWhiteSpace(draft.HouseNumber)
            && !string.IsNullOrWhiteSpace(draft.City)
            && !string.IsNullOrWhiteSpace(draft.PostalCode)
            && !string.IsNullOrWhiteSpace(draft.CountryCode);
    }

    private static bool IsPaymentStepComplete(CheckoutDraft draft)
    {
        return !string.IsNullOrWhiteSpace(draft.PaymentMethod);
    }

    private static CheckoutVm CreateCheckoutVm(
        int step,
        CartVm cartVm,
        CheckoutDraft draft,
        IReadOnlyList<CheckoutAddressOptionVm> savedAddresses,
        IReadOnlyList<CheckoutPaymentMethodOptionVm> savedPaymentMethods)
    {
        return new CheckoutVm
        {
            Step = step,
            Cart = cartVm,
            SavedAddresses = savedAddresses,
            SavedPaymentMethods = savedPaymentMethods,
            User = new CheckoutUserStepVm
            {
                FullName = draft.FullName,
                Email = draft.Email,
                Phone = draft.Phone
            },
            Address = new CheckoutAddressStepVm
            {
                SelectedAddressId = draft.SelectedAddressId,
                Title = draft.AddressTitle,
                Street = draft.Street,
                HouseNumber = draft.HouseNumber,
                Line2 = draft.AddressLine2,
                City = draft.City,
                PostalCode = draft.PostalCode,
                State = draft.State,
                CountryCode = string.IsNullOrWhiteSpace(draft.CountryCode) ? "DE" : draft.CountryCode,
                PhoneNumber = draft.AddressPhone
            },
            Payment = new CheckoutPaymentStepVm
            {
                SelectedPaymentMethodId = draft.SelectedPaymentMethodId,
                Method = string.IsNullOrWhiteSpace(draft.PaymentMethod) ? PaymentMethodType.Visa.ToString() : draft.PaymentMethod,
                CardHolder = draft.CardHolder,
                CardLast4 = draft.CardLast4
            }
        };
    }

    private async Task<IActionResult> RenderCheckoutStepWithModelErrorsAsync(
        int step,
        CheckoutUserStepVm? user,
        CheckoutAddressStepVm? address,
        CheckoutPaymentStepVm? payment,
        CancellationToken ct)
    {
        var cartVm = await BuildCartVmAsync(ct);
        var draft = ReadCheckoutDraft();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var (savedAddresses, savedPaymentMethods) = await LoadSavedCheckoutOptionsAsync(userId, ct);
        var vm = CreateCheckoutVm(step, cartVm, draft, savedAddresses, savedPaymentMethods);

        if (user is not null)
            vm.User = user;
        if (address is not null)
            vm.Address = address;
        if (payment is not null)
            vm.Payment = payment;

        return View("Checkout", vm);
    }

    private async Task<(IReadOnlyList<CheckoutAddressOptionVm> addresses, IReadOnlyList<CheckoutPaymentMethodOptionVm> payments)> LoadSavedCheckoutOptionsAsync(
        string? userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (Array.Empty<CheckoutAddressOptionVm>(), Array.Empty<CheckoutPaymentMethodOptionVm>());

        var addresses = await _db.UserAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Select(x => new CheckoutAddressOptionVm
            {
                Id = x.Id,
                IsDefault = x.IsDefault,
                Label = $"{x.Street} {x.HouseNumber}, {x.PostalCode} {x.City} ({x.CountryCode})"
            })
            .ToListAsync(ct);

        var paymentMethods = await _db.UserPaymentMethods
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Select(x => new CheckoutPaymentMethodOptionVm
            {
                Id = x.Id,
                IsDefault = x.IsDefault,
                Label = x.DisplayName,
                Type = x.Type.ToString()
            })
            .ToListAsync(ct);

        return (addresses, paymentMethods);
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

    private void ClearCart()
    {
        Response.Cookies.Delete(CartCookieKey);
    }

    private CheckoutDraft ReadCheckoutDraft()
    {
        if (!Request.Cookies.TryGetValue(CheckoutDraftCookieKey, out var json) || string.IsNullOrWhiteSpace(json))
            return new CheckoutDraft();

        try
        {
            return JsonSerializer.Deserialize<CheckoutDraft>(json) ?? new CheckoutDraft();
        }
        catch
        {
            return new CheckoutDraft();
        }
    }

    private void WriteCheckoutDraft(CheckoutDraft draft)
    {
        var json = JsonSerializer.Serialize(draft);
        Response.Cookies.Append(
            CheckoutDraftCookieKey,
            json,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(2)
            });
    }

    private void ClearCheckoutDraft()
    {
        Response.Cookies.Delete(CheckoutDraftCookieKey);
    }

    private static string? NormalizeOption(string? value)
    {
        var normalized = (value ?? "").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static PaymentMethodType ParsePaymentMethodTypeOrDefault(string? value)
    {
        if (Enum.TryParse<PaymentMethodType>((value ?? "").Trim(), true, out var parsed))
            return parsed;

        return PaymentMethodType.Visa;
    }

    private static string BuildPaymentDisplayName(PaymentMethodType type, string? cardLast4, string? cardHolder)
    {
        if (type is PaymentMethodType.Visa or PaymentMethodType.Mastercard)
        {
            var last4 = (cardLast4 ?? "").Trim();
            return string.IsNullOrWhiteSpace(last4) ? type.ToString() : $"{type} •••• {last4}";
        }

        var holder = (cardHolder ?? "").Trim();
        return string.IsNullOrWhiteSpace(holder) ? type.ToString() : $"{type} ({holder})";
    }

    private static string BuildDisplayName(string? userName, string? email)
    {
        var normalizedUserName = (userName ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedUserName))
            return normalizedUserName;

        var normalizedEmail = (email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return "";

        var at = normalizedEmail.IndexOf('@');
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    private static (string firstName, string lastName) SplitName(string fullName)
    {
        var normalized = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return ("Musteri", "Hesabi");

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
            return (parts[0], parts[0]);

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private async Task TrySendOrderEmailAsync(string userId, Order order, CheckoutDraft draft, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            var email = user?.Email;
            if (string.IsNullOrWhiteSpace(email))
                return;

            var subject = $"Siparisiniz alindi #{order.Id}";
            var body = $"""
                        <h2>Siparisiniz alindi</h2>
                        <p>Siparis numaraniz: <strong>#{order.Id}</strong></p>
                        <p>Toplam tutar: <strong>{order.TotalAmount:0.00} {order.Currency}</strong></p>
                        <p>Durum: {order.Status}</p>
                        <p>Olusturma tarihi (UTC): {order.CreatedAt:yyyy-MM-dd HH:mm:ss}</p>
                        <hr/>
                        <p><strong>Ad Soyad:</strong> {draft.FullName}</p>
                        <p><strong>Telefon:</strong> {draft.Phone}</p>
                        <p><strong>Adres:</strong> {draft.Street} {draft.HouseNumber} {draft.AddressLine2}, {draft.City}, {draft.PostalCode}, {draft.CountryCode}</p>
                        <p><strong>Odeme:</strong> {draft.PaymentMethod}</p>
                        """;

            await _emailSender.SendAsync(email, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order confirmation email could not be sent for order {OrderId}", order.Id);
        }
    }

    private sealed class CartCookieItem
    {
        public int ProductId { get; set; }
        public int Qty { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
    }

    private sealed class CheckoutDraft
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public int? SelectedAddressId { get; set; }
        public string AddressTitle { get; set; } = "";
        public string Street { get; set; } = "";
        public string HouseNumber { get; set; } = "";
        public string AddressLine2 { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string State { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public string AddressPhone { get; set; } = "";
        public int? SelectedPaymentMethodId { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string CardHolder { get; set; } = "";
        public string CardLast4 { get; set; } = "";
    }
}
