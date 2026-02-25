namespace Pehlione.Models.Catalog;

public sealed class MenuNodeTranslation
{
    public int NodeId { get; set; }
    public MenuNode? Node { get; set; }

    public string Locale { get; set; } = "";
    public string Label { get; set; } = "";
}
