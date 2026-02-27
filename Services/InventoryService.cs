using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Inventory;

namespace Pehlione.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly PehlioneDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        PehlioneDbContext db,
        INotificationService notificationService,
        ILogger<InventoryService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ReceiveStockResult> ReceiveStockAsync(int productId, int qty, string? note, string? userId, CancellationToken ct = default)
    {
        if (productId <= 0)
            return ReceiveStockResult.Fail("Gecersiz urun.");

        if (qty <= 0)
            return ReceiveStockResult.Fail("Adet pozitif olmalidir.");

        var productExists = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId, ct);

        if (!productExists)
            return ReceiveStockResult.Fail("Urun bulunamadi.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE stocks SET quantity = quantity + {qty} WHERE product_id = {productId}",
            ct);

        if (rows == 0)
        {
            _db.Stocks.Add(new Stock
            {
                ProductId = productId,
                Quantity = qty
            });

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE stocks SET quantity = quantity + {qty} WHERE product_id = {productId}",
                    ct);
            }
        }

        _db.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            Type = StockMovementType.In,
            Quantity = qty,
            Reason = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedByUserId = string.IsNullOrWhiteSpace(userId) ? null : userId
        });

        await _db.SaveChangesAsync(ct);

        var currentQty = await _db.Stocks
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .Select(x => x.Quantity)
            .FirstAsync(ct);

        await tx.CommitAsync(ct);

        await _notificationService.CreateAsync(
            department: "Sales",
            title: "Stok girisi tamamlandi",
            message: $"Urun #{productId} icin {qty} adet stok girisi yapildi. Guncel stok: {currentQty}",
            relatedEntityType: "Product",
            relatedEntityId: productId.ToString(),
            ct: ct);

        _logger.LogInformation("Stock received. ProductId={ProductId}, Qty={Qty}, CurrentQty={CurrentQty}", productId, qty, currentQty);

        return ReceiveStockResult.Ok(currentQty);
    }
}
