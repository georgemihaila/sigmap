import { Divider, Group, Stack, Text, Title } from '@mantine/core';
import { CircleMarker, MapContainer, TileLayer, Tooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { useAppSelector } from '../store/hooks';
import type { MapPoint } from '../store/liveMapSlice';

const COLORS: Record<string, string> = {
  AP: '#e03131',
  BLUETOOTH: '#1971c2',
  BT_LE: '#2f9e44',
  CLIENT: '#f08c00',
  UNKNOWN: '#868e96',
};

interface CoveragePoint {
  lat: number;
  lon: number;
}

export function LiveMap({ coverage }: { coverage?: CoveragePoint[] }) {
  const points = useAppSelector((s) => s.liveMap.points);
  const batchCount = useAppSelector((s) => s.liveMap.batchCount);
  const markers: MapPoint[] = Object.values(points);

  return (
    <Stack gap="sm">
      <Group justify="space-between">
        <Title order={3}>Live map</Title>
        <Text size="sm" c="dimmed">
          {markers.length} devices · {batchCount} batches{coverage ? ` · ${coverage.length} coverage pts` : ''}
        </Text>
      </Group>
      <Divider />
      <div style={{ height: 'calc(100vh - 160px)' }}>
        <MapContainer
          center={[52.52, 13.405]}
          zoom={13}
          style={{ height: '100%', width: '100%' }}
        >
          <TileLayer
            attribution='&copy; OpenStreetMap contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          {coverage?.map((p, i) => (
            <CircleMarker
              key={`cov-${i}`}
              center={[p.lat, p.lon]}
              radius={2}
              pathOptions={{ color: '#1971c2', weight: 0.5, opacity: 0.35 }}
            />
          ))}
          {markers.map((p: MapPoint) => (
            <CircleMarker
              key={p.mac}
              center={[p.lat, p.lon]}
              radius={6}
              pathOptions={{ color: COLORS[p.type] ?? COLORS.UNKNOWN, weight: 1 }}
            >
              <Tooltip>
                <strong>{p.ssid ?? p.mac}</strong>
                <br />
                {p.type} · {p.signalDbm} dBm
              </Tooltip>
            </CircleMarker>
          ))}
        </MapContainer>
      </div>
    </Stack>
  );
}
