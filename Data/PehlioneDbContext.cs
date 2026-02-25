using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models.Catalog;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public sealed class PehlioneDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionProduct> CollectionProducts => Set<CollectionProduct>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuNode> MenuNodes => Set<MenuNode>();
    public DbSet<MenuNodeTranslation> MenuNodeTranslations => Set<MenuNodeTranslation>();
    public DbSet<Pehlione.Models.TodoItem> TodoItems => Set<Pehlione.Models.TodoItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(b =>
        {
            b.ToTable("categories");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ParentId).HasColumnName("parent_id");
            b.Property(x => x.Code).HasColumnName("code");
            b.Property(x => x.Name).HasColumnName("name");
            b.Property(x => x.Slug).HasColumnName("slug");
            b.Property(x => x.SortOrder).HasColumnName("sort_order");
            b.Property(x => x.IsActive).HasColumnName("is_active");

            b.Property(x => x.Code).HasMaxLength(60);
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            b.Property(x => x.SortOrder).HasDefaultValue(0);
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => new { x.IsActive, x.SortOrder });

            b.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.CategoryId).HasColumnName("category_id");
            b.Property(x => x.Name).HasColumnName("name");
            b.Property(x => x.Sku).HasColumnName("sku");
            b.Property(x => x.Price).HasColumnName("price");
            b.Property(x => x.IsActive).HasColumnName("is_active");

            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.Sku).IsUnique();

            b.Property(x => x.Price).HasPrecision(18, 2);

            b.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Collection>(b =>
        {
            b.ToTable("collections");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name");
            b.Property(x => x.Slug).HasColumnName("slug");
            b.Property(x => x.Kind).HasColumnName("kind");
            b.Property(x => x.RuleJson).HasColumnName("rule_json");
            b.Property(x => x.IsActive).HasColumnName("is_active");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            b.Property(x => x.Kind)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            b.Property(x => x.RuleJson).HasColumnType("json");
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<CollectionProduct>(b =>
        {
            b.ToTable("collection_products");
            b.Property(x => x.CollectionId).HasColumnName("collection_id");
            b.Property(x => x.ProductId).HasColumnName("product_id");
            b.Property(x => x.SortOrder).HasColumnName("sort_order");

            b.HasKey(x => new { x.CollectionId, x.ProductId });
            b.Property(x => x.SortOrder).HasDefaultValue(0);
            b.HasIndex(x => new { x.CollectionId, x.SortOrder });

            b.HasOne(x => x.Collection)
                .WithMany(x => x.CollectionProducts)
                .HasForeignKey(x => x.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CmsPage>(b =>
        {
            b.ToTable("cms_pages");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Title).HasColumnName("title");
            b.Property(x => x.Slug).HasColumnName("slug");
            b.Property(x => x.Content).HasColumnName("content");
            b.Property(x => x.IsActive).HasColumnName("is_active");

            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            b.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Activity>(b =>
        {
            b.ToTable("activities");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name");
            b.Property(x => x.Slug).HasColumnName("slug");
            b.Property(x => x.IconUrl).HasColumnName("icon_url");

            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            b.Property(x => x.IconUrl).HasMaxLength(500);
            b.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Menu>(b =>
        {
            b.ToTable("menus");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Code).HasColumnName("code");
            b.Property(x => x.Name).HasColumnName("name");
            b.Property(x => x.Locale).HasColumnName("locale");
            b.Property(x => x.IsActive).HasColumnName("is_active");

            b.Property(x => x.Code).HasMaxLength(60).IsRequired();
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Locale).HasMaxLength(10).IsRequired();
            b.HasIndex(x => new { x.Code, x.Locale }).IsUnique();
        });

        builder.Entity<MenuNode>(b =>
        {
            b.ToTable("menu_nodes");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.MenuId).HasColumnName("menu_id");
            b.Property(x => x.ParentId).HasColumnName("parent_id");
            b.Property(x => x.NodeKind).HasColumnName("node_kind");
            b.Property(x => x.Label).HasColumnName("label");
            b.Property(x => x.LinkType).HasColumnName("link_type");
            b.Property(x => x.RefId).HasColumnName("ref_id");
            b.Property(x => x.Url).HasColumnName("url");
            b.Property(x => x.MegaColumn).HasColumnName("mega_column");
            b.Property(x => x.SortOrder).HasColumnName("sort_order");
            b.Property(x => x.IconUrl).HasColumnName("icon_url");
            b.Property(x => x.Badge).HasColumnName("badge");
            b.Property(x => x.Style).HasColumnName("style");
            b.Property(x => x.IsActive).HasColumnName("is_active");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            b.Property(x => x.NodeKind)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            b.Property(x => x.Label).HasMaxLength(200);
            b.Property(x => x.LinkType)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            b.Property(x => x.Url).HasMaxLength(500);
            b.Property(x => x.IconUrl).HasMaxLength(500);
            b.Property(x => x.Badge).HasMaxLength(40);
            b.Property(x => x.Style)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            b.Property(x => x.SortOrder).HasDefaultValue(0);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            b.HasIndex(x => new { x.ParentId, x.SortOrder });
            b.HasIndex(x => x.MenuId);
            b.HasIndex(x => new { x.MenuId, x.MegaColumn });

            b.HasOne(x => x.Menu)
                .WithMany(x => x.Nodes)
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MenuNodeTranslation>(b =>
        {
            b.ToTable("menu_node_translations");
            b.Property(x => x.NodeId).HasColumnName("node_id");
            b.Property(x => x.Locale).HasColumnName("locale");
            b.Property(x => x.Label).HasColumnName("label");

            b.HasKey(x => new { x.NodeId, x.Locale });
            b.Property(x => x.Locale).HasMaxLength(10).IsRequired();
            b.Property(x => x.Label).HasMaxLength(200).IsRequired();

            b.HasOne(x => x.Node)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
