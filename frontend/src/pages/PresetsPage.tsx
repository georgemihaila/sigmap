import {
  ActionIcon,
  Badge,
  Button,
  Card,
  Group,
  Modal,
  Stack,
  Table,
  Text,
  TextInput,
  Textarea,
  Tooltip,
} from '@mantine/core';
import { IconPlus, IconTrash, IconVersions, IconWorld } from '@tabler/icons-react';
import { useState } from 'react';
import { ConfigEditor, stateToConfig, toState } from '../components/ConfigEditor';
import { ConfigDiffPreview } from '../components/ConfigDiffPreview';
import { PageHeader } from '../components/PageHeader';
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
    <>
      <PageHeader
        title="Presets"
        subtitle="Reusable scan configurations applied to devices or the whole fleet."
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
            New preset
          </Button>
        }
      />

      <Modal opened={creating} onClose={() => setCreating(false)} title="New preset" size="lg">
        <Stack>
          <TextInput label="Name" value={name} onChange={(e) => setName(e.currentTarget.value)} />
          <Textarea label="Description" value={description} onChange={(e) => setDescription(e.currentTarget.value)} />
          <ConfigEditor initial={toState(draft)} onChange={(s) => setDraft(stateToConfig(s))} />
          <Button onClick={savePreset} disabled={!name.trim()}>Save preset</Button>
        </Stack>
      </Modal>

      <Card p={0}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              <Table.Th>Origin</Table.Th>
              <Table.Th>Updated</Table.Th>
              <Table.Th ta="right" />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {presets?.map((p) => (
              <Table.Tr key={p.id}>
                <Table.Td>
                  <Text fw={600}>{p.name}</Text>
                  {p.description && <Text size="xs" c="dimmed">{p.description}</Text>}
                </Table.Td>
                <Table.Td>
                  {p.isBuiltin ? <Badge color="blue">built-in</Badge> : <Badge color="gray" variant="light">user</Badge>}
                </Table.Td>
                <Table.Td>{new Date(p.updatedAt).toLocaleString()}</Table.Td>
                <Table.Td>
                  <Group gap="xs" justify="flex-end">
                    <Button
                      size="compact-sm"
                      variant="light"
                      leftSection={<IconWorld size={14} />}
                      onClick={() => applyToAll(p)}
                      disabled={!activeSessionId}
                    >
                      Apply to fleet
                    </Button>
                    <Button
                      size="compact-sm"
                      variant="light"
                      leftSection={<IconVersions size={14} />}
                      onClick={() => setDiffFor({ preset: p, deviceId: devices?.[0]?.id ?? '' })}
                      disabled={!devices?.length}
                    >
                      Diff
                    </Button>
                    {!p.isBuiltin && (
                      <Tooltip label={`Delete ${p.name}`}>
                        <ActionIcon
                          color="red"
                          variant="subtle"
                          onClick={() => deletePreset(p.id)}
                          aria-label={`Delete ${p.name}`}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Tooltip>
                    )}
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
            {presets?.length === 0 && (
              <Table.Tr>
                <Table.Td colSpan={4}>
                  <Text c="dimmed" py="sm" ta="center">
                    No presets yet.
                  </Text>
                </Table.Td>
              </Table.Tr>
            )}
          </Table.Tbody>
        </Table>
      </Card>

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
    </>
  );
}
