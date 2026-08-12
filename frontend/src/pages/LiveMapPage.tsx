import { Badge, Box, Card, Group, Select, Stack, Switch, Text } from '@mantine/core';
import { IconMap2, IconRadar } from '@tabler/icons-react';
import { useState } from 'react';
import { LiveMap } from '../components/LiveMap';
import { PageHeader } from '../components/PageHeader';
import { useLiveStream } from '../hooks/useLiveStream';
import { useGetCoverageQuery, useListSessionsQuery } from '../store/api';
import { useAppSelector } from '../store/hooks';
import { useSession } from '../store/session';
import type { RecentDetection } from '../store/liveMapSlice';

const TYPE_COLORS: Record<string, string> = {
  AP: '#e03131',
  BLUETOOTH: '#1971c2',
  BT_LE: '#2f9e44',
  CLIENT: '#f08c00',
};

function DetectionFeed() {
  const { recent, detectionCount } = useAppSelector((s) => s.liveMap);

  return (
    <Card p="md" style={{ flex: '1 1 30%', minWidth: 260 }}>
      <Group justify="space-between" mb="sm">
        <Text fw={600}>Live detections</Text>
        <Badge color="teal" variant="light">
          {detectionCount}
        </Badge>
      </Group>
      <Stack gap={6} mah={480} style={{ overflowY: 'auto' }}>
        {recent.length === 0 && (
          <Text size="sm" c="dimmed">
            Waiting for detections… No GPS on the scanner, so devices can't be plotted yet — the
            feed below shows everything the card hears.
          </Text>
        )}
        {recent.map((d: RecentDetection, i: number) => (
          <Group key={`${d.mac}-${i}`} gap="xs" justify="space-between" wrap="nowrap">
            <Group gap={6} style={{ minWidth: 0 }}>
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: '50%',
                  background: TYPE_COLORS[d.type] ?? '#868e96',
                  flexShrink: 0,
                }}
              />
              <Text size="sm" fw={500} truncate style={{ maxWidth: 180 }}>
                {d.ssid || d.mac}
              </Text>
              <Text size="xs" c="dimmed" ff="monospace">
                {d.type}
              </Text>
            </Group>
            <Text size="xs" c="dimmed" style={{ flexShrink: 0 }}>
              {d.signalDbm} dBm
            </Text>
          </Group>
        ))}
      </Stack>
    </Card>
  );
}

export function LiveMapPage() {
  const { activeSessionId, setActiveSessionId } = useSession();
  const { data: sessions } = useListSessionsQuery();
  const { detectionCount, batchCount } = useAppSelector((s) => s.liveMap);
  const [showCoverage, setShowCoverage] = useState(false);
  const { data: coverage } = useGetCoverageQuery(activeSessionId ?? '', {
    skip: !activeSessionId || !showCoverage,
  });

  useLiveStream(activeSessionId);

  return (
    <>
      <PageHeader
        title="Live map"
        subtitle="Live detections stream over gRPC-Web; colors = AP / Bluetooth / BLE / client."
        actions={
          <Group gap="xs">
            <Badge color={detectionCount > 0 ? 'teal' : 'gray'} variant="light" leftSection={<IconRadar size={12} />}>
              {detectionCount} detections · {batchCount} batches
            </Badge>
            <Switch
              label="Coverage heatmap"
              checked={showCoverage}
              onChange={(e) => setShowCoverage(e.currentTarget.checked)}
            />
            <Select
              data={(sessions ?? []).map((s) => ({ value: s.id, label: s.name }))}
              value={activeSessionId}
              onChange={setActiveSessionId}
              placeholder="Session (all if empty)"
              clearable
              searchable
              w={240}
              leftSection={<IconMap2 size={16} />}
              aria-label="Active session"
            />
          </Group>
        }
      />
      <Group align="start" gap="md">
        <Box style={{ flex: '1 1 70%', minWidth: 0 }}>
          <LiveMap coverage={showCoverage ? coverage?.points : undefined} />
        </Box>
        <DetectionFeed />
      </Group>
    </>
  );
}
