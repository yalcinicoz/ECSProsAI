import type { Firm, FirmPlatform } from './ChannelsPage'

export function getFirmName(f: Pick<Firm, 'nameI18n' | 'code'>) {
  const n = f.nameI18n
  if (!n) return f.code
  return n['tr'] ?? n[Object.keys(n)[0]] ?? f.code
}

export function getPlatformTypeName(pt: Pick<FirmPlatform, 'platformTypeCode' | 'platformTypeNameI18n'>) {
  const n = pt.platformTypeNameI18n
  if (!n) return pt.platformTypeCode
  return n['tr'] ?? n[Object.keys(n)[0]] ?? pt.platformTypeCode
}

export function getChannelName(ch: Pick<FirmPlatform, 'code' | 'nameI18n'>) {
  const n = ch.nameI18n
  if (!n) return ch.code
  return n['tr'] ?? n[Object.keys(n)[0]] ?? ch.code
}

// ── Dynamic Field ─────────────────────────────────────────────────────────────

