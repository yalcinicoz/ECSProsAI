/**
 * Operasyon ekranları için ses geri bildirimi (WebAudio + speechSynthesis).
 *
 * Tarayıcılar AudioContext'i kullanıcı etkileşimi olmadan "suspended" başlatır;
 * her çalma öncesi resume() denenir. İlk dokunma/tuş vuruşunda da bir kez
 * resume edilir (aşağıdaki global dinleyiciler).
 */

let ctx: AudioContext | null = null

function getCtx(): AudioContext | null {
  if (typeof window === 'undefined') return null
  const AC = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!AC) return null
  if (!ctx) ctx = new AC()
  return ctx
}

/** İlk kullanıcı etkileşiminde context'i uyandır (autoplay policy). */
function uyandir() {
  const c = getCtx()
  if (c && c.state === 'suspended') void c.resume().catch(() => {})
}
if (typeof window !== 'undefined') {
  window.addEventListener('pointerdown', uyandir, { passive: true })
  window.addEventListener('keydown', uyandir, { passive: true })
}

function bip(c: AudioContext, freq: number, baslangic: number, sure: number, tip: OscillatorType = 'sine', gainVal = 0.25) {
  const osc = c.createOscillator()
  const gain = c.createGain()
  osc.type = tip
  osc.frequency.value = freq
  osc.connect(gain)
  gain.connect(c.destination)
  const t0 = c.currentTime + baslangic
  gain.gain.setValueAtTime(0.0001, t0)
  gain.gain.exponentialRampToValueAtTime(gainVal, t0 + 0.01)
  gain.gain.exponentialRampToValueAtTime(0.0001, t0 + sure)
  osc.start(t0)
  osc.stop(t0 + sure + 0.02)
}

/** Kısa tiz bip — başarılı okutma. */
export function basariSesi() {
  const c = getCtx()
  if (!c) return
  if (c.state === 'suspended') void c.resume().catch(() => {})
  bip(c, 1320, 0, 0.12)
}

/** Çift pes bip — hata; depo gürültüsünde belirgin olsun diye uzun ve güçlü. */
export function hataSesi() {
  const c = getCtx()
  if (!c) return
  if (c.state === 'suspended') void c.resume().catch(() => {})
  bip(c, 220, 0, 0.25, 'square', 0.35)
  bip(c, 165, 0.28, 0.35, 'square', 0.35)
}

/** Metni Türkçe seslendir (destek yoksa sessizce geçer). */
export function seslendir(metin: string) {
  if (typeof window === 'undefined' || !('speechSynthesis' in window)) return
  try {
    const u = new SpeechSynthesisUtterance(metin)
    u.lang = 'tr-TR'
    const trVoice = window.speechSynthesis.getVoices().find(v => v.lang?.toLowerCase().startsWith('tr'))
    if (trVoice) u.voice = trVoice
    u.rate = 1.05
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(u)
  } catch { /* ses yoksa akış bozulmasın */ }
}
