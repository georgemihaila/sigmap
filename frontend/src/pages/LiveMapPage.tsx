import { Badge, Group, Select, Switch } from '@mantine/core';
import { IconMap2, IconRadar } from '@tabler/icons-react';
import { useState } from 'react';
import { LiveMap } from '../components/LiveMap';
import { PageHeader } from '../components/PageHeader';
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
    <>
      <PageHeader
        title="Live map"
        subtitle="Live detections stream over gRPC-Web; colors = AP / Bluetooth / BLE / client."
        actions={
          <Group gap="xs">
            <Badge color={liveCount > 0 ? 'teal' : 'gray'} variant="light" leftSection={<IconRadar size={12} />}>
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
              w={240}
              leftSection={<IconMap2 size={16} />}
              aria-label="Active session"
            />
          </Group>
        }
      />
      <LiveMap coverage={showCoverage ? coverage?.points : undefined} />
    </>
  );
}
