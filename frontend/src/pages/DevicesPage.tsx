import { Badge, Button, Card, Drawer, Group, Stack, Table, Text, Title } from '@mantine/core';
import { IconArrowsDown, IconAntenna, IconMapPin } from '@tabler/icons-react';
import { useState } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { PageHeader } from '../components/PageHeader';
import { useGetSignalSeriesQuery, useListDetectedDevicesQuery, type DetectedDevice } from '../store/api';

const TYPE_LABEL: Record<number, string> = { 1: 'AP', 2: 'Bluetooth', 3: 'BLE', 4: 'Client' };
const TYPE_COLOR: Record<number, string> = { 1: 'blue', 2: 'indigo', 3: 'teal', 4: 'orange' };

function SignalChart({ mac }: { mac: string }) {
  const { data: points, isLoading } = useGetSignalSeriesQuery(mac);
  const data = (points ?? []).map((p) => ({
    time: new Date(p.at).toLocaleTimeString(),
    dBm: p.signalDbm,
  }));
  return (
    <Card p="md">
      <Title order={5} mb="sm">
        Signal strength over time
      </Title>
      {isLoading && <Text c="dimmed">Loading…</Text>}
      {!isLoading && data.length === 0 && <Text c="dimmed">No signal history yet.</Text>}
      {data.length > 0 && (
        <div style={{ height: 260, width: '100%' }}>
          <ResponsiveContainer>
            <LineChart data={data}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--mantine-color-dark-4)" />
              <XAxis dataKey="time" stroke="var(--mantine-color-dimmed)" tick={{ fontSize: 11 }} />
              <YAxis
                domain={[-100, -20]}
                stroke="var(--mantine-color-dimmed)"
                tick={{ fontSize: 11 }}
              />
              <Tooltip
                contentStyle={{
                  background: 'var(--mantine-color-dark-6)',
                  border: '1px solid var(--mantine-color-dark-4)',
                  borderRadius: '0.5rem',
                }}
              />
              <Line type="monotone" dataKey="dBm" stroke="var(--mantine-color-teal-5)" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </Card>
  );
}

function DeviceDetail({ device, onClose }: { device: DetectedDevice; onClose: () => void }) {
  return (
    <Drawer opened onClose={onClose} title={device.ssidLatest ?? device.mac}>
      <Stack>
        <Group>
          <Badge color={TYPE_COLOR[device.deviceType]}>{TYPE_LABEL[device.deviceType] ?? 'Unknown'}</Badge>
          {device.vendorName && <Badge color="gray" variant="light">{device.vendorName}</Badge>}
        </Group>
        <Text size="sm"><strong>MAC:</strong> {device.mac}</Text>
        <Text size="sm"><strong>First seen:</strong> {new Date(device.firstSeenAt).toLocaleString()}</Text>
        <Text size="sm"><strong>Last seen:</strong> {new Date(device.lastSeenAt).toLocaleString()}</Text>
        <Text size="sm">
          <strong>Position:</strong>{' '}
          {device.latitude != null
            ? `${device.latitude.toFixed(5)}, ${device.longitude?.toFixed(5)}`
            : 'unknown'}
        </Text>
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
    <>
      <PageHeader
        title="Detected devices"
        subtitle={`${data?.items.length ?? 0} loaded · keyset pagination`}
      />
      <Card p={0}>
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
                <Table.Td>
                  <Group gap={6}>
                    <IconAntenna size={16} color="var(--mantine-color-teal-5)" />
                    <Text fw={600}>{d.ssidLatest ?? '—'}</Text>
                  </Group>
                </Table.Td>
                <Table.Td>
                  <Text ff="monospace" size="sm">{d.mac}</Text>
                </Table.Td>
                <Table.Td>
                  <Badge color={TYPE_COLOR[d.deviceType]} variant="light">
                    {TYPE_LABEL[d.deviceType] ?? d.deviceType}
                  </Badge>
                </Table.Td>
                <Table.Td>{d.vendorName ?? '—'}</Table.Td>
                <Table.Td>
                  <Group gap={4}>
                    {d.latitude != null && <IconMapPin size={14} color="var(--mantine-color-dimmed)" />}
                    <Text size="sm">{new Date(d.lastSeenAt).toLocaleString()}</Text>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
            {data?.items.length === 0 && (
              <Table.Tr>
                <Table.Td colSpan={5}>
                  <Text c="dimmed" py="sm" ta="center">
                    No devices detected yet.
                  </Text>
                </Table.Td>
              </Table.Tr>
            )}
          </Table.Tbody>
        </Table>
      </Card>

      {data?.nextCursor && (
        <Group justify="center">
          <Button
            variant="light"
            leftSection={<IconArrowsDown size={16} />}
            onClick={loadMore}
            loading={isFetching}
          >
            Load more
          </Button>
        </Group>
      )}

      {selected && <DeviceDetail device={selected} onClose={() => setSelected(null)} />}
    </>
  );
}
