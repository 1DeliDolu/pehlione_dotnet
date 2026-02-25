namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductDeleteVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string CategoryName { get; set; } = "";
}
