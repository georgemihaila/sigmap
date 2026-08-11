import { Button, Drawer, Group, Stack, Table, Text, Title, Badge } from '@mantine/core';
import { useState } from 'react';
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useGetSignalSeriesQuery, useListDetectedDevicesQuery, type DetectedDevice } from '../store/api';

const TYPE_LABEL: Record<number, string> = { 1: 'AP', 2: 'Bluetooth', 3: 'BLE', 4: 'Client' };

function SignalChart({ mac }: { mac: string }) {
  const { data: points, isLoading } = useGetSignalSeriesQuery(mac);
  const data = (points ?? []).map((p) => ({
    time: new Date(p.at).toLocaleTimeString(),
    dBm: p.signalDbm,
  }));
  return (
    <Stack>
      <Title order={5}>Signal strength over time</Title>
      {isLoading && <Text c="dimmed">Loading…</Text>}
      {!isLoading && data.length === 0 && <Text c="dimmed">No signal history yet.</Text>}
      {data.length > 0 && (
        <div style={{ height: 260, width: '100%' }}>
          <ResponsiveContainer>
            <LineChart data={data}>
              <XAxis dataKey="time" />
              <YAxis domain={[-100, -20]} />
              <Tooltip />
              <Line type="monotone" dataKey="dBm" stroke="#1971c2" dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </Stack>
  );
}

function DeviceDetail({ device, onClose }: { device: DetectedDevice; onClose: () => void }) {
  return (
    <Drawer opened onClose={onClose} title={device.ssidLatest ?? device.mac} position="right" size="md">
      <Stack>
        <Group>
          <Badge>{TYPE_LABEL[device.deviceType] ?? 'Unknown'}</Badge>
          {device.vendorName && <Badge color="teal" variant="light">{device.vendorName}</Badge>}
        </Group>
        <Text size="sm"><strong>MAC:</strong> {device.mac}</Text>
        <Text size="sm"><strong>First seen:</strong> {new Date(device.firstSeenAt).toLocaleString()}</Text>
        <Text size="sm"><strong>Last seen:</strong> {new Date(device.lastSeenAt).toLocaleString()}</Text>
        <Text size="sm"><strong>Position:</strong> {device.latitude != null ? `${device.latitude.toFixed(5)}, ${device.longitude?.toFixed(5)}` : 'unknown'}</Text>
        <SignalChart mac={device.mac} />
      </Stack>
    </Drawer>
  );
}

export function DevicesPage() {
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [selected, setSelected] = useState<DetectedDevice | null>(null);
  const { data, isFetching } = useListDetectedDevicesQuery({ cursor, limit: 50 });

  const loadMore = () => {
    if (data?.nextCursor) setCursor(data.nextCursor);
  };

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Detected devices</Title>
        <Text size="sm" c="dimmed">{data?.items.length ?? 0} loaded (keyset pagination)</Text>
      </Group>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>SSID</Table.Th>
            <Table.Th>MAC</Table.Th>
            <Table.Th>Type</Table.Th>
            <Table.Th>Vendor</Table.Th>
            <Table.Th>Last seen</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {data?.items.map((d) => (
            <Table.Tr
              key={d.id}
              onClick={() => setSelected(d)}
              style={{ cursor: 'pointer' }}
            >
              <Table.Td>{d.ssidLatest ?? '—'}</Table.Td>
              <Table.Td>{d.mac}</Table.Td>
              <Table.Td>{TYPE_LABEL[d.deviceType] ?? d.deviceType}</Table.Td>
              <Table.Td>{d.vendorName ?? '—'}</Table.Td>
              <Table.Td>{new Date(d.lastSeenAt).toLocaleString()}</Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {data?.nextCursor && (
        <Group justify="center">
          <Button variant="light" onClick={loadMore} loading={isFetching}>
            Load more
          </Button>
        </Group>
      )}

      {selected && <DeviceDetail device={selected} onClose={() => setSelected(null)} />}
    </Stack>
  );
}
