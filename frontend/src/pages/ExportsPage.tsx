import {
  Alert,
  Badge,
  Button,
  Divider,
  Group,
  PasswordInput,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { useState } from 'react';
import {
  useCreateExportMutation,
  useGetWigleSettingsQuery,
  useImportWigleMutation,
  useListExportsQuery,
  useListSessionsQuery,
  usePutWigleSettingsMutation,
  useUploadWigleMutation,
} from '../store/api';

export function ExportsPage() {
  const { data: sessions } = useListSessionsQuery();
  const { data: exports } = useListExportsQuery();
  const { data: settings } = useGetWigleSettingsQuery();
  const [putSettings] = usePutWigleSettingsMutation();
  const [createExport] = useCreateExportMutation();
  const [uploadWigle] = useUploadWigleMutation();
  const [importWigle] = useImportWigleMutation();

  const [sessionId, setSessionId] = useState<string | null>(null);
  const [format, setFormat] = useState<string | null>('wigle_csv');
  const [apiName, setApiName] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [message, setMessage] = useState<string | null>(null);

  const sessionOptions = (sessions ?? []).map((s) => ({ value: s.id, label: s.name }));

  const saveSettings = async () => {
    await putSettings({ apiName: apiName || undefined, apiKey: apiKey || undefined });
    setApiKey('');
    setMessage('WiGLE settings saved');
  };

  return (
    <Stack>
      <Title order={2}>Export center</Title>
      {message && <Alert color="blue">{message}</Alert>}

      <Group align="start" gap="xl">
        <Stack w={420}>
          <Title order={4}>WiGLE credentials</Title>
          <TextInput label="API name" value={apiName} placeholder={settings?.apiName ?? 'wigle-api-name'} onChange={(e) => setApiName(e.currentTarget.value)} />
          <PasswordInput label="API key" value={apiKey} onChange={(e) => setApiKey(e.currentTarget.value)} />
          <Group>
            <Badge color={settings?.apiKeySet ? 'teal' : 'gray'}>
              {settings?.apiKeySet ? 'API key set' : 'no API key'}
            </Badge>
            <Button size="compact-sm" onClick={saveSettings}>Save</Button>
          </Group>
        </Stack>

        <Stack w={420}>
          <Title order={4}>Exports</Title>
          <Group>
            <Select data={sessionOptions} value={sessionId} onChange={setSessionId} placeholder="Session (all if empty)" clearable searchable w={200} />
            <Select
              data={[
                { value: 'wigle_csv', label: 'WiGLE CSV' },
                { value: 'csv', label: 'CSV' },
                { value: 'geojson', label: 'GeoJSON' },
              ]}
              value={format}
              onChange={setFormat}
              w={140}
            />
            <Button onClick={() => createExport({ sessionId, format: format ?? 'wigle_csv' })}>Export</Button>
          </Group>

          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Format</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Created</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {exports?.map((e) => (
                <Table.Tr key={e.id}>
                  <Table.Td>{e.format}</Table.Td>
                  <Table.Td><Badge color={e.status === 'Done' ? 'teal' : e.status === 'Failed' ? 'red' : 'gray'}>{e.status}</Badge></Table.Td>
                  <Table.Td>{new Date(e.createdAt).toLocaleString()}</Table.Td>
                  <Table.Td>
                    {e.status === 'Done' && (
                      <Button size="compact-xs" component="a" href={`/api/exports/${e.id}/download`}>
                        Download
                      </Button>
                    )}
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Stack>
      </Group>

      <Divider />

      <Group>
        <Title order={4}>WiGLE</Title>
        <Button
          variant="light"
          disabled={!sessionId}
          onClick={async () => {
            if (!sessionId) return;
            const res = await uploadWigle({ sessionId }).unwrap();
            setMessage(res.status === 'Ok' ? 'Upload accepted by WiGLE' : `Upload failed: ${res.message}`);
          }}
        >
          Upload session to WiGLE
        </Button>
        <Button
          variant="light"
          onClick={async () => {
            const n = await importWigle().unwrap();
            setMessage(`Imported/merged ${n} devices from WiGLE`);
          }}
        >
          Import from WiGLE
        </Button>
        <Text size="xs" c="dimmed">
          Import pulls your uploads (KML) and merges them into the detected-devices table.
        </Text>
      </Group>
    </Stack>
  );
}
