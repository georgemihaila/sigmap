import {
  ActionIcon,
  Badge,
  Button,
  Divider,
  Group,
  Modal,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { useRef, useState } from 'react';
import {
  useCompareSessionsQuery,
  useCreateSessionMutation,
  useImportGpxMutation,
  useListSessionsQuery,
} from '../store/api';
import { useSession } from '../store/session';

const STATUS_LABEL: Record<number, string> = {
  0: 'Planned',
  1: 'Active',
  2: 'Archived',
};

export function SessionsPage() {
  const { data: sessions, isLoading } = useListSessionsQuery();
  const [createSession] = useCreateSessionMutation();
  const [importGpx] = useImportGpxMutation();
  const { activeSessionId, setActiveSessionId } = useSession();

  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');

  const [compareA, setCompareA] = useState<string | null>(null);
  const [compareB, setCompareB] = useState<string | null>(null);
  const { data: comparison } = useCompareSessionsQuery(
    { a: compareA ?? '', b: compareB ?? '' },
    { skip: !compareA || !compareB },
  );

  const [gpxSessionId, setGpxSessionId] = useState<string | null>(null);
  const [gpxMessage, setGpxMessage] = useState<string | null>(null);
  const gpxInput = useRef<HTMLInputElement>(null);

  const sessionOptions = (sessions ?? []).map((s) => ({ value: s.id, label: s.name }));

  const submit = async () => {
    if (!name.trim()) return;
    await createSession({ name: name.trim() });
    setName('');
    setOpen(false);
  };

  const onGpxFile = async (file: File | undefined) => {
    if (!file || !gpxSessionId) return;
    const xml = await file.text();
    const n = await importGpx({ sessionId: gpxSessionId, xml }).unwrap();
    setGpxMessage(`Imported ${n} planned route points for coverage comparison.`);
  };

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Sessions</Title>
        <Button onClick={() => setOpen(true)}>New session</Button>
      </Group>

      <Modal opened={open} onClose={() => setOpen(false)} title="New session">
        <Stack>
          <TextInput label="Name" value={name} onChange={(e) => setName(e.currentTarget.value)} />
          <Button onClick={submit} disabled={!name.trim()}>Create</Button>
        </Stack>
      </Modal>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Created</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {sessions?.map((s) => (
            <Table.Tr key={s.id} style={s.id === activeSessionId ? { background: 'var(--mantine-color-blue-light)' } : undefined}>
              <Table.Td>
                <ActionIcon
                  variant="subtle"
                  onClick={() => setActiveSessionId(s.id)}
                  aria-label={`Focus ${s.name}`}
                >
                  <Text fw={600}>{s.name}</Text>
                </ActionIcon>
              </Table.Td>
              <Table.Td>
                <Badge color={s.status === 2 ? 'gray' : 'blue'}>{STATUS_LABEL[s.status] ?? s.status}</Badge>
              </Table.Td>
              <Table.Td>{new Date(s.createdAt).toLocaleString()}</Table.Td>
              <Table.Td>
                <Button size="compact-xs" variant="light" onClick={() => setActiveSessionId(s.id)}>
                  Focus
                </Button>
              </Table.Td>
            </Table.Tr>
          ))}
          {!isLoading && sessions?.length === 0 && (
            <Table.Tr>
              <Table.Td colSpan={4}>
                <Text c="dimmed">No sessions yet. Create one to start a campaign.</Text>
              </Table.Td>
            </Table.Tr>
          )}
        </Table.Tbody>
      </Table>

      <Divider />

      <Stack gap="sm">
        <Title order={4}>Compare sessions</Title>
        <Group>
          <Select data={sessionOptions} value={compareA} onChange={setCompareA} placeholder="Session A" searchable w={220} />
          <Select data={sessionOptions} value={compareB} onChange={setCompareB} placeholder="Session B" searchable w={220} />
          {comparison && (
            <Text size="sm">
              <Badge color="teal" variant="light">both: {comparison.inBoth}</Badge>{' '}
              <Badge color="red" variant="light">only A: {comparison.onlyInA}</Badge>{' '}
              <Badge color="blue" variant="light">only B: {comparison.onlyInB}</Badge>
            </Text>
          )}
        </Group>

        <Group>
          <Select
            data={sessionOptions}
            value={gpxSessionId}
            onChange={setGpxSessionId}
            placeholder="Session for planned route"
            searchable
            w={260}
          />
          <Button variant="light" onClick={() => gpxInput.current?.click()}>
            Import GPX route
          </Button>
          <input
            ref={gpxInput}
            type="file"
            accept=".gpx"
            style={{ display: 'none' }}
            onChange={(e) => void onGpxFile(e.target.files?.[0])}
          />
          {gpxMessage && <Text size="sm" c="dimmed">{gpxMessage}</Text>}
        </Group>
      </Stack>
    </Stack>
  );
}
