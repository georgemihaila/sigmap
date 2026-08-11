import {
  Alert,
  Badge,
  Button,
  Card,
  Group,
  PasswordInput,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { IconCloudUpload, IconDownload, IconKey, IconUpload } from '@tabler/icons-react';
import { useState } from 'react';
import { PageHeader } from '../components/PageHeader';
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
    <>
      <PageHeader
        title="Export center"
        subtitle="Download session data or sync it to WiGLE."
      />
      {message && <Alert color="teal">{message}</Alert>}

      <Group align="start" gap="md" wrap="wrap">
        <Stack flex={1} miw={340} maw={460}>
          <Card p="lg">
            <Group mb="sm" gap="xs">
              <IconKey size={18} color="var(--mantine-color-teal-5)" />
              <Title order={4}>WiGLE credentials</Title>
            </Group>
            <Stack gap="md">
              <TextInput
                label="API name"
                value={apiName}
                placeholder={settings?.apiName ?? 'wigle-api-name'}
                onChange={(e) => setApiName(e.currentTarget.value)}
              />
              <PasswordInput
                label="API key"
                value={apiKey}
                onChange={(e) => setApiKey(e.currentTarget.value)}
              />
              <Group>
                <Badge color={settings?.apiKeySet ? 'teal' : 'gray'}>
                  {settings?.apiKeySet ? 'API key set' : 'no API key'}
                </Badge>
                <Button onClick={saveSettings}>Save</Button>
              </Group>
            </Stack>
          </Card>
        </Stack>

        <Stack flex={1} miw={420}>
          <Card p="md">
            <Group mb="sm" justify="space-between">
              <Title order={4}>Exports</Title>
              <Group gap="xs">
                <Select
                  data={sessionOptions}
                  value={sessionId}
                  onChange={setSessionId}
                  placeholder="Session (all if empty)"
                  clearable
                  searchable
                  w={190}
                />
                <Select
                  data={[
                    { value: 'wigle_csv', label: 'WiGLE CSV' },
                    { value: 'csv', label: 'CSV' },
                    { value: 'geojson', label: 'GeoJSON' },
                  ]}
                  value={format}
                  onChange={setFormat}
                  w={130}
                />
                <Button
                  leftSection={<IconDownload size={16} />}
                  onClick={() => createExport({ sessionId, format: format ?? 'wigle_csv' })}
                >
                  Export
                </Button>
              </Group>
            </Group>

            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Format</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Created</Table.Th>
                  <Table.Th ta="right" />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {exports?.map((e) => (
                  <Table.Tr key={e.id}>
                    <Table.Td>
                      <Text ff="monospace" size="sm">{e.format}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={e.status === 'Done' ? 'teal' : e.status === 'Failed' ? 'red' : 'gray'}>
                        {e.status}
                      </Badge>
                    </Table.Td>
                    <Table.Td>{new Date(e.createdAt).toLocaleString()}</Table.Td>
                    <Table.Td ta="right">
                      {e.status === 'Done' && (
                        <Button
                          size="compact-sm"
                          component="a"
                          href={`/api/exports/${e.id}/download`}
                        >
                          Download
                        </Button>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
                {exports?.length === 0 && (
                  <Table.Tr>
                    <Table.Td colSpan={4}>
                      <Text c="dimmed" py="sm" ta="center">
                        No exports yet.
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                )}
              </Table.Tbody>
            </Table>
          </Card>

          <Card p="lg">
            <Group gap="xs" mb="sm">
              <IconCloudUpload size={18} color="var(--mantine-color-teal-5)" />
              <Title order={4}>WiGLE sync</Title>
            </Group>
            <Group>
              <Button
                variant="light"
                leftSection={<IconUpload size={16} />}
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
          </Card>
        </Stack>
      </Group>
    </>
  );
}
