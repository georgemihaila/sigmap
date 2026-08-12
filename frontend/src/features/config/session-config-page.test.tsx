import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { SessionConfigPage } from './session-config-page';
import { renderWithProviders } from '@/test/utils';
import { getDb } from '@/mocks/db';

describe('SessionConfigPage — bulk edit → diff preview → push', () => {
  it('previews per-device diffs, toggles selection and pushes', async () => {
    const db = getDb();
    const session = db.sessions.find((s) => s.status === 'active')!;

    renderWithProviders(<SessionConfigPage />, { sessionId: session.id });

    // The editor is available; enable Bluetooth scanning in the target config.
    const bluetoothSwitch = await screen.findByRole('switch', { name: 'Scan Bluetooth' });
    fireEvent.click(bluetoothSwitch);

    // Preview the diff against the fleet.
    fireEvent.click(screen.getByRole('button', { name: 'Preview diff' }));

    // At least one device should show pending changes.
    await waitFor(() => {
      expect(screen.getAllByText(/change\(s\)/).length).toBeGreaterThan(0);
    });

    // Push button appears with the full selection count.
    const pushButton = await screen.findByRole('button', { name: /^Push to \d+ device/ });
    const fullCount = Number(/Push to (\d+) device/.exec(pushButton.textContent ?? '')?.[1]);

    // Deselect the first device → count drops by one.
    const firstCheckbox = screen.getAllByRole('checkbox')[0];
    fireEvent.click(firstCheckbox);
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /^Push to \d+ device/ }).textContent?.trim()).toBe(
        `Push to ${fullCount - 1} device(s)`,
      );
    });

    // Push the config.
    fireEvent.click(screen.getByRole('button', { name: /^Push to \d+ device/ }));
    await screen.findByText(/Config pushed to \d+ device/);
  });

  it('shows the diff summary after previewing', async () => {
    const db = getDb();
    const session = db.sessions.find((s) => s.status === 'active')!;

    renderWithProviders(<SessionConfigPage />, { sessionId: session.id });

    fireEvent.click(await screen.findByRole('button', { name: 'Preview diff' }));

    await waitFor(() => {
      expect(screen.getAllByText(/\d+ device\(s\)/).length).toBeGreaterThan(0);
      expect(screen.getByText(/\d+ will change/)).toBeInTheDocument();
    });
  });
});
