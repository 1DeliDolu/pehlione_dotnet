namespace Pehlione.Models.ViewModels.Staff;

public sealed class HrDashboardVm
{
    public IReadOnlyList<HrPersonRowVm> People { get; set; } = Array.Empty<HrPersonRowVm>();
}

public sealed class HrPersonRowVm
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string[] Departments { get; set; } = Array.Empty<string>();
    public string? Position { get; set; }
}
