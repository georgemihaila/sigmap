import {
  ActionIcon,
  AppShell,
  Avatar,
  Badge,
  Center,
  Group,
  Loader,
  NavLink,
  Stack,
  Text,
  Title,
  Tooltip,
  useMantineColorScheme,
} from '@mantine/core';
import {
  IconAdjustments,
  IconCalendarStats,
  IconDevices,
  IconDownload,
  IconLogout,
  IconMap,
  IconMoon,
  IconQrcode,
  IconRadar,
  IconSun,
} from '@tabler/icons-react';
import { useState } from 'react';
import { LiveMapPage } from './pages/LiveMapPage';
import { SessionsPage } from './pages/SessionsPage';
import { FleetPage } from './pages/FleetPage';
import { PresetsPage } from './pages/PresetsPage';
import { DevicesPage } from './pages/DevicesPage';
import { ExportsPage } from './pages/ExportsPage';
import { PairingPage } from './pages/PairingPage';
import { LoginPage } from './components/LoginPage';
import { SessionProvider } from './store/session';
import { useMeQuery, useLogoutMutation } from './store/auth';

type Page = 'live' | 'sessions' | 'fleet' | 'presets' | 'devices' | 'exports' | 'pairing';

const PAGES: Array<{ key: Page; label: string; icon: React.ReactNode; operatorOnly?: boolean }> = [
  { key: 'live', label: 'Live map', icon: <IconMap size={18} /> },
  { key: 'sessions', label: 'Sessions', icon: <IconCalendarStats size={18} /> },
  { key: 'fleet', label: 'Device fleet', icon: <IconDevices size={18} /> },
  { key: 'devices', label: 'Detected devices', icon: <IconRadar size={18} /> },
  { key: 'presets', label: 'Presets', icon: <IconAdjustments size={18} /> },
  { key: 'exports', label: 'Export center', icon: <IconDownload size={18} /> },
  { key: 'pairing', label: 'Pairing', icon: <IconQrcode size={18} />, operatorOnly: true },
];

function ThemeToggle() {
  const { colorScheme, toggleColorScheme } = useMantineColorScheme();
  return (
    <Tooltip label={colorScheme === 'dark' ? 'Switch to light' : 'Switch to dark'}>
      <ActionIcon variant="subtle" onClick={toggleColorScheme} aria-label="Toggle color scheme">
        {colorScheme === 'dark' ? <IconSun size={18} /> : <IconMoon size={18} />}
      </ActionIcon>
    </Tooltip>
  );
}

function Shell() {
  const [page, setPage] = useState<Page>('live');
  const { data: me, isLoading } = useMeQuery();
  const [logout] = useLogoutMutation();

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
  const initials = (me.username ?? 'U').slice(0, 2).toUpperCase();

  return (
    <AppShell
      navbar={{ width: 240, breakpoint: 'sm' }}
      header={{ height: 60 }}
      padding="md"
    >
      <AppShell.Header px="md">
        <Group h="100%" justify="space-between">
          <Group gap="xs">
            <ActionIcon variant="light" color="teal" size="lg" radius="md">
              <IconRadar size={20} />
            </ActionIcon>
            <Title order={3}>Sigmap</Title>
          </Group>
          <Group gap="xs">
            <ThemeToggle />
            <Avatar radius="xl" size="sm" color="teal" variant="light">
              {initials}
            </Avatar>
            <Stack gap={0} justify="center" visibleFrom="sm">
              <Text size="sm" fw={600}>
                {me.username}
              </Text>
              <Badge size="xs" variant="light" color={isOperator ? 'orange' : 'teal'} tt="capitalize">
                {me.role}
              </Badge>
            </Stack>
            <Tooltip label="Sign out">
              <ActionIcon variant="subtle" onClick={() => void logout()} aria-label="Sign out" ml="xs">
                <IconLogout size={18} />
              </ActionIcon>
            </Tooltip>
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <Text size="xs" fw={600} tt="uppercase" c="dimmed" px="xs" pt="xs" pb="sm">
          Operations
        </Text>
        <Stack gap={2}>
          {visiblePages.map((p) => (
            <NavLink
              key={p.key}
              label={p.label}
              leftSection={p.icon}
              active={page === p.key}
              onClick={() => setPage(p.key)}
              color={page === p.key ? 'teal' : undefined}
            />
          ))}
        </Stack>
      </AppShell.Navbar>
      <AppShell.Main bg="var(--mantine-color-body)">
        <Stack gap="md" maw={1400} mx="auto">
          {page === 'live' && <LiveMapPage />}
          {page === 'sessions' && <SessionsPage />}
          {page === 'fleet' && <FleetPage />}
          {page === 'presets' && <PresetsPage />}
          {page === 'devices' && <DevicesPage />}
          {page === 'exports' && <ExportsPage />}
          {page === 'pairing' && isOperator && <PairingPage />}
        </Stack>
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
