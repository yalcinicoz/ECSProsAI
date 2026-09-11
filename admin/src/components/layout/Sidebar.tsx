import { useId, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { useUIStore } from '@/store/ui'
import { useAuthStore } from '@/store/auth'
import { useQuestionAlertStore } from '@/store/questionAlerts'
import { useTicketAlertStore } from '@/store/ticketAlerts'
import { ChevronDown, Search, X, SlidersHorizontal } from 'lucide-react'

import { NAV_SECTIONS, findActiveItem, permittedSections, searchSections, itemBadge } from './sidebarNavigation'

// Using inline SVG paths for compact icons (matches FA icons used in option-h)
const ICON: Record<string, React.ReactNode> = {
  gauge:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M12 2a10 10 0 1 0 10 10"/><path d="M12 12 8.5 8.5"/><circle cx="12" cy="12" r="1"/><path d="M16.51 17.35a8 8 0 0 0 1.49-8.35"/></svg>,
  box:           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>,
  inbox:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><polyline points="22 12 16 12 14 15 10 15 8 12 2 12"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/></svg>,
  sliders:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><line x1="4" x2="4" y1="21" y2="14"/><line x1="4" x2="4" y1="10" y2="3"/><line x1="12" x2="12" y1="21" y2="12"/><line x1="12" x2="12" y1="8" y2="3"/><line x1="20" x2="20" y1="21" y2="16"/><line x1="20" x2="20" y1="12" y2="3"/><line x1="2" x2="6" y1="14" y2="14"/><line x1="10" x2="14" y1="8" y2="8"/><line x1="18" x2="22" y1="16" y2="16"/></svg>,
  layers:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z"/><path d="m22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65"/><path d="m22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65"/></svg>,
  sitemap:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="6" height="4" x="9" y="14" rx="1"/><rect width="6" height="4" x="2" y="14" rx="1"/><rect width="6" height="4" x="16" y="14" rx="1"/><rect width="6" height="4" x="9" y="6" rx="1"/><path d="M5 10v4"/><path d="M12 10v4"/><path d="M19 10v4"/><path d="M5 10H19"/></svg>,
  shoppingbag:   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z"/><line x1="3" x2="21" y1="6" y2="6"/><path d="M16 10a4 4 0 0 1-8 0"/></svg>,
  truck:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M14 18V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v11a1 1 0 0 0 1 1h2"/><path d="M15 18H9"/><path d="M19 18h2a1 1 0 0 0 1-1v-3.65a1 1 0 0 0-.22-.62l-3.48-4.35a1 1 0 0 0-.78-.38H14"/><circle cx="17" cy="18" r="2"/><circle cx="7" cy="18" r="2"/></svg>,
  rotateccw:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></svg>,
  filetext:      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><line x1="10" x2="16" y1="9" y2="9"/><line x1="10" x2="16" y1="13" y2="13"/><line x1="10" x2="14" y1="17" y2="17"/></svg>,
  handshake:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m11 17 2 2a1 1 0 1 0 3-3"/><path d="m14 14 2.5 2.5a1 1 0 1 0 3-3l-3.88-3.88a3 3 0 0 0-4.24 0l-.88.88a1 1 0 1 1-3-3l2.81-2.81a5.79 5.79 0 0 1 7.06-.87l.47.28a2 2 0 0 0 1.42.25L21 4"/><path d="m21 3 1 11h-2"/><path d="M3 3 2 14l6.5 6.5a1 1 0 1 0 3-3"/><path d="M3 4h8"/></svg>,
  users:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>,
  usersround:    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M18 21a8 8 0 0 0-16 0"/><circle cx="10" cy="8" r="5"/><path d="M22 20c0-3.37-2-6.5-4-8a5 5 0 0 0-.45-8.3"/></svg>,
  warehouse:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M22 8.35V20a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8.35A2 2 0 0 1 3.26 6.5l8-3.2a2 2 0 0 1 1.48 0l8 3.2A2 2 0 0 1 22 8.35Z"/><path d="M6 18h12"/><path d="M6 14h12"/><rect width="8" height="6" x="8" y="18" rx="1"/></svg>,
  boxes:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M2.97 12.92A2 2 0 0 0 2 14.63v3.24a2 2 0 0 0 .97 1.71l3 1.8a2 2 0 0 0 2.06 0L12 19v-5.5l-5-3-4.03 2.42Z"/><path d="m7 16.5-4.74-2.85"/><path d="m7 16.5 5-3"/><path d="M7 16.5v5.17"/><path d="M12 13.5V19l3.97 2.38a2 2 0 0 0 2.06 0l3-1.8a2 2 0 0 0 .97-1.71v-3.24a2 2 0 0 0-.97-1.71L17 10.5l-5 3Z"/><path d="m17 16.5-5-3"/><path d="m17 16.5 4.74-2.85"/><path d="M17 16.5v5.17"/><path d="M7.97 4.42A2 2 0 0 0 7 6.13v4.37l5 3 5-3V6.13a2 2 0 0 0-.97-1.71l-3-1.8a2 2 0 0 0-2.06 0l-3 1.8Z"/><path d="M12 8 7.26 5.15"/><path d="m12 8 4.74-2.85"/><path d="M12 13.5V8"/></svg>,
  refreshcw:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/></svg>,
  dice:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect x="3" y="3" width="18" height="18" rx="3"/><circle cx="8" cy="8" r="1.2" fill="currentColor"/><circle cx="16" cy="8" r="1.2" fill="currentColor"/><circle cx="12" cy="12" r="1.2" fill="currentColor"/><circle cx="8" cy="16" r="1.2" fill="currentColor"/><circle cx="16" cy="16" r="1.2" fill="currentColor"/></svg>,
  percent:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><line x1="19" x2="5" y1="5" y2="19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/></svg>,
  ticket:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"/></svg>,
  gift:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect x="3" y="8" width="18" height="4" rx="1"/><path d="M12 8v13"/><path d="M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7"/><path d="M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5"/></svg>,
  creditcard:    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="20" height="14" x="2" y="5" rx="2"/><line x1="2" x2="22" y1="10" y2="10"/></svg>,
  plug:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M12 22v-5"/><path d="M9 8V2"/><path d="M15 8V2"/><path d="M18 8H6a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-6a2 2 0 0 0-2-2Z"/></svg>,
  settings:      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>,
  clipboard:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="8" height="4" x="8" y="2" rx="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><line x1="12" x2="12" y1="11" y2="17"/><line x1="9" x2="15" y1="14" y2="14"/></svg>,
  monitor:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="20" height="14" x="2" y="3" rx="2"/><line x1="8" x2="16" y1="21" y2="21"/><line x1="12" x2="12" y1="17" y2="21"/></svg>,
  building2:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z"/><path d="M6 12H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2"/><path d="M18 9h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-2"/><path d="M10 6h4"/><path d="M10 10h4"/><path d="M10 14h4"/><path d="M10 18h4"/></svg>,
  globe:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>,
  languages:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m5 8 6 6"/><path d="m4 14 6-6 2-3"/><path d="M2 5h12"/><path d="M7 2h1"/><path d="m22 22-5-10-5 10"/><path d="M14 18h6"/></svg>,
  databasezap:   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5V19A9 3 0 0 0 15 21.84"/><path d="M21 5V8"/><path d="M21 12L18 17H22L19 22"/><path d="M3 12A9 3 0 0 0 14.59 14.87"/></svg>,
  images:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="18" height="18" x="3" y="3" rx="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg>,
  filter: <SlidersHorizontal size={15} />,
  palette:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/><circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/><circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/><circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/><path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z"/></svg>,
  layout:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="18" height="18" x="3" y="3" rx="2"/><path d="M3 9h18"/><path d="M9 21V9"/></svg>,
  mail:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>,
  bell:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>,
  scan:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M3 7V5a2 2 0 0 1 2-2h2"/><path d="M17 3h2a2 2 0 0 1 2 2v2"/><path d="M21 17v2a2 2 0 0 1-2 2h-2"/><path d="M7 21H5a2 2 0 0 1-2-2v-2"/><path d="M8 8v8"/><path d="M12 8v8"/><path d="M16 8v8"/></svg>,
  store:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m2 7 4.41-4.41A2 2 0 0 1 7.83 2h8.34a2 2 0 0 1 1.42.59L22 7"/><path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8"/><path d="M15 22v-4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4"/><path d="M2 7h20"/><path d="M22 7v3a2 2 0 0 1-2 2a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 16 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 12 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 8 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 4 12a2 2 0 0 1-2-2V7"/></svg>,
}


// ── Component ─────────────────────────────────────────────────────────────────

interface SidebarProps {
  onMobileClose?: () => void
}

export function Sidebar({ onMobileClose }: SidebarProps) {
  const { sidebarCollapsed: masaustuDaraltilmis, toggleSidebar } = useUIStore()
  // Mobil overlay (onMobileClose dolu) HER ZAMAN geniş çizilir: masaüstünde daraltılmış
  // bırakılan menü, telefonda 248px'lik overlay içinde 60px ikon rayı olarak görünüyordu.
  const sidebarCollapsed = onMobileClose ? false : masaustuDaraltilmis
  const { user, logout } = useAuthStore()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const [search, setSearch] = useState('')
  // Cevap bekleyen ürün sorusu — QuestionAlerts besler; sıfırlanana kadar kırmızı rozet
  const bekleyenSoru = useQuestionAlertStore((s) => s.pendingCount)
  const bekleyenKayit = useTicketAlertStore((s) => s.pendingCount)

  const location = useLocation()
  const instanceId = useId()
  const visibleSections = permittedSections(NAV_SECTIONS, hasPermission)
  // En özel route önce tüm envanterden belirlenir: gizli bir çocuk, parent'ı yanlış aktifleştirmez.
  const activeItem = findActiveItem(location.pathname)
  const activeGroup = visibleSections.find((section) => section.items.some((item) => item.to === activeItem?.to))?.id ?? null
  const route = location.pathname + location.search + location.hash
  const [navigation, setNavigation] = useState({ route, activeGroup, openGroup: activeGroup })
  // Sadece URL/erişim değişiminde ayarla; aynı sayfadaki manuel grup seçimini koru.
  if (navigation.route !== route || navigation.activeGroup !== activeGroup) {
    setNavigation({ route, activeGroup, openGroup: activeGroup })
  }
  const searching = !sidebarCollapsed && Boolean(search.trim())
  const filtered = searchSections(visibleSections, searching ? search : '')
  const badgeFor = (item: (typeof NAV_SECTIONS)[number]['items'][number]) => itemBadge(item, bekleyenSoru, bekleyenKayit)

  // Initials for user avatar
  const initials = user?.fullName
    ?.split(' ')
    .slice(0, 2)
    .map((n) => n[0])
    .join('')
    .toUpperCase() ?? 'AD'

  return (
    <aside
      className={cn(
        'flex flex-col h-full overflow-hidden white-space-nowrap',
        'transition-all duration-[280ms] ease-[cubic-bezier(.4,0,.2,1)]',
        sidebarCollapsed ? 'w-[60px]' : 'w-[248px]',
      )}
      style={{ background: 'var(--sidebar)' }}
    >
      {/* ── Logo + toggle ── */}
      <div
        className="flex items-center justify-between px-4 py-4 flex-shrink-0"
        style={{ borderBottom: '1px solid rgba(255,255,255,.1)' }}
      >
        <Link to="/" onClick={onMobileClose} className="flex items-center gap-2.5 min-w-0 hover:opacity-80 transition-opacity">
          <div
            className="w-8 h-8 rounded-xl flex items-center justify-center flex-shrink-0"
            style={{ background: 'var(--brand)' }}
          >
            <span className="text-white font-black text-sm">E</span>
          </div>
          {!sidebarCollapsed && (
            <span className="text-white font-bold text-base tracking-tight truncate">ECSPros</span>
          )}
        </Link>
        <div className="flex items-center gap-1 flex-shrink-0">
          {onMobileClose && (
            <button
              type="button"
              onClick={onMobileClose}
              className="md:hidden w-7 h-7 flex items-center justify-center rounded-lg hover:bg-white/10 transition-colors flex-shrink-0"
              style={{ color: 'rgba(255,255,255,.4)' }}
            >
              <X size={16} />
            </button>
          )}
          {!sidebarCollapsed && (
            <button
              type="button"
              onClick={toggleSidebar}
              className="hidden md:flex w-7 h-7 items-center justify-center rounded-lg hover:bg-white/10 transition-colors flex-shrink-0"
              style={{ color: 'rgba(255,255,255,.3)' }}
              title="Daralt"
            >
              {/* bars-staggered icon */}
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
                <line x1="4" x2="20" y1="6" y2="6"/><line x1="4" x2="14" y1="12" y2="12"/><line x1="4" x2="17" y1="18" y2="18"/>
              </svg>
            </button>
          )}
        </div>
      </div>

      {/* ── Menu search (only when expanded) ── */}
      {!sidebarCollapsed && (
        <div className="px-3 pt-3 pb-1 flex-shrink-0">
          <div className="relative">
            <Search
              size={12}
              className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              style={{ color: 'rgba(255,255,255,.25)' }}
            />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Menüde ara…"
              aria-label="Menüde ara"
              onKeyDown={(event) => { if (event.key === 'Escape') setSearch('') }}
              className="w-full pl-8 pr-8 py-2 text-sm rounded-lg outline-none"
              style={{
                background: 'rgba(255,255,255,.07)',
                border: '1px solid rgba(255,255,255,.1)',
                color: 'rgba(255,255,255,.75)',
              }}
            />
            {search && (
              <button type="button" aria-label="Menü aramasını temizle" onClick={() => setSearch('')}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-white/60 rounded focus-visible:outline-2 focus-visible:outline-emerald-300">
                <X size={14} />
              </button>
            )}
          </div>
        </div>
      )}

      {/* ── Nav ── */}
      <nav aria-label="Ana menü" className="flex-1 overflow-y-auto thin-scroll px-3 py-2 space-y-0.5">
        {filtered.length === 0 && <p role="status" className="px-2 py-4 text-xs text-white/60">Eşleşen menü bulunamadı.</p>}
        {filtered.map((section) => {
          const open = !sidebarCollapsed && (searching || section.direct || navigation.openGroup === section.id)
          const panelId = instanceId + '-' + section.id
          const hasNotification = section.items.some((item) => (badgeFor(item) ?? 0) > 0)
          return (
          <div key={section.id}>
            {searching ? (
              <div className="nav-section-lbl">{section.label}</div>
            ) : !section.direct && (
              <button
                type="button"
                aria-label={section.label}
                aria-expanded={Boolean(open)}
                aria-controls={panelId}
                title={sidebarCollapsed ? section.label : undefined}
                onClick={() => {
                  if (sidebarCollapsed) { toggleSidebar(); setSearch('') }
                  setNavigation((current) => ({
                    ...current,
                    openGroup: sidebarCollapsed || current.openGroup !== section.id ? section.id : null,
                  }))
                }}
                className={cn('nav-lnk w-full text-left focus-visible:outline-2 focus-visible:outline-emerald-300 focus-visible:outline-offset-[-2px]',
                  activeGroup === section.id && 'active', sidebarCollapsed && 'justify-center px-0 py-2')}
              >
                <span className="ni flex-shrink-0 relative">
                  {ICON[section.icon]}
                  {!open && hasNotification && (
                    <span aria-label="Yeni bildirim var" className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-red-500" />
                  )}
                </span>
                {!sidebarCollapsed && <>
                  <span className="truncate">{section.label}</span>
                  <ChevronDown aria-hidden="true" size={14} className={cn('ml-auto flex-shrink-0 transition-transform duration-150 motion-reduce:transition-none', open && 'rotate-180')} />
                </>}
              </button>
            )}
            <div id={panelId} hidden={!section.direct && !open} className={cn(!section.direct && !sidebarCollapsed && 'ml-2 border-l border-white/10 pl-1')}>
            {section.items.map((item) => {
              const soruRozeti = item.to === '/storefront/questions' && bekleyenSoru > 0
              const badge = badgeFor(item)
              const isActive = item.to === activeItem?.to
              return (
              <Link
                key={item.to}
                to={item.to}
                onClick={onMobileClose}
                aria-current={isActive ? 'page' : undefined}
                title={item.label}
                className={cn('nav-lnk focus-visible:outline-2 focus-visible:outline-emerald-300 focus-visible:outline-offset-[-2px]',
                  isActive && 'active', sidebarCollapsed && 'justify-center px-0 py-2')}
              >
                <span className="ni flex-shrink-0 relative">
                  {ICON[item.icon]}
                  {sidebarCollapsed && (badge ?? 0) > 0 && (
                    <span aria-label="Yeni bildirim var" className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-red-500" />
                  )}
                </span>
                {sidebarCollapsed ? <span className="sr-only">{item.label}</span> : <span className="truncate">{item.label}</span>}
                {!sidebarCollapsed && badge != null && (
                  <span
                    className="ml-auto text-[10px] font-bold px-1.5 py-0.5 rounded-full flex-shrink-0"
                    style={soruRozeti
                      ? { background: '#ef4444', color: '#fff' }
                      : { background: 'rgba(52,211,153,.2)', color: '#34d399' }}
                  >
                    {badge}
                  </span>
                )}
              </Link>
              )
            })}
            </div>
          </div>
          )
        })}
      </nav>

      {/* ── Kullanım Rehberi (2026-08-23): tüm admin panelleri için ortak statik rehber (/rehber) ── */}
      <a
        href="/rehber/"
        target="_blank"
        rel="noopener"
        className="flex items-center gap-2 px-3 py-2 text-xs hover:bg-white/10 transition-colors flex-shrink-0"
        style={{ color: 'rgba(255,255,255,.55)', borderTop: '1px solid rgba(255,255,255,.1)' }}
        title="Kullanım Rehberi (yeni sekmede açılır)"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4 flex-shrink-0">
          <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
        </svg>
        {!sidebarCollapsed && <span>Kullanım Rehberi</span>}
      </a>

      {/* ── User ── */}
      <div
        className="flex items-center gap-3 px-3 py-3 flex-shrink-0"
        style={{ borderTop: '1px solid rgba(255,255,255,.1)' }}
      >
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0"
          style={{ background: 'var(--brand)' }}
        >
          {initials}
        </div>
        {!sidebarCollapsed && (
          <>
            <div className="min-w-0 flex-1">
              <div className="text-white text-sm font-medium truncate">{user?.fullName}</div>
              <div className="text-xs truncate" style={{ color: 'rgba(255,255,255,.35)' }}>
                {user?.email}
              </div>
            </div>
            <button
              type="button"
              onClick={logout}
              className="flex-shrink-0 w-7 h-7 flex items-center justify-center rounded-lg hover:bg-white/10 transition-colors"
              style={{ color: 'rgba(255,255,255,.35)' }}
              title="Çıkış"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/>
              </svg>
            </button>
          </>
        )}
      </div>
    </aside>
  )
}
