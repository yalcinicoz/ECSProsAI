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
import { IntegrationServicesPage } from '@/pages/settings/IntegrationServicesPage'
import { ChannelsPage } from '@/pages/settings/ChannelsPage'
import { FirmsPage } from '@/pages/settings/FirmsPage'
import { FirmDetailPage } from '@/pages/settings/FirmDetailPage'
import { CatalogSettingsPage } from '@/pages/catalog/CatalogSettingsPage'
import { BulkImageUploadPage } from '@/pages/catalog/BulkImageUploadPage'
import { MenusPage } from '@/pages/cms/MenusPage'
import { ChannelCategoriesPage } from '@/pages/storefront/ChannelCategoriesPage'
import { ChannelProductsPage } from '@/pages/storefront/ChannelProductsPage'
import { ChannelCategoryDetailPage } from '@/pages/storefront/ChannelCategoryDetailPage'
import { CollectionsModerationPage } from '@/pages/storefront/CollectionsModerationPage'
import { ReviewsModerationPage } from '@/pages/storefront/ReviewsModerationPage'
import { PagesManagementPage } from '@/pages/storefront/PagesManagementPage'
import { PageBlockDetailPage } from '@/pages/storefront/PageBlockDetailPage'
import { ContactMessagesPage } from '@/pages/storefront/ContactMessagesPage'
import { NotificationsMonitorPage } from '@/pages/storefront/NotificationsMonitorPage'
import { NewsletterSubscribersPage } from '@/pages/storefront/NewsletterSubscribersPage'
import { MenuDetailPage } from '@/pages/cms/MenuDetailPage'
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
import { CouponsPage } from '@/pages/promotion/CouponsPage'
import { MembersPage } from '@/pages/crm/MembersPage'
import { MemberDetailPage } from '@/pages/crm/MemberDetailPage'
import { MemberGroupsPage } from '@/pages/crm/MemberGroupsPage'

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
            { path: 'storefront/channel-categories',      element: <ChannelCategoriesPage /> },
            { path: 'storefront/channel-categories/:id',  element: <ChannelCategoryDetailPage /> },
            { path: 'storefront/channel-products',         element: <ChannelProductsPage /> },
            { path: 'storefront/collections',              element: <CollectionsModerationPage /> },
            { path: 'storefront/reviews',                  element: <ReviewsModerationPage /> },
            { path: 'storefront/pages',                    element: <PagesManagementPage /> },
            { path: 'storefront/pages/:id',                element: <PageBlockDetailPage /> },
            { path: 'storefront/contact-messages',         element: <ContactMessagesPage /> },
            { path: 'storefront/notifications',            element: <NotificationsMonitorPage /> },
            { path: 'storefront/newsletter',               element: <NewsletterSubscribersPage /> },

            // Envanter
            { path: 'inventory/warehouses',     element: <WarehousesPage /> },
            { path: 'inventory/warehouses/:id', element: <WarehouseDetailPage /> },
            { path: 'inventory/stocks',         element: <StocksPage /> },
            { path: 'inventory/transfers',      element: <TransfersPage /> },
            { path: 'inventory/transfers/:id',  element: <TransferDetailPage /> },

            // Siparişler
            { path: 'orders',              element: <OrdersPage /> },
            { path: 'orders/:id',          element: <OrderAdminDetailPage /> },
            { path: 'orders/returns',      element: <ReturnsPage /> },
            { path: 'orders/returns/:id',  element: <ReturnDetailPage /> },
            { path: 'orders/quotes',       element: <PlaceholderPage title="Teklifler" /> },
            { path: 'orders/invoices',     element: <InvoicesPage /> },
            { path: 'orders/gift-cards',   element: <PlaceholderPage title="Hediye Kartları" /> },

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
            { path: 'pos/sales',     element: <PlaceholderPage title="POS Satışları" /> },
            { path: 'pos/registers', element: <PlaceholderPage title="Kasalar" /> },

            // Finans
            { path: 'finance/suppliers',         element: <PlaceholderPage title="Tedarikçiler" /> },
            { path: 'finance/supplier-invoices', element: <PlaceholderPage title="Tedarikçi Faturaları" /> },

            // Promosyon
            { path: 'promotion/campaigns', element: <CampaignsPage /> },
            { path: 'promotion/coupons',   element: <CouponsPage /> },

            // Fulfillment
            { path: 'fulfillment/picking-plans',    element: <PlaceholderPage title="Picking Planları" /> },
            { path: 'fulfillment/packing-stations', element: <PlaceholderPage title="Paketleme İstasyonları" /> },

            // CMS
            { path: 'cms/pages',                element: <CmsPagesPage /> },
            { path: 'cms/pages/:id',            element: <CmsPageDetailPage /> },
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
            { path: 'settings/integration-services', element: <IntegrationServicesPage /> },
            { path: 'settings/channels',         element: <ChannelsPage /> },
            { path: 'settings/languages',       element: <PlaceholderPage title="Diller" /> },
            { path: 'settings/lookup-types',    element: <PlaceholderPage title="Lookup Tipleri" /> },
            { path: 'settings/migration',       element: <MigrationPage /> },
          ],
        },
      ],
    },
  ],
  { basename: '/admin' },
)
