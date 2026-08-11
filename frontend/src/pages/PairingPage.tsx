import { Badge, Button, Group, Select, Stack, Table, Text, Title } from '@mantine/core';
import { QRCodeCanvas } from 'qrcode.react';
import { useState } from 'react';
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
    <Stack>
      <Title order={2}>Pairing</Title>

      <Group align="start" gap="xl">
        <Stack w={400}>
          <Title order={4}>Pairing QR</Title>
          <Text size="sm" c="dimmed">
            Select a session to generate a pairing QR code. A device that scans it and
            submits the token appears below for approval.
          </Text>
          <Select data={sessionOptions} value={qrSessionId} onChange={setQrSessionId} placeholder="Session" searchable w={280} />
          {qrPayload && (
            <div style={{ background: 'white', padding: 12, width: 180 }}>
              <QRCodeCanvas value={qrPayload} size={160} />
            </div>
          )}
        </Stack>

        <Stack flex={1}>
          <Title order={4}>Pending approvals</Title>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Device</Table.Th>
                <Table.Th>Platform</Table.Th>
                <Table.Th>Requested</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {pending?.map((d) => (
                <Table.Tr key={d.id}>
                  <Table.Td>{d.name}</Table.Td>
                  <Table.Td><Badge variant="light">{d.platform || '—'}</Badge></Table.Td>
                  <Table.Td>{new Date(d.pairedAt).toLocaleString()}</Table.Td>
                  <Table.Td>
                    <Group gap="xs">
                      <Button
                        size="compact-xs"
                        disabled={!qrSessionId}
                        onClick={() => approve({ deviceId: d.id, sessionId: qrSessionId! })}
                      >
                        Approve into session
                      </Button>
                      <Button size="compact-xs" color="red" variant="subtle" onClick={() => reject(d.id)}>
                        Reject
                      </Button>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
              {!pending?.length && (
                <Table.Tr>
                  <Table.Td colSpan={4}>
                    <Text c="dimmed">No devices waiting for approval.</Text>
                  </Table.Td>
                </Table.Tr>
              )}
            </Table.Tbody>
          </Table>
        </Stack>
      </Group>
    </Stack>
  );
}
