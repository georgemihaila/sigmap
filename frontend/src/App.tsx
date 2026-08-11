import { AppShell, Loader, NavLink, Title, Center } from '@mantine/core';
import { useState } from 'react';
import { LiveMapPage } from './pages/LiveMapPage';
import { SessionsPage } from './pages/SessionsPage';
import { FleetPage } from './pages/FleetPage';
import { PresetsPage } from './pages/PresetsPage';
import { DevicesPage } from './pages/DevicesPage';
import { ExportsPage } from './pages/ExportsPage';
import { PairingPage } from './pages/PairingPage';
import { LoginPage } from './components/LoginPage';
import { SessionProvider, useSession } from './store/session';
import { useMeQuery, useLogoutMutation } from './store/auth';

type Page = 'live' | 'sessions' | 'fleet' | 'presets' | 'devices' | 'exports' | 'pairing';

const PAGES: Array<{ key: Page; label: string; operatorOnly?: boolean }> = [
  { key: 'live', label: 'Live map' },
  { key: 'sessions', label: 'Sessions' },
  { key: 'fleet', label: 'Device fleet' },
  { key: 'devices', label: 'Detected devices' },
  { key: 'presets', label: 'Presets' },
  { key: 'exports', label: 'Export center' },
  { key: 'pairing', label: 'Pairing', operatorOnly: true },
];

function Shell() {
  const [page, setPage] = useState<Page>('live');
  const { data: me, isLoading } = useMeQuery();
  const [logout] = useLogoutMutation();
  const { activeSessionId } = useSession();

  if (isLoading) {
    return (
      <Center h="100vh">
        <Loader />
      </Center>
    );
  }

  if (!me?.authenticated) {
    return <LoginPage />;
  }

  const isOperator = me.role === 'operator';
  const visiblePages = PAGES.filter((p) => !p.operatorOnly || isOperator);

  return (
    <AppShell navbar={{ width: 220, breakpoint: 'sm' }} padding="md">
      <AppShell.Header p="sm" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Title order={3}>Sigmap</Title>
        <NavLink
          label={`${me.username} (${me.role}) · sign out`}
          onClick={() => void logout()}
          style={{ width: 200 }}
        />
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        {visiblePages.map((p) => (
          <NavLink
            key={p.key}
            label={p.label}
            active={page === p.key}
            onClick={() => setPage(p.key)}
          />
        ))}
      </AppShell.Navbar>
      <AppShell.Main>
        {page === 'live' && <LiveMapPage />}
        {page === 'sessions' && <SessionsPage />}
        {page === 'fleet' && <FleetPage />}
        {page === 'presets' && <PresetsPage />}
        {page === 'devices' && <DevicesPage />}
        {page === 'exports' && <ExportsPage />}
        {page === 'pairing' && isOperator && <PairingPage />}
        {activeSessionId == null && <span />}
      </AppShell.Main>
    </AppShell>
  );
}

export default function App() {
  return (
    <SessionProvider>
      <Shell />
    </SessionProvider>
  );
}
