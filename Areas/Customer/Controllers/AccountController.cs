using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models;
using Pehlione.Models.Commerce;
using Pehlione.Models.Communication;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Customer;
using Pehlione.Services;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize(Roles = IdentitySeed.RoleCustomer)]
public sealed class AccountController : Controller
{
    private readonly PehlioneDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOrderStatusEmailService _orderStatusEmailService;
    private readonly IOrderWorkflowNotificationService _orderWorkflowNotificationService;
    private readonly IOrderStatusTimelineService _orderStatusTimelineService;
    private readonly INotificationService _notificationService;
    private readonly IAppEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public AccountController(
        PehlioneDbContext db,
        UserManager<ApplicationUser> userManager,
        IOrderStatusEmailService orderStatusEmailService,
        IOrderWorkflowNotificationService orderWorkflowNotificationService,
        IOrderStatusTimelineService orderStatusTimelineService,
        INotificationService notificationService,
        IAppEmailSender emailSender,
        IConfiguration configuration)
    {
        _db = db;
        _userManager = userManager;
        _orderStatusEmailService = orderStatusEmailService;
        _orderWorkflowNotificationService = orderWorkflowNotificationService;
        _orderStatusTimelineService = orderStatusTimelineService;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? editAddressId = null, int? editPaymentId = null, CancellationToken ct = default)
    {
        var vm = await BuildVmAsync(editAddressId, editPaymentId, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileUpdateVm model, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["AccountError"] = "Profil formu gecersiz.";
            return RedirectToAction(nameof(Index));
        }

        var email = (model.Email ?? "").Trim();
        var userName = (model.UserName ?? "").Trim();
        var phone = (model.PhoneNumber ?? "").Trim();

        var emailOwner = await _userManager.FindByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != user.Id)
        {
            TempData["AccountError"] = "Bu e-posta baska bir kullanicida kayitli.";
            return RedirectToAction(nameof(Index));
        }

        user.Email = email;
        user.UserName = string.IsNullOrWhiteSpace(userName) ? email : userName;
        user.PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            TempData["AccountError"] = string.Join(" | ", update.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["AccountSuccess"] = "Profil bilgileri guncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PasswordChangeVm model, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["AccountError"] = "Sifre formu gecersiz.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            TempData["AccountError"] = string.Join(" | ", result.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["AccountSuccess"] = "Sifreniz degistirildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAddress(AddressEditVm model, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        model.FirstName = (model.FirstName ?? "").Trim();
        model.LastName = (model.LastName ?? "").Trim();
        model.Street = (model.Street ?? "").Trim();
        model.HouseNumber = (model.HouseNumber ?? "").Trim();
        model.PostalCode = (model.PostalCode ?? "").Trim();
        model.City = (model.City ?? "").Trim();
        model.CountryCode = (model.CountryCode ?? "").Trim().ToUpperInvariant();

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Values
                .SelectMany(x => x.Errors)
                .Select(e => e.ErrorMessage)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();
            TempData["AccountError"] = errors.Count == 0
                ? "Adres formu gecersiz."
                : "Adres formu gecersiz: " + string.Join(" | ", errors);
            return RedirectToAction(nameof(Index));
        }

        UserAddress entity;
        if (model.Id.HasValue && model.Id.Value > 0)
        {
            entity = await _db.UserAddresses.FirstOrDefaultAsync(x => x.Id == model.Id.Value && x.UserId == userId, ct) ?? new UserAddress { UserId = userId };
            if (entity.Id == 0)
            {
                TempData["AccountError"] = "Adres kaydi bulunamadi.";
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            entity = new UserAddress { UserId = userId, Type = AddressType.Shipping, CreatedAtUtc = DateTime.UtcNow };
            _db.UserAddresses.Add(entity);
        }

        entity.FirstName = model.FirstName.Trim();
        entity.LastName = model.LastName.Trim();
        entity.Company = string.IsNullOrWhiteSpace(model.Company) ? null : model.Company.Trim();
        entity.Street = model.Street.Trim();
        entity.HouseNumber = model.HouseNumber.Trim();
        entity.AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim();
        entity.PostalCode = model.PostalCode.Trim();
        entity.City = model.City.Trim();
        entity.State = string.IsNullOrWhiteSpace(model.State) ? null : model.State.Trim();
        entity.CountryCode = model.CountryCode.Trim().ToUpperInvariant();
        entity.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        entity.Type = AddressType.Shipping;
        entity.IsDefault = model.IsDefault;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (model.IsDefault)
        {
            await _db.UserAddresses
                .Where(x => x.UserId == userId && x.Id != entity.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
        }

        await _db.SaveChangesAsync(ct);
        TempData["AccountSuccess"] = "Adres kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var entity = await _db.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (entity is null)
        {
            TempData["AccountError"] = "Adres bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        _db.UserAddresses.Remove(entity);
        await _db.SaveChangesAsync(ct);

        TempData["AccountSuccess"] = "Adres silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (id <= 0)
        {
            TempData["AccountError"] = "Gecersiz siparis.";
            return RedirectToAction(nameof(Index));
        }

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (order is null)
        {
            TempData["AccountError"] = "Siparis bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        var current = OrderStatusWorkflow.Normalize(order.Status);
        if (!OrderStatusWorkflow.CanTransition(current, OrderStatusWorkflow.Cancelled))
        {
            TempData["AccountError"] = $"Bu siparis bu asamada iptal edilemez ({current}).";
            return RedirectToAction(nameof(Index));
        }

        var oldStatus = order.Status;
        order.Status = OrderStatusWorkflow.Cancelled;
        await _db.SaveChangesAsync(ct);
        await _orderStatusTimelineService.LogStatusChangedAsync(
            orderId: order.Id,
            fromStatus: oldStatus,
            toStatus: OrderStatusWorkflow.Cancelled,
            changedByUserId: userId,
            changedByDepartment: "Customer",
            ct: ct);
        await _orderStatusEmailService.NotifyStatusChangedAsync(order, oldStatus, OrderStatusWorkflow.Cancelled, ct);
        await _orderWorkflowNotificationService.OnStatusChangedAsync(order, oldStatus, OrderStatusWorkflow.Cancelled, ct);

        TempData["AccountSuccess"] = $"Siparis #{id} iptal edildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactCustomerRelations(CustomerRelationsMessageVm model, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["AccountError"] = "Iletisim formu gecersiz.";
            return RedirectToAction(nameof(Index));
        }

        var subject = (model.Subject ?? "").Trim();
        var message = (model.Message ?? "").Trim();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["AccountError"] = "Konu ve mesaj zorunludur.";
            return RedirectToAction(nameof(Index));
        }

        var customerEmail = user.Email ?? user.UserName ?? "unknown@pehlione.local";
        var relationsEmail = _configuration["Mail:CustomerRelationsEmail"] ?? "support@pehlione.local";
        var mailSubject = $"Musteri Iliskileri Talebi - {customerEmail}";
        var mailBody = $@"
            <h2>Musteri Iliskileri Talebi</h2>
            <p><strong>Musteri:</strong> {System.Net.WebUtility.HtmlEncode(customerEmail)}</p>
            <p><strong>Konu:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>
            <p><strong>Mesaj:</strong><br />{System.Net.WebUtility.HtmlEncode(message).Replace("\n", "<br />")}</p>
        ";

        _db.CustomerRelationsMessages.Add(new CustomerRelationsMessage
        {
            UserId = userId,
            CustomerEmail = customerEmail,
            Subject = subject,
            Message = message
        });
        await _db.SaveChangesAsync(ct);

        await _emailSender.SendAsync(relationsEmail, mailSubject, mailBody, ct);

        await _notificationService.CreateAsync(
            department: NotificationDepartments.Sales,
            title: "Musteri iliskileri talebi",
            message: $"{customerEmail} musterisi iletisim talebi gonderdi: {subject}",
            relatedEntityType: "Customer",
            relatedEntityId: userId,
            ct: ct);

        TempData["AccountSuccess"] = "Mesajiniz musteri iliskilerine iletildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePayment(PaymentEditVm model, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!Enum.IsDefined(typeof(PaymentMethodType), model.Type))
        {
            TempData["AccountError"] = "Gecersiz odeme tipi.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            TempData["AccountError"] = "Odeme formu gecersiz.";
            return RedirectToAction(nameof(Index));
        }

        UserPaymentMethod entity;
        if (model.Id.HasValue && model.Id.Value > 0)
        {
            entity = await _db.UserPaymentMethods.FirstOrDefaultAsync(x => x.Id == model.Id.Value && x.UserId == userId, ct) ?? new UserPaymentMethod { UserId = userId };
            if (entity.Id == 0)
            {
                TempData["AccountError"] = "Odeme kaydi bulunamadi.";
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            entity = new UserPaymentMethod { UserId = userId, CreatedAtUtc = DateTime.UtcNow };
            _db.UserPaymentMethods.Add(entity);
        }

        entity.Type = (PaymentMethodType)model.Type;
        entity.DisplayName = model.DisplayName.Trim();
        entity.ProviderReference = string.IsNullOrWhiteSpace(model.ProviderReference) ? null : model.ProviderReference.Trim();
        entity.CardLast4 = string.IsNullOrWhiteSpace(model.CardLast4) ? null : model.CardLast4.Trim();
        entity.ExpMonth = model.ExpMonth;
        entity.ExpYear = model.ExpYear;
        entity.IsDefault = model.IsDefault;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (model.IsDefault)
        {
            await _db.UserPaymentMethods
                .Where(x => x.UserId == userId && x.Id != entity.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
        }

        await _db.SaveChangesAsync(ct);
        TempData["AccountSuccess"] = "Odeme/banka bilgisi kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePayment(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var entity = await _db.UserPaymentMethods.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (entity is null)
        {
            TempData["AccountError"] = "Odeme kaydi bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        _db.UserPaymentMethods.Remove(entity);
        await _db.SaveChangesAsync(ct);

        TempData["AccountSuccess"] = "Odeme kaydi silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AccountDashboardVm> BuildVmAsync(int? editAddressId, int? editPaymentId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return new AccountDashboardVm();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return new AccountDashboardVm();

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new CustomerOrderHistoryItemVm
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                Status = OrderStatusWorkflow.Normalize(x.Status),
                CanCancel = OrderStatusWorkflow.CanTransition(x.Status, OrderStatusWorkflow.Cancelled),
                ShippingCarrier = x.ShippingCarrier,
                TrackingCode = x.TrackingCode,
                TotalAmount = x.TotalAmount,
                Currency = x.Currency,
                ItemCount = x.Items.Count
            })
            .ToListAsync(ct);

        var processNotifications = orders
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CustomerProcessNotificationVm
            {
                OrderId = x.Id,
                Status = x.Status,
                StatusMessage = BuildCustomerStatusMessage(x.Status),
                EventAt = x.CreatedAt,
                ShippingCarrier = x.ShippingCarrier,
                TrackingCode = x.TrackingCode
            })
            .ToList();

        var relationHistory = await _db.CustomerRelationsMessages
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new CustomerRelationsHistoryItemVm
            {
                Id = x.Id,
                Subject = x.Subject,
                Message = x.Message,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        var addressesRaw = await _db.UserAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        var paymentsRaw = await _db.UserPaymentMethods
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        var addressForm = new AddressEditVm();
        if (editAddressId.HasValue && editAddressId.Value > 0)
        {
            var e = addressesRaw.FirstOrDefault(x => x.Id == editAddressId.Value);
            if (e is not null)
            {
                addressForm = new AddressEditVm
                {
                    Id = e.Id,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Company = e.Company,
                    Street = e.Street,
                    HouseNumber = e.HouseNumber,
                    AddressLine2 = e.AddressLine2,
                    PostalCode = e.PostalCode,
                    City = e.City,
                    State = e.State,
                    CountryCode = e.CountryCode,
                    PhoneNumber = e.PhoneNumber,
                    IsDefault = e.IsDefault
                };
            }
        }

        var paymentForm = new PaymentEditVm();
        if (editPaymentId.HasValue && editPaymentId.Value > 0)
        {
            var p = paymentsRaw.FirstOrDefault(x => x.Id == editPaymentId.Value);
            if (p is not null)
            {
                paymentForm = new PaymentEditVm
                {
                    Id = p.Id,
                    Type = (int)p.Type,
                    DisplayName = p.DisplayName,
                    ProviderReference = p.ProviderReference,
                    CardLast4 = p.CardLast4,
                    ExpMonth = p.ExpMonth,
                    ExpYear = p.ExpYear,
                    IsDefault = p.IsDefault
                };
            }
        }

        return new AccountDashboardVm
        {
            Profile = new ProfileUpdateVm
            {
                Email = user.Email ?? "",
                UserName = user.UserName ?? "",
                PhoneNumber = user.PhoneNumber ?? ""
            },
            Password = new PasswordChangeVm(),
            AddressForm = addressForm,
            PaymentForm = paymentForm,
            CustomerRelationsForm = new CustomerRelationsMessageVm(),
            Orders = orders,
            ProcessNotifications = processNotifications,
            CustomerRelationsHistory = relationHistory,
            Addresses = addressesRaw.Select(x => new CustomerAddressListItemVm
            {
                Id = x.Id,
                FullName = x.FirstName + " " + x.LastName,
                AddressLine = x.Street + " " + x.HouseNumber,
                CityLine = x.PostalCode + " " + x.City + " / " + x.CountryCode,
                PhoneNumber = x.PhoneNumber,
                IsDefault = x.IsDefault
            }).ToList(),
            Payments = paymentsRaw.Select(x => new CustomerPaymentListItemVm
            {
                Id = x.Id,
                Type = x.Type.ToString(),
                DisplayName = x.DisplayName,
                ProviderReference = x.ProviderReference,
                CardLast4 = x.CardLast4,
                ExpMonth = x.ExpMonth,
                ExpYear = x.ExpYear,
                IsDefault = x.IsDefault
            }).ToList()
        };
    }

    private static string BuildCustomerStatusMessage(string status)
    {
        var normalized = OrderStatusWorkflow.Normalize(status);
        return normalized switch
        {
            OrderStatusWorkflow.Pending => "Siparisiniz alindi.",
            OrderStatusWorkflow.Paid => "Odemeniz onaylandi.",
            OrderStatusWorkflow.Processing => "Siparisiniz hazirlaniyor.",
            OrderStatusWorkflow.Packed => "Siparisiniz paketlendi.",
            OrderStatusWorkflow.Shipped => "Siparisiniz kargoya verildi.",
            OrderStatusWorkflow.CourierPickedUp => "Kurye siparisinizi teslim aldi.",
            OrderStatusWorkflow.OutForDelivery => "Siparisiniz dagitimda.",
            OrderStatusWorkflow.Delivered => "Siparisiniz teslim edildi.",
            OrderStatusWorkflow.Completed => "Siparis sureci tamamlandi.",
            OrderStatusWorkflow.Cancelled => "Siparisiniz iptal edildi.",
            OrderStatusWorkflow.ReturnPickedUp => "Iade urunu kurye tarafindan teslim alindi.",
            OrderStatusWorkflow.ReturnDeliveredToSeller => "Iade urunu saticiya teslim edildi.",
            OrderStatusWorkflow.Refunded => "Geri odeme islemi tamamlandi.",
            _ => normalized
        };
    }
}
