namespace Pehlione.Services;

public interface IInventoryService
{
    Task<ReceiveStockResult> ReceiveStockAsync(int productId, int qty, string? note, string? userId, CancellationToken ct = default);
    Task<ReceiveStockResult> ReduceStockAsync(int productId, int qty, string? note, string? userId, CancellationToken ct = default);
}

public sealed record ReceiveStockResult(bool Success, string? Error, int CurrentQuantity)
{
    public static ReceiveStockResult Ok(int currentQuantity) => new(true, null, currentQuantity);
    public static ReceiveStockResult Fail(string error) => new(false, error, 0);
}
