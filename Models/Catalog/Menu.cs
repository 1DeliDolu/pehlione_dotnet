namespace Pehlione.Models.Catalog;

public sealed class Menu
{
    public int Id { get; set; }

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Locale { get; set; } = "tr-TR";

    public bool IsActive { get; set; } = true;

    public ICollection<MenuNode> Nodes { get; set; } = new List<MenuNode>();
}
