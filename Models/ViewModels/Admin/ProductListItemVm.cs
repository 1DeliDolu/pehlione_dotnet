namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}
