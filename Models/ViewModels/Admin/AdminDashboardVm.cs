namespace Pehlione.Models.ViewModels.Admin;

public sealed class AdminDashboardVm
{
    public CreateUserVm QuickUser { get; set; } = new();
    public ProductCreateVm QuickProduct { get; set; } = new();
    public QuickStockOperationVm QuickStockOperation { get; set; } = new();
    public QuickPersonnelUpdateVm QuickPersonnelUpdate { get; set; } = new();
    public IReadOnlyList<AdminSelectOptionVm> ProductOptions { get; set; } = Array.Empty<AdminSelectOptionVm>();
    public IReadOnlyList<AdminSelectOptionVm> PersonnelOptions { get; set; } = Array.Empty<AdminSelectOptionVm>();
    public int TotalUsers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCategories { get; set; }
    public int TotalOrders { get; set; }
    public int TotalStockQuantity { get; set; }
    public int ActiveProducts { get; set; }
    public int InactiveProducts { get; set; }
    public int LowStockProducts { get; set; }
    public decimal OrdersRevenue { get; set; }
    public IReadOnlyList<AdminCategoryStockVm> CategoryStock { get; set; } = Array.Empty<AdminCategoryStockVm>();
    public IReadOnlyList<AdminMonthlyOrderVm> MonthlyOrders { get; set; } = Array.Empty<AdminMonthlyOrderVm>();
}

public sealed class AdminCategoryStockVm
{
    public string CategoryName { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class AdminMonthlyOrderVm
{
    public string MonthLabel { get; set; } = "";
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class AdminSelectOptionVm
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class QuickStockOperationVm
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public string OperationType { get; set; } = "Increase";
}

public sealed class QuickPersonnelUpdateVm
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "Staff";
    public string? Department { get; set; }
    public string? Position { get; set; }
}
