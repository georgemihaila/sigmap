import { ActionIcon, Card, Group, Text, Tooltip } from '@mantine/core';
import { IconCurrentLocation } from '@tabler/icons-react';
import type { Map as LeafletMap } from 'leaflet';
import { useCallback, useEffect, useRef, useState } from 'react';
import { CircleMarker, MapContainer, TileLayer, Tooltip as LeafletTooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { useAppSelector } from '../store/hooks';
import type { MapPoint } from '../store/liveMapSlice';

const FALLBACK_CENTER: [number, number] = [44.4268, 26.1025];

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

  const mapRef = useRef<LeafletMap | null>(null);
  const [center, setCenter] = useState<[number, number]>(FALLBACK_CENTER);
  const [locating, setLocating] = useState(false);

  const locate = useCallback(() => {
    if (!navigator.geolocation) return;
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const latlng: [number, number] = [pos.coords.latitude, pos.coords.longitude];
        setCenter(latlng);
        mapRef.current?.flyTo(latlng, Math.max(mapRef.current.getZoom(), 14));
        setLocating(false);
      },
      () => {
        // Permission denied or unavailable: keep the current center.
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 10_000, maximumAge: 30_000 },
    );
  }, []);

  useEffect(() => {
    // Ask for location once on mount and default the map to the user.
    locate();
  }, [locate]);

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
      <div style={{ height: 'calc(100vh - 220px)', minHeight: 480, position: 'relative' }}>
        <MapContainer
          ref={mapRef}
          center={center}
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
              <LeafletTooltip>
                <strong>{p.ssid ?? p.mac}</strong>
                <br />
                {p.type} · {p.signalDbm} dBm
              </LeafletTooltip>
            </CircleMarker>
          ))}
        </MapContainer>
        <Tooltip label="Center on my location" position="left">
          <ActionIcon
            variant="filled"
            color="teal"
            radius="xl"
            size="lg"
            loading={locating}
            onClick={locate}
            style={{ position: 'absolute', top: 12, right: 12, zIndex: 1001 }}
            aria-label="Center on my location"
          >
            <IconCurrentLocation size={18} />
          </ActionIcon>
        </Tooltip>
      </div>
    </Card>
  );
}
