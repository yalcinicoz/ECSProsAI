import { createBrowserRouter } from 'react-router-dom'
import { MainLayout } from '@/components/layout/MainLayout'
import { AuthGuard } from '@/components/layout/AuthGuard'
import { LoginPage } from '@/pages/auth/LoginPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { PlaceholderPage } from '@/pages/PlaceholderPage'
import { AttributeTypesPage } from '@/pages/catalog/AttributeTypesPage'
import { AttributeTypeDetailPage } from '@/pages/catalog/AttributeTypeDetailPage'
import { ProductGroupsPage } from '@/pages/catalog/ProductGroupsPage'
import { ProductGroupDetailPage } from '@/pages/catalog/ProductGroupDetailPage'
import { ProductsPage } from '@/pages/catalog/ProductsPage'
import { ProductDetailPage } from '@/pages/catalog/ProductDetailPage'
import { ProductCreatePage } from '@/pages/catalog/ProductCreatePage'
import { TranslationsPage } from '@/pages/settings/TranslationsPage'
import { PlatformTypesPage } from '@/pages/settings/PlatformTypesPage'
import { ChannelsPage } from '@/pages/settings/ChannelsPage'
import { FirmsPage } from '@/pages/settings/FirmsPage'
import { FirmDetailPage } from '@/pages/settings/FirmDetailPage'
import { CatalogSettingsPage } from '@/pages/catalog/CatalogSettingsPage'
import { BulkImageUploadPage } from '@/pages/catalog/BulkImageUploadPage'
import { FilterColorsPage } from '@/pages/catalog/FilterColorsPage'
import { MenusPage } from '@/pages/cms/MenusPage'
import { ChannelCategoriesPage } from '@/pages/storefront/ChannelCategoriesPage'
import { ChannelCategoryDetailPage } from '@/pages/storefront/ChannelCategoryDetailPage'
import { MenuDetailPage } from '@/pages/cms/MenuDetailPage'
import { WarehousesPage } from '@/pages/inventory/WarehousesPage'
import { WarehouseDetailPage } from '@/pages/inventory/WarehouseDetailPage'
import { StocksPage } from '@/pages/inventory/StocksPage'
import { TransfersPage } from '@/pages/inventory/TransfersPage'
import { TransferDetailPage } from '@/pages/inventory/TransferDetailPage'
import { AccountGroupsPage } from '@/pages/accounts/AccountGroupsPage'
import { AccountsPage } from '@/pages/accounts/AccountsPage'
import { AccountDetailPage, AccountCreatePage } from '@/pages/accounts/AccountDetailPage'

export const router = createBrowserRouter(
  [
    { path: '/login', element: <LoginPage /> },
    {
      element: <AuthGuard />,
      children: [
        {
          element: <MainLayout />,
          children: [
            { index: true, element: <DashboardPage /> },

            // Katalog
            { path: 'catalog/product-groups',     element: <ProductGroupsPage /> },
            { path: 'catalog/product-groups/:id', element: <ProductGroupDetailPage /> },
            { path: 'catalog/attribute-types',    element: <AttributeTypesPage /> },
            { path: 'catalog/attribute-types/:id', element: <AttributeTypeDetailPage /> },
            { path: 'catalog/products',          element: <ProductsPage /> },
            { path: 'catalog/products/new',    element: <ProductCreatePage /> },
            { path: 'catalog/products/:code',  element: <ProductDetailPage /> },
            { path: 'catalog/settings',          element: <CatalogSettingsPage /> },
            { path: 'catalog/bulk-images',       element: <BulkImageUploadPage /> },
            { path: 'catalog/filter-colors',     element: <FilterColorsPage /> },
            { path: 'storefront/channel-categories',      element: <ChannelCategoriesPage /> },
            { path: 'storefront/channel-categories/:id',  element: <ChannelCategoryDetailPage /> },

            // Envanter
            { path: 'inventory/warehouses',     element: <WarehousesPage /> },
            { path: 'inventory/warehouses/:id', element: <WarehouseDetailPage /> },
            { path: 'inventory/stocks',         element: <StocksPage /> },
            { path: 'inventory/transfers',      element: <TransfersPage /> },
            { path: 'inventory/transfers/:id',  element: <TransferDetailPage /> },

            // Siparişler
            { path: 'orders',              element: <PlaceholderPage title="Siparişler" /> },
            { path: 'orders/:id',          element: <PlaceholderPage title="Sipariş Detayı" /> },
            { path: 'orders/returns',      element: <PlaceholderPage title="İadeler" /> },
            { path: 'orders/quotes',       element: <PlaceholderPage title="Teklifler" /> },
            { path: 'orders/invoices',     element: <PlaceholderPage title="Faturalar" /> },
            { path: 'orders/gift-cards',   element: <PlaceholderPage title="Hediye Kartları" /> },

            // Cari Kartlar
            { path: 'accounts/groups', element: <AccountGroupsPage /> },
            { path: 'accounts',        element: <AccountsPage /> },
            { path: 'accounts/new',    element: <AccountCreatePage /> },
            { path: 'accounts/:id',    element: <AccountDetailPage /> },

            // CRM
            { path: 'crm/members',       element: <PlaceholderPage title="Üyeler" /> },
            { path: 'crm/members/:id',   element: <PlaceholderPage title="Üye Detayı" /> },
            { path: 'crm/member-groups', element: <PlaceholderPage title="Üye Grupları" /> },

            // POS
            { path: 'pos/sales',     element: <PlaceholderPage title="POS Satışları" /> },
            { path: 'pos/registers', element: <PlaceholderPage title="Kasalar" /> },

            // Finans
            { path: 'finance/suppliers',         element: <PlaceholderPage title="Tedarikçiler" /> },
            { path: 'finance/supplier-invoices', element: <PlaceholderPage title="Tedarikçi Faturaları" /> },

            // Promosyon
            { path: 'promotion/campaigns', element: <PlaceholderPage title="Kampanyalar" /> },

            // Fulfillment
            { path: 'fulfillment/picking-plans',    element: <PlaceholderPage title="Picking Planları" /> },
            { path: 'fulfillment/packing-stations', element: <PlaceholderPage title="Paketleme İstasyonları" /> },

            // CMS
            { path: 'cms/pages',                element: <PlaceholderPage title="Sayfalar" /> },
            { path: 'navigation/menus',         element: <MenusPage /> },
            { path: 'navigation/menus/:id',     element: <MenuDetailPage /> },

            // Entegrasyon
            { path: 'integrations/logs', element: <PlaceholderPage title="Entegrasyon Logları" /> },

            // Ayarlar
            { path: 'settings/translations',    element: <TranslationsPage /> },
            { path: 'settings/users',           element: <PlaceholderPage title="Kullanıcılar" /> },
            { path: 'settings/roles',           element: <PlaceholderPage title="Roller" /> },
            { path: 'settings/audit-logs',      element: <PlaceholderPage title="Denetim Logları" /> },
            { path: 'settings/firms',           element: <FirmsPage /> },
            { path: 'settings/firms/:id',       element: <FirmDetailPage /> },
            { path: 'settings/platform-types',  element: <PlatformTypesPage /> },
            { path: 'settings/channels',         element: <ChannelsPage /> },
            { path: 'settings/languages',       element: <PlaceholderPage title="Diller" /> },
            { path: 'settings/lookup-types',    element: <PlaceholderPage title="Lookup Tipleri" /> },
          ],
        },
      ],
    },
  ],
  { basename: '/admin' },
)
