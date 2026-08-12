import { Radar } from 'lucide-react';
import { Outlet, useLocation } from 'react-router-dom';
import { ThemeToggle } from '@/components/theme-toggle';
import { SidebarNav } from '@/components/sidebar-nav';
import { SessionSwitcher } from '@/components/session-switcher';
import { UserMenu } from '@/components/user-menu';
import { Sidebar, SidebarContent, SidebarFooter, SidebarHeader, SidebarInset, SidebarProvider, SidebarRail, SidebarTrigger } from '@/components/ui/sidebar';

export function AppShell() {
  const { pathname } = useLocation();
  const inSession = pathname.startsWith('/sessions/');

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex items-center gap-2 px-2 py-1">
            <Radar className="size-5" />
            <span className="font-heading text-lg tracking-tight">SIGMAP</span>
          </div>
        </SidebarHeader>
        <SidebarContent>
          <SidebarNav />
        </SidebarContent>
        <SidebarFooter>
          <UserMenu />
        </SidebarFooter>
        <SidebarRail />
      </Sidebar>
      <SidebarInset>
        <header className="sticky top-0 z-10 flex h-14 items-center gap-3 border-b-2 border-border bg-background px-4">
          <SidebarTrigger />
          {inSession ? <SessionSwitcher /> : null}
          <div className="ml-auto flex items-center gap-2">
            <ThemeToggle />
          </div>
        </header>
        <main className="p-4 md:p-6">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
