using System.Text.Json;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Catalog;
using Pehlione.Models.Commerce;
using Pehlione.Models.Identity;
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
    private readonly ILogger<CartController> _logger;

    public CartController(
        PehlioneDbContext db,
        UserManager<ApplicationUser> userManager,
        IAppEmailSender emailSender,
        ILogger<CartController> logger)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
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

        return View(CreateCheckoutVm(step, cartVm, draft));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutUser(CheckoutUserStepVm model, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/Customer/Cart/Checkout" });

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
    public async Task<IActionResult> CheckoutAddress(CheckoutAddressStepVm model, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/Customer/Cart/Checkout" });

        if (!TryValidateModel(model, nameof(CheckoutVm.Address)))
            return await RenderCheckoutStepWithModelErrorsAsync(2, null, model, null, ct);

        var draft = ReadCheckoutDraft();
        draft.AddressTitle = model.Title.Trim();
        draft.AddressLine1 = model.Line1.Trim();
        draft.AddressLine2 = (model.Line2 ?? "").Trim();
        draft.City = model.City.Trim();
        draft.PostalCode = (model.PostalCode ?? "").Trim();
        draft.Country = model.Country.Trim();
        WriteCheckoutDraft(draft);

        return RedirectToAction(nameof(Checkout), new { step = 3 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutPayment(CheckoutPaymentStepVm model, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/Customer/Cart/Checkout" });

        if (!TryValidateModel(model, nameof(CheckoutVm.Payment)))
            return await RenderCheckoutStepWithModelErrorsAsync(3, null, null, model, ct);

        var draft = ReadCheckoutDraft();
        draft.PaymentMethod = model.Method.Trim();
        draft.CardHolder = (model.CardHolder ?? "").Trim();
        draft.CardLast4 = (model.CardLast4 ?? "").Trim();
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
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        await TrySendOrderEmailAsync(userId, order, draft, ct);

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
        if (IsUserStepComplete(draft))
            return draft;

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return draft;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return draft;

        draft.FullName = string.IsNullOrWhiteSpace(draft.FullName) ? (user.UserName ?? "") : draft.FullName;
        draft.Email = string.IsNullOrWhiteSpace(draft.Email) ? (user.Email ?? "") : draft.Email;
        draft.Phone = string.IsNullOrWhiteSpace(draft.Phone) ? (user.PhoneNumber ?? "") : draft.Phone;
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
            && !string.IsNullOrWhiteSpace(draft.AddressLine1)
            && !string.IsNullOrWhiteSpace(draft.City)
            && !string.IsNullOrWhiteSpace(draft.Country);
    }

    private static bool IsPaymentStepComplete(CheckoutDraft draft)
    {
        return !string.IsNullOrWhiteSpace(draft.PaymentMethod);
    }

    private static CheckoutVm CreateCheckoutVm(int step, CartVm cartVm, CheckoutDraft draft)
    {
        return new CheckoutVm
        {
            Step = step,
            Cart = cartVm,
            User = new CheckoutUserStepVm
            {
                FullName = draft.FullName,
                Email = draft.Email,
                Phone = draft.Phone
            },
            Address = new CheckoutAddressStepVm
            {
                Title = draft.AddressTitle,
                Line1 = draft.AddressLine1,
                Line2 = draft.AddressLine2,
                City = draft.City,
                PostalCode = draft.PostalCode,
                Country = string.IsNullOrWhiteSpace(draft.Country) ? "TR" : draft.Country
            },
            Payment = new CheckoutPaymentStepVm
            {
                Method = string.IsNullOrWhiteSpace(draft.PaymentMethod) ? "Card" : draft.PaymentMethod,
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
        var vm = CreateCheckoutVm(step, cartVm, draft);

        if (user is not null)
            vm.User = user;
        if (address is not null)
            vm.Address = address;
        if (payment is not null)
            vm.Payment = payment;

        return View("Checkout", vm);
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
                        <p><strong>Adres:</strong> {draft.AddressLine1} {draft.AddressLine2}, {draft.City}, {draft.PostalCode}, {draft.Country}</p>
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
        public string AddressTitle { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string AddressLine2 { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string Country { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public string CardHolder { get; set; } = "";
        public string CardLast4 { get; set; } = "";
    }
}
