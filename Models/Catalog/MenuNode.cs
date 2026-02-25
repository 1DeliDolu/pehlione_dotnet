namespace Pehlione.Models.Catalog;

public enum MenuNodeKind
{
    Top = 1,
    Section = 2,
    Link = 3,
    Separator = 4
}

public enum MenuLinkType
{
    None = 1,
    Category = 2,
    Collection = 3,
    Page = 4,
    Activity = 5,
    Url = 6
}

public enum MenuNodeStyle
{
    Normal = 1,
    Highlight = 2,
    Muted = 3
}

public sealed class MenuNode
{
    public int Id { get; set; }

    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    public int? ParentId { get; set; }
    public MenuNode? Parent { get; set; }

    public MenuNodeKind NodeKind { get; set; }
    public string? Label { get; set; }

    public MenuLinkType LinkType { get; set; } = MenuLinkType.None;
    public int? RefId { get; set; }
    public string? Url { get; set; }

    public byte? MegaColumn { get; set; }
    public int SortOrder { get; set; }

    public string? IconUrl { get; set; }
    public string? Badge { get; set; }

    public MenuNodeStyle Style { get; set; } = MenuNodeStyle.Normal;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MenuNode> Children { get; set; } = new List<MenuNode>();
    public ICollection<MenuNodeTranslation> Translations { get; set; } = new List<MenuNodeTranslation>();
}
