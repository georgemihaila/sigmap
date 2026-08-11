import {
  Badge,
  Button,
  Card,
  Drawer,
  Select,
  Stack,
  Table,
  Text,
} from '@mantine/core';
import { IconPencil } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { ConfigEditor, stateToConfig, toState } from '../components/ConfigEditor';
import { PageHeader } from '../components/PageHeader';
import { timeAgo } from '../lib/time';
import {
  useApplyConfigMutation,
  useGetDeviceConfigQuery,
  useListDevicesQuery,
  useListPresetsQuery,
  type Device,
} from '../store/api';
import { useSession } from '../store/session';
import { ScanConfig } from '../generated/config_pb';

function useNow(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

function DeviceRow({
  device,
  sessionId,
  now,
}: {
  device: Device;
  sessionId: string | null;
  now: number;
}) {
  const { data: presets } = useListPresetsQuery();
  const [applyConfig] = useApplyConfigMutation();
  const [openEditor, setOpenEditor] = useState(false);
  const { data: config } = useGetDeviceConfigQuery(
    { sessionId: sessionId ?? '', deviceId: device.id },
    { skip: !sessionId },
  );

  const lastSeen = device.lastHeartbeatAt ? new Date(device.lastHeartbeatAt) : null;
  const stale = lastSeen ? now - lastSeen.getTime() > 60_000 : true;
  const drift = !!sessionId && config?.pushState === 'Pending';

  const applyPreset = async (presetId: string | null) => {
    if (!sessionId || !presetId) return;
    const preset = presets?.find((p) => p.id === presetId);
    if (!preset) return;
    await applyConfig({ sessionId, deviceId: device.id, config: preset.configJson, presetId });
  };

  return (
    <>
      <Table.Tr>
        <Table.Td>
          <Text fw={600}>{device.name}</Text>
          <Text size="xs" c="dimmed">{device.platform || 'unknown platform'}</Text>
        </Table.Td>
        <Table.Td>
          <Badge color={stale ? 'red' : device.status === 'Online' ? 'teal' : 'gray'}>
            {stale ? 'Offline' : device.status}
          </Badge>
        </Table.Td>
        <Table.Td>
          <Text size="sm">{timeAgo(device.lastHeartbeatAt, now)}</Text>
        </Table.Td>
        <Table.Td>
          {drift ? (
            <Badge color="orange">config not acked</Badge>
          ) : (
            <Badge color="green" variant="light">synced</Badge>
          )}
        </Table.Td>
        <Table.Td>
          <Select
            data={(presets ?? []).filter((p) => !p.isBuiltin).map((p) => ({ value: p.id, label: p.name }))}
            value={config?.presetId ?? null}
            onChange={applyPreset}
            placeholder="Apply preset"
            clearable
            w={180}
            aria-label={`Preset for ${device.name}`}
          />
        </Table.Td>
        <Table.Td ta="right">
          <Button
            size="compact-sm"
            variant="light"
            leftSection={<IconPencil size={14} />}
            onClick={() => setOpenEditor(true)}
          >
            Edit config
          </Button>
        </Table.Td>
      </Table.Tr>

      <Drawer opened={openEditor} onClose={() => setOpenEditor(false)} title={`Config — ${device.name}`}>
        {sessionId && config?.configJson ? (
          <ConfigEditorShell
            initialConfig={config.configJson}
            onPush={(config) => applyConfig({ sessionId, deviceId: device.id, config, presetId: null })}
          />
        ) : (
          <Text c="dimmed">No config pushed for this device yet.</Text>
        )}
      </Drawer>
    </>
  );
}

function ConfigEditorShell({
  initialConfig,
  onPush,
}: {
  initialConfig: string;
  onPush: (configJson: string) => void;
}) {
  const [config, setConfig] = useState<ScanConfig>(() => ScanConfig.fromJson(JSON.parse(initialConfig)));
  return (
    <Stack>
      <ConfigEditor
        initial={toState(config)}
        onChange={(state) => setConfig(stateToConfig(state))}
      />
      <Button onClick={() => onPush(JSON.stringify(config.toJson()))}>Push config</Button>
    </Stack>
  );
}

export function FleetPage() {
  const { data: devices } = useListDevicesQuery();
  const { activeSessionId } = useSession();
  const now = useNow(30_000);

  return (
    <>
      <PageHeader
        title="Device fleet"
        subtitle={`${devices?.length ?? 0} devices · live status, config drift and preset switching`}
      />
      <Card p={0}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Device</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Last heartbeat</Table.Th>
              <Table.Th>Config</Table.Th>
              <Table.Th>Preset</Table.Th>
              <Table.Th ta="right" />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {devices?.map((d) => (
              <DeviceRow key={d.id} device={d} sessionId={activeSessionId} now={now} />
            ))}
            {devices?.length === 0 && (
              <Table.Tr>
                <Table.Td colSpan={6}>
                  <Text c="dimmed" py="sm" ta="center">
                    No devices paired yet. Scan a pairing QR to add one.
                  </Text>
                </Table.Td>
              </Table.Tr>
            )}
          </Table.Tbody>
        </Table>
      </Card>
    </>
  );
}
