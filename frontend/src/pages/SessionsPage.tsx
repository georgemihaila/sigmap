import {
  Badge,
  Button,
  Card,
  Divider,
  Group,
  Modal,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
  Tooltip,
} from '@mantine/core';
import { IconCrosshair, IconFileImport, IconPlus, IconTrash } from '@tabler/icons-react';
import { useRef, useState } from 'react';
import { PageHeader } from '../components/PageHeader';
import {
  useCompareSessionsQuery,
  useCreateSessionMutation,
  useDeleteSessionMutation,
  useImportGpxMutation,
  useListSessionsQuery,
  type Session,
} from '../store/api';
import { useSession } from '../store/session';

const STATUS: Record<number, { label: string; color: string }> = {
  0: { label: 'Planned', color: 'blue' },
  1: { label: 'Active', color: 'teal' },
  2: { label: 'Archived', color: 'gray' },
};

export function SessionsPage() {
  const { data: sessions, isLoading } = useListSessionsQuery();
  const [createSession] = useCreateSessionMutation();
  const [deleteSession, { isLoading: deleting }] = useDeleteSessionMutation();
  const [importGpx] = useImportGpxMutation();
  const { activeSessionId, setActiveSessionId } = useSession();

  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');

  const [confirmingDelete, setConfirmingDelete] = useState<Session | null>(null);

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

  const handleConfirmDelete = async () => {
    if (!confirmingDelete) return;
    await deleteSession(confirmingDelete.id).unwrap();
    if (activeSessionId === confirmingDelete.id) setActiveSessionId(null);
    setConfirmingDelete(null);
  };

  const onGpxFile = async (file: File | undefined) => {
    if (!file || !gpxSessionId) return;
    const xml = await file.text();
    const n = await importGpx({ sessionId: gpxSessionId, xml }).unwrap();
    setGpxMessage(`Imported ${n} planned route points for coverage comparison.`);
  };

  return (
    <>
      <PageHeader
        title="Sessions"
        subtitle="Plan, run and archive wardriving campaigns."
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setOpen(true)}>
            New session
          </Button>
        }
      />

      <Modal opened={open} onClose={() => setOpen(false)} title="New session">
        <Stack>
          <TextInput
            label="Name"
            placeholder="Summer drive 2026"
            value={name}
            onChange={(e) => setName(e.currentTarget.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void submit();
            }}
            autoFocus
          />
          <Button onClick={submit} disabled={!name.trim()}>
            Create
          </Button>
        </Stack>
      </Modal>

      <Modal
        opened={confirmingDelete !== null}
        onClose={() => setConfirmingDelete(null)}
        title="Delete session?"
      >
        <Stack>
          <Text size="sm">
            Delete <Text span fw={600}>{confirmingDelete?.name}</Text>? This removes the session,
            its swarms and device assignments from the dashboard. Detection and GPS records for it
            are retained in the database.
          </Text>
          <Group justify="flex-end">
            <Button variant="light" onClick={() => setConfirmingDelete(null)}>
              Cancel
            </Button>
            <Button
              color="red"
              leftSection={<IconTrash size={16} />}
              loading={deleting}
              onClick={() => void handleConfirmDelete()}
            >
              Delete session
            </Button>
          </Group>
        </Stack>
      </Modal>

      <Card p={0}>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Created</Table.Th>
              <Table.Th ta="right" />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {sessions?.map((s) => {
              const st = STATUS[s.status] ?? { label: String(s.status), color: 'gray' };
              return (
                <Table.Tr
                  key={s.id}
                  style={
                    s.id === activeSessionId
                      ? { background: 'var(--mantine-color-teal-light)' }
                      : undefined
                  }
                >
                  <Table.Td>
                    <Text fw={600}>{s.name}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Badge color={st.color} variant="light">
                      {st.label}
                    </Badge>
                  </Table.Td>
                  <Table.Td>{new Date(s.createdAt).toLocaleString()}</Table.Td>
                  <Table.Td ta="right">
                    <Group gap="xs" justify="flex-end">
                      <Button
                        size="compact-sm"
                        variant={s.id === activeSessionId ? 'filled' : 'light'}
                        leftSection={<IconCrosshair size={14} />}
                        onClick={() => setActiveSessionId(s.id)}
                      >
                        Focus
                      </Button>
                      <Tooltip label={`Delete ${s.name}`}>
                        <Button
                          size="compact-sm"
                          color="red"
                          variant="subtle"
                          leftSection={<IconTrash size={14} />}
                          onClick={() => setConfirmingDelete(s)}
                        >
                          Delete
                        </Button>
                      </Tooltip>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              );
            })}
            {!isLoading && sessions?.length === 0 && (
              <Table.Tr>
                <Table.Td colSpan={4}>
                  <Text c="dimmed" py="sm" ta="center">
                    No sessions yet. Create one to start a campaign.
                  </Text>
                </Table.Td>
              </Table.Tr>
            )}
          </Table.Tbody>
        </Table>
      </Card>

      <Divider />

      <Stack gap="sm">
        <Title order={4}>Compare sessions</Title>
        <Group>
          <Select data={sessionOptions} value={compareA} onChange={setCompareA} placeholder="Session A" searchable w={220} />
          <Select data={sessionOptions} value={compareB} onChange={setCompareB} placeholder="Session B" searchable w={220} />
          {comparison && (
            <Group gap={6}>
              <Badge color="teal" variant="light">both: {comparison.inBoth}</Badge>
              <Badge color="red" variant="light">only A: {comparison.onlyInA}</Badge>
              <Badge color="blue" variant="light">only B: {comparison.onlyInB}</Badge>
            </Group>
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
          <Button
            variant="light"
            leftSection={<IconFileImport size={16} />}
            onClick={() => gpxInput.current?.click()}
          >
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
    </>
  );
}
