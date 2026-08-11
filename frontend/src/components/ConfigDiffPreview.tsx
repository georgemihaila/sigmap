import { Button, Group, Stack, Table, Text, Badge } from '@mantine/core';
import { ScanConfig } from '../generated/config_pb';
import { diffScanConfig, type ConfigFieldDiff } from '../store/configDiff';
import { useGetDeviceConfigQuery } from '../store/api';

export function ConfigDiffList({ diffs }: { diffs: ConfigFieldDiff[] }) {
  if (diffs.length === 0) {
    return <Text c="dimmed">No differences — the device already matches this preset.</Text>;
  }
  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>Field</Table.Th>
          <Table.Th>Current</Table.Th>
          <Table.Th>Preset</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {diffs.map((d) => (
          <Table.Tr key={d.field}>
            <Table.Td><Badge variant="light" color="blue">{d.field}</Badge></Table.Td>
            <Table.Td>{formatValue(d.from)}</Table.Td>
            <Table.Td><Text fw={600}>{formatValue(d.to)}</Text></Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}

function formatValue(value: unknown): string {
  if (typeof value === 'boolean') return value ? 'on' : 'off';
  if (Array.isArray(value)) return value.length ? value.map((i) => (typeof i === 'object' ? (i as { name?: string }).name ?? JSON.stringify(i) : i)).join(', ') : 'none';
  if (value === null || value === undefined) return '—';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

interface Props {
  presetConfig: string;
  deviceId: string;
  sessionId: string | null;
  onApply: (configJson: string) => void;
}

export function ConfigDiffPreview({ presetConfig, deviceId, sessionId, onApply }: Props) {
  const { data: deviceConfig } = useGetDeviceConfigQuery(
    { sessionId: sessionId ?? '', deviceId },
    { skip: !sessionId },
  );

  const preset = ScanConfig.fromJson(JSON.parse(presetConfig));
  const current = deviceConfig?.configJson
    ? ScanConfig.fromJson(JSON.parse(deviceConfig.configJson))
    : null;

  const diffs = current ? diffScanConfig(current, preset) : [{ field: 'config', from: null, to: 'no current config' } as ConfigFieldDiff];

  return (
    <Stack>
      <ConfigDiffList diffs={diffs} />
      <Group justify="flex-end">
        <Button onClick={() => onApply(JSON.stringify(preset.toJson()))}>
          Apply preset to device
        </Button>
      </Group>
    </Stack>
  );
}
