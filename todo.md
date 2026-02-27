### A) Hedef

Veritabanını **ilk kez oluşturacak EF Core migration** dosyalarını projeye ekleyip, **tek komutla MySQL’e şemayı basarak bağlantıyı doğrulayacağız**.

### B) CLI Komutu (maks. 1 komut)

```bash
dotnet ef database update
```

### C) Dosya Değişiklikleri (tam içerik)

#### ./Migrations/20260227090000_InitialCreate.cs

```csharp
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pehlione.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sku = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_ProductId",
                table: "Stocks",
                column: "ProductId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
```

#### ./Migrations/ApplicationDbContextModelSnapshot.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pehlione.Data;

#nullable disable

namespace Pehlione.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder
                .HasAnnotation("MySql:CharSet", "utf8mb4");

            modelBuilder.Entity("Pehlione.Models.Product", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("varchar(200)")
                        .HasAnnotation("MySql:CharSet", "utf8mb4");

                    b.Property<string>("Sku")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)")
                        .HasAnnotation("MySql:CharSet", "utf8mb4");

                    b.HasKey("Id");

                    b.HasIndex("Sku")
                        .IsUnique();

                    b.ToTable("Products");
                });

            modelBuilder.Entity("Pehlione.Models.Stock", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("ProductId")
                        .HasColumnType("int");

                    b.Property<int>("Quantity")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("ProductId")
                        .IsUnique();

                    b.ToTable("Stocks");
                });

            modelBuilder.Entity("Pehlione.Models.Stock", b =>
                {
                    b.HasOne("Pehlione.Models.Product", "Product")
                        .WithOne("Stock")
                        .HasForeignKey("Pehlione.Models.Stock", "ProductId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Product");
                });

            modelBuilder.Entity("Pehlione.Models.Product", b =>
                {
                    b.Navigation("Stock");
                });
#pragma warning restore 612, 618
        }
    }
}
```

### D) Kısa Açıklama (en fazla 5 madde)

* Bu migration, **Products** ve **Stocks** tablolarını ve index/foreign key’leri oluşturur.
* `dotnet ef database update` komutu, migration’ı **MySQL’e uygular**; burada bağlantı da fiilen test edilmiş olur (hata yoksa bağlandı).
* `Sku` unique; `Stocks.ProductId` unique: “1 ürün = 1 stok satırı” kuralı garanti altına alınır.
* Bir sonraki adımda RBAC için altyapıyı kurup **Purchasing sadece artırma**, **IT silme** yetkisini net policy/role ile ayıracağız.
* Stok artırma işlemini “silme yok” şeklinde **endpoint + UI** tarafında role/policy ile kilitleyeceğiz.

### E) Git Commit

**Commit mesajı:** `Add initial EF Core migration for Products and Stocks`

```bash
git add -A && git commit -m "Add initial EF Core migration for Products and Stocks"
```

“**bitti**” yazınca bir sonraki adımda: **RBAC temelini atıp** (roller/policy), **Purchasing’e sadece stok artırma**, **IT’ye stok silme** yetkisini verecek şekilde ilk controller iskeletini çıkaracağız.
