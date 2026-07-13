import { useEffect, useRef } from 'react'
import Quill from 'quill'
import 'quill/dist/quill.snow.css'

// P2b: rich_text içerik editörü — Quill pakete gömülü (dış CDN yok).
// value yalnız ilk kurulumda okunur; dıştan sıfırlamak için bileşeni key ile yeniden kur.
export function QuillEditor({ initialHtml, onChange }: {
  initialHtml: string
  onChange: (html: string) => void
}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const quillRef = useRef<Quill | null>(null)
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange

  useEffect(() => {
    if (!containerRef.current || quillRef.current) return
    const q = new Quill(containerRef.current, {
      theme: 'snow',
      modules: {
        toolbar: [
          [{ header: [1, 2, 3, false] }],
          ['bold', 'italic', 'underline', 'strike'],
          [{ list: 'ordered' }, { list: 'bullet' }],
          ['link'],
          ['clean'],
        ],
      },
    })
    q.clipboard.dangerouslyPasteHTML(initialHtml)
    q.on('text-change', () => onChangeRef.current(q.root.innerHTML))
    quillRef.current = q
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="quill-wrap rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
      <div ref={containerRef} style={{ minHeight: 220, background: 'var(--surface)', color: 'var(--text)' }} />
    </div>
  )
}
