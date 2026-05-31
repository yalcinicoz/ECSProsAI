import { useState, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  FolderOpen, Upload, CheckCircle2, AlertCircle, X, Star,
  ChevronRight, Image as ImageIcon, Loader2, FolderSymlink,
} from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'

// ── Types ──────────────────────────────────────────────────────────────────────

interface ImageSet { id: string; code: string; name: string; isDefault: boolean; isActive: boolean }

interface FileEntry {
  file: File
  fileHandle: FileSystemFileHandle | null   // null when loaded via <input>
  order: number
  previewUrl: string
}

interface BarcodeGroup {
  barcode: string
  files: FileEntry[]
}

interface VariantInfo {
  productId: string
  variantId: string
  sku: string
  productCode: string
  productNameI18n: Record<string, string>
}

type LookupState = 'idle' | 'loading' | 'found' | 'notfound'

interface GroupState {
  barcodeGroup: BarcodeGroup
  lookup: LookupState
  variantInfo: VariantInfo | null
  uploadStatus: 'idle' | 'uploading' | 'done' | 'error'
  uploadError?: string
  movedCount?: number
}

// ── Filename parsing ───────────────────────────────────────────────────────────

function parseFileName(name: string): { barcode: string; order: number } {
  const withoutExt = name.replace(/\.[^/.]+$/, '')
  const lastUnderscore = withoutExt.lastIndexOf('_')
  if (lastUnderscore === -1) return { barcode: withoutExt, order: 0 }
  const suffix = withoutExt.slice(lastUnderscore + 1)
  const num = parseInt(suffix, 10)
  if (isNaN(num)) return { barcode: withoutExt, order: 0 }
  return { barcode: withoutExt.slice(0, lastUnderscore), order: num }
}

// ── File System Access API helpers ────────────────────────────────────────────

const fsSupportd = typeof window !== 'undefined' && 'showDirectoryPicker' in window

/** Files from a directory (top-level images only, skip "yuklenenler") */
async function readDirImages(
  dirHandle: FileSystemDirectoryHandle,
): Promise<{ file: File; fileHandle: FileSystemFileHandle }[]> {
  const result: { file: File; fileHandle: FileSystemFileHandle }[] = []
  for await (const [name, handle] of (dirHandle as any).entries()) {
    if (handle.kind !== 'file') continue
    if (name === 'yuklenenler') continue
    const lower = name.toLowerCase()
    if (!lower.match(/\.(jpe?g|png|webp|gif|bmp|tiff?)$/)) continue
    const file = await (handle as FileSystemFileHandle).getFile()
    result.push({ file, fileHandle: handle as FileSystemFileHandle })
  }
  return result
}

/** Move file to yuklenenler/{serverFileName}, delete original */
async function moveToUploaded(
  dirHandle: FileSystemDirectoryHandle,
  _fileHandle: FileSystemFileHandle,
  originalFile: File,
  serverFileName: string,
): Promise<void> {
  const subDir = await (dirHandle as any).getDirectoryHandle('yuklenenler', { create: true })
  const newHandle: FileSystemFileHandle = await subDir.getFileHandle(serverFileName, { create: true })
  const writable = await (newHandle as any).createWritable()
  await writable.write(originalFile)
  await writable.close()
  await (dirHandle as any).removeEntry(originalFile.name)
}

// ── BarcodeGroupCard ───────────────────────────────────────────────────────────

function BarcodeGroupCard({
  state,
  onRemoveFile,
}: {
  state: GroupState
  onRemoveFile: (barcode: string, idx: number) => void
}) {
  const { barcodeGroup, lookup, variantInfo, uploadStatus, uploadError, movedCount } = state
  const productName = variantInfo?.productNameI18n?.['tr']
    ?? variantInfo?.productNameI18n?.[Object.keys(variantInfo.productNameI18n)[0]]
    ?? ''

  return (
    <div
      className="card"
      style={{
        borderLeft: `3px solid ${
          lookup === 'notfound' ? '#f97316'
          : lookup === 'found'  ? 'var(--brand)'
          : 'var(--border)'
        }`,
      }}
    >
      {/* Header */}
      <div className="flex items-center gap-3 mb-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-xs font-mono font-bold" style={{ color: 'var(--text)' }}>
              {barcodeGroup.barcode}
            </span>
            {lookup === 'loading' && <Loader2 size={13} className="animate-spin" style={{ color: 'var(--text-s)' }} />}
            {lookup === 'found' && <CheckCircle2 size={13} style={{ color: 'var(--brand)' }} />}
            {lookup === 'notfound' && (
              <span className="flex items-center gap-1 text-[11px] font-semibold text-orange-500">
                <AlertCircle size={12} /> Bulunamadı
              </span>
            )}
          </div>
          {variantInfo && (
            <div className="flex items-center gap-1.5 mt-0.5">
              <Link
                to={`/catalog/products/${variantInfo.productCode}`}
                className="text-[11px] hover:underline"
                style={{ color: 'var(--brand)' }}
                target="_blank"
              >
                {productName || variantInfo.productCode}
              </Link>
              <ChevronRight size={10} style={{ color: 'var(--text-s)' }} />
              <span className="text-[11px]" style={{ color: 'var(--text-s)' }}>{variantInfo.sku}</span>
            </div>
          )}
        </div>

        <span className="text-[11px] px-2 py-0.5 rounded-full font-semibold"
          style={{ background: 'var(--surface2)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>
          {barcodeGroup.files.length} resim
        </span>

        {uploadStatus === 'uploading' && (
          <span className="flex items-center gap-1 text-[11px]" style={{ color: 'var(--text-s)' }}>
            <Loader2 size={12} className="animate-spin" /> Yükleniyor
          </span>
        )}
        {uploadStatus === 'done' && (
          <span className="flex items-center gap-1 text-[11px] font-semibold" style={{ color: 'var(--brand)' }}>
            <CheckCircle2 size={13} /> Yüklendi
            {movedCount !== undefined && movedCount > 0 && (
              <span className="flex items-center gap-0.5 ml-1" style={{ color: 'var(--text-s)' }}>
                <FolderSymlink size={11} /> {movedCount} taşındı
              </span>
            )}
          </span>
        )}
        {uploadStatus === 'error' && (
          <span className="flex items-center gap-1 text-[11px] text-red-500">
            <AlertCircle size={13} /> Hata
          </span>
        )}
      </div>

      {uploadError && (
        <p className="text-[11px] text-red-500 mb-2">{uploadError}</p>
      )}

      {/* Thumbnails */}
      <div className="flex gap-2 flex-wrap">
        {barcodeGroup.files.map((fe, idx) => (
          <div
            key={idx}
            className="relative group rounded-lg overflow-hidden flex-shrink-0"
            style={{
              width: 72, height: 72,
              background: 'var(--surface2)',
              border: idx === 0 ? '2px solid var(--brand)' : '1px solid var(--border)',
            }}
          >
            <img src={fe.previewUrl} alt={fe.file.name} className="w-full h-full object-cover" />
            {idx === 0 && (
              <div className="absolute top-1 left-1">
                <span className="text-[9px] font-bold px-1 py-0.5 rounded-full text-white flex items-center gap-0.5"
                  style={{ background: 'var(--brand)' }}>
                  <Star size={8} fill="white" /> Kapak
                </span>
              </div>
            )}
            <div className="absolute bottom-1 right-1">
              <span className="text-[9px] font-bold w-4 h-4 rounded-full bg-black/50 text-white flex items-center justify-center">
                {fe.order}
              </span>
            </div>
            {uploadStatus === 'idle' && (
              <button
                onClick={() => onRemoveFile(barcodeGroup.barcode, idx)}
                className="absolute top-1 right-1 w-4 h-4 rounded-full bg-red-500 text-white items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity hidden group-hover:flex"
              >
                <X size={9} />
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}

// ── Main Page ──────────────────────────────────────────────────────────────────

export function BulkImageUploadPage() {
  const [groups, setGroups] = useState<GroupState[]>([])
  const [dirHandle, setDirHandle] = useState<FileSystemDirectoryHandle | null>(null)
  const [selectedSetId, setSelectedSetId] = useState('')
  const [replaceSet, setReplaceSet] = useState(true)
  const [isUploading, setIsUploading] = useState(false)
  const [uploadSummary, setUploadSummary] = useState<{ ok: number; fail: number; moved: number } | null>(null)

  const { data: imageSets = [] } = useQuery<ImageSet[]>({
    queryKey: ['image-sets'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/image-sets')
      const sets = data.data as ImageSet[]
      if (!selectedSetId && sets.length > 0) {
        const def = sets.find((s: ImageSet) => s.isDefault) ?? sets[0]
        setSelectedSetId(def.id)
      }
      return sets
    },
  })

  // ── Process files list → groups + barcode lookup ──────────────────────────

  const processFiles = async (
    entries: { file: File; fileHandle: FileSystemFileHandle | null }[],
  ) => {
    setUploadSummary(null)

    const imageEntries = entries.filter(e => e.file.type.startsWith('image/'))
    const buckets = new Map<string, FileEntry[]>()
    for (const { file, fileHandle } of imageEntries) {
      const { barcode, order } = parseFileName(file.name)
      const entry: FileEntry = { file, fileHandle, order, previewUrl: URL.createObjectURL(file) }
      buckets.set(barcode, [...(buckets.get(barcode) ?? []), entry])
    }

    const barcodeGroups: BarcodeGroup[] = []
    for (const [barcode, files] of buckets) {
      barcodeGroups.push({ barcode, files: files.sort((a, b) => a.order - b.order) })
    }
    barcodeGroups.sort((a, b) => a.barcode.localeCompare(b.barcode))

    const initial: GroupState[] = barcodeGroups.map(g => ({
      barcodeGroup: g, lookup: 'loading', variantInfo: null, uploadStatus: 'idle',
    }))
    setGroups(initial)

    // Parallel barcode lookups (batch 10)
    const BATCH = 10
    for (let i = 0; i < barcodeGroups.length; i += BATCH) {
      const batch = barcodeGroups.slice(i, i + BATCH)
      await Promise.all(batch.map(async (g) => {
        try {
          const { data } = await api.get(`/catalog/variants/by-barcode/${encodeURIComponent(g.barcode)}`)
          setGroups(prev => prev.map(s =>
            s.barcodeGroup.barcode === g.barcode
              ? { ...s, lookup: 'found', variantInfo: data.data as VariantInfo }
              : s
          ))
        } catch {
          setGroups(prev => prev.map(s =>
            s.barcodeGroup.barcode === g.barcode ? { ...s, lookup: 'notfound' } : s
          ))
        }
      }))
    }
  }

  // ── Folder picker (File System Access API) ────────────────────────────────

  const handlePickFolder = async () => {
    try {
      const handle = await (window as any).showDirectoryPicker({ mode: 'readwrite' })
      setDirHandle(handle)
      setGroups([])
      const entries = await readDirImages(handle)
      await processFiles(entries)
    } catch (e: any) {
      if (e?.name !== 'AbortError') console.error(e)
    }
  }

  // ── Fallback file input (no FS API) ──────────────────────────────────────

  const handleFallbackChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? [])
    e.target.value = ''
    if (!files.length) return
    setDirHandle(null)
    await processFiles(files.map(f => ({ file: f, fileHandle: null })))
  }

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault()
    const items = Array.from(e.dataTransfer.items)
    // Try File System Access API via DataTransferItem.getAsFileSystemHandle
    if (fsSupportd && items[0] && 'getAsFileSystemHandle' in items[0]) {
      const handle = await (items[0] as any).getAsFileSystemHandle()
      if (handle?.kind === 'directory') {
        setDirHandle(handle)
        setGroups([])
        const entries = await readDirImages(handle)
        await processFiles(entries)
        return
      }
    }
    // Plain file drop fallback
    const files = Array.from(e.dataTransfer.files)
    setDirHandle(null)
    await processFiles(files.map(f => ({ file: f, fileHandle: null })))
  }

  const handleRemoveFile = (barcode: string, fileIndex: number) => {
    setGroups(prev => prev.map(s => {
      if (s.barcodeGroup.barcode !== barcode) return s
      const newFiles = s.barcodeGroup.files.filter((_, i) => i !== fileIndex)
      if (newFiles.length === 0) return null as any
      return { ...s, barcodeGroup: { ...s.barcodeGroup, files: newFiles } }
    }).filter(Boolean))
  }

  // ── Stats ─────────────────────────────────────────────────────────────────

  const stats = useMemo(() => ({
    found:       groups.filter(g => g.lookup === 'found').length,
    notFound:    groups.filter(g => g.lookup === 'notfound').length,
    loading:     groups.filter(g => g.lookup === 'loading').length,
    totalImages: groups.reduce((s, g) => s + g.barcodeGroup.files.length, 0),
  }), [groups])

  // ── Upload ────────────────────────────────────────────────────────────────

  const handleUpload = async () => {
    if (!selectedSetId) return
    setIsUploading(true)
    setUploadSummary(null)

    const toUpload = groups.filter(g => g.lookup === 'found' && g.uploadStatus === 'idle')
    let ok = 0, fail = 0, totalMoved = 0

    for (const group of toUpload) {
      const { variantInfo, barcodeGroup } = group
      if (!variantInfo) continue

      setGroups(prev => prev.map(s =>
        s.barcodeGroup.barcode === barcodeGroup.barcode ? { ...s, uploadStatus: 'uploading' } : s
      ))

      try {
        const extensions = barcodeGroup.files.map(
          fe => fe.file.name.split('.').pop()?.toLowerCase() ?? 'jpg'
        )

        // Prepare
        const prepareRes = await api.post(
          `/catalog/products/${variantInfo.productId}/images/prepare`,
          { imageSetId: selectedSetId, variantId: variantInfo.variantId, fileExtensions: extensions, replaceSet },
        )
        const { batchId, images: prepared } = prepareRes.data.data as {
          batchId: string; images: { imageId: string; fileName: string }[]
        }

        // Upload files one by one
        const token = localStorage.getItem('access_token')
        const successfulIds: string[] = []
        // Track imageId → { fileEntry, serverFileName } for move step
        const moveMap: { imageId: string; fe: FileEntry; serverFileName: string }[] = []

        for (let i = 0; i < barcodeGroup.files.length; i++) {
          const fe = barcodeGroup.files[i]
          const formData = new FormData()
          formData.append(prepared[i].imageId, fe.file)
          try {
            const res = await fetch(`/api/catalog/upload/${batchId}`, {
              method: 'POST',
              headers: { Authorization: `Bearer ${token}` },
              body: formData,
            })
            const json = await res.json()
            const perFile = (json?.data?.results as { imageId: string; success: boolean }[] | undefined)?.[0]
            if (res.ok && perFile?.success) {
              successfulIds.push(prepared[i].imageId)
              moveMap.push({ imageId: prepared[i].imageId, fe, serverFileName: prepared[i].fileName })
            }
          } catch { /* skip */ }
        }

        if (successfulIds.length === 0) throw new Error('Hiçbir dosya yüklenemedi')

        // Confirm
        const confirmedImages = prepared
          .filter(x => successfulIds.includes(x.imageId))
          .map((x, i) => ({
            imageId: x.imageId, sortOrder: i + 1, isProductCover: i === 0, isVariantCover: i === 0,
          }))

        await api.post(`/catalog/products/${variantInfo.productId}/images/confirm`, {
          batchId, replaceSet, confirmedImages,
        })

        // Move successfully uploaded files to "yuklenenler"
        let moved = 0
        if (dirHandle) {
          for (const { fe, serverFileName } of moveMap) {
            if (!fe.fileHandle) continue
            try {
              await moveToUploaded(dirHandle, fe.fileHandle, fe.file, serverFileName)
              moved++
            } catch (e) {
              console.warn('Dosya taşınamadı:', fe.file.name, e)
            }
          }
        }

        ok++
        totalMoved += moved
        setGroups(prev => prev.map(s =>
          s.barcodeGroup.barcode === barcodeGroup.barcode
            ? { ...s, uploadStatus: 'done', movedCount: moved }
            : s
        ))
      } catch (e: any) {
        fail++
        setGroups(prev => prev.map(s =>
          s.barcodeGroup.barcode === barcodeGroup.barcode
            ? { ...s, uploadStatus: 'error', uploadError: e?.response?.data?.error ?? e?.message ?? 'Hata' }
            : s
        ))
      }
    }

    setIsUploading(false)
    setUploadSummary({ ok, fail, moved: totalMoved })
  }

  const canUpload = groups.some(g => g.lookup === 'found' && g.uploadStatus === 'idle')
    && !!selectedSetId
    && stats.loading === 0

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="vc flex flex-col gap-5" style={{ maxWidth: 900 }}>
      {/* Breadcrumb */}
      <div className="flex items-center gap-1.5 text-xs" style={{ color: 'var(--text-s)' }}>
        <Link to="/catalog/products" className="hover:underline" style={{ color: 'var(--text-m)' }}>
          Ürün Kartları
        </Link>
        <ChevronRight size={12} />
        <span>Toplu Resim Yükleme</span>
      </div>

      <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Toplu Resim Yükleme</h1>

      {/* Settings bar */}
      <div className="card flex flex-wrap items-center gap-4">
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Set:</span>
          <div className="flex gap-1.5 flex-wrap">
            {(imageSets as ImageSet[]).map(s => (
              <button
                key={s.id}
                onClick={() => setSelectedSetId(s.id)}
                className={cn(
                  'px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors',
                  selectedSetId === s.id ? 'text-white border-transparent' : 'border-[var(--border)] hover:border-[var(--brand)]'
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
        <label className="flex items-center gap-2 cursor-pointer select-none ml-auto">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={replaceSet} onChange={e => setReplaceSet(e.target.checked)} />
          <span className="text-xs font-medium" style={{ color: 'var(--text-m)' }}>
            Mevcut resimleri arşivle
          </span>
        </label>
      </div>

      {/* Drop zone */}
      {fsSupportd ? (
        /* File System Access API — full drag+drop + button */
        <div
          className="rounded-xl border-2 border-dashed flex flex-col items-center justify-center py-10 gap-3 transition-colors hover:border-[var(--brand)] cursor-pointer"
          style={{ borderColor: 'var(--border)' }}
          onClick={handlePickFolder}
          onDrop={handleDrop}
          onDragOver={e => e.preventDefault()}
        >
          <FolderOpen size={28} style={{ color: 'var(--text-s)' }} />
          <div className="text-center">
            <p className="text-sm font-semibold" style={{ color: 'var(--text-m)' }}>
              Klasör seç veya sürükle-bırak
            </p>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Dosya formatı: <span className="font-mono">barkod_1.jpg, barkod_2.jpg, ...</span>
            </p>
            <p className="text-xs mt-1 flex items-center justify-center gap-1" style={{ color: 'var(--brand)' }}>
              <FolderSymlink size={12} />
              Yüklenen resimler klasör içinde <strong>yuklenenler/</strong> alt klasörüne taşınır
            </p>
          </div>
          {dirHandle && (
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold"
              style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
              <FolderOpen size={13} style={{ color: 'var(--brand)' }} />
              {(dirHandle as any).name}
            </div>
          )}
        </div>
      ) : (
        /* Fallback for browsers without File System Access API */
        <label
          className="relative rounded-xl border-2 border-dashed flex flex-col items-center justify-center py-10 gap-3 cursor-pointer transition-colors hover:border-[var(--brand)]"
          style={{ borderColor: 'var(--border)' }}
          onDrop={handleDrop}
          onDragOver={e => e.preventDefault()}
        >
          <FolderOpen size={28} style={{ color: 'var(--text-s)', pointerEvents: 'none' }} />
          <div className="text-center pointer-events-none">
            <p className="text-sm font-semibold" style={{ color: 'var(--text-m)' }}>
              Klasör seç veya sürükle-bırak
            </p>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Dosya formatı: <span className="font-mono">barkod_1.jpg, barkod_2.jpg, ...</span>
            </p>
            <p className="text-xs mt-1 text-orange-500 pointer-events-none">
              Bu tarayıcı otomatik taşıma desteklemiyor — yükleme sonrası dosyalar yerinde kalır
            </p>
          </div>
          <input
            type="file" multiple accept="image/*"
            // @ts-ignore
            webkitdirectory=""
            onChange={handleFallbackChange}
            style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: 0, cursor: 'pointer' }}
          />
        </label>
      )}

      {/* Summary bar */}
      {groups.length > 0 && (
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex items-center gap-1.5 text-xs" style={{ color: 'var(--text-m)' }}>
            <ImageIcon size={13} />
            <span><strong>{stats.totalImages}</strong> resim</span>
          </div>
          <div className="flex items-center gap-1.5 text-xs" style={{ color: 'var(--text-m)' }}>
            <CheckCircle2 size={13} style={{ color: 'var(--brand)' }} />
            <span><strong>{stats.found}</strong> ürün eşleşti</span>
          </div>
          {stats.notFound > 0 && (
            <div className="flex items-center gap-1.5 text-xs text-orange-500">
              <AlertCircle size={13} />
              <span><strong>{stats.notFound}</strong> barkod bulunamadı</span>
            </div>
          )}
          {stats.loading > 0 && (
            <div className="flex items-center gap-1.5 text-xs" style={{ color: 'var(--text-s)' }}>
              <Loader2 size={13} className="animate-spin" />
              <span>Sorgulanıyor...</span>
            </div>
          )}

          <div className="ml-auto flex items-center gap-3">
            {uploadSummary && (
              <span className="text-xs">
                <span style={{ color: 'var(--brand)' }}>{uploadSummary.ok} yüklendi</span>
                {uploadSummary.moved > 0 && (
                  <span className="ml-1.5 flex items-center gap-0.5 inline-flex" style={{ color: 'var(--text-s)' }}>
                    <FolderSymlink size={12} /> {uploadSummary.moved} taşındı
                  </span>
                )}
                {uploadSummary.fail > 0 && (
                  <span className="ml-1.5 text-red-500">{uploadSummary.fail} hata</span>
                )}
              </span>
            )}
            <Button
              size="sm"
              onClick={handleUpload}
              loading={isUploading}
              disabled={!canUpload || isUploading}
            >
              <Upload size={13} className="mr-1.5" />
              Tümünü Yükle ({groups.filter(g => g.lookup === 'found' && g.uploadStatus === 'idle').length})
            </Button>
          </div>
        </div>
      )}

      {/* Group cards */}
      {groups.length > 0 && (
        <div className="flex flex-col gap-3">
          {groups.map(state => (
            <BarcodeGroupCard
              key={state.barcodeGroup.barcode}
              state={state}
              onRemoveFile={handleRemoveFile}
            />
          ))}
        </div>
      )}
    </div>
  )
}
