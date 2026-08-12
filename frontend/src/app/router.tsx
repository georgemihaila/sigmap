import { createBrowserRouter, Navigate } from 'react-router-dom';

import { AppShell } from '@/components/app-shell';
import { RequireAuth } from '@/features/auth/require-auth';
import { LoginPage } from '@/features/auth/login-page';
import { DashboardPage } from '@/features/dashboard/dashboard-page';
import { SessionsPage } from '@/features/sessions/sessions-page';
import { SessionLayout } from '@/features/sessions/session-layout';
import { SessionOverviewPage } from '@/features/sessions/session-overview-page';
import { LiveMapPage } from '@/features/map/live-map-page';
import { FleetPage } from '@/features/fleet/fleet-page';
import { SessionConfigPage } from '@/features/config/session-config-page';
import { PresetsPage } from '@/features/presets/presets-page';
import { DetectedDevicesPage } from '@/features/detected/detected-devices-page';
import { DetectedDeviceDetailPage } from '@/features/detected/detected-device-detail-page';
import { ExportsPage } from '@/features/exports/exports-page';
import { PairingPage } from '@/features/pairing/pairing-page';

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      { path: '/', element: <DashboardPage /> },
      { path: '/sessions', element: <SessionsPage /> },
      {
        path: '/sessions/:sessionId',
        element: <SessionLayout />,
        children: [
          { index: true, element: <SessionOverviewPage /> },
          { path: 'map', element: <LiveMapPage /> },
          { path: 'fleet', element: <FleetPage /> },
          { path: 'config', element: <SessionConfigPage /> },
        ],
      },
      { path: '/presets', element: <PresetsPage /> },
      { path: '/detected', element: <DetectedDevicesPage /> },
      { path: '/detected/:mac', element: <DetectedDeviceDetailPage /> },
      { path: '/export', element: <ExportsPage /> },
      { path: '/pairing', element: <PairingPage /> },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
]);
