namespace Pehlione.Models.ViewModels.Admin;

public sealed class AdminStockFormVm
{
    public QuickStockOperationVm Form { get; set; } = new();
    public IReadOnlyList<AdminSelectOptionVm> ProductOptions { get; set; } = Array.Empty<AdminSelectOptionVm>();
}
