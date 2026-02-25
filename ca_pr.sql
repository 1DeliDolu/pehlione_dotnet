-- ===========================
-- DDL: taxonomy + optional closure
-- ===========================

use pehlione_dotnet;

-- Idempotent temiz başlangıç: seed tablolarını dependency sırasıyla kaldır.
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS menu_node_translations;
DROP TABLE IF EXISTS menu_nodes;
DROP TABLE IF EXISTS menus;
DROP TABLE IF EXISTS collection_products;
DROP TABLE IF EXISTS collections;
DROP TABLE IF EXISTS activities;
DROP TABLE IF EXISTS cms_pages;
DROP TABLE IF EXISTS category_closure;
DROP TABLE IF EXISTS categories;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE IF NOT EXISTS categories (
  id         BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  parent_id  BIGINT UNSIGNED NULL,
  code       VARCHAR(60) NULL,            -- opsiyonel: MEN, WOMEN, KIDS
  name       VARCHAR(150) NOT NULL,
  slug       VARCHAR(180) NOT NULL,
  sort_order INT NOT NULL DEFAULT 0,
  is_active  TINYINT(1) NOT NULL DEFAULT 1,

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  CONSTRAINT fk_cat_parent
    FOREIGN KEY (parent_id) REFERENCES categories(id)
    ON DELETE SET NULL,

  UNIQUE KEY uq_cat_slug (slug),
  KEY idx_cat_parent (parent_id),
  KEY idx_cat_active_sort (is_active, sort_order)
) ENGINE=InnoDB;

-- Opsiyonel ama önerilir: tüm alt kategori sorguları için closure table
CREATE TABLE IF NOT EXISTS category_closure (
  ancestor_id   BIGINT UNSIGNED NOT NULL,
  descendant_id BIGINT UNSIGNED NOT NULL,
  depth         INT NOT NULL,
  PRIMARY KEY (ancestor_id, descendant_id),
  KEY idx_cc_desc (descendant_id),
  CONSTRAINT fk_cc_a FOREIGN KEY (ancestor_id) REFERENCES categories(id) ON DELETE CASCADE,
  CONSTRAINT fk_cc_d FOREIGN KEY (descendant_id) REFERENCES categories(id) ON DELETE CASCADE
) ENGINE=InnoDB;


-- ===========================
-- DDL: collections
-- ===========================
CREATE TABLE IF NOT EXISTS collections (
  id         BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  name       VARCHAR(160) NOT NULL,
  slug       VARCHAR(180) NOT NULL,
  kind       ENUM('manual','rule') NOT NULL DEFAULT 'rule',
  rule_json  JSON NULL,                  -- ör: {"gender":"men","tag":"new"} gibi
  is_active  TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_col_slug (slug)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS collection_products (
  collection_id BIGINT UNSIGNED NOT NULL,
  product_id    BIGINT UNSIGNED NOT NULL,
  sort_order    INT NOT NULL DEFAULT 0,
  PRIMARY KEY (collection_id, product_id),
  KEY idx_cp_sort (collection_id, sort_order)
) ENGINE=InnoDB;


-- ===========================
-- DDL: cms + activities
-- ===========================
CREATE TABLE IF NOT EXISTS cms_pages (
  id         BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  title      VARCHAR(200) NOT NULL,
  slug       VARCHAR(220) NOT NULL,
  content    LONGTEXT NULL,
  is_active  TINYINT(1) NOT NULL DEFAULT 1,
  UNIQUE KEY uq_page_slug (slug)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS activities (
  id        BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  name      VARCHAR(120) NOT NULL,      -- Ski & Snowboard, Wandern...
  slug      VARCHAR(160) NOT NULL,
  icon_url  VARCHAR(500) NULL,
  UNIQUE KEY uq_act_slug (slug)
) ENGINE=InnoDB;


-- ===========================
-- DDL: menus
-- ===========================
CREATE TABLE IF NOT EXISTS menus (
  id         BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  code       VARCHAR(60) NOT NULL,      -- main_header, footer vb.
  name       VARCHAR(120) NOT NULL,
  locale     VARCHAR(10) NOT NULL DEFAULT 'tr-TR',  -- de-DE gibi
  is_active  TINYINT(1) NOT NULL DEFAULT 1,
  UNIQUE KEY uq_menu_code_locale (code, locale)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS menu_nodes (
  id          BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  menu_id     BIGINT UNSIGNED NOT NULL,
  parent_id   BIGINT UNSIGNED NULL,

  node_kind   ENUM('top','section','link','separator') NOT NULL,

  label       VARCHAR(200) NULL,

  -- link hedefi (tek tabloda polimorfik)
  link_type   ENUM('none','category','collection','page','activity','url') NOT NULL DEFAULT 'none',
  ref_id      BIGINT UNSIGNED NULL,        -- link_type’a göre ilgili tablonun id’si
  url         VARCHAR(500) NULL,           -- link_type='url' ise

  -- mega menü layout
  mega_column TINYINT UNSIGNED NULL,       -- 1..N (Columbia’da genelde 5)
  sort_order  INT NOT NULL DEFAULT 0,

  -- görsel/etiket
  icon_url    VARCHAR(500) NULL,
  badge       VARCHAR(40) NULL,            -- NEW, SALE vb.
  style       ENUM('normal','highlight','muted') NOT NULL DEFAULT 'normal',

  is_active   TINYINT(1) NOT NULL DEFAULT 1,

  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  CONSTRAINT fk_mn_menu FOREIGN KEY (menu_id) REFERENCES menus(id) ON DELETE CASCADE,
  CONSTRAINT fk_mn_parent FOREIGN KEY (parent_id) REFERENCES menu_nodes(id) ON DELETE CASCADE,

  KEY idx_mn_parent_sort (parent_id, sort_order),
  KEY idx_mn_menu (menu_id),
  KEY idx_mn_megacol (menu_id, mega_column)
) ENGINE=InnoDB;

-- Çok dil istersen (Columbia gibi de-DE / en-US / tr-TR)
CREATE TABLE IF NOT EXISTS menu_node_translations (
  node_id BIGINT UNSIGNED NOT NULL,
  locale  VARCHAR(10) NOT NULL,
  label   VARCHAR(200) NOT NULL,
  PRIMARY KEY (node_id, locale),
  CONSTRAINT fk_mnt_node FOREIGN KEY (node_id) REFERENCES menu_nodes(id) ON DELETE CASCADE
) ENGINE=InnoDB;


-- ===========================
-- SEED: Columbia-style mega menu (de-DE)
-- ===========================
/* =========================================================
   SEED: Columbia-style Mega Menu (de-DE)
   ========================================================= */

/* ---------------------------
   1) CATEGORIES (Katalog ağacı)
   --------------------------- */

INSERT INTO categories (id, parent_id, code, name, slug, sort_order, is_active) VALUES
-- Root / Header entry points
(1000, NULL, 'MEN',    'Männer',      'maenner',     10, 1),
(2000, NULL, 'WOMEN',  'Frauen',      'frauen',      20, 1),
(3000, NULL, 'KIDS',   'Kinder',      'kinder',      30, 1),
(4000, NULL, 'SHOES',  'Schuhe',      'schuhe',      40, 1),
(5000, NULL, 'ACC',    'Accessoires', 'accessoires', 50, 1),

/* Männer (giyim odaklı) */
(1100, 1000, NULL, 'Jacken & Westen',        'maenner-jacken-westen', 10, 1),
(1110, 1100, NULL, 'Skijacken',              'maenner-skijacken', 10, 1),
(1120, 1100, NULL, 'Stepp- und Daunenjacken','maenner-stepp-daunenjacken', 20, 1),
(1130, 1100, NULL, '3-in-1 Jacken',          'maenner-3in1-jacken', 30, 1),
(1140, 1100, NULL, 'Regenjacken',            'maenner-regenjacken', 40, 1),
(1150, 1100, NULL, 'Wanderjacken',           'maenner-wanderjacken', 50, 1),
(1160, 1100, NULL, 'Softshelljacken',        'maenner-softshelljacken', 60, 1),
(1170, 1100, NULL, 'Windjacken',             'maenner-windjacken', 70, 1),
(1180, 1100, NULL, 'Westen',                 'maenner-westen', 80, 1),
(1190, 1100, NULL, 'Mäntel und Parkas',      'maenner-maentel-parkas', 90, 1),

(1200, 1000, NULL, 'Fleecejacken',           'maenner-fleecejacken', 20, 1),
(1210, 1200, NULL, 'Sherpa fleece',          'maenner-sherpa-fleece', 10, 1),
(1220, 1200, NULL, 'Alltags-Fleece',         'maenner-alltags-fleece', 20, 1),
(1230, 1200, NULL, 'Technische Fleece',      'maenner-technische-fleece', 30, 1),
(1240, 1200, NULL, 'Fleecewesten',           'maenner-fleecewesten', 40, 1),

(1300, 1000, NULL, 'Oberteile',              'maenner-oberteile', 30, 1),
(1310, 1300, NULL, 'Hemden',                 'maenner-hemden', 10, 1),
(1320, 1300, NULL, 'T-Shirts',               'maenner-tshirts', 20, 1),
(1330, 1300, NULL, 'Poloshirts',             'maenner-poloshirts', 30, 1),
(1340, 1300, NULL, 'Sweatshirts',            'maenner-sweatshirts', 40, 1),

(1400, 1000, NULL, 'Hosen',                  'maenner-hosen', 40, 1),
(1410, 1400, NULL, 'Skihosen',               'maenner-skihosen', 10, 1),
(1420, 1400, NULL, 'Wanderhosen',            'maenner-wanderhosen', 20, 1),
(1430, 1400, NULL, 'Wandelbare Hosen',       'maenner-wandelbare-hosen', 30, 1),
(1440, 1400, NULL, 'Freizeithosen',          'maenner-freizeithosen', 40, 1),
(1450, 1400, NULL, 'Kurze Wanderhosen',      'maenner-kurze-wanderhosen', 50, 1),
(1460, 1400, NULL, 'Shorts',                 'maenner-shorts', 60, 1),
(1470, 1400, NULL, 'Regenhosen',             'maenner-regenhosen', 70, 1),

(1500, 1000, NULL, 'Unterwäsche & Socken',   'maenner-unterwaesche-socken', 50, 1),
(1510, 1500, NULL, 'Funktionsshirts',        'maenner-funktionsshirts', 10, 1),
(1520, 1500, NULL, 'Socken',                 'maenner-socken', 20, 1),
(1530, 1500, NULL, 'Unterwäschelinie',       'maenner-unterwaeschelinie', 30, 1),

(1800, 1000, NULL, 'Bekleidung in Übergrößen','maenner-uebergroessen', 60, 1),

/* Frauen (minimum set, Columbia benzeri genişletilebilir) */
(2100, 2000, NULL, 'Jacken & Westen',        'frauen-jacken-westen', 10, 1),
(2200, 2000, NULL, 'Oberteile',              'frauen-oberteile', 20, 1),
(2300, 2000, NULL, 'Hosen',                  'frauen-hosen', 30, 1),

/* Kinder */
(3100, 3000, NULL, 'Jungen (4-18 jahre)',     'kinder-jungen-4-18', 10, 1),
(3110, 3100, NULL, 'Jacken & Westen',         'kinder-jungen-jacken-westen', 10, 1),
(3120, 3100, NULL, 'Fleecejacken & Sweatshirts','kinder-jungen-fleece-sweatshirts', 20, 1),
(3130, 3100, NULL, 'T-Shirts',                'kinder-jungen-tshirts', 30, 1),
(3140, 3100, NULL, 'Hosen',                   'kinder-jungen-hosen', 40, 1),
(3150, 3100, NULL, 'Shorts',                  'kinder-jungen-shorts', 50, 1),
(3160, 3100, NULL, 'Accessoires',             'kinder-jungen-accessoires', 60, 1),

(3200, 3000, NULL, 'Mädchen (4-18 jahre)',     'kinder-maedchen-4-18', 20, 1),
(3210, 3200, NULL, 'Jacken & Westen',          'kinder-maedchen-jacken-westen', 10, 1),
(3220, 3200, NULL, 'Fleecejacken & Sweatshirts','kinder-maedchen-fleece-sweatshirts', 20, 1),
(3230, 3200, NULL, 'T-Shirts',                 'kinder-maedchen-tshirts', 30, 1),
(3240, 3200, NULL, 'Hosen',                    'kinder-maedchen-hosen', 40, 1),
(3250, 3200, NULL, 'Accessoires',              'kinder-maedchen-accessoires', 50, 1),

(3300, 3000, NULL, 'Kleinkinder & Babys (0-4 jahre)','kinder-babys-0-4', 30, 1),
(3310, 3300, NULL, 'Anzüge',                   'kinder-babys-anzuege', 10, 1),
(3320, 3300, NULL, 'Jacken',                   'kinder-babys-jacken', 20, 1),
(3330, 3300, NULL, 'Fleecejacken',             'kinder-babys-fleecejacken', 30, 1),

/* Schuhe (global) */
(4100, 4000, NULL, 'Herrenschuhe',             'herrenschuhe', 10, 1),
(4110, 4100, NULL, 'Wanderschuhe',             'herrenschuhe-wanderschuhe', 10, 1),
(4120, 4100, NULL, 'Wasserdichte Schuhe',      'herrenschuhe-wasserdicht', 20, 1),
(4130, 4100, NULL, 'Winterstiefel',            'herrenschuhe-winterstiefel', 30, 1),
(4140, 4100, NULL, 'Freizeitschuhe',           'herrenschuhe-freizeit', 40, 1),
(4150, 4100, NULL, 'Trail Running Schuhe',     'herrenschuhe-trail-running', 50, 1),
(4160, 4100, NULL, 'Sandalen & Sommerschuhe',  'herrenschuhe-sandalen-sommer', 60, 1),

(4200, 4000, NULL, 'Damenschuhe',              'damenschuhe', 20, 1),
(4210, 4200, NULL, 'Wanderschuhe',             'damenschuhe-wanderschuhe', 10, 1),
(4220, 4200, NULL, 'Wasserdichte Schuhe',      'damenschuhe-wasserdicht', 20, 1),
(4230, 4200, NULL, 'Winterstiefel',            'damenschuhe-winterstiefel', 30, 1),
(4240, 4200, NULL, 'Freizeitschuhe',           'damenschuhe-freizeit', 40, 1),
(4250, 4200, NULL, 'Trail Running Schuhe',     'damenschuhe-trail-running', 50, 1),
(4260, 4200, NULL, 'Sandalen & Sommerschuhe',  'damenschuhe-sandalen-sommer', 60, 1),

(4300, 4000, NULL, 'Kinder',                   'kinderschuhe', 30, 1),
(4310, 4300, NULL, 'Schuhe für Jugendliche (größen 32-39EU)','kinderschuhe-jugendliche-32-39', 10, 1),
(4320, 4300, NULL, 'Schuhe für Kinder (größen 25-31EU)',     'kinderschuhe-kinder-25-31', 20, 1),
(4330, 4300, NULL, 'Jungenschuhe (größen 25-39EU)',          'kinderschuhe-jungen-25-39', 30, 1),
(4340, 4300, NULL, 'Mädchenschuhe (größen 25-39EU)',         'kinderschuhe-maedchen-25-39', 40, 1),

/* Accessoires (global) */
(5100, 5000, NULL, 'Mützen & Schals',          'accessoires-muetzen-schals', 10, 1),
(5200, 5000, NULL, 'Ski- & Winterhandschuhe', 'accessoires-winterhandschuhe', 20, 1),
(5300, 5000, NULL, 'Caps & Hats',              'accessoires-caps-hats', 30, 1);




/* --------------------------------
   2) COLLECTIONS (Kürasyon listeleri)
   -------------------------------- */

INSERT INTO collections (id, name, slug, kind, rule_json, is_active) VALUES
(8001, 'Neu & angesagt',     'neu-angesagt',     'rule',  JSON_OBJECT('tag','featured'), 1),
(8002, 'Neuheiten',          'neuheiten',        'rule',  JSON_OBJECT('tag','new'), 1),
(8003, 'Best Sellers',       'best-sellers',     'rule',  JSON_OBJECT('sort','bestseller'), 1),
(8004, 'Outlet',             'outlet',           'rule',  JSON_OBJECT('tag','sale'), 1),

-- Männer: Ausgewählte Artikel (Columbia ekranındaki örnekler)
(8101, 'Titanium Ski',       'titanium-ski',     'manual', NULL, 1),
(8102, 'Titanium Wandern',   'titanium-wandern', 'manual', NULL, 1),
(8103, 'Outdry Extreme',     'outdry-extreme',   'manual', NULL, 1),
(8104, 'Ready to Roam',      'ready-to-roam',    'manual', NULL, 1),
(8105, 'Omni-MAX™',          'omni-max',         'manual', NULL, 1),

-- Kinder: Mickey’s Outdoor Club
(8301, 'Mickey’s Outdoor Club','mickeys-outdoor-club','manual', NULL, 1),

-- Schuhe: Ausgewählte Artikel (Columbia ekranındaki örnekler)
(8201, 'Konos™',             'konos',            'manual', NULL, 1),
(8202, 'Omni-MAX™',          'omni-max-shoes',   'manual', NULL, 1),
(8203, 'Peakfreak™',         'peakfreak',        'manual', NULL, 1);



/* ---------------------------
   3) ACTIVITIES (Nach Aktivität shoppen)
   --------------------------- */

INSERT INTO activities (id, name, slug, icon_url) VALUES
(9001, 'Ski & Snowboard',     'ski-snowboard',     NULL),
(9002, 'Wandern',             'wandern',           NULL),
(9003, 'Urbane Abenteuer',    'urbane-abenteuer',  NULL),
(9004, 'Fishing',             'fishing',           NULL),
(9005, 'Sommer Aktivitäten',  'sommer-aktivitaeten',NULL),
(9006, 'Trail Running',       'trail-running',     NULL),
(9007, 'Winter und Schnee',   'winter-und-schnee', NULL),
(9008, 'Gehen',               'gehen',             NULL),
(9009, 'Schnelle Wanderungen','schnelle-wanderungen',NULL);



/* ---------------------------
   4) CMS PAGES (Produkthilfe, Über uns)
   --------------------------- */

INSERT INTO cms_pages (id, title, slug, content, is_active) VALUES
(7001, 'Über uns',                         'ueber-uns',                         NULL, 1),
(7101, 'Schuhberater',                     'produkthilfe-schuhberater',          NULL, 1),
(7102, 'Finde die perfekte jacke',         'produkthilfe-perfekte-jacke',        NULL, 1),
(7103, 'Guide Für Wasserdichte Artikel',   'guide-wasserdichte-artikel',         NULL, 1),
(7104, 'Produktberater für Kinder-Jacken – Jungen','produktberater-kinder-jacken-jungen', NULL, 1);



/* ---------------------------
   5) MENUS + MENU_NODES (Mega Menü)
   --------------------------- */

INSERT INTO menus (id, code, name, locale, is_active) VALUES
(1, 'main_header', 'Main Header', 'de-DE', 1);

/* TOP NAV */
INSERT INTO menu_nodes
(id, menu_id, parent_id, node_kind, label, link_type, ref_id, url, mega_column, sort_order, style, is_active)
VALUES
(10000, 1, NULL, 'top', 'Neu & angesagt', 'collection', 8001, NULL, NULL, 10, 'highlight', 1),
(10010, 1, NULL, 'top', 'Männer',         'category',   1000, NULL, NULL, 20, 'normal', 1),
(10020, 1, NULL, 'top', 'Frauen',         'category',   2000, NULL, NULL, 30, 'normal', 1),
(10030, 1, NULL, 'top', 'Kinder',         'category',   3000, NULL, NULL, 40, 'normal', 1),
(10040, 1, NULL, 'top', 'Schuhe',         'category',   4000, NULL, NULL, 50, 'normal', 1),
(10050, 1, NULL, 'top', 'Accessoires',    'category',   5000, NULL, NULL, 60, 'normal', 1),
(10060, 1, NULL, 'top', 'Outlet',         'collection', 8004, NULL, NULL, 70, 'highlight', 1),
(10070, 1, NULL, 'top', 'Über uns',       'page',       7001, NULL, NULL, 80, 'normal', 1);


/* ===== Männer Mega Menü (Columbia benzeri) ===== */

-- Column 1: Neue kollektion / Best Sellers / Ausgewählte Artikel
INSERT INTO menu_nodes VALUES
(10100,1,10010,'section','Neue kollektion','collection',8002,NULL,1,10,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10110,1,10010,'section','Best Sellers','collection',8003,NULL,1,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10120,1,10010,'section','Ausgewählte Artikel','none',NULL,NULL,1,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(10121,1,10120,'link','Titanium Ski','collection',8101,NULL,1,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10122,1,10120,'link','Titanium Wandern','collection',8102,NULL,1,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10123,1,10120,'link','Outdry Extreme','collection',8103,NULL,1,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10124,1,10120,'link','Ready to Roam','collection',8104,NULL,1,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10125,1,10120,'link','Omni-MAX™','collection',8105,NULL,1,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 2: Jacken & Westen + Fleecejacken
INSERT INTO menu_nodes VALUES
(10200,1,10010,'section','Jacken & Westen','category',1100,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10210,1,10010,'section','Fleecejacken','category',1200,NULL,2,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(10201,1,10200,'link','Skijacken','category',1110,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10202,1,10200,'link','Stepp- und Daunenjacken','category',1120,NULL,2,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10203,1,10200,'link','3-in-1 Jacken','category',1130,NULL,2,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10204,1,10200,'link','Regenjacken','category',1140,NULL,2,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10205,1,10200,'link','Wanderjacken','category',1150,NULL,2,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10206,1,10200,'link','Softshelljacken','category',1160,NULL,2,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10207,1,10200,'link','Windjacken','category',1170,NULL,2,70,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10208,1,10200,'link','Westen','category',1180,NULL,2,80,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10209,1,10200,'link','Mäntel und Parkas','category',1190,NULL,2,90,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(10211,1,10210,'link','Sherpa fleece','category',1210,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10212,1,10210,'link','Alltags-Fleece','category',1220,NULL,2,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10213,1,10210,'link','Technische Fleece','category',1230,NULL,2,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10214,1,10210,'link','Fleecewesten','category',1240,NULL,2,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 3: Oberteile + Hosen + Unterwäsche & Socken
INSERT INTO menu_nodes VALUES
(10300,1,10010,'section','Oberteile','category',1300,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10310,1,10010,'section','Hosen','category',1400,NULL,3,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10320,1,10010,'section','Unterwäsche & Socken','category',1500,NULL,3,80,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(10301,1,10300,'link','Hemden','category',1310,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10302,1,10300,'link','T-Shirts','category',1320,NULL,3,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10303,1,10300,'link','Poloshirts','category',1330,NULL,3,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10304,1,10300,'link','Sweatshirts','category',1340,NULL,3,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(10311,1,10310,'link','Skihosen','category',1410,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10312,1,10310,'link','Wanderhosen','category',1420,NULL,3,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10313,1,10310,'link','Wandelbare Hosen','category',1430,NULL,3,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10314,1,10310,'link','Freizeithosen','category',1440,NULL,3,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10315,1,10310,'link','Kurze Wanderhosen','category',1450,NULL,3,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10316,1,10310,'link','Shorts','category',1460,NULL,3,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10317,1,10310,'link','Regenhosen','category',1470,NULL,3,70,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(10321,1,10320,'link','Funktionsshirts','category',1510,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10322,1,10320,'link','Socken','category',1520,NULL,3,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10323,1,10320,'link','Unterwäschelinie','category',1530,NULL,3,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 4: Schuhe + Accessoires + Übergrößen + Alle Männer-Produkte
INSERT INTO menu_nodes VALUES
(10400,1,10010,'section','Schuhe','category',4000,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10410,1,10010,'section','Accessoires','category',5000,NULL,4,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10420,1,10010,'section','Bekleidung in Übergrößen','category',1800,NULL,4,80,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10430,1,10010,'link','Alle Männer-Produkte','category',1000,NULL,4,110,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(10401,1,10400,'link','Wanderschuhe','category',4110,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10402,1,10400,'link','Wasserdichte Schuhe','category',4120,NULL,4,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10403,1,10400,'link','Winterstiefel','category',4130,NULL,4,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10404,1,10400,'link','Freizeitschuhe','category',4140,NULL,4,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10405,1,10400,'link','Trail Running Schuhe','category',4150,NULL,4,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10406,1,10400,'link','Sandalen & Sommerschuhe','category',4160,NULL,4,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(10411,1,10410,'link','Mützen & Schals','category',5100,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10412,1,10410,'link','Ski- & Winterhandschuhe','category',5200,NULL,4,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10413,1,10410,'link','Caps & Hats','category',5300,NULL,4,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 5: Outlet + Nach Aktivität shoppen + Produkthilfe
INSERT INTO menu_nodes VALUES
(10500,1,10010,'section','Outlet','collection',8004,NULL,5,10,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10510,1,10010,'section','Nach Aktivität shoppen','none',NULL,NULL,5,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10520,1,10010,'section','Produkthilfe','none',NULL,NULL,5,80,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(10511,1,10510,'link','Ski & Snowboard','activity',9001,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10512,1,10510,'link','Wandern','activity',9002,NULL,5,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10513,1,10510,'link','Urbane Abenteuer','activity',9003,NULL,5,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10514,1,10510,'link','Fishing','activity',9004,NULL,5,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10515,1,10510,'link','Sommer Aktivitäten','activity',9005,NULL,5,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10516,1,10510,'link','Trail Running','activity',9006,NULL,5,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(10521,1,10520,'link','Finde die richtigen Schuhe','page',7101,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10522,1,10520,'link','Finde die perfekte jacke','page',7102,NULL,5,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(10523,1,10520,'link','Guide Für Wasserdichte Artikel','page',7103,NULL,5,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);



/* ===== Kinder Mega Menü (Columbia benzeri) ===== */

-- Column 1: Neue kollektion / Best Sellers / Mickey’s Outdoor Club
INSERT INTO menu_nodes VALUES
(20100,1,10030,'section','Neue kollektion','collection',8002,NULL,1,10,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20110,1,10030,'section','Best Sellers','collection',8003,NULL,1,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20120,1,10030,'section','Mickey’s Outdoor Club','collection',8301,NULL,1,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 2: Jungen
INSERT INTO menu_nodes VALUES
(20200,1,10030,'section','Jungen (4-18 jahre)','category',3100,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(20201,1,20200,'link','Jacken & Westen','category',3110,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20202,1,20200,'link','Fleecejacken & Sweatshirts','category',3120,NULL,2,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20203,1,20200,'link','T-Shirts','category',3130,NULL,2,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20204,1,20200,'link','Hosen','category',3140,NULL,2,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20205,1,20200,'link','Shorts','category',3150,NULL,2,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20206,1,20200,'link','Accessoires','category',3160,NULL,2,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 3: Mädchen + Kleinkinder & Babys
INSERT INTO menu_nodes VALUES
(20300,1,10030,'section','Mädchen (4-18 jahre)','category',3200,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20310,1,10030,'section','Kleinkinder & Babys (0-4 jahre)','category',3300,NULL,3,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(20301,1,20300,'link','Jacken & Westen','category',3210,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20302,1,20300,'link','Fleecejacken & Sweatshirts','category',3220,NULL,3,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20303,1,20300,'link','T-Shirts','category',3230,NULL,3,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20304,1,20300,'link','Hosen','category',3240,NULL,3,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20305,1,20300,'link','Accessoires','category',3250,NULL,3,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(20311,1,20310,'link','Anzüge','category',3310,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20312,1,20310,'link','Jacken','category',3320,NULL,3,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20313,1,20310,'link','Fleecejacken','category',3330,NULL,3,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 4: Schuhe + Alle Kinder-Produkte
INSERT INTO menu_nodes VALUES
(20400,1,10030,'section','Schuhe','category',4300,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20410,1,10030,'link','Alle Kinder-Produkte','category',3000,NULL,4,80,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(20401,1,20400,'link','Schuhe für Jugendliche (größen 32-39EU)','category',4310,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20402,1,20400,'link','Schuhe für Kinder (größen 25-31EU)','category',4320,NULL,4,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20403,1,20400,'link','Jungenschuhe (größen 25-39EU)','category',4330,NULL,4,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20404,1,20400,'link','Mädchenschuhe (größen 25-39EU)','category',4340,NULL,4,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 5: Outlet + Nach Aktivität shoppen + Produkthilfe
INSERT INTO menu_nodes VALUES
(20500,1,10030,'section','Outlet','collection',8004,NULL,5,10,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20510,1,10030,'section','Nach Aktivität shoppen','none',NULL,NULL,5,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20520,1,10030,'section','Produkthilfe','none',NULL,NULL,5,80,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(20511,1,20510,'link','Ski & Snowboard','activity',9001,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20512,1,20510,'link','Wandern','activity',9002,NULL,5,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20513,1,20510,'link','Urbane Abenteuer','activity',9003,NULL,5,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20514,1,20510,'link','Sommer Aktivitäten','activity',9005,NULL,5,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(20521,1,20520,'link','Finde die richtigen Schuhe','page',7101,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(20522,1,20520,'link','Produktberater für Kinder-Jacken – Jungen','page',7104,NULL,5,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);



/* ===== Schuhe Mega Menü (Columbia benzeri) ===== */

-- Column 1: Neuheiten / Best Sellers / Ausgewählte Artikel
INSERT INTO menu_nodes VALUES
(30100,1,10040,'section','Neuheiten','collection',8002,NULL,1,10,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30110,1,10040,'section','Best Sellers','collection',8003,NULL,1,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30120,1,10040,'section','Ausgewählte Artikel','none',NULL,NULL,1,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(30121,1,30120,'link','Konos™','collection',8201,NULL,1,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30122,1,30120,'link','Omni-MAX™','collection',8202,NULL,1,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30123,1,30120,'link','Peakfreak™','collection',8203,NULL,1,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 2: Herrenschuhe
INSERT INTO menu_nodes VALUES
(30200,1,10040,'section','Herrenschuhe','category',4100,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);
INSERT INTO menu_nodes VALUES
(30201,1,30200,'link','Wanderschuhe','category',4110,NULL,2,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30202,1,30200,'link','Wasserdichte Schuhe','category',4120,NULL,2,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30203,1,30200,'link','Winterstiefel','category',4130,NULL,2,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30204,1,30200,'link','Freizeitschuhe','category',4140,NULL,2,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30205,1,30200,'link','Trail Running Schuhe','category',4150,NULL,2,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30206,1,30200,'link','Sandalen & Sommerschuhe','category',4160,NULL,2,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 3: Damenschuhe
INSERT INTO menu_nodes VALUES
(30300,1,10040,'section','Damenschuhe','category',4200,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);
INSERT INTO menu_nodes VALUES
(30301,1,30300,'link','Wanderschuhe','category',4210,NULL,3,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30302,1,30300,'link','Wasserdichte Schuhe','category',4220,NULL,3,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30303,1,30300,'link','Winterstiefel','category',4230,NULL,3,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30304,1,30300,'link','Freizeitschuhe','category',4240,NULL,3,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30305,1,30300,'link','Trail Running Schuhe','category',4250,NULL,3,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30306,1,30300,'link','Sandalen & Sommerschuhe','category',4260,NULL,3,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 4: Kinder + Alle Schuhe
INSERT INTO menu_nodes VALUES
(30400,1,10040,'section','Kinder','category',4300,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30410,1,10040,'link','Alle Schuhe','category',4000,NULL,4,80,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(30401,1,30400,'link','Schuhe für Jugendliche (größen 32-39EU)','category',4310,NULL,4,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30402,1,30400,'link','Schuhe für Kinder (größen 25-31EU)','category',4320,NULL,4,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30403,1,30400,'link','Jungenschuhe (größen 25-39EU)','category',4330,NULL,4,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30404,1,30400,'link','Mädchenschuhe (größen 25-39EU)','category',4340,NULL,4,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

-- Column 5: Nach Aktivität shoppen + Produkthilfe + Outlet
INSERT INTO menu_nodes VALUES
(30500,1,10040,'section','Nach Aktivität shoppen','none',NULL,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30510,1,10040,'section','Produkthilfe','none',NULL,NULL,5,60,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30520,1,10040,'section','Outlet','collection',8004,NULL,5,90,NULL,NULL,'highlight',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);

INSERT INTO menu_nodes VALUES
(30501,1,30500,'link','Wandern','activity',9002,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30502,1,30500,'link','Winter und Schnee','activity',9007,NULL,5,20,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30503,1,30500,'link','Gehen','activity',9008,NULL,5,30,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30504,1,30500,'link','Schnelle Wanderungen','activity',9009,NULL,5,40,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
(30505,1,30500,'link','Trail-Running','activity',9006,NULL,5,50,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),

(30511,1,30510,'link','Schuhberater','page',7101,NULL,5,10,NULL,NULL,'normal',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);



/* --------------------------------
   6) OPTIONAL: rebuild category_closure (MySQL 8+)
   -------------------------------- */
TRUNCATE TABLE category_closure;

INSERT INTO category_closure (ancestor_id, descendant_id, depth)
WITH RECURSIVE cte AS (
  SELECT id AS ancestor_id, id AS descendant_id, 0 AS depth
  FROM categories
  UNION ALL
  SELECT cte.ancestor_id, c.id, cte.depth + 1
  FROM cte
  JOIN categories c ON c.parent_id = cte.descendant_id
)
SELECT ancestor_id, descendant_id, depth
FROM cte;



-- ===========================
-- CHECK QUERY
-- ===========================
SELECT *
FROM menu_nodes
WHERE menu_id = 1 AND is_active=1
ORDER BY
  CASE WHEN parent_id IS NULL THEN 0 ELSE 1 END,
  parent_id,
  mega_column,
  sort_order;
