import { CalendarDays, Download, LayoutDashboard, QrCode, Radar, SlidersHorizontal } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';
import { SidebarGroup, SidebarGroupContent, SidebarGroupLabel, SidebarMenu, SidebarMenuButton, SidebarMenuItem } from '@/components/ui/sidebar';

interface NavItem {
  to: string;
  label: string;
  icon: typeof LayoutDashboard;
  end?: boolean;
}

export const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/sessions', label: 'Sessions', icon: CalendarDays },
  { to: '/presets', label: 'Presets', icon: SlidersHorizontal },
  { to: '/detected', label: 'Detected Devices', icon: Radar },
  { to: '/export', label: 'Exports', icon: Download },
  { to: '/pairing', label: 'Pairing', icon: QrCode },
];

export function SidebarNav() {
  const { pathname } = useLocation();
  return (
    <SidebarGroup>
      <SidebarGroupLabel>Platform</SidebarGroupLabel>
      <SidebarGroupContent>
        <SidebarMenu>
          {NAV_ITEMS.map((item) => {
            const active = item.end ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`);
            const Icon = item.icon;
            return (
              <SidebarMenuItem key={item.to}>
                <SidebarMenuButton asChild isActive={active} tooltip={item.label}>
                  <Link to={item.to}>
                    <Icon />
                    <span>{item.label}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            );
          })}
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  );
}
