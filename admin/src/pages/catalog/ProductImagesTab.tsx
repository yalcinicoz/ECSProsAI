import { useState, useEffect, useMemo, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Upload, Trash2, Star, X, CheckCircle2, Image, Film } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'
import { cn } from '@/lib/utils'

// ── Types ─────────────────────────────────────────────────────────────────────

interface ImageSet { id: string; code: string; name: string; isDefault: boolean; isActive: boolean }

interface ProductImageDto {
  id: string; productId: string; variantId: string | null
  imageSetId: string; imageSetCode: string; fileName: string
  sortOrder: number; isProductCover: boolean; isVariantCover: boolean
  status: string; batchId: string
}

interface ProductVideoDto {
  id: string; productId: string; imageSetId: string; imageSetCode: string
  fileName: string; thumbnailFileName: string | null
  sortOrder: number; status: string; batchId: string
}

interface CatalogSetting { key: string; value: string }
interface VariantAttribute { attributeTypeId: string; attributeValueId: string; attributeValueNameI18n: Record<string, string> }
interface Variant { id: string; sku: string; variantAttributes: VariantAttribute[] }

// A primary-axis value entry: e.g. "Kırmızı" → representative variantId
interface PrimaryAxisValue { valueId: string; label: string; representativeVariantId: string }

interface Props {
  productId: string
  variants: Variant[]
  primaryAxisAttributeTypeId?: string | null
}

// ── Shared Helpers ─────────────────────────────────────────────────────────────

function SetSelector({ imageSets, selectedSetId, onSelect }: {
  imageSets: ImageSet[]; selectedSetId: string; onSelect: (id: string) => void
}) {
  return (
    <div className="flex items-center gap-2">
      <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Set:</span>
      <div className="flex gap-1.5 flex-wrap">
        {imageSets.map(s => (
          <button
            key={s.id}
            onClick={() => onSelect(s.id)}
            className={cn(
              'px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors',
              selectedSetId === s.id
                ? 'text-white border-transparent'
                : 'border-[var(--border)] hover:border-[var(--brand)]'
            )}
            style={selectedSetId === s.id
              ? { background: 'var(--brand)', borderColor: 'var(--brand)' }
              : { color: 'var(--text-m)', background: 'var(--surface2)' }}
          >
            {s.name}
          </button>
        ))}
      </div>
    </div>
  )
}

// ── Images Section ─────────────────────────────────────────────────────────────

function ImagesSection({
  productId, imageSets, publicBaseUrl, primaryAxisValues, variants, primaryAxisAttributeTypeId,
}: {
  productId: string
  imageSets: ImageSet[]
  publicBaseUrl: string
  primaryAxisValues: PrimaryAxisValue[]
  variants: Variant[]
  primaryAxisAttributeTypeId?: string | null
}) {
  const qc = useQueryClient()
  const imageInputRef = useRef<HTMLInputElement>(null)

  const [selectedSetId, setSelectedSetId] = useState('')
  const [selectedVariantId, setSelectedVariantId] = useState('')
  const [activeViewTab, setActiveViewTab] = useState<string>('__genel__')
  const [replaceSet, setReplaceSet] = useState(true)
  const [pendingFiles, setPendingFiles] = useState<File[]>([])
  const [uploadProgress, setUploadProgress] = useState<{ current: number; total: number } | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [uploadDone, setUploadDone] = useState(false)

  useEffect(() => {
    if (imageSets.length > 0 && !selectedSetId) {
      const def = imageSets.find(s => s.isDefault) ?? imageSets[0]
      setSelectedSetId(def.id)
    }
  }, [imageSets, selectedSetId])

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? [])
    e.target.value = ''
    if (!files.length) return
    setPendingFiles(prev => [...prev, ...files])
    setUploadError(null)
    setUploadDone(false)
  }

  // Tüm ürün resimlerini çek — display için set/varyant filtresi yok
  const { data: images = [], isLoading: imagesLoading } = useQuery<ProductImageDto[]>({
    queryKey: ['product-images', productId],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/products/${productId}/images?applyFallback=false`)
      return data.data as ProductImageDto[]
    },
    refetchOnWindowFocus: false,
  })

  // variantId → primary axis label
  const variantIdsByLabel = useMemo(() => {
    const map = new Map<string, string[]>()
    if (primaryAxisAttributeTypeId) {
      for (const v of variants) {
        const ax = v.variantAttributes.find(a => a.attributeTypeId === primaryAxisAttributeTypeId)
        if (ax) {
          const lbl = ax.attributeValueNameI18n['tr']
            ?? ax.attributeValueNameI18n[Object.keys(ax.attributeValueNameI18n)[0]]
            ?? ax.attributeValueId
          map.set(lbl, [...(map.get(lbl) ?? []), v.id])
        }
      }
    }
    return map
  }, [variants, primaryAxisAttributeTypeId])

  // Tabs: Ürün Geneli + primary axis labels in order
  const viewTabs = useMemo(() => {
    const tabs: { key: string; label: string }[] = [{ key: '__genel__', label: 'Ürün Geneli' }]
    for (const pv of primaryAxisValues) {
      tabs.push({ key: pv.label, label: pv.label })
    }
    for (const lbl of variantIdsByLabel.keys()) {
      if (!tabs.some(t => t.key === lbl)) tabs.push({ key: lbl, label: lbl })
    }
    return tabs
  }, [primaryAxisValues, variantIdsByLabel])

  // For active tab: per-set image lists (ALL sets always shown)
  const viewGroups = useMemo(() => {
    return imageSets.map(set => {
      const setImages = images.filter(img => img.imageSetId === set.id)
      let tabImages: ProductImageDto[]
      if (activeViewTab === '__genel__') {
        tabImages = setImages.filter(img => img.variantId == null)
      } else {
        const ids = variantIdsByLabel.get(activeViewTab) ?? []
        tabImages = setImages.filter(img => img.variantId != null && ids.includes(img.variantId))
      }
      return { set, images: tabImages.sort((a, b) => a.sortOrder - b.sortOrder) }
    })
  }, [images, imageSets, activeViewTab, variantIdsByLabel])

  // Per-tab stats (computed across ALL sets, not just active tab)
  const tabStats = useMemo(() => {
    const result = new Map<string, { total: number }>()
    for (const tab of viewTabs) {
      let total = 0
      for (const set of imageSets) {
        const setImages = images.filter(img => img.imageSetId === set.id)
        if (tab.key === '__genel__') {
          total += setImages.filter(img => img.variantId == null).length
        } else {
          const ids = variantIdsByLabel.get(tab.key) ?? []
          total += setImages.filter(img => img.variantId != null && ids.includes(img.variantId)).length
        }
      }
      result.set(tab.key, { total })
    }
    return result
  }, [viewTabs, imageSets, images, variantIdsByLabel])

  const handleUpload = async () => {
    if (!selectedSetId || pendingFiles.length === 0) return
    setUploadError(null); setUploadDone(false)
    setUploadProgress({ current: 0, total: pendingFiles.length })
    try {
      const extensions = pendingFiles.map(f => f.name.split('.').pop()?.toLowerCase() ?? 'jpg')
      const prepareRes = await api.post(`/catalog/products/${productId}/images/prepare`, {
        imageSetId: selectedSetId,
        variantId: selectedVariantId || null,
        fileExtensions: extensions,
        replaceSet,
      })
      const { batchId, images: preparedItems } = prepareRes.data.data as {
        batchId: string; images: { imageId: string; fileName: string }[]
      }

      const token = localStorage.getItem('access_token')
      const successfulIds: string[] = []
      let lastUploadError = ''
      for (let i = 0; i < pendingFiles.length; i++) {
        const formData = new FormData()
        formData.append(preparedItems[i].imageId, pendingFiles[i])
        try {
          const res = await fetch(`/api/catalog/upload/${batchId}`, {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}` },
            body: formData,
          })
          const json = await res.json()
          if (!res.ok) {
            lastUploadError = `HTTP ${res.status}: ${json?.error ?? JSON.stringify(json)}`
            continue
          }
          const perFileResult = (json?.data?.results as { imageId: string; success: boolean; error?: string }[] | undefined)?.[0]
          if (perFileResult?.success === true) {
            successfulIds.push(preparedItems[i].imageId)
          } else {
            lastUploadError = perFileResult?.error ?? `Sunucu başarısız döndü: ${JSON.stringify(json)}`
          }
        } catch (e: any) {
          lastUploadError = e?.message ?? 'Ağ hatası'
        }
        setUploadProgress({ current: i + 1, total: pendingFiles.length })
      }

      if (successfulIds.length === 0) {
        setUploadError(`Dosya yüklenemedi: ${lastUploadError}`)
        return
      }

      const setVariantImages = images.filter(img =>
        img.imageSetId === selectedSetId &&
        (selectedVariantId ? img.variantId === selectedVariantId : img.variantId == null))
      const existingCovers = setVariantImages.filter(img => img.isProductCover)
      const confirmedImages = preparedItems
        .filter(x => successfulIds.includes(x.imageId))
        .map((x, i) => ({
          imageId: x.imageId,
          sortOrder: setVariantImages.length + i + 1,
          isProductCover: i === 0 && existingCovers.length === 0,
          isVariantCover: i === 0 && !!selectedVariantId && setVariantImages.filter(img => img.isVariantCover).length === 0,
        }))

      await api.post(`/catalog/products/${productId}/images/confirm`, { batchId, replaceSet, confirmedImages })
      setPendingFiles([]); setUploadDone(true)
      qc.invalidateQueries({ queryKey: ['product-images', productId] })
    } catch (e: any) {
      setUploadError(e?.response?.data?.error ?? 'Yükleme sırasında hata oluştu.')
    } finally {
      setUploadProgress(null)
    }
  }

  const archiveMutation = useMutation({
    mutationFn: (imageId: string) => api.delete(`/catalog/products/${productId}/images/${imageId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['product-images', productId] }),
  })

  const toggleCover = (img: ProductImageDto) => {
    api.patch(`/catalog/products/${productId}/images/${img.id}`, {
      sortOrder: img.sortOrder, isProductCover: !img.isProductCover, isVariantCover: img.isVariantCover,
    }).then(() => qc.invalidateQueries({ queryKey: ['product-images', productId] }))
  }

  const isUploading = uploadProgress !== null

  return (
    <div className="vc flex flex-col gap-5">
      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <SetSelector imageSets={imageSets} selectedSetId={selectedSetId} onSelect={setSelectedSetId} />

        {/* Variant / Primary-axis selector */}
        {primaryAxisValues.length > 0 ? (
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Ana Varyant:</span>
            <div className="flex gap-1.5 flex-wrap">
              <button
                onClick={() => setSelectedVariantId('')}
                className={cn('px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors',
                  selectedVariantId === '' ? 'text-white border-transparent' : 'border-[var(--border)] hover:border-[var(--brand)]')}
                style={selectedVariantId === '' ? { background: 'var(--brand)' } : { color: 'var(--text-m)', background: 'var(--surface2)' }}
              >
                Ürün Geneli
              </button>
              {primaryAxisValues.map(pv => (
                <button
                  key={pv.valueId}
                  onClick={() => setSelectedVariantId(pv.representativeVariantId)}
                  className={cn('px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors',
                    selectedVariantId === pv.representativeVariantId ? 'text-white border-transparent' : 'border-[var(--border)] hover:border-[var(--brand)]')}
                  style={selectedVariantId === pv.representativeVariantId ? { background: 'var(--brand)' } : { color: 'var(--text-m)', background: 'var(--surface2)' }}
                >
                  {pv.label}
                </button>
              ))}
            </div>
          </div>
        ) : variants.length > 0 && (
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Varyant:</span>
            <select className="sel text-xs" value={selectedVariantId} onChange={e => setSelectedVariantId(e.target.value)}
              style={{ padding: '6px 10px', minWidth: 160 }}>
              <option value="">Ürün Geneli</option>
              {variants.map(v => <option key={v.id} value={v.id}>{v.sku}</option>)}
            </select>
          </div>
        )}
      </div>

      {/* Upload zone */}
      <div className="card" style={{ maxWidth: 680 }}>
        <h2 className="text-sm font-bold mb-3" style={{ color: 'var(--text)' }}>Resim Yükle</h2>
        <label
          className="relative rounded-xl border-2 border-dashed flex flex-col items-center justify-center py-8 gap-2 cursor-pointer transition-colors hover:border-[var(--brand)]"
          style={{ borderColor: 'var(--border)' }}
        >
          <Upload size={20} style={{ color: 'var(--text-s)', pointerEvents: 'none' }} />
          <p className="text-sm font-medium" style={{ color: 'var(--text-m)', pointerEvents: 'none' }}>Tıkla veya sürükle &amp; bırak</p>
          <p className="text-xs" style={{ color: 'var(--text-s)', pointerEvents: 'none' }}>JPG, PNG, WEBP · Çoklu seçim desteklenir</p>
          <input
            ref={imageInputRef}
            type="file"
            multiple
            accept="image/*"
            onChange={handleFileChange}
            style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: 0, cursor: 'pointer' }}
          />
        </label>

        {pendingFiles.length > 0 && (
          <div className="mt-3 grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 gap-2">
            {pendingFiles.map((f, i) => {
              const url = URL.createObjectURL(f)
              return (
                <div key={i} className="relative group rounded-lg overflow-hidden aspect-square"
                  style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
                  <img src={url} alt={f.name} className="w-full h-full object-cover"
                    onLoad={() => URL.revokeObjectURL(url)} />
                  <div className="absolute inset-0 flex flex-col items-center justify-center opacity-0 group-hover:opacity-100 transition-all gap-1"
                    style={{ background: 'rgba(0,0,0,.55)' }}>
                    <p className="text-[10px] text-white px-1 text-center leading-tight line-clamp-2">{f.name}</p>
                    <p className="text-[10px]" style={{ color: 'rgba(255,255,255,.6)' }}>{(f.size / 1024).toFixed(0)} KB</p>
                    <button onClick={() => setPendingFiles(p => p.filter((_, j) => j !== i))}
                      className="mt-1 w-6 h-6 rounded-full bg-red-500 flex items-center justify-center text-white hover:bg-red-600 transition-colors">
                      <X size={11} />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
        )}

        <label className="flex items-center gap-2 mt-3 cursor-pointer select-none">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={replaceSet} onChange={e => setReplaceSet(e.target.checked)} />
          <span className="text-xs font-medium" style={{ color: 'var(--text-m)' }}>Mevcut resimleri arşivle (bu set + varyant için)</span>
        </label>

        {isUploading && (
          <div className="mt-3">
            <div className="flex items-center justify-between text-xs mb-1" style={{ color: 'var(--text-s)' }}>
              <span>Yükleniyor...</span>
              <span>{uploadProgress!.current} / {uploadProgress!.total}</span>
            </div>
            <div className="w-full h-1.5 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
              <div className="h-full rounded-full transition-all"
                style={{ width: `${(uploadProgress!.current / uploadProgress!.total) * 100}%`, background: 'var(--brand)' }} />
            </div>
          </div>
        )}
        {uploadDone && <p className="flex items-center gap-1.5 text-xs mt-3" style={{ color: 'var(--brand)' }}><CheckCircle2 size={13} /> Resimler başarıyla yüklendi.</p>}
        {uploadError && <p className="text-xs mt-3" style={{ color: '#ef4444' }}>{uploadError}</p>}

        <div className="flex justify-end mt-4">
          <Button size="sm" onClick={handleUpload} loading={isUploading} disabled={pendingFiles.length === 0 || !selectedSetId}>
            <Upload size={13} className="mr-1.5" /> Yükle ({pendingFiles.length})
          </Button>
        </div>
      </div>

      {/* Active images — tabbed by variant, per-set sections always shown */}
      <div style={{ maxWidth: 680 }}>
        <h2 className="text-sm font-bold mb-3" style={{ color: 'var(--text)' }}>
          Mevcut Resimler
          {images.length > 0 && <span className="ml-2 text-xs font-normal" style={{ color: 'var(--text-s)' }}>{images.length} adet</span>}
        </h2>

        {/* Variant tabs */}
        {viewTabs.length > 1 && (
          <div className="flex gap-1.5 flex-wrap mb-4">
            {viewTabs.map(tab => {
              const stats = tabStats.get(tab.key)
              const isActive = activeViewTab === tab.key
              return (
                <button
                  key={tab.key}
                  onClick={() => setActiveViewTab(tab.key)}
                  className={cn(
                    'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors',
                    isActive
                      ? 'text-white border-transparent'
                      : 'border-[var(--border)] hover:border-[var(--brand)]'
                  )}
                  style={isActive
                    ? { background: 'var(--brand)', borderColor: 'var(--brand)' }
                    : { color: 'var(--text-m)', background: 'var(--surface2)' }}
                >
                  {tab.label}
                  {/* Image count */}
                  {stats && (
                    <span
                      className={cn(
                        'px-1.5 py-0.5 rounded-full text-[10px] font-bold leading-none',
                        isActive
                          ? 'bg-white/25 text-white'
                          : 'bg-[var(--surface)] text-[var(--text-s)]'
                      )}
                    >
                      {stats.total}
                    </span>
                  )}
                  {/* Orange dot — no images at all */}
                  {stats?.total === 0 && (
                    <span
                      className="w-1.5 h-1.5 rounded-full bg-orange-400 flex-shrink-0"
                      title="Resim yüklenmemiş"
                    />
                  )}
                </button>
              )
            })}
          </div>
        )}

        {imagesLoading ? <PageSpinner /> : (
          <div className="flex flex-col gap-5">
            {viewGroups.map(({ set, images: setTabImages }) => (
              <div key={set.id}>
                {/* Set separator — always shown */}
                <div className="flex items-center gap-2 mb-3">
                  <span className="text-[11px] font-semibold whitespace-nowrap" style={{ color: 'var(--text-m)' }}>
                    {set.name}
                  </span>
                  <div className="flex-1 h-px" style={{ background: 'var(--border)' }} />
                </div>

                {setTabImages.length === 0 ? (
                  <div className="rounded-xl flex items-center justify-center gap-2 py-6 mb-1"
                    style={{ background: 'var(--surface2)', color: 'var(--text-s)', border: '1px dashed var(--border)' }}>
                    <Image size={13} />
                    <p className="text-xs">Resim yok</p>
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
                    {setTabImages.map(img => {
                      const url = publicBaseUrl
                        ? `${publicBaseUrl}/${img.fileName}`
                        : `/api/catalog/images/file/${img.fileName}`
                      return (
                        <div key={img.id} className="relative group rounded-xl overflow-hidden aspect-square"
                          style={{ background: 'var(--surface2)', border: img.isProductCover ? '2px solid var(--brand)' : '1px solid var(--border)' }}>
                          <img src={url} alt={img.fileName} className="w-full h-full object-cover"
                            onError={e => { (e.target as HTMLImageElement).style.display = 'none' }} />
                          {img.isProductCover && (
                            <div className="absolute top-2 left-2">
                              <span className="text-[10px] font-bold px-1.5 py-0.5 rounded-full text-white" style={{ background: 'var(--brand)' }}>Ana</span>
                            </div>
                          )}
                          <div className="absolute inset-0 flex items-center justify-center gap-2 opacity-0 group-hover:opacity-100 transition-all"
                            style={{ background: 'rgba(0,0,0,.45)' }}>
                            <button title={img.isProductCover ? 'Ana görsel' : 'Ana görsel yap'} onClick={() => toggleCover(img)}
                              className="w-8 h-8 rounded-lg bg-white flex items-center justify-center transition-colors hover:bg-yellow-50"
                              style={{ color: img.isProductCover ? '#f59e0b' : '#6b7280' }}>
                              <Star size={13} fill={img.isProductCover ? '#f59e0b' : 'none'} />
                            </button>
                            <button title="Arşivle" onClick={() => archiveMutation.mutate(img.id)}
                              className="w-8 h-8 rounded-lg bg-white flex items-center justify-center text-red-400 hover:bg-red-50 transition-colors">
                              <Trash2 size={13} />
                            </button>
                          </div>
                        </div>
                      )
                    })}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
        {images.length > 0 && (
          <p className="text-[11px] mt-3" style={{ color: 'var(--text-s)' }}>
            Yıldız ikonu ile ana görsel atayabilirsiniz. Çöp kutusu ile resmi arşive taşıyabilirsiniz.
          </p>
        )}
      </div>
    </div>
  )
}

// ── Videos Section ─────────────────────────────────────────────────────────────

function VideosSection({ productId, imageSets, publicBaseUrl }: {
  productId: string; imageSets: ImageSet[]; publicBaseUrl: string
}) {
  const qc = useQueryClient()
  const videoInputRef = useRef<HTMLInputElement>(null)

  const [selectedSetId, setSelectedSetId] = useState('')
  const [replaceSet, setReplaceSet] = useState(false)
  const [pendingFiles, setPendingFiles] = useState<File[]>([])
  const [uploadProgress, setUploadProgress] = useState<{ current: number; total: number } | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [uploadDone, setUploadDone] = useState(false)

  useEffect(() => {
    if (imageSets.length > 0 && !selectedSetId) {
      const def = imageSets.find(s => s.isDefault) ?? imageSets[0]
      setSelectedSetId(def.id)
    }
  }, [imageSets, selectedSetId])

  const { data: videos = [], isLoading: videosLoading } = useQuery<ProductVideoDto[]>({
    queryKey: ['product-videos', productId, selectedSetId],
    queryFn: async () => {
      if (!selectedSetId) return []
      const { data } = await api.get(`/catalog/products/${productId}/videos?imageSetId=${selectedSetId}`)
      return data.data as ProductVideoDto[]
    },
    enabled: !!selectedSetId,
    refetchOnWindowFocus: false,
  })

  const handleVideoFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? [])
    e.target.value = ''
    if (!files.length) return
    setPendingFiles(prev => [...prev, ...files])
    setUploadError(null)
    setUploadDone(false)
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    const files = Array.from(e.dataTransfer.files).filter(f => f.type.startsWith('video/'))
    setPendingFiles(prev => [...prev, ...files])
    setUploadError(null); setUploadDone(false)
  }

  const handleUpload = async () => {
    if (!selectedSetId || pendingFiles.length === 0) return
    setUploadError(null); setUploadDone(false)
    setUploadProgress({ current: 0, total: pendingFiles.length })
    try {
      const extensions = pendingFiles.map(f => f.name.split('.').pop()?.toLowerCase() ?? 'mp4')
      const prepareRes = await api.post(`/catalog/products/${productId}/videos/prepare`, {
        imageSetId: selectedSetId, fileExtensions: extensions, replaceSet,
      })
      const { batchId, videos: preparedItems } = prepareRes.data.data as {
        batchId: string; videos: { videoId: string; fileName: string }[]
      }

      const token = localStorage.getItem('access_token')
      const successfulIds: string[] = []
      for (let i = 0; i < pendingFiles.length; i++) {
        const formData = new FormData()
        formData.append(preparedItems[i].videoId, pendingFiles[i])
        try {
          const res = await fetch(`/api/catalog/upload/videos/${batchId}`, {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}` },
            body: formData,
          })
          if (res.ok) successfulIds.push(preparedItems[i].videoId)
        } catch { /* skip */ }
        setUploadProgress({ current: i + 1, total: pendingFiles.length })
      }

      const confirmedVideos = preparedItems
        .filter(x => successfulIds.includes(x.videoId))
        .map((x, i) => ({ videoId: x.videoId, sortOrder: videos.length + i + 1, thumbnailFileName: null }))

      await api.post(`/catalog/products/${productId}/videos/confirm`, {
        batchId, replaceSet, confirmedVideos,
      })
      setPendingFiles([]); setUploadDone(true)
      qc.invalidateQueries({ queryKey: ['product-videos', productId] })
    } catch (e: any) {
      setUploadError(e?.response?.data?.error ?? 'Yükleme sırasında hata oluştu.')
    } finally {
      setUploadProgress(null)
    }
  }

  const archiveMutation = useMutation({
    mutationFn: (videoId: string) => api.delete(`/catalog/products/${productId}/videos/${videoId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['product-videos', productId] }),
  })

  const isUploading = uploadProgress !== null

  // Determine video file extension to show icon or native player
  function isPlayable(fileName: string) {
    const ext = fileName.split('.').pop()?.toLowerCase() ?? ''
    return ['mp4', 'webm', 'ogg'].includes(ext)
  }

  return (
    <div className="vc flex flex-col gap-5">
      {/* Set selector */}
      <div className="flex flex-wrap items-center gap-3">
        <SetSelector imageSets={imageSets} selectedSetId={selectedSetId} onSelect={setSelectedSetId} />
      </div>

      {/* Upload zone */}
      <div className="card" style={{ maxWidth: 680 }}>
        <h2 className="text-sm font-bold mb-3" style={{ color: 'var(--text)' }}>Video Yükle</h2>
        <label
          onDragOver={e => e.preventDefault()}
          onDrop={e => { e.preventDefault(); handleDrop(e) }}
          className="relative rounded-xl border-2 border-dashed flex flex-col items-center justify-center py-8 gap-2 cursor-pointer transition-colors hover:border-[var(--brand)] hover:bg-[var(--brand-bg)]"
          style={{ borderColor: 'var(--border)' }}>
          <Film size={20} style={{ color: 'var(--text-s)', position: 'relative', zIndex: 0, pointerEvents: 'none' }} />
          <p className="text-sm font-medium" style={{ color: 'var(--text-m)', position: 'relative', zIndex: 0, pointerEvents: 'none' }}>Tıkla veya sürükle &amp; bırak</p>
          <p className="text-xs" style={{ color: 'var(--text-s)', position: 'relative', zIndex: 0, pointerEvents: 'none' }}>MP4, WEBM, MOV · Çoklu seçim desteklenir</p>
          <input
            ref={videoInputRef}
            type="file"
            multiple
            accept="video/*"
            onChange={handleVideoFileChange}
            style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: 0, cursor: 'pointer', zIndex: 1 }}
          />
        </label>

        {pendingFiles.length > 0 && (
          <div className="mt-3 space-y-1.5">
            {pendingFiles.map((f, i) => (
              <div key={i} className="flex items-center gap-2 px-3 py-2 rounded-lg text-xs"
                style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
                <Film size={12} style={{ color: 'var(--text-s)', flexShrink: 0 }} />
                <span className="flex-1 truncate">{f.name}</span>
                <span style={{ color: 'var(--text-s)' }}>{(f.size / 1024 / 1024).toFixed(1)} MB</span>
                <button onClick={() => setPendingFiles(p => p.filter((_, j) => j !== i))} className="p-0.5 rounded hover:text-red-400 transition-colors"><X size={12} /></button>
              </div>
            ))}
          </div>
        )}

        <label className="flex items-center gap-2 mt-3 cursor-pointer select-none">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={replaceSet} onChange={e => setReplaceSet(e.target.checked)} />
          <span className="text-xs font-medium" style={{ color: 'var(--text-m)' }}>Mevcut videoları arşivle (bu set için)</span>
        </label>

        {isUploading && (
          <div className="mt-3">
            <div className="flex items-center justify-between text-xs mb-1" style={{ color: 'var(--text-s)' }}>
              <span>Yükleniyor...</span>
              <span>{uploadProgress!.current} / {uploadProgress!.total}</span>
            </div>
            <div className="w-full h-1.5 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
              <div className="h-full rounded-full transition-all"
                style={{ width: `${(uploadProgress!.current / uploadProgress!.total) * 100}%`, background: 'var(--brand)' }} />
            </div>
          </div>
        )}
        {uploadDone && <p className="flex items-center gap-1.5 text-xs mt-3" style={{ color: 'var(--brand)' }}><CheckCircle2 size={13} /> Videolar başarıyla yüklendi.</p>}
        {uploadError && <p className="text-xs mt-3" style={{ color: '#ef4444' }}>{uploadError}</p>}

        <div className="flex justify-end mt-4">
          <Button size="sm" onClick={handleUpload} loading={isUploading} disabled={pendingFiles.length === 0 || !selectedSetId}>
            <Upload size={13} className="mr-1.5" /> Yükle ({pendingFiles.length})
          </Button>
        </div>
      </div>

      {/* Active videos list */}
      <div style={{ maxWidth: 680 }}>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>
            Mevcut Videolar
            {videos.length > 0 && <span className="ml-2 text-xs font-normal" style={{ color: 'var(--text-s)' }}>{videos.length} adet</span>}
          </h2>
        </div>

        {videosLoading ? <PageSpinner /> : videos.length === 0 ? (
          <div className="rounded-xl flex flex-col items-center justify-center py-10 gap-2" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
            <Film size={24} />
            <p className="text-sm">Bu set için henüz video yüklenmemiş.</p>
          </div>
        ) : (
          <div className="space-y-2">
            {videos.sort((a, b) => a.sortOrder - b.sortOrder).map(vid => {
              const url = publicBaseUrl
                ? `${publicBaseUrl}/${vid.fileName}`
                : `/api/catalog/images/file/${vid.fileName}`
              return (
                <div key={vid.id} className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)', background: 'var(--surface2)' }}>
                  {isPlayable(vid.fileName) ? (
                    <video
                      src={url}
                      controls
                      preload="metadata"
                      className="w-full"
                      style={{ maxHeight: 240, background: '#000' }}
                    />
                  ) : (
                    <div className="flex items-center justify-center py-8" style={{ background: '#111' }}>
                      <Film size={32} style={{ color: '#555' }} />
                    </div>
                  )}
                  <div className="flex items-center justify-between px-3 py-2.5">
                    <div className="flex items-center gap-2 min-w-0">
                      <Film size={13} style={{ color: 'var(--text-s)', flexShrink: 0 }} />
                      <span className="text-xs truncate font-mono" style={{ color: 'var(--text-m)' }}>{vid.fileName}</span>
                    </div>
                    <button
                      title="Arşivle"
                      onClick={() => archiveMutation.mutate(vid.id)}
                      disabled={archiveMutation.isPending}
                      className="ml-3 flex-shrink-0 w-7 h-7 rounded-lg flex items-center justify-center text-red-400 hover:bg-red-50 transition-colors"
                    >
                      <Trash2 size={13} />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}

// ── ProductImagesTab ───────────────────────────────────────────────────────────

type MediaTab = 'images' | 'videos'

export function ProductImagesTab({ productId, variants, primaryAxisAttributeTypeId }: Props) {
  const [mediaTab, setMediaTab] = useState<MediaTab>('images')

  // Build primary axis values list (deduplicated, keep first occurrence per value)
  const primaryAxisValues = useMemo<PrimaryAxisValue[]>(() => {
    if (!primaryAxisAttributeTypeId || variants.length === 0) return []
    const seen = new Map<string, PrimaryAxisValue>()
    for (const v of variants) {
      const axisAttr = v.variantAttributes.find(a => a.attributeTypeId === primaryAxisAttributeTypeId)
      if (!axisAttr) continue
      if (!seen.has(axisAttr.attributeValueId)) {
        const label = axisAttr.attributeValueNameI18n['tr']
          ?? axisAttr.attributeValueNameI18n[Object.keys(axisAttr.attributeValueNameI18n)[0]]
          ?? axisAttr.attributeValueId
        seen.set(axisAttr.attributeValueId, { valueId: axisAttr.attributeValueId, label, representativeVariantId: v.id })
      }
    }
    return Array.from(seen.values())
  }, [variants, primaryAxisAttributeTypeId])

  // Shared queries
  const { data: imageSets = [], isLoading: setsLoading } = useQuery<ImageSet[]>({
    queryKey: ['image-sets', true],
    queryFn: async () => {
      const { data } = await api.get('/catalog/image-sets?activeOnly=true')
      return data.data as ImageSet[]
    },
  })

  const { data: settings = [] } = useQuery<CatalogSetting[]>({
    queryKey: ['catalog-settings'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/settings')
      return data.data as CatalogSetting[]
    },
  })

  const publicBaseUrl = (settings.find(s => s.key === 'ImageServer.PublicBaseUrl')?.value ?? '').replace(/\/$/, '')

  if (setsLoading) return <PageSpinner />

  if (imageSets.length === 0) {
    return (
      <div className="vc flex flex-col items-center justify-center gap-3 py-16" style={{ color: 'var(--text-s)' }}>
        <p className="text-sm">Henüz resim seti tanımlanmamış.</p>
        <p className="text-xs">
          <a href="/admin/catalog/settings" className="underline" style={{ color: 'var(--brand)' }}>Katalog Ayarları</a>{' '}
          sayfasından resim seti oluşturun.
        </p>
      </div>
    )
  }

  return (
    <div className="flex flex-col">
      {/* Media sub-tabs */}
      <div className="flex items-center gap-1 px-5 pt-4 pb-0 border-b" style={{ borderColor: 'var(--border)' }}>
        {([
          { key: 'images' as MediaTab, label: 'Resimler', icon: Image },
          { key: 'videos' as MediaTab, label: 'Videolar', icon: Film },
        ]).map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            onClick={() => setMediaTab(key)}
            className={cn(
              'flex items-center gap-1.5 px-4 py-2.5 text-sm font-semibold border-b-2 transition-colors -mb-px',
              mediaTab === key
                ? 'border-[var(--brand)]'
                : 'border-transparent hover:border-[var(--border)]'
            )}
            style={{ color: mediaTab === key ? 'var(--brand)' : 'var(--text-s)' }}
          >
            <Icon size={14} />
            {label}
          </button>
        ))}
      </div>

      {mediaTab === 'images' && (
        <ImagesSection
          productId={productId}
          imageSets={imageSets}
          publicBaseUrl={publicBaseUrl}
          primaryAxisValues={primaryAxisValues}
          variants={variants}
          primaryAxisAttributeTypeId={primaryAxisAttributeTypeId}
        />
      )}

      {mediaTab === 'videos' && (
        <VideosSection
          productId={productId}
          imageSets={imageSets}
          publicBaseUrl={publicBaseUrl}
        />
      )}
    </div>
  )
}
