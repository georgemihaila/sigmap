import { Badge, Group, Select, Stack, Switch, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { LiveMap } from '../components/LiveMap';
import { useLiveStream } from '../hooks/useLiveStream';
import { useGetCoverageQuery, useListSessionsQuery } from '../store/api';
import { useAppSelector } from '../store/hooks';
import { useSession } from '../store/session';

export function LiveMapPage() {
  const { activeSessionId, setActiveSessionId } = useSession();
  const { data: sessions } = useListSessionsQuery();
  const liveCount = useAppSelector((s) => Object.keys(s.liveMap.points).length);
  const [showCoverage, setShowCoverage] = useState(false);
  const { data: coverage } = useGetCoverageQuery(activeSessionId ?? '', {
    skip: !activeSessionId || !showCoverage,
  });

  useLiveStream(activeSessionId);

  return (
    <Stack gap="sm">
      <Group justify="space-between">
        <Title order={2}>Live map</Title>
        <Group>
          <Badge color={liveCount > 0 ? 'teal' : 'gray'} variant="light">
            {liveCount} live devices
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
            w={260}
            aria-label="Active session"
          />
        </Group>
      </Group>
      <Text size="sm" c="dimmed">
        Live detections stream over gRPC-Web; colors = AP / Bluetooth / BLE / client.
        Coverage shows the physical areas actually scanned in the selected session.
      </Text>
      <LiveMap coverage={showCoverage ? coverage?.points : undefined} />
    </Stack>
  );
}
