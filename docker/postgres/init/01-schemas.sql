-- ECSPros Veritabanı Schema Yapısı
-- Modüler Monolith: Her modülün kendi schema'sı var

CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS crm;
CREATE SCHEMA IF NOT EXISTS "order";
CREATE SCHEMA IF NOT EXISTS fulfillment;
CREATE SCHEMA IF NOT EXISTS finance;
CREATE SCHEMA IF NOT EXISTS promotion;
CREATE SCHEMA IF NOT EXISTS iam;
CREATE SCHEMA IF NOT EXISTS cms;
CREATE SCHEMA IF NOT EXISTS integration;
CREATE SCHEMA IF NOT EXISTS pos;

-- search_path: varsayılan olarak tüm schema'lar aranabilir
ALTER DATABASE ecommerce_db SET search_path TO public, core, catalog, inventory, crm, "order", fulfillment, finance, promotion, iam, cms, integration, pos;
