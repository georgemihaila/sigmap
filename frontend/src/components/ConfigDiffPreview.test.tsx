import { describe, expect, it } from 'vitest';
import type React from 'react';
import { render, screen } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';
import { ConfigDiffList } from './ConfigDiffPreview';
import type { ConfigFieldDiff } from '../store/configDiff';

function renderWithMantine(ui: React.ReactElement) {
  return render(<MantineProvider>{ui}</MantineProvider>);
}

describe('ConfigDiffList', () => {
  it('renders each field diff with current and preset values', () => {
    const diffs: ConfigFieldDiff[] = [
      { field: 'scanWifi', from: false, to: true },
      { field: 'channelHopMs', from: 500, to: 250 },
      { field: 'interfaces', from: [], to: [{ name: 'wlan0' }] },
    ];
    renderWithMantine(<ConfigDiffList diffs={diffs} />);

    expect(screen.getByText('scanWifi')).toBeInTheDocument();
    expect(screen.getByText('off')).toBeInTheDocument();
    expect(screen.getByText('on')).toBeInTheDocument();
    expect(screen.getByText('250')).toBeInTheDocument();
    expect(screen.getByText('wlan0')).toBeInTheDocument();
  });

  it('shows a friendly empty state when there are no diffs', () => {
    renderWithMantine(<ConfigDiffList diffs={[]} />);
    expect(screen.getByText(/no differences/i)).toBeInTheDocument();
  });
});
