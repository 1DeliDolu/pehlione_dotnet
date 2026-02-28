namespace Pehlione.Models.ViewModels.Admin;

public sealed class AdminPersonnelFormVm
{
    public QuickPersonnelUpdateVm Form { get; set; } = new();
    public IReadOnlyList<AdminSelectOptionVm> PersonnelOptions { get; set; } = Array.Empty<AdminSelectOptionVm>();
}
