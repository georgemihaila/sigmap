import {
  Alert,
  Badge,
  Button,
  Card,
  Drawer,
  Group,
  Loader,
  Modal,
  Select,
  Stack,
  Table,
  Text,
  Tooltip,
} from '@mantine/core';
import { IconPencil, IconSettings } from '@tabler/icons-react';
import { useEffect, useRef, useState } from 'react';
import { ConfigEditor, stateToConfig, toState } from '../components/ConfigEditor';
import { PageHeader } from '../components/PageHeader';
import { timeAgo } from '../lib/time';
import {
  useApplyConfigMutation,
  useApplyFleetConfigMutation,
  useGetDeviceConfigQuery,
  useListDevicesQuery,
  useListPresetsQuery,
  useListSessionsQuery,
  type Device,
} from '../store/api';
import { useSession } from '../store/session';
import { ScanConfig } from '../generated/config_pb';

function defaultScanConfig(): ScanConfig {
  return new ScanConfig({ channelHopMs: 500, batchIntervalMs: 1500 });
}

function mergeInterfaces(configJson: string, current: string[]): string {
  if (current.length === 0) return configJson;
  try {
    const parsed = JSON.parse(configJson) as {
      interfaces?: Array<{ name: string; monitorMode?: boolean; enabled?: boolean }>;
    };
    if (parsed.interfaces && parsed.interfaces.length > 0) return configJson;
    parsed.interfaces = current.map((name) => ({ name, monitorMode: true, enabled: true }));
    return JSON.stringify(parsed);
  } catch {
    return configJson;
  }
}

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

  const [applying, setApplying] = useState(false);
  const [applyError, setApplyError] = useState<string | null>(null);

  const currentInterfaces = (): string[] => {
    if (!config?.configJson) return [];
    try {
      const parsed = JSON.parse(config.configJson) as { interfaces?: Array<{ name: string }> };
      return (parsed.interfaces ?? []).map((i) => i.name);
    } catch {
      return [];
    }
  };

  const applyPreset = async (presetId: string | null) => {
    if (!sessionId || !presetId) return;
    const preset = presets?.find((p) => p.id === presetId);
    if (!preset) return;
    const merged = mergeInterfaces(preset.configJson, currentInterfaces());
    setApplyError(null);
    setApplying(true);
    try {
      await applyConfig({ sessionId, deviceId: device.id, config: merged, presetId }).unwrap();
    } catch {
      setApplyError('Failed to apply preset');
    } finally {
      setApplying(false);
    }
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
          <Stack gap={4}>
            <Tooltip label={sessionId ? undefined : 'Select a session to apply presets'} disabled={!!sessionId}>
              <Select
                data={(presets ?? []).filter((p) => !p.isBuiltin).map((p) => ({ value: p.id, label: p.name }))}
                value={config?.presetId ?? null}
                onChange={applyPreset}
                placeholder="Apply preset"
                clearable
                disabled={!sessionId}
                w={180}
                aria-label={`Preset for ${device.name}`}
              />
            </Tooltip>
            {applyError && (
              <Text size="xs" c="red">
                {applyError}
              </Text>
            )}
            {applying && (
              <Text size="xs" c="dimmed">
                Pushing…
              </Text>
            )}
          </Stack>
        </Table.Td>
        <Table.Td ta="right">
          <Tooltip label={sessionId ? undefined : 'Select a session to edit config'} disabled={!!sessionId}>
            <Button
              size="compact-sm"
              variant="light"
              leftSection={<IconPencil size={14} />}
              disabled={!sessionId}
              onClick={() => setOpenEditor(true)}
            >
              Edit config
            </Button>
          </Tooltip>
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

function FleetConfigModal({
  opened,
  onClose,
  sessionId,
}: {
  opened: boolean;
  onClose: () => void;
  sessionId: string | null;
}) {
  const { data: devices } = useListDevicesQuery();
  const [applyFleetConfig, { isLoading: applying }] = useApplyFleetConfigMutation();
  const firstDeviceId = devices?.[0]?.id;
  const { data: firstConfig, isLoading: configLoading } = useGetDeviceConfigQuery(
    { sessionId: sessionId ?? '', deviceId: firstDeviceId ?? '' },
    { skip: !sessionId || !firstDeviceId },
  );

  const [config, setConfig] = useState<ScanConfig>(() => defaultScanConfig());
  const [message, setMessage] = useState<string | null>(null);
  const [applied, setApplied] = useState<number | null>(null);
  const didInit = useRef(false);

  const ready = opened && !configLoading;

  useEffect(() => {
    if (opened && ready && !didInit.current) {
      didInit.current = true;
      setMessage(null);
      setApplied(null);
      setConfig(
        firstConfig?.configJson
          ? ScanConfig.fromJson(JSON.parse(firstConfig.configJson))
          : defaultScanConfig(),
      );
    }
    if (!opened) didInit.current = false;
  }, [opened, ready, firstConfig]);

  const submit = async () => {
    if (!sessionId) return;
    const res = await applyFleetConfig({
      sessionId,
      config: JSON.stringify(config.toJson()),
      presetId: null,
    }).unwrap();
    setApplied(res.deviceCount);
    setMessage(`Config pushed to ${res.deviceCount} device${res.deviceCount === 1 ? '' : 's'} in the active session.`);
  };

  return (
    <Modal opened={opened} onClose={onClose} title="Reconfigure fleet" size="lg">
      {!ready ? (
        <Group justify="center" py="xl">
          <Loader />
        </Group>
      ) : (
        <Stack>
          <Text size="sm" c="dimmed">
            Edits apply to every device currently assigned to the active session. Devices outside
            the session are left untouched.
          </Text>
          <ConfigEditor
            initial={toState(config)}
            onChange={(s) => setConfig(stateToConfig(s))}
          />
          {message && (
            <Text size="sm" c={applied != null ? 'teal' : 'red'}>
              {message}
            </Text>
          )}
          <Group justify="flex-end">
            <Button variant="light" onClick={onClose}>
              Close
            </Button>
            <Button
              leftSection={<IconSettings size={16} />}
              loading={applying}
              onClick={() => void submit()}
            >
              Push to fleet
            </Button>
          </Group>
        </Stack>
      )}
    </Modal>
  );
}

export function FleetPage() {
  const { data: devices } = useListDevicesQuery();
  const { data: sessions } = useListSessionsQuery();
  const { activeSessionId, setActiveSessionId } = useSession();
  const now = useNow(30_000);
  const [reconfigOpen, setReconfigOpen] = useState(false);

  return (
    <>
      <PageHeader
        title="Device fleet"
        subtitle={`${devices?.length ?? 0} devices · live status, config drift and preset switching`}
        actions={
          <Group gap="xs">
            <Select
              data={(sessions ?? []).map((s) => ({ value: s.id, label: s.name }))}
              value={activeSessionId}
              onChange={setActiveSessionId}
              placeholder="Active session"
              clearable
              searchable
              w={220}
              aria-label="Active session"
            />
            <Button
              leftSection={<IconSettings size={16} />}
              disabled={!activeSessionId}
              onClick={() => setReconfigOpen(true)}
            >
              Reconfigure fleet
            </Button>
          </Group>
        }
      />
      {!activeSessionId && (
        <Alert color="yellow" variant="light">
          No active session selected. Pick one above to apply presets, edit configs or reconfigure
          the fleet.
        </Alert>
      )}
      <FleetConfigModal
        opened={reconfigOpen}
        onClose={() => setReconfigOpen(false)}
        sessionId={activeSessionId}
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
