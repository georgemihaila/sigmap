import { Card, Group, Text } from '@mantine/core';
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

const LEGEND: Array<{ key: string; label: string }> = [
  { key: 'AP', label: 'AP' },
  { key: 'BLUETOOTH', label: 'Bluetooth' },
  { key: 'BT_LE', label: 'BLE' },
  { key: 'CLIENT', label: 'Client' },
];

interface CoveragePoint {
  lat: number;
  lon: number;
}

export function LiveMap({ coverage }: { coverage?: CoveragePoint[] }) {
  const points = useAppSelector((s) => s.liveMap.points);
  const batchCount = useAppSelector((s) => s.liveMap.batchCount);
  const markers: MapPoint[] = Object.values(points);

  return (
    <Card p={0} style={{ overflow: 'hidden' }}>
      <Group justify="space-between" px="md" py="sm">
        <Group gap="sm">
          {LEGEND.map((l) => (
            <Group key={l.key} gap={6}>
              <span
                style={{
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  background: COLORS[l.key],
                  display: 'inline-block',
                }}
              />
              <Text size="xs" c="dimmed">
                {l.label}
              </Text>
            </Group>
          ))}
        </Group>
        <Text size="sm" c="dimmed">
          {markers.length} devices · {batchCount} batches
          {coverage ? ` · ${coverage.length} coverage pts` : ''}
        </Text>
      </Group>
      <div style={{ height: 'calc(100vh - 220px)', minHeight: 480 }}>
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
    </Card>
  );
}
