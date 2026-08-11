import { createBrowserRouter, Navigate } from 'react-router-dom'
import { MainLayout } from '@/components/layout/MainLayout'
import { AuthGuard } from '@/components/layout/AuthGuard'
import { LoginPage } from '@/pages/auth/LoginPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { ProductsPage } from '@/pages/products/ProductsPage'
import { ProductDetailPage } from '@/pages/products/ProductDetailPage'
import { ProductFormPage } from '@/pages/products/ProductFormPage'
import { OrdersPage } from '@/pages/orders/OrdersPage'
import { OrderDetailPage } from '@/pages/orders/OrderDetailPage'
import { FinancePage } from '@/pages/finance/FinancePage'
import { CampaignsPage } from '@/pages/campaigns/CampaignsPage'
import { AccountPage } from '@/pages/account/AccountPage'

export const router = createBrowserRouter(
  [
    { path: '/login', element: <LoginPage /> },
    {
      element: <AuthGuard />,
      children: [
        {
          element: <MainLayout />,
          children: [
            { path: '/', element: <DashboardPage /> },
            { path: '/products', element: <ProductsPage /> },
            { path: '/products/new', element: <ProductFormPage /> },
            { path: '/products/:code', element: <ProductDetailPage /> },
            { path: '/products/:code/edit', element: <ProductFormPage /> },
            { path: '/orders', element: <OrdersPage /> },
            { path: '/orders/:orderNumber', element: <OrderDetailPage /> },
            { path: '/finance', element: <FinancePage /> },
            { path: '/campaigns', element: <CampaignsPage /> },
            { path: '/account', element: <AccountPage /> },
          ],
        },
      ],
    },
    { path: '*', element: <Navigate to="/" replace /> },
  ],
  // 2026-08-11: subdomain kökü (satici.misharitalia.com) — path öneki kalktı
  { basename: '/' },
)
