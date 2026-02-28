namespace Pehlione.Models.ViewModels.Admin;

public sealed class UserListItemVm
{
    public string Email { get; set; } = "";
    public string UserName { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Departments { get; set; } = Array.Empty<string>();
}
