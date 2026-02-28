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
    public AdminOrderTimingSummaryVm OrderTimings { get; set; } = new();
    public IReadOnlyList<AdminOrderTransitionTimingVm> TransitionTimings { get; set; } = Array.Empty<AdminOrderTransitionTimingVm>();
    public int SelectedRangeDays { get; set; } = 30;
    public DateTime? CustomStartDate { get; set; }
    public DateTime? CustomEndDate { get; set; }
    public string RangeLabel { get; set; } = "";
    public IReadOnlyList<int> RangeOptions { get; set; } = Array.Empty<int>();
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

public sealed class AdminOrderTimingSummaryVm
{
    public double AvgApprovalHours { get; set; }
    public int ApprovalSampleCount { get; set; }
    public double AvgDispatchHours { get; set; }
    public int DispatchSampleCount { get; set; }
    public double AvgShippingHours { get; set; }
    public int ShippingSampleCount { get; set; }
    public double AvgEndToEndHours { get; set; }
    public int EndToEndSampleCount { get; set; }
}

public sealed class AdminOrderTransitionTimingVm
{
    public string Label { get; set; } = "";
    public double AvgHours { get; set; }
    public int SampleCount { get; set; }
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
