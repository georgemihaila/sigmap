import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { PairingPage } from './pairing-page';
import { renderWithProviders } from '@/test/utils';
import { getDb } from '@/mocks/db';

describe('PairingPage — permanent pairing', () => {
  it('shows a pairing QR immediately without picking a session', async () => {
    renderWithProviders(<PairingPage />);

    expect(await screen.findByText(/sigmap-pair:\/\/\?host=.+&token=/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copy pairing payload' })).toBeInTheDocument();
    // No session selector guards the QR anymore.
    expect(screen.queryByRole('combobox', { name: 'Session' })).not.toBeInTheDocument();
  });

  it('approving without a session pairs the device permanently and keeps it unassigned', async () => {
    const db = getDb();
    const first = [...db.pairings]
      .filter((p) => p.status === 'pending')
      .sort((a, b) => a.requestedAt.localeCompare(b.requestedAt))[0]!;

    renderWithProviders(<PairingPage />);

    const approveButtons = await screen.findAllByRole('button', { name: /Approve/ });
    fireEvent.click(approveButtons[0]);

    await waitFor(() => {
      const paired = db.devices.find((d) => d.id === first.deviceId);
      expect(paired).toBeDefined();
      expect(paired?.status).toBe('offline');
      expect(db.sessionDevices.some((sd) => sd.deviceId === first.deviceId)).toBe(false);
    });
    expect(db.pairings.find((p) => p.deviceId === first.deviceId)?.status).toBe('approved');
  });

  it('rejects a pending pairing request', async () => {
    const db = getDb();
    const first = [...db.pairings]
      .filter((p) => p.status === 'pending')
      .sort((a, b) => a.requestedAt.localeCompare(b.requestedAt))[0]!;

    renderWithProviders(<PairingPage />);

    const rejectButtons = await screen.findAllByRole('button', { name: /Reject/ });
    fireEvent.click(rejectButtons[0]);

    await waitFor(() => {
      expect(db.pairings.find((p) => p.deviceId === first.deviceId)?.status).toBe('rejected');
    });
  });
});
