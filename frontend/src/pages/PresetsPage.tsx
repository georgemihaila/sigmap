import {
  ActionIcon,
  Badge,
  Button,
  Group,
  Modal,
  Stack,
  Table,
  Text,
  TextInput,
  Textarea,
  Title,
} from '@mantine/core';
import { useState } from 'react';
import { ConfigEditor, stateToConfig, toState } from '../components/ConfigEditor';
import { ConfigDiffPreview } from '../components/ConfigDiffPreview';
import {
  useApplyConfigMutation,
  useCreatePresetMutation,
  useDeletePresetMutation,
  useListDevicesQuery,
  useListPresetsQuery,
  type ScanPreset,
} from '../store/api';
import { useSession } from '../store/session';
import { ScanConfig } from '../generated/config_pb';

export function PresetsPage() {
  const { data: presets } = useListPresetsQuery();
  const { data: devices } = useListDevicesQuery();
  const { activeSessionId } = useSession();
  const [createPreset] = useCreatePresetMutation();
  const [deletePreset] = useDeletePresetMutation();
  const [applyConfig] = useApplyConfigMutation();

  const [creating, setCreating] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [draft, setDraft] = useState<ScanConfig>(new ScanConfig({
    channelHopMs: 500,
    batchIntervalMs: 1500,
  }));

  const [diffFor, setDiffFor] = useState<{ preset: ScanPreset; deviceId: string } | null>(null);

  const savePreset = async () => {
    if (!name.trim()) return;
    await createPreset({
      name: name.trim(),
      description: description || undefined,
      configJson: JSON.stringify(draft.toJson()),
    });
    setName('');
    setDescription('');
    setCreating(false);
  };

  const applyToAll = async (preset: ScanPreset) => {
    if (!activeSessionId) return;
    for (const device of devices ?? []) {
      await applyConfig({
        sessionId: activeSessionId,
        deviceId: device.id,
        config: preset.configJson,
        presetId: preset.id,
      });
    }
  };

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Presets</Title>
        <Button onClick={() => setCreating(true)}>New preset</Button>
      </Group>

      <Modal opened={creating} onClose={() => setCreating(false)} title="New preset">
        <Stack>
          <TextInput label="Name" value={name} onChange={(e) => setName(e.currentTarget.value)} />
          <Textarea label="Description" value={description} onChange={(e) => setDescription(e.currentTarget.value)} />
          <ConfigEditor initial={toState(draft)} onChange={(s) => setDraft(stateToConfig(s))} />
          <Button onClick={savePreset} disabled={!name.trim()}>Save preset</Button>
        </Stack>
      </Modal>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Built-in</Table.Th>
            <Table.Th>Updated</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {presets?.map((p) => (
            <Table.Tr key={p.id}>
              <Table.Td>
                <Text fw={500}>{p.name}</Text>
                {p.description && <Text size="xs" c="dimmed">{p.description}</Text>}
              </Table.Td>
              <Table.Td>{p.isBuiltin ? <Badge color="blue">built-in</Badge> : <Badge color="gray" variant="light">user</Badge>}</Table.Td>
              <Table.Td>{new Date(p.updatedAt).toLocaleString()}</Table.Td>
              <Table.Td>
                <Group gap="xs">
                  <Button size="compact-xs" variant="light" onClick={() => applyToAll(p)} disabled={!activeSessionId}>
                    Apply to fleet
                  </Button>
                  <Button
                    size="compact-xs"
                    variant="light"
                    onClick={() => setDiffFor({ preset: p, deviceId: devices?.[0]?.id ?? '' })}
                    disabled={!devices?.length}
                  >
                    Diff
                  </Button>
                  {!p.isBuiltin && (
                    <ActionIcon color="red" variant="subtle" onClick={() => deletePreset(p.id)} aria-label={`Delete ${p.name}`}>
                      ✕
                    </ActionIcon>
                  )}
                </Group>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      <Modal
        opened={diffFor !== null}
        onClose={() => setDiffFor(null)}
        title={`Diff — ${diffFor?.preset.name ?? ''}`}
        size="lg"
      >
        {diffFor && (
          <ConfigDiffPreview
            presetConfig={diffFor.preset.configJson}
            deviceId={diffFor.deviceId}
            sessionId={activeSessionId}
            onApply={(configJson) =>
              applyConfig({ sessionId: diffFor.deviceId ? activeSessionId ?? '' : '', deviceId: diffFor.deviceId, config: configJson, presetId: diffFor.preset.id })
            }
          />
        )}
      </Modal>
    </Stack>
  );
}
