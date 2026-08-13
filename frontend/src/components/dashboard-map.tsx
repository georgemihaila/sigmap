import { useEffect, useMemo, useRef, useState } from 'react';
import { useTheme } from 'next-themes';
import maplibregl, { type Map as MapLibreMap } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import type { DetectedDevice, DeviceType } from '@/lib/domain';

export type DashboardMapMode = 'heatmap' | 'devices';

const TYPE_COLORS: Record<DeviceType, string> = {
  AP: '#3b82f6',
  BLUETOOTH: '#a855f7',
  BT_LE: '#f59e0b',
  CLIENT: '#10b981',
};

const LIGHT_STYLE = 'https://basemaps.cartocdn.com/gl/positron-gl-style/style.json';
const DARK_STYLE = 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json';
const FALLBACK_CENTER: [number, number] = [11.5754, 48.1371];

const HEATMAP_LAYER = 'devices-heatmap';
const POINTS_LAYER = 'devices-points';

export function DashboardMap({
  devices,
  mode,
}: {
  devices: DetectedDevice[];
  mode: DashboardMapMode;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const [styleReady, setStyleReady] = useState(false);
  const { resolvedTheme } = useTheme();
  const styleUrl = resolvedTheme === 'dark' ? DARK_STYLE : LIGHT_STYLE;

  const features = useMemo(
    () =>
      devices.map((d) => ({
        type: 'Feature' as const,
        geometry: { type: 'Point' as const, coordinates: [d.longitude as number, d.latitude as number] },
        properties: { mac: d.mac, type: d.deviceType, ssid: d.ssidLatest, btName: d.btNameLatest, vendor: d.vendorName, count: d.detectionCount },
      })),
    [devices],
  );

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: styleUrl,
      center: FALLBACK_CENTER,
      zoom: 11,
      attributionControl: { compact: true },
    });
    mapRef.current = map;
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
    map.addControl(new maplibregl.GeolocateControl({
      positionOptions: { enableHighAccuracy: true },
      fitBoundsOptions: { maxZoom: 12 },
      trackUserLocation: true,
    }), 'top-right');

    map.on('load', () => {
      map.addSource('devices', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
      map.addLayer({
        id: HEATMAP_LAYER,
        type: 'heatmap',
        source: 'devices',
        paint: {
          'heatmap-radius': ['interpolate', ['linear'], ['zoom'], 0, 18, 9, 34, 12, 44],
          'heatmap-intensity': ['interpolate', ['linear'], ['zoom'], 0, 1, 9, 2],
          'heatmap-color': [
            'interpolate', ['linear'], ['heatmap-density'],
            0, 'rgba(33,102,172,0)',
            0.25, 'rgba(103,169,207,1)',
            0.5, 'rgba(209,229,240,1)',
            0.75, 'rgba(253,219,199,1)',
            0.9, 'rgba(239,138,98,1)',
            1, 'rgba(178,24,43,1)',
          ],
          'heatmap-opacity': 0.85,
        },
      });
      map.addLayer({
        id: POINTS_LAYER,
        type: 'circle',
        source: 'devices',
        paint: {
          'circle-color': [
            'match', ['get', 'type'],
            'AP', TYPE_COLORS.AP,
            'BLUETOOTH', TYPE_COLORS.BLUETOOTH,
            'BT_LE', TYPE_COLORS.BT_LE,
            'CLIENT', TYPE_COLORS.CLIENT,
            '#64748b',
          ],
          'circle-radius': 5.5,
          'circle-stroke-width': 1.5,
          'circle-stroke-color': '#000000',
          'circle-opacity': 0.9,
        },
      });
      setStyleReady(true);
    });

    map.on('click', POINTS_LAYER, (e) => {
      const feature = e.features?.[0];
      if (!feature?.geometry || feature.geometry.type !== 'Point') return;
      const props = feature.properties as Record<string, string>;
      const label = props.ssid ?? props.btName ?? props.mac;
      const html = `
        <div class="text-sm font-semibold">${label}</div>
        <div class="text-xs opacity-80">${props.mac}</div>
        <div class="text-xs opacity-80">${props.vendor ?? 'unknown vendor'} · ${props.type}</div>
      `;
      new maplibregl.Popup({ closeButton: false, offset: 12 })
        .setLngLat(feature.geometry.coordinates as [number, number])
        .setHTML(html)
        .addTo(map);
    });

    map.on('mouseenter', POINTS_LAYER, () => {
      map.getCanvas().style.cursor = 'pointer';
    });
    map.on('mouseleave', POINTS_LAYER, () => {
      map.getCanvas().style.cursor = '';
    });

    return () => {
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Follow the theme.
  useEffect(() => {
    const map = mapRef.current;
    if (map && map.isStyleLoaded()) {
      setStyleReady(false);
      map.setStyle(styleUrl);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [styleUrl]);

  // Push located devices into the source and toggle the active mode.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !styleReady) return;
    const source = map.getSource('devices') as maplibregl.GeoJSONSource | undefined;
    if (!source) return;
    source.setData({ type: 'FeatureCollection', features });
    for (const layerId of [HEATMAP_LAYER, POINTS_LAYER]) {
      const visible = (mode === 'heatmap') === (layerId === HEATMAP_LAYER);
      map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
    }
  }, [features, mode, styleReady]);

  return <div ref={containerRef} className="h-full w-full" />;
}
