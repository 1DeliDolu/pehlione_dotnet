using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models;
using Pehlione.Models.Catalog;
using Pehlione.Models.Communication;
using Pehlione.Models.Commerce;
using Pehlione.Models.Identity;
using Pehlione.Models.Inventory;
using Pehlione.Models.Security;

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
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DepartmentConstraint> DepartmentConstraints => Set<DepartmentConstraint>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<UserPaymentMethod> UserPaymentMethods => Set<UserPaymentMethod>();
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

        builder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            b.Property(x => x.ShippingCarrier).HasColumnName("shipping_carrier").HasMaxLength(120);
            b.Property(x => x.TrackingCode).HasColumnName("tracking_code").HasMaxLength(120);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.CreatedAt);

            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderItem>(b =>
        {
            b.ToTable("order_items");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.OrderId).HasColumnName("order_id");
            b.Property(x => x.ProductId).HasColumnName("product_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            b.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64).IsRequired();
            b.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            b.Property(x => x.Quantity).HasColumnName("quantity");
            b.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);

            b.HasIndex(x => x.OrderId);
            b.HasIndex(x => x.ProductId);

            b.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Stock>(b =>
        {
            b.ToTable("stocks");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ProductId).HasColumnName("product_id");
            b.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(0);

            b.HasIndex(x => x.ProductId).IsUnique();

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockMovement>(b =>
        {
            b.ToTable("stock_movements");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ProductId).HasColumnName("product_id");
            b.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(8).IsRequired();
            b.Property(x => x.Quantity).HasColumnName("quantity");
            b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(255);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            b.HasIndex(x => x.ProductId);
            b.HasIndex(x => x.CreatedAt);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(b =>
        {
            b.ToTable("notifications");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Department).HasColumnName("department").HasMaxLength(64).IsRequired();
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(180).IsRequired();
            b.Property(x => x.Message).HasColumnName("message").HasMaxLength(1000).IsRequired();
            b.Property(x => x.RelatedEntityType).HasColumnName("related_entity_type").HasMaxLength(64);
            b.Property(x => x.RelatedEntityId).HasColumnName("related_entity_id").HasMaxLength(64);
            b.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            b.HasIndex(x => new { x.Department, x.IsRead, x.CreatedAt });
        });

        builder.Entity<DepartmentConstraint>(b =>
        {
            b.ToTable("department_constraints");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Department).HasColumnName("department").HasMaxLength(64).IsRequired();
            b.Property(x => x.CanIncreaseStock).HasColumnName("can_increase_stock").HasDefaultValue(false);
            b.Property(x => x.CanDeleteStock).HasColumnName("can_delete_stock").HasDefaultValue(false);
            b.Property(x => x.MaxReceiveQuantity).HasColumnName("max_receive_quantity");
            b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").HasMaxLength(255);
            b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            b.HasIndex(x => x.Department).IsUnique();
        });

        builder.Entity<UserAddress>(b =>
        {
            b.ToTable("user_addresses");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(255).IsRequired();
            b.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(60).IsRequired();
            b.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(60).IsRequired();
            b.Property(x => x.Company).HasColumnName("company").HasMaxLength(120);
            b.Property(x => x.Street).HasColumnName("street").HasMaxLength(120).IsRequired();
            b.Property(x => x.HouseNumber).HasColumnName("house_number").HasMaxLength(15).IsRequired();
            b.Property(x => x.AddressLine2).HasColumnName("address_line2").HasMaxLength(120);
            b.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(10).IsRequired();
            b.Property(x => x.City).HasColumnName("city").HasMaxLength(80).IsRequired();
            b.Property(x => x.State).HasColumnName("state").HasMaxLength(80);
            b.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
            b.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
            b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
            b.Property(x => x.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
            b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            b.HasIndex(x => x.UserId);
            b.HasIndex(x => new { x.UserId, x.Type, x.IsDefault });

            b.HasOne(x => x.User)
                .WithMany(x => x.Addresses)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserPaymentMethod>(b =>
        {
            b.ToTable("user_payment_methods");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(255).IsRequired();
            b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
            b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(80).IsRequired();
            b.Property(x => x.ProviderReference).HasColumnName("provider_reference").HasMaxLength(120);
            b.Property(x => x.CardLast4).HasColumnName("card_last4").HasMaxLength(4);
            b.Property(x => x.ExpMonth).HasColumnName("exp_month");
            b.Property(x => x.ExpYear).HasColumnName("exp_year");
            b.Property(x => x.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
            b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            b.HasIndex(x => x.UserId);
            b.HasIndex(x => new { x.UserId, x.Type, x.IsDefault });

            b.HasOne(x => x.User)
                .WithMany(x => x.PaymentMethods)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
