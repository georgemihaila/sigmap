import { Badge, Button, Card, Group, Select, Stack, Table, Text, Title } from '@mantine/core';
import { IconCheck, IconQrcode, IconX } from '@tabler/icons-react';
import { QRCodeCanvas } from 'qrcode.react';
import { useState } from 'react';
import { PageHeader } from '../components/PageHeader';
import {
  useApprovePairingMutation,
  useListPendingPairingsQuery,
  useListSessionsQuery,
  usePairingQrQuery,
  useRejectPairingMutation,
} from '../store/api';

export function PairingPage() {
  const { data: pending } = useListPendingPairingsQuery();
  const { data: sessions } = useListSessionsQuery();
  const [approve] = useApprovePairingMutation();
  const [reject] = useRejectPairingMutation();
  const [qrSessionId, setQrSessionId] = useState<string | null>(null);
  const { data: qrPayload } = usePairingQrQuery(qrSessionId ?? '', { skip: !qrSessionId });

  const sessionOptions = (sessions ?? []).map((s) => ({ value: s.id, label: s.name }));

  return (
    <>
      <PageHeader
        title="Pairing"
        subtitle="Generate pairing QR codes and approve new scanner devices."
      />

      <Group align="start" gap="md" wrap="wrap">
        <Stack w={360}>
          <Card p="lg">
            <Group gap="xs" mb="sm">
              <IconQrcode size={18} color="var(--mantine-color-teal-5)" />
              <Title order={4}>Pairing QR</Title>
            </Group>
            <Stack gap="md">
              <Select
                data={sessionOptions}
                value={qrSessionId}
                onChange={setQrSessionId}
                placeholder="Session"
                searchable
                w="100%"
              />
              {qrPayload ? (
                <Group justify="center">
                  <div style={{ background: 'white', padding: 12, borderRadius: 12 }}>
                    <QRCodeCanvas value={qrPayload} size={160} />
                  </div>
                </Group>
              ) : (
                <Text size="sm" c="dimmed">
                  Select a session to generate a pairing QR code. A device that scans it and
                  submits the token appears below for approval.
                </Text>
              )}
            </Stack>
          </Card>
        </Stack>

        <Stack flex={1} miw={420}>
          <Card p={0}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Device</Table.Th>
                  <Table.Th>Platform</Table.Th>
                  <Table.Th>Requested</Table.Th>
                  <Table.Th ta="right" />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {pending?.map((d) => (
                  <Table.Tr key={d.id}>
                    <Table.Td>
                      <Text fw={600}>{d.name}</Text>
                    </Table.Td>
                    <Table.Td><Badge variant="light">{d.platform || '—'}</Badge></Table.Td>
                    <Table.Td>{new Date(d.pairedAt).toLocaleString()}</Table.Td>
                    <Table.Td>
                      <Group gap="xs" justify="flex-end">
                        <Button
                          size="compact-sm"
                          leftSection={<IconCheck size={14} />}
                          disabled={!qrSessionId}
                          onClick={() => approve({ deviceId: d.id, sessionId: qrSessionId! })}
                        >
                          Approve into session
                        </Button>
                        <Button
                          size="compact-sm"
                          color="red"
                          variant="subtle"
                          leftSection={<IconX size={14} />}
                          onClick={() => reject(d.id)}
                        >
                          Reject
                        </Button>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
                {!pending?.length && (
                  <Table.Tr>
                    <Table.Td colSpan={4}>
                      <Text c="dimmed" py="sm" ta="center">
                        No devices waiting for approval.
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                )}
              </Table.Tbody>
            </Table>
          </Card>
        </Stack>
      </Group>
    </>
  );
}
