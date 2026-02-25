using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pehlione.Migrations
{
    /// <inheritdoc />
    public partial class AddMegaMenuDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `categories` (
                  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `parent_id` BIGINT UNSIGNED NULL,
                  `code` varchar(60) NULL,
                  `name` varchar(120) NOT NULL,
                  `slug` varchar(160) NOT NULL,
                  `sort_order` int NOT NULL DEFAULT 0,
                  `is_active` tinyint(1) NOT NULL DEFAULT 1,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'categories'
                        AND COLUMN_NAME = 'code'
                    ),
                    'SELECT 1',
                    'ALTER TABLE `categories` ADD COLUMN `code` varchar(60) NULL'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'categories'
                        AND COLUMN_NAME = 'parent_id'
                    ),
                    'SELECT 1',
                    'ALTER TABLE `categories` ADD COLUMN `parent_id` BIGINT UNSIGNED NULL'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'categories'
                        AND COLUMN_NAME = 'sort_order'
                    ),
                    'SELECT 1',
                    'ALTER TABLE `categories` ADD COLUMN `sort_order` int NOT NULL DEFAULT 0'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.STATISTICS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'categories'
                        AND INDEX_NAME = 'IX_categories_slug'
                    ),
                    'SELECT 1',
                    'CREATE UNIQUE INDEX `IX_categories_slug` ON `categories` (`slug`)'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.STATISTICS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'categories'
                        AND INDEX_NAME = 'IX_categories_is_active_sort_order'
                    ),
                    'SELECT 1',
                    'CREATE INDEX `IX_categories_is_active_sort_order` ON `categories` (`is_active`, `sort_order`)'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.STATISTICS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'categories'
                        AND INDEX_NAME = 'IX_categories_parent_id'
                    ),
                    'SELECT 1',
                    'CREATE INDEX `IX_categories_parent_id` ON `categories` (`parent_id`)'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `products` (
                  `id` int NOT NULL AUTO_INCREMENT,
                  `category_id` BIGINT UNSIGNED NOT NULL,
                  `name` varchar(160) NOT NULL,
                  `sku` varchar(64) NOT NULL,
                  `price` decimal(18,2) NOT NULL,
                  `is_active` tinyint(1) NOT NULL DEFAULT 1,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.STATISTICS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'products'
                        AND INDEX_NAME = 'IX_products_sku'
                    ),
                    'SELECT 1',
                    'CREATE UNIQUE INDEX `IX_products_sku` ON `products` (`sku`)'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @sql = (
                  SELECT IF(
                    EXISTS (
                      SELECT 1
                      FROM INFORMATION_SCHEMA.STATISTICS
                      WHERE TABLE_SCHEMA = DATABASE()
                        AND TABLE_NAME = 'products'
                        AND INDEX_NAME = 'IX_products_category_id'
                    ),
                    'SELECT 1',
                    'CREATE INDEX `IX_products_category_id` ON `products` (`category_id`)'
                  )
                );
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `activities` (
                  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `name` varchar(120) NOT NULL,
                  `slug` varchar(160) NOT NULL,
                  `icon_url` varchar(500) NULL,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `cms_pages` (
                  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `title` varchar(200) NOT NULL,
                  `slug` varchar(220) NOT NULL,
                  `content` LONGTEXT NULL,
                  `is_active` tinyint(1) NOT NULL DEFAULT 1,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `collections` (
                  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `name` varchar(160) NOT NULL,
                  `slug` varchar(180) NOT NULL,
                  `kind` varchar(16) NOT NULL,
                  `rule_json` JSON NULL,
                  `is_active` tinyint(1) NOT NULL DEFAULT 1,
                  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `collection_products` (
                  `collection_id` BIGINT UNSIGNED NOT NULL,
                  `product_id` int NOT NULL,
                  `sort_order` int NOT NULL DEFAULT 0,
                  PRIMARY KEY (`collection_id`, `product_id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `menus` (
                  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `code` varchar(60) NOT NULL,
                  `name` varchar(120) NOT NULL,
                  `locale` varchar(10) NOT NULL DEFAULT 'tr-TR',
                  `is_active` tinyint(1) NOT NULL DEFAULT 1,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `menu_nodes` (
                  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `menu_id` BIGINT UNSIGNED NOT NULL,
                  `parent_id` BIGINT UNSIGNED NULL,
                  `node_kind` varchar(16) NOT NULL,
                  `label` varchar(200) NULL,
                  `link_type` varchar(16) NOT NULL,
                  `ref_id` BIGINT UNSIGNED NULL,
                  `url` varchar(500) NULL,
                  `mega_column` tinyint unsigned NULL,
                  `sort_order` int NOT NULL DEFAULT 0,
                  `icon_url` varchar(500) NULL,
                  `badge` varchar(40) NULL,
                  `style` varchar(16) NOT NULL,
                  `is_active` tinyint(1) NOT NULL DEFAULT 1,
                  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                  PRIMARY KEY (`id`)
                ) ENGINE=InnoDB;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `menu_node_translations` (
                  `node_id` BIGINT UNSIGNED NOT NULL,
                  `locale` varchar(10) NOT NULL,
                  `label` varchar(200) NOT NULL,
                  PRIMARY KEY (`node_id`, `locale`)
                ) ENGINE=InnoDB;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty to avoid destructive behavior on existing data.
        }
    }
}
