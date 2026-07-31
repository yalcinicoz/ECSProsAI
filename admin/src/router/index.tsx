import { createBrowserRouter, Navigate } from 'react-router-dom'
import { MainLayout } from '@/components/layout/MainLayout'
import { AuthGuard } from '@/components/layout/AuthGuard'
import { LoginPage } from '@/pages/auth/LoginPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { MarketplacesPage } from '@/pages/marketplaces/MarketplacesPage'
import { MarketplaceStoreDetailPage } from '@/pages/marketplaces/MarketplaceStoreDetailPage'
import { MappingPage } from '@/pages/marketplaces/MappingPage'
import { AttributeTypesPage } from '@/pages/catalog/AttributeTypesPage'
import { AttributeTypeDetailPage } from '@/pages/catalog/AttributeTypeDetailPage'
import { ProductGroupsPage } from '@/pages/catalog/ProductGroupsPage'
import { ProductGroupDetailPage } from '@/pages/catalog/ProductGroupDetailPage'
import { ProductsPage } from '@/pages/catalog/ProductsPage'
import { ProductDetailPage } from '@/pages/catalog/ProductDetailPage'
import { ProductSubmissionsPage } from '@/pages/catalog/ProductSubmissionsPage'
import { ProductSubmissionDetailPage } from '@/pages/catalog/ProductSubmissionDetailPage'
import { ProductCreatePage } from '@/pages/catalog/ProductCreatePage'
import { TranslationsPage } from '@/pages/settings/TranslationsPage'
import { PlatformTypesPage } from '@/pages/settings/PlatformTypesPage'
import { IntegrationServicesPage } from '@/pages/settings/IntegrationServicesPage'
import { ChannelsPage } from '@/pages/settings/ChannelsPage'
import { FirmsPage } from '@/pages/settings/FirmsPage'
import { FirmDetailPage } from '@/pages/settings/FirmDetailPage'
import { CatalogSettingsPage } from '@/pages/catalog/CatalogSettingsPage'
import { BulkImageUploadPage } from '@/pages/catalog/BulkImageUploadPage'
import { ChannelCategoriesPage } from '@/pages/storefront/ChannelCategoriesPage'
import { MenuPlacementPage } from '@/pages/storefront/MenuPlacementPage'
import { ChannelProductsPage } from '@/pages/storefront/ChannelProductsPage'
import { ChannelCategoryDetailPage } from '@/pages/storefront/ChannelCategoryDetailPage'
import { CollectionsModerationPage } from '@/pages/storefront/CollectionsModerationPage'
import { ReviewsModerationPage } from '@/pages/storefront/ReviewsModerationPage'
import { PagesManagementPage } from '@/pages/storefront/PagesManagementPage'
import { PagesHistoryPage } from '@/pages/storefront/PagesHistoryPage'
import { PageBlockDetailPage } from '@/pages/storefront/PageBlockDetailPage'
import { ContactMessagesPage } from '@/pages/storefront/ContactMessagesPage'
import { RequestsPage } from '@/pages/requests/RequestsPage'
import { RequestDetailPage } from '@/pages/requests/RequestDetailPage'
import { NotificationsMonitorPage } from '@/pages/storefront/NotificationsMonitorPage'
import { NewsletterSubscribersPage } from '@/pages/storefront/NewsletterSubscribersPage'
import { WarehousesPage } from '@/pages/inventory/WarehousesPage'
import { WarehouseDetailPage } from '@/pages/inventory/WarehouseDetailPage'
import { StocksPage } from '@/pages/inventory/StocksPage'
import { TransfersPage } from '@/pages/inventory/TransfersPage'
import { TransferDetailPage } from '@/pages/inventory/TransferDetailPage'
import { AccountGroupsPage } from '@/pages/accounts/AccountGroupsPage'
import { AccountsPage } from '@/pages/accounts/AccountsPage'
import { AccountDetailPage, AccountCreatePage } from '@/pages/accounts/AccountDetailPage'
import { MigrationPage } from '@/pages/settings/MigrationPage'
import { OrdersPage } from '@/pages/orders/OrdersPage'
import { OrderDetailPage as OrderAdminDetailPage } from '@/pages/orders/OrderDetailPage'
import { ReturnsPage } from '@/pages/orders/ReturnsPage'
import { ReturnDetailPage } from '@/pages/orders/ReturnDetailPage'
import { InvoicesPage } from '@/pages/orders/InvoicesPage'
import { CmsPagesPage } from '@/pages/cms/CmsPagesPage'
import { CmsPageDetailPage } from '@/pages/cms/CmsPageDetailPage'
import { CampaignsPage } from '@/pages/promotion/CampaignsPage'
import { CampaignTypesPage } from '@/pages/promotion/CampaignTypesPage'
import { CampaignDetailPage } from '@/pages/promotion/CampaignDetailPage'
import { CouponsPage } from '@/pages/promotion/CouponsPage'
import { MembersPage } from '@/pages/crm/MembersPage'
import { MemberDetailPage } from '@/pages/crm/MemberDetailPage'
import { MemberGroupsPage } from '@/pages/crm/MemberGroupsPage'
import { QuotesPage } from '@/pages/orders/QuotesPage'
import { GiftCardsPage } from '@/pages/orders/GiftCardsPage'
import { NumberSeriesPage } from '@/pages/orders/NumberSeriesPage'
import { CargoZonesPage } from '@/pages/orders/CargoZonesPage'
import { PosSalesPage } from '@/pages/pos/PosSalesPage'
import { RegistersPage } from '@/pages/pos/RegistersPage'
import { SupplierInvoicesPage } from '@/pages/finance/SupplierInvoicesPage'
import { PickingPlansPage } from '@/pages/fulfillment/PickingPlansPage'
import { PackingStationsPage } from '@/pages/fulfillment/PackingStationsPage'
import { IntegrationLogsPage } from '@/pages/integrations/IntegrationLogsPage'
import { UsersPage } from '@/pages/settings/UsersPage'
import { RolesPage } from '@/pages/settings/RolesPage'
import { AuditLogsPage } from '@/pages/settings/AuditLogsPage'
import { LanguagesPage } from '@/pages/settings/LanguagesPage'
import { LookupTypesPage } from '@/pages/settings/LookupTypesPage'

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
            { path: 'catalog/product-submissions',     element: <ProductSubmissionsPage /> },
            { path: 'catalog/product-submissions/:id', element: <ProductSubmissionDetailPage /> },
            { path: 'catalog/settings',          element: <CatalogSettingsPage /> },
            { path: 'catalog/bulk-images',       element: <BulkImageUploadPage /> },
            { path: 'storefront/channel-categories',      element: <ChannelCategoriesPage /> },
            { path: 'storefront/menu-placement',          element: <MenuPlacementPage /> },
            { path: 'storefront/channel-categories/:id',  element: <ChannelCategoryDetailPage /> },
            { path: 'storefront/channel-products',         element: <ChannelProductsPage /> },
            { path: 'storefront/collections',              element: <CollectionsModerationPage /> },
            { path: 'storefront/reviews',                  element: <ReviewsModerationPage /> },
            { path: 'storefront/pages',                    element: <PagesManagementPage /> },
            { path: 'storefront/pages/history',            element: <PagesHistoryPage /> },
            { path: 'storefront/pages/:id',                element: <PageBlockDetailPage /> },
            { path: 'storefront/contact-messages',         element: <ContactMessagesPage /> },
            { path: 'requests',     element: <RequestsPage /> },
            { path: 'requests/:id', element: <RequestDetailPage /> },
            { path: 'storefront/notifications',            element: <NotificationsMonitorPage /> },
            { path: 'storefront/newsletter',               element: <NewsletterSubscribersPage /> },

            // Envanter
            { path: 'inventory/warehouses',     element: <WarehousesPage /> },
            { path: 'inventory/warehouses/:id', element: <WarehouseDetailPage /> },
            { path: 'inventory/stocks',         element: <StocksPage /> },
            { path: 'inventory/transfers',      element: <TransfersPage /> },
            { path: 'inventory/transfers/:id',  element: <TransferDetailPage /> },

            // Pazaryerleri
            { path: 'marketplaces',            element: <MarketplacesPage /> },
            { path: 'marketplaces/eslestirme', element: <MappingPage /> },
            { path: 'marketplaces/:id',        element: <MarketplaceStoreDetailPage /> },

            // Siparişler
            { path: 'orders',              element: <OrdersPage /> },
            { path: 'orders/:id',          element: <OrderAdminDetailPage /> },
            { path: 'orders/returns',      element: <ReturnsPage /> },
            { path: 'orders/returns/:id',  element: <ReturnDetailPage /> },
            { path: 'orders/quotes',       element: <QuotesPage /> },
            { path: 'orders/invoices',     element: <InvoicesPage /> },
            { path: 'orders/gift-cards',   element: <GiftCardsPage /> },
            { path: 'orders/number-series', element: <NumberSeriesPage /> },
            { path: 'orders/cargo-zones',  element: <CargoZonesPage /> },

            // Cari Kartlar
            { path: 'accounts/groups', element: <AccountGroupsPage /> },
            { path: 'accounts',        element: <AccountsPage /> },
            { path: 'accounts/new',    element: <AccountCreatePage /> },
            { path: 'accounts/:id',    element: <AccountDetailPage /> },

            // CRM
            { path: 'crm/members',       element: <MembersPage /> },
            { path: 'crm/members/:id',   element: <MemberDetailPage /> },
            { path: 'crm/member-groups', element: <MemberGroupsPage /> },

            // POS
            { path: 'pos/sales',     element: <PosSalesPage /> },
            { path: 'pos/registers', element: <RegistersPage /> },

            // Finans
            { path: 'finance/suppliers',         element: <Navigate to="/accounts?accountType=supplier" replace /> },
            { path: 'finance/supplier-invoices', element: <SupplierInvoicesPage /> },

            // Promosyon
            { path: 'promotion/campaigns', element: <CampaignsPage /> },
            { path: 'promotion/campaigns/:id', element: <CampaignDetailPage /> },
            { path: 'promotion/campaign-types', element: <CampaignTypesPage /> },
            { path: 'promotion/coupons',   element: <CouponsPage /> },

            // Fulfillment
            { path: 'fulfillment/picking-plans',    element: <PickingPlansPage /> },
            { path: 'fulfillment/packing-stations', element: <PackingStationsPage /> },

            // CMS
            { path: 'cms/pages',                element: <CmsPagesPage /> },
            { path: 'cms/pages/:id',            element: <CmsPageDetailPage /> },
            // nav_menus sistemi emekli — site menüsü Kanal Kategorileri'nden yönetilir (B-07)
            { path: 'navigation/menus',         element: <Navigate to="/storefront/menu-placement" replace /> },
            { path: 'navigation/menus/:id',     element: <Navigate to="/storefront/menu-placement" replace /> },

            // Entegrasyon
            { path: 'integrations/logs', element: <IntegrationLogsPage /> },

            // Ayarlar
            { path: 'settings/translations',    element: <TranslationsPage /> },
            { path: 'settings/users',           element: <UsersPage /> },
            { path: 'settings/roles',           element: <RolesPage /> },
            { path: 'settings/audit-logs',      element: <AuditLogsPage /> },
            { path: 'settings/firms',           element: <FirmsPage /> },
            { path: 'settings/firms/:id',       element: <FirmDetailPage /> },
            { path: 'settings/platform-types',  element: <PlatformTypesPage /> },
            { path: 'settings/integration-services', element: <IntegrationServicesPage /> },
            { path: 'settings/channels',         element: <ChannelsPage /> },
            { path: 'settings/languages',       element: <LanguagesPage /> },
            { path: 'settings/lookup-types',    element: <LookupTypesPage /> },
            { path: 'settings/migration',       element: <MigrationPage /> },
          ],
        },
      ],
    },
  ],
  { basename: '/admin' },
)
