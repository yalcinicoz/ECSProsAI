# E-Ticaret Altyapı Projesi - Frontend Mimari

**Doküman Versiyonu:** 1.0  
**Son Güncelleme:** Ocak 2025  
**Framework:** React 18 + TypeScript  
**Build Tool:** Vite

---

## 1. Genel Bakış

Üç ayrı frontend uygulaması bulunmaktadır:

| Uygulama | Amaç | Kullanıcılar |
|----------|------|--------------|
| Admin Panel | Yönetim arayüzü | Çalışanlar, yöneticiler |
| Storefront | Müşteri sitesi | Son kullanıcılar |
| POS | Mağaza satış terminali | Kasiyerler, mağaza personeli |

Her uygulama aynı teknoloji stack'ini kullanır ancak farklı yapı ve ihtiyaçlara sahiptir.

---

## 2. Teknoloji Stack

| Teknoloji | Kullanım | Versiyon |
|-----------|----------|----------|
| React | UI framework | 18.x |
| TypeScript | Tip güvenliği | 5.x |
| Vite | Build tool | 5.x |
| React Router | Routing | 6.x |
| TanStack Query | Server state yönetimi | 5.x |
| Zustand | Client state yönetimi | 4.x |
| React Hook Form | Form yönetimi | 7.x |
| Zod | Validation | 3.x |
| Tailwind CSS | Styling | 3.x |
| Headless UI | Accessible components | 1.x |
| Radix UI | Primitive components | - |
| Axios | HTTP client | 1.x |
| i18next | Çok dilli destek | 23.x |
| @microsoft/signalr | Real-time iletişim | 8.x |
| date-fns | Tarih işlemleri | 2.x |
| Lucide React | İkonlar | - |

---

## 3. Admin Panel Yapısı

```
admin/
├── public/
│   ├── favicon.ico
│   └── locales/
│       ├── tr/
│       │   └── translation.json
│       └── en/
│           └── translation.json
│
├── src/
│   ├── app/
│   │   ├── App.tsx
│   │   ├── routes.tsx
│   │   └── providers.tsx
│   │
│   ├── core/
│   │   ├── api/
│   │   │   ├── axios-instance.ts
│   │   │   ├── api-client.ts
│   │   │   └── endpoints.ts
│   │   ├── auth/
│   │   │   ├── auth-context.tsx
│   │   │   ├── auth-guard.tsx
│   │   │   ├── permission-guard.tsx
│   │   │   └── use-auth.ts
│   │   ├── i18n/
│   │   │   └── i18n.ts
│   │   ├── hooks/
│   │   │   ├── use-debounce.ts
│   │   │   ├── use-local-storage.ts
│   │   │   └── use-media-query.ts
│   │   ├── stores/
│   │   │   ├── ui-store.ts
│   │   │   └── preference-store.ts
│   │   ├── types/
│   │   │   ├── api.types.ts
│   │   │   ├── common.types.ts
│   │   │   └── index.ts
│   │   └── utils/
│   │       ├── format.ts
│   │       ├── date.ts
│   │       └── validation.ts
│   │
│   ├── shared/
│   │   ├── components/
│   │   │   ├── ui/
│   │   │   ├── form/
│   │   │   ├── layout/
│   │   │   └── data/
│   │   └── hooks/
│   │
│   ├── modules/
│   │   ├── core/
│   │   ├── catalog/
│   │   ├── inventory/
│   │   ├── crm/
│   │   ├── orders/
│   │   ├── fulfillment/
│   │   ├── finance/
│   │   ├── promotions/
│   │   ├── iam/
│   │   ├── cms/
│   │   ├── integrations/
│   │   └── dashboard/
│   │
│   ├── styles/
│   │   ├── globals.css
│   │   └── tailwind.css
│   │
│   └── main.tsx
│
├── index.html
├── vite.config.ts
├── tsconfig.json
├── tailwind.config.js
└── package.json
```

### 3.1 Core Klasörü

Uygulamanın temel altyapısını içerir.

**api/** - HTTP istemci yapılandırması
```typescript
// axios-instance.ts
const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  timeout: 30000,
});

axiosInstance.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  config.headers['Accept-Language'] = i18n.language;
  config.headers['X-Firm-Id'] = getFirmId();
  return config;
});

axiosInstance.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Redirect to login
    }
    return Promise.reject(error);
  }
);
```

**auth/** - Kimlik doğrulama
```typescript
// auth-guard.tsx
export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <LoadingScreen />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
}

// permission-guard.tsx
export function PermissionGuard({ 
  permission, 
  children 
}: { 
  permission: string; 
  children: React.ReactNode;
}) {
  const { hasPermission } = useAuth();

  if (!hasPermission(permission)) {
    return <AccessDenied />;
  }

  return <>{children}</>;
}
```

**stores/** - Client state
```typescript
// ui-store.ts
interface UiState {
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  theme: 'light' | 'dark' | 'system';
  toggleSidebar: () => void;
  collapseSidebar: () => void;
  setTheme: (theme: 'light' | 'dark' | 'system') => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarOpen: true,
      sidebarCollapsed: false,
      theme: 'system',
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
      collapseSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
      setTheme: (theme) => set({ theme }),
    }),
    { name: 'ui-store' }
  )
);
```

### 3.2 Shared Klasörü

Tüm modüller tarafından kullanılan ortak bileşenler.

**components/ui/** - Temel UI bileşenleri
```
Button/
├── Button.tsx
├── Button.test.tsx
└── index.ts

Input/
├── Input.tsx
├── Input.test.tsx
└── index.ts

Select/
Modal/
Table/
Pagination/
Toast/
Dropdown/
Tabs/
Card/
Badge/
Spinner/
Tooltip/
Avatar/
```

**components/form/** - Form bileşenleri
```
FormField/
FormSelect/
FormCheckbox/
FormRadio/
FormSwitch/
FormDatePicker/
FormTimePicker/
FormImageUpload/
FormFileUpload/
FormRichText/
FormColorPicker/
FormJsonEditor/
```

**components/layout/** - Sayfa düzeni bileşenleri
```
MainLayout/
├── MainLayout.tsx
├── index.ts
Sidebar/
├── Sidebar.tsx
├── SidebarItem.tsx
├── SidebarGroup.tsx
├── index.ts
Header/
├── Header.tsx
├── UserMenu.tsx
├── NotificationDropdown.tsx
├── LanguageSelector.tsx
├── FirmSelector.tsx
├── index.ts
PageHeader/
├── PageHeader.tsx
├── index.ts
```

**components/data/** - Veri gösterim bileşenleri
```
DataTable/
├── DataTable.tsx
├── DataTableHead.tsx
├── DataTableBody.tsx
├── DataTableRow.tsx
├── DataTablePagination.tsx
├── index.ts
FilterPanel/
SearchInput/
ExportButton/
ImportButton/
BulkActions/
```

### 3.3 Modules Klasörü

Her modül kendi içinde bağımsız yapıdadır.

```
modules/
├── catalog/
│   ├── api/
│   │   ├── products.api.ts
│   │   ├── categories.api.ts
│   │   ├── attributes.api.ts
│   │   └── index.ts
│   │
│   ├── hooks/
│   │   ├── use-products.ts
│   │   ├── use-product.ts
│   │   ├── use-create-product.ts
│   │   ├── use-update-product.ts
│   │   ├── use-delete-product.ts
│   │   ├── use-categories.ts
│   │   └── index.ts
│   │
│   ├── pages/
│   │   ├── ProductsPage/
│   │   │   ├── ProductsPage.tsx
│   │   │   ├── ProductsPage.test.tsx
│   │   │   └── index.ts
│   │   ├── ProductDetailPage/
│   │   ├── ProductCreatePage/
│   │   ├── CategoriesPage/
│   │   ├── CategoryDetailPage/
│   │   ├── AttributesPage/
│   │   └── ProductGroupsPage/
│   │
│   ├── components/
│   │   ├── ProductForm/
│   │   ├── VariantForm/
│   │   ├── VariantTable/
│   │   ├── CategoryTree/
│   │   ├── CategoryForm/
│   │   ├── AttributeForm/
│   │   ├── ImageGallery/
│   │   ├── PriceEditor/
│   │   └── ProductFilters/
│   │
│   ├── types/
│   │   └── catalog.types.ts
│   │
│   └── index.ts
```

**API katmanı örneği:**
```typescript
// api/products.api.ts
import { apiClient } from '@/core/api';
import { Product, ProductFilters, CreateProductDto, UpdateProductDto } from '../types';
import { PaginatedResponse } from '@/core/types';

export const productsApi = {
  getAll: (filters: ProductFilters) => 
    apiClient.get<PaginatedResponse<Product>>('/catalog/products', { params: filters }),
  
  getById: (id: string) => 
    apiClient.get<Product>(`/catalog/products/${id}`),
  
  create: (data: CreateProductDto) => 
    apiClient.post<Product>('/catalog/products', data),
  
  update: (id: string, data: UpdateProductDto) => 
    apiClient.put<Product>(`/catalog/products/${id}`, data),
  
  delete: (id: string) => 
    apiClient.delete(`/catalog/products/${id}`),
  
  activate: (id: string) => 
    apiClient.patch(`/catalog/products/${id}/activate`),
  
  deactivate: (id: string) => 
    apiClient.patch(`/catalog/products/${id}/deactivate`),
  
  bulkUpdate: (data: BulkUpdateDto) => 
    apiClient.post('/catalog/products/bulk-update', data),
  
  export: (filters: ProductFilters) => 
    apiClient.get('/catalog/products/export', { 
      params: filters, 
      responseType: 'blob' 
    }),
};
```

**Hooks katmanı örneği:**
```typescript
// hooks/use-products.ts
import { useQuery } from '@tanstack/react-query';
import { productsApi } from '../api';
import { ProductFilters } from '../types';

export const productKeys = {
  all: ['products'] as const,
  lists: () => [...productKeys.all, 'list'] as const,
  list: (filters: ProductFilters) => [...productKeys.lists(), filters] as const,
  details: () => [...productKeys.all, 'detail'] as const,
  detail: (id: string) => [...productKeys.details(), id] as const,
};

export function useProducts(filters: ProductFilters) {
  return useQuery({
    queryKey: productKeys.list(filters),
    queryFn: () => productsApi.getAll(filters),
    staleTime: 1000 * 60 * 5, // 5 minutes
  });
}

// hooks/use-product.ts
export function useProduct(id: string) {
  return useQuery({
    queryKey: productKeys.detail(id),
    queryFn: () => productsApi.getById(id),
    enabled: !!id,
  });
}

// hooks/use-create-product.ts
import { useMutation, useQueryClient } from '@tanstack/react-query';

export function useCreateProduct() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: productsApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: productKeys.lists() });
      toast.success('Ürün başarıyla oluşturuldu');
    },
    onError: (error) => {
      toast.error('Ürün oluşturulurken hata oluştu');
    },
  });
}

// hooks/use-update-product.ts
export function useUpdateProduct(id: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateProductDto) => productsApi.update(id, data),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: productKeys.lists() });
      queryClient.setQueryData(productKeys.detail(id), data);
      toast.success('Ürün başarıyla güncellendi');
    },
  });
}
```

**Page örneği:**
```typescript
// pages/ProductsPage/ProductsPage.tsx
import { useState } from 'react';
import { useProducts } from '../../hooks';
import { ProductFilters } from '../../types';
import { DataTable, PageHeader, FilterPanel } from '@/shared/components';
import { ProductFiltersPanel } from '../../components';
import { useTranslation } from 'react-i18next';

export function ProductsPage() {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<ProductFilters>({
    page: 1,
    pageSize: 20,
  });

  const { data, isLoading, error } = useProducts(filters);

  const columns = [
    { key: 'code', header: t('products.code'), sortable: true },
    { key: 'name', header: t('products.name'), sortable: true },
    { key: 'categoryName', header: t('products.category') },
    { key: 'variantCount', header: t('products.variants') },
    { key: 'isActive', header: t('common.status'), render: (row) => <StatusBadge active={row.isActive} /> },
    { key: 'actions', header: '', render: (row) => <ProductActions product={row} /> },
  ];

  return (
    <div>
      <PageHeader
        title={t('products.title')}
        actions={
          <Button as={Link} to="/catalog/products/new">
            {t('products.create')}
          </Button>
        }
      />
      
      <ProductFiltersPanel filters={filters} onChange={setFilters} />
      
      <DataTable
        columns={columns}
        data={data?.data ?? []}
        loading={isLoading}
        pagination={{
          currentPage: filters.page,
          pageSize: filters.pageSize,
          totalCount: data?.meta.totalCount ?? 0,
          onPageChange: (page) => setFilters({ ...filters, page }),
          onPageSizeChange: (pageSize) => setFilters({ ...filters, pageSize, page: 1 }),
        }}
        sorting={{
          sortKey: filters.sort?.split(':')[0],
          sortDirection: filters.sort?.split(':')[1] as 'asc' | 'desc',
          onSort: (key, direction) => setFilters({ ...filters, sort: `${key}:${direction}` }),
        }}
      />
    </div>
  );
}
```

---

## 4. Storefront Yapısı

```
storefront/
├── public/
│   └── locales/
│
├── src/
│   ├── app/
│   │   ├── App.tsx
│   │   ├── routes.tsx
│   │   └── providers.tsx
│   │
│   ├── core/
│   │   ├── api/
│   │   ├── auth/
│   │   ├── i18n/
│   │   ├── hooks/
│   │   ├── stores/
│   │   │   ├── cart-store.ts
│   │   │   └── ui-store.ts
│   │   ├── types/
│   │   └── utils/
│   │
│   ├── shared/
│   │   ├── components/
│   │   │   ├── ui/
│   │   │   ├── layout/
│   │   │   └── common/
│   │   └── hooks/
│   │
│   ├── features/
│   │   ├── home/
│   │   ├── catalog/
│   │   ├── cart/
│   │   ├── checkout/
│   │   ├── account/
│   │   ├── auth/
│   │   ├── cms/
│   │   └── b2b/                      # B2B özellikleri
│   │
│   ├── styles/
│   │
│   └── main.tsx
│
├── index.html
├── vite.config.ts
├── tsconfig.json
├── tailwind.config.js
└── package.json
```

### 4.1 Sepet Yönetimi (Zustand)

```typescript
// stores/cart-store.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface CartItem {
  variantId: string;
  productId: string;
  name: string;
  variantInfo: string;
  imageUrl: string;
  price: number;
  quantity: number;
}

interface CartState {
  items: CartItem[];
  couponCode: string | null;
  
  addItem: (item: Omit<CartItem, 'quantity'>, quantity?: number) => void;
  removeItem: (variantId: string) => void;
  updateQuantity: (variantId: string, quantity: number) => void;
  clearCart: () => void;
  applyCoupon: (code: string) => void;
  removeCoupon: () => void;
  
  // Computed
  totalItems: () => number;
  subtotal: () => number;
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      items: [],
      couponCode: null,

      addItem: (item, quantity = 1) => {
        set((state) => {
          const existingItem = state.items.find((i) => i.variantId === item.variantId);
          
          if (existingItem) {
            return {
              items: state.items.map((i) =>
                i.variantId === item.variantId
                  ? { ...i, quantity: i.quantity + quantity }
                  : i
              ),
            };
          }
          
          return {
            items: [...state.items, { ...item, quantity }],
          };
        });
      },

      removeItem: (variantId) => {
        set((state) => ({
          items: state.items.filter((i) => i.variantId !== variantId),
        }));
      },

      updateQuantity: (variantId, quantity) => {
        if (quantity <= 0) {
          get().removeItem(variantId);
          return;
        }
        
        set((state) => ({
          items: state.items.map((i) =>
            i.variantId === variantId ? { ...i, quantity } : i
          ),
        }));
      },

      clearCart: () => set({ items: [], couponCode: null }),
      
      applyCoupon: (code) => set({ couponCode: code }),
      
      removeCoupon: () => set({ couponCode: null }),

      totalItems: () => get().items.reduce((sum, item) => sum + item.quantity, 0),
      
      subtotal: () => get().items.reduce((sum, item) => sum + item.price * item.quantity, 0),
    }),
    {
      name: 'cart-storage',
    }
  )
);
```

### 4.2 Feature Modülleri

**catalog/**
```
catalog/
├── api/
│   └── catalog.api.ts
├── hooks/
│   ├── use-categories.ts
│   ├── use-products.ts
│   ├── use-product.ts
│   └── use-search.ts
├── pages/
│   ├── CategoryPage/
│   ├── ProductListPage/
│   ├── ProductDetailPage/
│   └── SearchResultsPage/
└── components/
    ├── ProductCard/
    ├── ProductGrid/
    ├── ProductFilters/
    ├── ProductSort/
    ├── ProductGallery/
    ├── VariantSelector/
    ├── ProductTabs/
    ├── RelatedProducts/
    └── RecentlyViewed/
```

**checkout/**
```
checkout/
├── api/
│   └── checkout.api.ts
├── hooks/
│   ├── use-checkout.ts
│   └── use-payment.ts
├── pages/
│   ├── CheckoutPage/
│   └── OrderConfirmationPage/
└── components/
    ├── CheckoutSteps/
    ├── AddressStep/
    ├── ShippingStep/
    ├── PaymentStep/
    ├── ReviewStep/
    ├── AddressForm/
    ├── AddressSelector/
    ├── ShippingOptions/
    ├── PaymentMethods/
    ├── CreditCardForm/
    └── OrderSummary/
```

**b2b/** (B2B özellikleri)
```
b2b/
├── api/
│   ├── quotes.api.ts
│   ├── credit.api.ts
│   └── templates.api.ts
├── hooks/
│   ├── use-quotes.ts
│   ├── use-credit.ts
│   └── use-order-templates.ts
├── pages/
│   ├── QuickOrderPage/           # SKU ile hızlı sipariş
│   ├── OrderTemplatesPage/       # Sipariş şablonları
│   ├── OrderTemplateDetailPage/
│   ├── QuoteRequestPage/         # Teklif talebi
│   ├── QuotesPage/               # Tekliflerim
│   ├── QuoteDetailPage/          # Teklif detayı
│   └── AccountStatementPage/     # Cari hesap ekstresi
└── components/
    ├── QuickOrderForm/           # SKU + miktar listesi
    ├── CsvImportForm/            # CSV ile toplu ekleme
    ├── UnitSelector/             # Adet/Düzine/Koli seçici
    ├── CreditLimitBadge/         # Kalan kredi limiti
    ├── CreditLimitWarning/       # Kredi limiti aşım uyarısı
    ├── WholesalePriceTable/      # Kademe fiyat tablosu
    ├── MinOrderWarning/          # Minimum sipariş uyarısı
    ├── QuoteCard/                # Teklif kartı
    ├── QuoteTimeline/            # Teklif durumu zaman çizelgesi
    ├── PaymentTermsSelector/     # Vade seçimi
    └── StatementTable/           # Cari hesap tablosu
```

---

## 5. POS Uygulama Yapısı

```
pos/
├── public/
│   └── locales/
│
├── src/
│   ├── app/
│   │   ├── App.tsx
│   │   ├── routes.tsx
│   │   └── providers.tsx
│   │
│   ├── core/
│   │   ├── api/
│   │   ├── auth/
│   │   ├── i18n/
│   │   ├── hooks/
│   │   ├── stores/
│   │   │   ├── cart-store.ts         # POS sepeti
│   │   │   ├── session-store.ts      # Kasa oturumu
│   │   │   └── customer-store.ts     # Aktif müşteri
│   │   ├── hardware/
│   │   │   ├── barcode-scanner.ts    # Barkod okuyucu
│   │   │   ├── receipt-printer.ts    # Fiş yazıcı
│   │   │   └── cash-drawer.ts        # Kasa çekmecesi
│   │   ├── types/
│   │   └── utils/
│   │
│   ├── shared/
│   │   └── components/
│   │       ├── ui/
│   │       │   ├── NumPad/           # Sayısal tuş takımı
│   │       │   ├── AmountInput/      # Tutar girişi
│   │       │   └── ...
│   │       ├── pos/
│   │       │   ├── ProductSearch/    # Ürün arama
│   │       │   ├── BarcodeInput/     # Barkod giriş
│   │       │   ├── QuickActions/     # Hızlı işlem butonları
│   │       │   └── CustomerBadge/    # Aktif müşteri rozeti
│   │       └── layout/
│   │           ├── PosLayout/        # Ana POS düzeni
│   │           ├── PosHeader/
│   │           └── StatusBar/        # Kasa durumu
│   │
│   ├── features/
│   │   ├── auth/
│   │   │   └── pages/
│   │   │       ├── LoginPage/        # Kasiyer girişi
│   │   │       └── PinLoginPage/     # PIN ile hızlı giriş
│   │   │
│   │   ├── session/
│   │   │   ├── api/
│   │   │   ├── hooks/
│   │   │   │   ├── use-session.ts
│   │   │   │   └── use-cash-movement.ts
│   │   │   ├── pages/
│   │   │   │   ├── OpenSessionPage/      # Kasa açma
│   │   │   │   ├── CloseSessionPage/     # Kasa kapama
│   │   │   │   └── SessionReportPage/    # Gün sonu rapor
│   │   │   └── components/
│   │   │       ├── CashCountForm/        # Nakit sayım formu
│   │   │       ├── SessionSummary/
│   │   │       └── CashMovementModal/    # Nakit giriş/çıkış
│   │   │
│   │   ├── sale/
│   │   │   ├── api/
│   │   │   │   └── sale.api.ts
│   │   │   ├── hooks/
│   │   │   │   ├── use-pos-cart.ts
│   │   │   │   ├── use-product-lookup.ts
│   │   │   │   └── use-quick-products.ts
│   │   │   ├── pages/
│   │   │   │   └── SalePage/             # Ana satış ekranı
│   │   │   └── components/
│   │   │       ├── CartPanel/            # Sepet paneli
│   │   │       ├── CartItem/
│   │   │       ├── CartSummary/
│   │   │       ├── ProductPanel/         # Ürün/kategori paneli
│   │   │       ├── QuickProducts/        # Hızlı erişim ürünleri
│   │   │       ├── CategoryGrid/
│   │   │       ├── ProductGrid/
│   │   │       ├── PaymentModal/         # Ödeme modalı
│   │   │       ├── SplitPayment/         # Bölünmüş ödeme
│   │   │       ├── CashPayment/          # Nakit ödeme
│   │   │       ├── CardPayment/          # Kart ödeme
│   │   │       ├── CustomerSelectModal/  # Müşteri seçimi
│   │   │       ├── DiscountModal/        # İndirim uygulama
│   │   │       ├── QuantityModal/        # Miktar değiştirme
│   │   │       └── HoldOrderModal/       # Siparişi beklet
│   │   │
│   │   ├── returns/
│   │   │   ├── api/
│   │   │   ├── hooks/
│   │   │   ├── pages/
│   │   │   │   └── ReturnPage/           # İade ekranı
│   │   │   └── components/
│   │   │       ├── ReceiptLookup/        # Fiş ile arama
│   │   │       ├── ReturnItems/
│   │   │       └── RefundOptions/
│   │   │
│   │   ├── customers/
│   │   │   ├── api/
│   │   │   ├── hooks/
│   │   │   ├── pages/
│   │   │   │   └── CustomerSearchPage/
│   │   │   └── components/
│   │   │       ├── CustomerSearch/
│   │   │       ├── CustomerQuickAdd/
│   │   │       ├── CustomerInfo/
│   │   │       └── CustomerHistory/      # Son alışverişler
│   │   │
│   │   ├── orders/
│   │   │   ├── pages/
│   │   │   │   ├── HeldOrdersPage/       # Bekleyen siparişler
│   │   │   │   └── OrderHistoryPage/     # Gün içi siparişler
│   │   │   └── components/
│   │   │       └── OrderCard/
│   │   │
│   │   └── wholesale/                    # Toptan mağaza ek özellikleri
│   │       ├── hooks/
│   │       │   └── use-credit-sale.ts
│   │       └── components/
│   │           ├── CreditSaleToggle/     # Cari satış açma/kapama
│   │           ├── PaymentTermsSelect/   # Vade seçimi
│   │           ├── CreditLimitWarning/   # Limit uyarısı
│   │           └── QuoteFromCart/        # Sepetten teklif
│   │
│   ├── styles/
│   │   ├── globals.css
│   │   └── pos-theme.css
│   │
│   └── main.tsx
│
├── index.html
├── vite.config.ts
├── tsconfig.json
├── tailwind.config.js
└── package.json
```

### 5.1 POS Sepet Yönetimi

```typescript
// stores/cart-store.ts
import { create } from 'zustand';

interface PosCartItem {
  id: string;
  variantId: string;
  sku: string;
  barcode: string;
  name: string;
  variantInfo: string;
  price: number;
  originalPrice: number;
  quantity: number;
  discount: number;
  discountType: 'percent' | 'amount' | null;
}

interface PosCartState {
  items: PosCartItem[];
  customerId: string | null;
  customerName: string | null;
  isWholesale: boolean;
  isCreditSale: boolean;
  paymentTermsDays: number | null;
  holdNote: string | null;
  
  // Actions
  addItem: (item: Omit<PosCartItem, 'id' | 'discount' | 'discountType'>) => void;
  removeItem: (id: string) => void;
  updateQuantity: (id: string, quantity: number) => void;
  applyItemDiscount: (id: string, discount: number, type: 'percent' | 'amount') => void;
  clearItemDiscount: (id: string) => void;
  setCustomer: (id: string, name: string, isWholesale: boolean) => void;
  clearCustomer: () => void;
  setCreditSale: (value: boolean, paymentTermsDays?: number) => void;
  holdCart: (note: string) => void;
  clearCart: () => void;
  
  // Computed
  subtotal: () => number;
  totalDiscount: () => number;
  total: () => number;
  itemCount: () => number;
}

export const usePosCartStore = create<PosCartState>((set, get) => ({
  items: [],
  customerId: null,
  customerName: null,
  isWholesale: false,
  isCreditSale: false,
  paymentTermsDays: null,
  holdNote: null,

  addItem: (item) => {
    set((state) => {
      const existing = state.items.find((i) => i.variantId === item.variantId);
      
      if (existing) {
        return {
          items: state.items.map((i) =>
            i.variantId === item.variantId
              ? { ...i, quantity: i.quantity + item.quantity }
              : i
          ),
        };
      }
      
      return {
        items: [...state.items, { 
          ...item, 
          id: crypto.randomUUID(),
          discount: 0,
          discountType: null,
        }],
      };
    });
  },

  removeItem: (id) => {
    set((state) => ({
      items: state.items.filter((i) => i.id !== id),
    }));
  },

  updateQuantity: (id, quantity) => {
    if (quantity <= 0) {
      get().removeItem(id);
      return;
    }
    
    set((state) => ({
      items: state.items.map((i) =>
        i.id === id ? { ...i, quantity } : i
      ),
    }));
  },

  applyItemDiscount: (id, discount, type) => {
    set((state) => ({
      items: state.items.map((i) =>
        i.id === id ? { ...i, discount, discountType: type } : i
      ),
    }));
  },

  clearItemDiscount: (id) => {
    set((state) => ({
      items: state.items.map((i) =>
        i.id === id ? { ...i, discount: 0, discountType: null } : i
      ),
    }));
  },

  setCustomer: (id, name, isWholesale) => {
    set({ customerId: id, customerName: name, isWholesale });
  },

  clearCustomer: () => {
    set({ 
      customerId: null, 
      customerName: null, 
      isWholesale: false,
      isCreditSale: false,
      paymentTermsDays: null,
    });
  },

  setCreditSale: (value, paymentTermsDays) => {
    set({ isCreditSale: value, paymentTermsDays: paymentTermsDays ?? null });
  },

  holdCart: (note) => {
    set({ holdNote: note });
  },

  clearCart: () => {
    set({
      items: [],
      customerId: null,
      customerName: null,
      isWholesale: false,
      isCreditSale: false,
      paymentTermsDays: null,
      holdNote: null,
    });
  },

  subtotal: () => {
    return get().items.reduce((sum, item) => sum + item.price * item.quantity, 0);
  },

  totalDiscount: () => {
    return get().items.reduce((sum, item) => {
      if (!item.discountType) return sum;
      
      const itemTotal = item.price * item.quantity;
      if (item.discountType === 'percent') {
        return sum + (itemTotal * item.discount / 100);
      }
      return sum + item.discount;
    }, 0);
  },

  total: () => {
    return get().subtotal() - get().totalDiscount();
  },

  itemCount: () => {
    return get().items.reduce((sum, item) => sum + item.quantity, 0);
  },
}));
```

### 5.2 Donanım Entegrasyonu

```typescript
// core/hardware/barcode-scanner.ts
type BarcodeHandler = (barcode: string) => void;

class BarcodeScanner {
  private buffer: string = '';
  private timeout: number | null = null;
  private handlers: Set<BarcodeHandler> = new Set();
  private readonly SCAN_TIMEOUT = 50; // ms

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('keydown', this.handleKeyDown);
    }
  }

  private handleKeyDown = (e: KeyboardEvent) => {
    // Ignore if focus is on input
    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
      return;
    }

    if (this.timeout) {
      clearTimeout(this.timeout);
    }

    if (e.key === 'Enter') {
      if (this.buffer.length > 0) {
        this.emitBarcode(this.buffer);
        this.buffer = '';
      }
      return;
    }

    if (e.key.length === 1) {
      this.buffer += e.key;
    }

    this.timeout = window.setTimeout(() => {
      this.buffer = '';
    }, this.SCAN_TIMEOUT);
  };

  private emitBarcode(barcode: string) {
    this.handlers.forEach((handler) => handler(barcode));
  }

  subscribe(handler: BarcodeHandler) {
    this.handlers.add(handler);
    return () => this.handlers.delete(handler);
  }

  destroy() {
    if (typeof window !== 'undefined') {
      window.removeEventListener('keydown', this.handleKeyDown);
    }
  }
}

export const barcodeScanner = new BarcodeScanner();

// React hook
export function useBarcodeScanner(onScan: BarcodeHandler) {
  useEffect(() => {
    return barcodeScanner.subscribe(onScan);
  }, [onScan]);
}
```

```typescript
// core/hardware/receipt-printer.ts
interface PrintOptions {
  copies?: number;
  openDrawer?: boolean;
}

class ReceiptPrinter {
  async print(receiptId: string, options: PrintOptions = {}) {
    const { copies = 1, openDrawer = false } = options;
    
    // Fiş PDF'ini al
    const response = await fetch(`/api/v1/pos/receipts/${receiptId}/pdf`);
    const blob = await response.blob();
    
    // Yazdır
    const url = URL.createObjectURL(blob);
    const printWindow = window.open(url);
    
    if (printWindow) {
      printWindow.onload = () => {
        for (let i = 0; i < copies; i++) {
          printWindow.print();
        }
        printWindow.close();
        URL.revokeObjectURL(url);
      };
    }
    
    // Kasa çekmecesi aç
    if (openDrawer) {
      await cashDrawer.open();
    }
  }
}

export const receiptPrinter = new ReceiptPrinter();
```

### 5.3 POS Ekran Düzeni

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  [Logo]  KASA-01  │  Kasiyer: Ahmet  │  14:32  │  [Müşteri: -]  │  [⚙]    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                         │                                   │
│  ┌───────────────────────────────────┐  │   SEPET                    [🗑]   │
│  │  [🔍 Barkod okut veya ara...]     │  │   ───────────────────────────    │
│  └───────────────────────────────────┘  │                                   │
│                                         │   T-Shirt Mavi L                  │
│  KATEGORİLER                            │   2 x ₺99,50           ₺199,00   │
│  ┌────────┐ ┌────────┐ ┌────────┐      │   [-] [+]                   [🗑]  │
│  │ Üst    │ │ Alt    │ │Aksesuar│      │                                   │
│  │ Giyim  │ │ Giyim  │ │        │      │   Pantolon Siyah 32               │
│  └────────┘ └────────┘ └────────┘      │   1 x ₺299,00          ₺299,00   │
│  ┌────────┐ ┌────────┐ ┌────────┐      │   [-] [+]                   [🗑]  │
│  │ Ayakkabı│ │ Çanta │ │ Diğer │      │                                   │
│  └────────┘ └────────┘ └────────┘      │   Kemer Deri                      │
│                                         │   1 x ₺89,00            ₺89,00   │
│  HIZLI ERİŞİM                           │   [-] [+]                   [🗑]  │
│  ┌────────┐ ┌────────┐ ┌────────┐      │                                   │
│  │ Poşet  │ │ Hediye │ │Kargo   │      │   ───────────────────────────    │
│  │ ₺2,00  │ │ Paketi │ │Kutusu  │      │   Ara Toplam:           ₺587,00  │
│  └────────┘ └────────┘ └────────┘      │   İndirim:               -₺50,00  │
│                                         │   ───────────────────────────    │
│                                         │   TOPLAM:               ₺537,00  │
│                                         │                                   │
│                                         │   ┌──────────┐ ┌──────────┐      │
│                                         │   │ İNDİRİM  │ │ MÜŞTERİ  │      │
│                                         │   │   (F5)   │ │   (F6)   │      │
│                                         │   └──────────┘ └──────────┘      │
│                                         │                                   │
│                                         │   ┌────────────────────────────┐ │
│                                         │   │      ÖDEME AL (F12)        │ │
│                                         │   └────────────────────────────┘ │
│                                         │                                   │
│  [F1 Beklet] [F2 İade] [F3 Rapor] [F4 Nakit Giriş/Çıkış]    [ESC İptal]   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 5.4 Klavye Kısayolları

| Kısayol | İşlev |
|---------|-------|
| F1 | Siparişi beklet |
| F2 | İade işlemi |
| F3 | Gün sonu raporu |
| F4 | Nakit giriş/çıkış |
| F5 | İndirim uygula |
| F6 | Müşteri seç |
| F7 | Ürün ara |
| F8 | Bekleyen siparişler |
| F9 | Son satışlar |
| F10 | Ayarlar |
| F12 | Ödeme al |
| ESC | İptal / Kapat |
| +/- | Miktar artır/azalt |
| Delete | Seçili ürünü sil |
| Enter | Barkod onayla |

---

## 6. Routing

### 6.1 Admin Panel Routes

```typescript
// app/routes.tsx
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AuthGuard, PermissionGuard } from '@/core/auth';
import { MainLayout } from '@/shared/components/layout';

// Lazy loaded pages
const DashboardPage = lazy(() => import('@/modules/dashboard/pages/DashboardPage'));
const ProductsPage = lazy(() => import('@/modules/catalog/pages/ProductsPage'));
// ... diğer sayfalar

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: (
      <AuthGuard>
        <MainLayout />
      </AuthGuard>
    ),
    children: [
      { index: true, element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard', element: <DashboardPage /> },
      
      // Catalog
      {
        path: 'catalog',
        children: [
          { index: true, element: <Navigate to="products" replace /> },
          { path: 'products', element: <ProductsPage /> },
          { path: 'products/new', element: <ProductCreatePage /> },
          { path: 'products/:id', element: <ProductDetailPage /> },
          { path: 'categories', element: <CategoriesPage /> },
          { path: 'categories/:id', element: <CategoryDetailPage /> },
          { path: 'attributes', element: <AttributesPage /> },
          { path: 'product-groups', element: <ProductGroupsPage /> },
        ],
      },
      
      // Inventory
      {
        path: 'inventory',
        children: [
          { path: 'warehouses', element: <WarehousesPage /> },
          { path: 'warehouses/:id', element: <WarehouseDetailPage /> },
          { path: 'stocks', element: <StocksPage /> },
          { path: 'movements', element: <StockMovementsPage /> },
          { path: 'transfers', element: <TransfersPage /> },
          { path: 'transfers/:id', element: <TransferDetailPage /> },
        ],
      },
      
      // Orders
      {
        path: 'orders',
        children: [
          { index: true, element: <OrdersPage /> },
          { path: ':id', element: <OrderDetailPage /> },
          { path: 'invoices', element: <InvoicesPage /> },
          { path: 'shipments', element: <ShipmentsPage /> },
          { path: 'returns', element: <ReturnsPage /> },
          { path: 'returns/:id', element: <ReturnDetailPage /> },
          { path: 'gift-cards', element: <GiftCardsPage /> },
        ],
      },
      
      // Fulfillment
      {
        path: 'fulfillment',
        children: [
          { path: 'dashboard', element: <FulfillmentDashboardPage /> },
          { path: 'picking-plans', element: <PickingPlansPage /> },
          { path: 'picking-plans/:id', element: <PickingPlanDetailPage /> },
          { path: 'picking', element: <PickingTerminalPage /> },
          { path: 'sorting', element: <SortingTerminalPage /> },
          { path: 'packing', element: <PackingTerminalPage /> },
          { path: 'stations', element: <PackingStationsPage /> },
        ],
      },
      
      // CRM
      {
        path: 'crm',
        children: [
          { path: 'members', element: <MembersPage /> },
          { path: 'members/:id', element: <MemberDetailPage /> },
          { path: 'member-groups', element: <MemberGroupsPage /> },
          { path: 'addresses', element: <AddressDefinitionsPage /> },
        ],
      },
      
      // Finance
      {
        path: 'finance',
        children: [
          { path: 'suppliers', element: <SuppliersPage /> },
          { path: 'suppliers/:id', element: <SupplierDetailPage /> },
          { path: 'invoices', element: <SupplierInvoicesPage /> },
          { path: 'deliveries', element: <SupplierDeliveriesPage /> },
          { path: 'payments', element: <SupplierPaymentsPage /> },
          { path: 'returns', element: <SupplierReturnsPage /> },
        ],
      },
      
      // Promotions
      {
        path: 'promotions',
        children: [
          { path: 'campaigns', element: <CampaignsPage /> },
          { path: 'campaigns/new', element: <CampaignCreatePage /> },
          { path: 'campaigns/:id', element: <CampaignDetailPage /> },
          { path: 'coupons', element: <CouponsPage /> },
        ],
      },
      
      // CMS
      {
        path: 'cms',
        children: [
          { path: 'menus', element: <MenusPage /> },
          { path: 'menus/:id', element: <MenuEditorPage /> },
          { path: 'pages', element: <PagesPage /> },
          { path: 'pages/:id', element: <PageEditorPage /> },
          { path: 'product-lists', element: <ProductListsPage /> },
        ],
      },
      
      // IAM
      {
        path: 'iam',
        children: [
          { path: 'users', element: <UsersPage /> },
          { path: 'users/:id', element: <UserDetailPage /> },
          { path: 'roles', element: <RolesPage /> },
          { path: 'roles/:id', element: <RoleDetailPage /> },
          { path: 'permissions', element: <PermissionsPage /> },
          { path: 'menus', element: <AdminMenusPage /> },
          { path: 'audit-logs', element: <AuditLogsPage /> },
        ],
      },
      
      // Settings
      {
        path: 'settings',
        children: [
          { path: 'firms', element: <FirmsPage /> },
          { path: 'firms/:id', element: <FirmDetailPage /> },
          { path: 'platforms', element: <PlatformsPage /> },
          { path: 'integrations', element: <IntegrationsPage /> },
          { path: 'notifications', element: <NotificationSettingsPage /> },
        ],
      },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);
```

### 6.2 Storefront Routes

```typescript
// app/routes.tsx
export const router = createBrowserRouter([
  {
    path: '/',
    element: <MainLayout />,
    children: [
      { index: true, element: <HomePage /> },
      
      // Catalog
      { path: 'category/:slug', element: <CategoryPage /> },
      { path: 'product/:slug', element: <ProductDetailPage /> },
      { path: 'search', element: <SearchResultsPage /> },
      
      // Cart & Checkout
      { path: 'cart', element: <CartPage /> },
      { 
        path: 'checkout', 
        element: <AuthGuard><CheckoutPage /></AuthGuard> 
      },
      { path: 'order-confirmation/:id', element: <OrderConfirmationPage /> },
      
      // Auth
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'forgot-password', element: <ForgotPasswordPage /> },
      { path: 'reset-password', element: <ResetPasswordPage /> },
      
      // Account
      {
        path: 'account',
        element: <AuthGuard><AccountLayout /></AuthGuard>,
        children: [
          { index: true, element: <Navigate to="profile" replace /> },
          { path: 'profile', element: <ProfilePage /> },
          { path: 'addresses', element: <AddressesPage /> },
          { path: 'orders', element: <OrdersPage /> },
          { path: 'orders/:id', element: <OrderDetailPage /> },
          { path: 'wallet', element: <WalletPage /> },
          { path: 'loyalty', element: <LoyaltyPage /> },
          // B2B
          { path: 'quotes', element: <QuotesPage /> },
          { path: 'quotes/:id', element: <QuoteDetailPage /> },
          { path: 'order-templates', element: <OrderTemplatesPage /> },
          { path: 'order-templates/:id', element: <OrderTemplateDetailPage /> },
          { path: 'credit', element: <CreditPage /> },
          { path: 'statement', element: <AccountStatementPage /> },
        ],
      },
      
      // B2B Pages
      { path: 'quick-order', element: <AuthGuard><QuickOrderPage /></AuthGuard> },
      { path: 'request-quote', element: <AuthGuard><QuoteRequestPage /></AuthGuard> },
      
      // CMS Pages
      { path: ':slug', element: <ContentPage /> },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);
```

### 6.3 POS Routes

```typescript
// app/routes.tsx
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/pin-login',
    element: <PinLoginPage />,
  },
  {
    path: '/',
    element: (
      <AuthGuard>
        <SessionGuard>
          <PosLayout />
        </SessionGuard>
      </AuthGuard>
    ),
    children: [
      { index: true, element: <SalePage /> },
      { path: 'return', element: <ReturnPage /> },
      { path: 'customers', element: <CustomerSearchPage /> },
      { path: 'held-orders', element: <HeldOrdersPage /> },
      { path: 'history', element: <OrderHistoryPage /> },
    ],
  },
  {
    path: '/session',
    element: <AuthGuard><SessionLayout /></AuthGuard>,
    children: [
      { path: 'open', element: <OpenSessionPage /> },
      { path: 'close', element: <CloseSessionPage /> },
      { path: 'report', element: <SessionReportPage /> },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);
```

---

## 7. State Yönetimi

### 7.1 Server State (TanStack Query)

API'den gelen veriler için kullanılır.

**Avantajları:**
- Otomatik caching
- Background refetching
- Optimistic updates
- Pagination & infinite scroll desteği
- Devtools

**Konfigürasyon:**
```typescript
// app/providers.tsx
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      gcTime: 1000 * 60 * 30, // 30 minutes
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools />
    </QueryClientProvider>
  );
}
```

### 7.2 Client State (Zustand)

UI state ve kullanıcı tercihleri için kullanılır.

**Admin Panel Stores:**
- `ui-store`: Sidebar, theme, layout
- `preference-store`: Dil, sayfa boyutu tercihleri

**Storefront Stores:**
- `cart-store`: Sepet yönetimi
- `ui-store`: Mobile menu, modals

**POS Stores:**
- `cart-store`: POS sepeti (ürünler, indirimler)
- `session-store`: Kasa oturumu bilgileri
- `customer-store`: Aktif müşteri bilgisi

---

## 8. Form Yönetimi

React Hook Form + Zod kombinasyonu kullanılır.

```typescript
// components/ProductForm/ProductForm.tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const productSchema = z.object({
  code: z.string().min(1, 'Ürün kodu zorunludur'),
  name: z.record(z.string(), z.string()).refine(
    (val) => val.tr?.length > 0,
    { message: 'Türkçe ürün adı zorunludur' }
  ),
  productGroupId: z.string().uuid('Geçerli bir ürün grubu seçiniz'),
  isActive: z.boolean().default(true),
});

type ProductFormData = z.infer<typeof productSchema>;

interface ProductFormProps {
  defaultValues?: Partial<ProductFormData>;
  onSubmit: (data: ProductFormData) => void;
  loading?: boolean;
}

export function ProductForm({ defaultValues, onSubmit, loading }: ProductFormProps) {
  const { t } = useTranslation();
  
  const form = useForm<ProductFormData>({
    resolver: zodResolver(productSchema),
    defaultValues: {
      code: '',
      name: { tr: '', en: '' },
      isActive: true,
      ...defaultValues,
    },
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <FormField
        label={t('products.code')}
        error={form.formState.errors.code?.message}
        {...form.register('code')}
      />
      
      <FormField
        label={t('products.name')}
        error={form.formState.errors.name?.message}
        {...form.register('name.tr')}
      />
      
      <FormSelect
        label={t('products.productGroup')}
        error={form.formState.errors.productGroupId?.message}
        {...form.register('productGroupId')}
        options={productGroups}
      />
      
      <FormSwitch
        label={t('common.active')}
        {...form.register('isActive')}
      />
      
      <Button type="submit" loading={loading}>
        {t('common.save')}
      </Button>
    </form>
  );
}
```

---

## 9. Çok Dilli Destek (i18next)

**Konfigürasyon:**
```typescript
// core/i18n/i18n.ts
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import Backend from 'i18next-http-backend';
import LanguageDetector from 'i18next-browser-languagedetector';

i18n
  .use(Backend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'tr',
    supportedLngs: ['tr', 'en'],
    ns: ['translation'],
    defaultNS: 'translation',
    backend: {
      loadPath: '/locales/{{lng}}/{{ns}}.json',
    },
    interpolation: {
      escapeValue: false,
    },
  });

export default i18n;
```

**Çeviri dosyası örneği:**
```json
// public/locales/tr/translation.json
{
  "common": {
    "save": "Kaydet",
    "cancel": "İptal",
    "delete": "Sil",
    "edit": "Düzenle",
    "search": "Ara",
    "filter": "Filtrele",
    "status": "Durum",
    "active": "Aktif",
    "inactive": "Pasif",
    "actions": "İşlemler",
    "loading": "Yükleniyor...",
    "noData": "Veri bulunamadı",
    "confirmDelete": "Bu kaydı silmek istediğinize emin misiniz?"
  },
  "products": {
    "title": "Ürünler",
    "create": "Yeni Ürün",
    "code": "Ürün Kodu",
    "name": "Ürün Adı",
    "category": "Kategori",
    "variants": "Varyantlar",
    "productGroup": "Ürün Grubu"
  },
  "orders": {
    "title": "Siparişler",
    "orderNumber": "Sipariş No",
    "customer": "Müşteri",
    "total": "Toplam",
    "status": "Durum"
  }
}
```

**Kullanım:**
```typescript
import { useTranslation } from 'react-i18next';

function MyComponent() {
  const { t, i18n } = useTranslation();
  
  return (
    <div>
      <h1>{t('products.title')}</h1>
      <button onClick={() => i18n.changeLanguage('en')}>
        English
      </button>
    </div>
  );
}
```

---

## 10. Real-time İletişim (SignalR)

**Hub bağlantısı:**
```typescript
// core/signalr/fulfillment-hub.ts
import * as signalR from '@microsoft/signalr';

class FulfillmentHub {
  private connection: signalR.HubConnection | null = null;

  async connect(token: string) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_URL}/hubs/fulfillment`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .build();

    await this.connection.start();
  }

  onPickingTaskAssigned(callback: (task: PickingTask) => void) {
    this.connection?.on('PickingTaskAssigned', callback);
  }

  onVoiceCommand(callback: (command: VoiceCommand) => void) {
    this.connection?.on('VoiceCommand', callback);
  }

  onOrderReadyToPack(callback: (orderId: string) => void) {
    this.connection?.on('OrderReadyToPack', callback);
  }

  async disconnect() {
    await this.connection?.stop();
  }
}

export const fulfillmentHub = new FulfillmentHub();
```

**React hook:**
```typescript
// modules/fulfillment/hooks/use-fulfillment-hub.ts
import { useEffect } from 'react';
import { fulfillmentHub } from '@/core/signalr';
import { useAuth } from '@/core/auth';

export function useFulfillmentHub() {
  const { token } = useAuth();

  useEffect(() => {
    if (token) {
      fulfillmentHub.connect(token);
    }

    return () => {
      fulfillmentHub.disconnect();
    };
  }, [token]);

  return fulfillmentHub;
}
```

---

## 11. Testing

**Test stack:**
- Vitest - Test runner
- React Testing Library - Component testing
- MSW - API mocking

**Örnek test:**
```typescript
// modules/catalog/pages/ProductsPage/ProductsPage.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProductsPage } from './ProductsPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false },
  },
});

const wrapper = ({ children }) => (
  <QueryClientProvider client={queryClient}>
    {children}
  </QueryClientProvider>
);

describe('ProductsPage', () => {
  it('renders products table', async () => {
    render(<ProductsPage />, { wrapper });
    
    await waitFor(() => {
      expect(screen.getByText('Ürünler')).toBeInTheDocument();
    });
  });

  it('shows loading state', () => {
    render(<ProductsPage />, { wrapper });
    expect(screen.getByText('Yükleniyor...')).toBeInTheDocument();
  });
});
```

---

## 12. Build & Deployment

**Vite konfigürasyonu:**
```typescript
// vite.config.ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom', 'react-router-dom'],
          query: ['@tanstack/react-query'],
          ui: ['@headlessui/react', '@radix-ui/react-dialog'],
        },
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
});
```

**Environment variables:**
```
# .env.development
VITE_API_URL=http://localhost:5000/api/v1
VITE_SIGNALR_URL=http://localhost:5000

# .env.production
VITE_API_URL=https://api.example.com/api/v1
VITE_SIGNALR_URL=https://api.example.com
```

---

## 13. Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 1.0 | Ocak 2025 | İlk versiyon |
| 1.1 | Ocak 2025 | B2B özellikleri eklendi: hızlı sipariş, sipariş şablonları, teklif sistemi, cari hesap sayfaları |
| 1.2 | Ocak 2025 | POS uygulaması eklendi: satış ekranı, kasa oturumu, barkod/yazıcı entegrasyonu, toptan mağaza desteği |
