import { useEffect, useMemo, useRef } from 'react';
import { useTheme } from 'next-themes';
import maplibregl, { type Map as MapLibreMap } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import type { Coverage, DeviceType, LiveMapPoint, LiveSessionState } from '@/lib/domain';

const TYPE_COLORS: Record<DeviceType, string> = {
  AP: '#3b82f6',
  BLUETOOTH: '#a855f7',
  BT_LE: '#f59e0b',
  CLIENT: '#10b981',
};

const LIGHT_STYLE = 'https://basemaps.cartocdn.com/gl/positron-gl-style/style.json';
const DARK_STYLE = 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json';

function toFeature(p: LiveMapPoint): GeoJSON.Feature<GeoJSON.Point> {
  return {
    type: 'Feature',
    geometry: { type: 'Point', coordinates: [p.lon, p.lat] },
    properties: { mac: p.mac, type: p.type, ssid: p.ssid, signalDbm: p.signalDbm },
  };
}

const TYPES: DeviceType[] = ['AP', 'BLUETOOTH', 'BT_LE', 'CLIENT'];

export function LiveMap({
  state,
  coverage,
}: {
  state: LiveSessionState;
  coverage?: Coverage;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const { resolvedTheme } = useTheme();
  const styleUrl = resolvedTheme === 'dark' ? DARK_STYLE : LIGHT_STYLE;

  const byType = useMemo(() => {
    const groups: Record<DeviceType, LiveMapPoint[]> = { AP: [], BLUETOOTH: [], BT_LE: [], CLIENT: [] };
    for (const p of Object.values(state.points)) groups[p.type]?.push(p);
    return groups;
  }, [state.points]);

  // Create the map once.
  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: styleUrl,
      center: [11.5754, 48.1371],
      zoom: 11,
      attributionControl: { compact: true },
    });
    mapRef.current = map;
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
    map.on('load', () => {
      TYPES.forEach((type) => {
        map.addSource(`points-${type}`, { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
        map.addLayer({
          id: `clusters-${type}`,
          type: 'circle',
          source: `points-${type}`,
          filter: ['has', 'point_count'],
          paint: {
            'circle-color': TYPE_COLORS[type],
            'circle-radius': ['step', ['get', 'point_count'], 18, 10, 24, 50, 30],
          },
        });
        map.addLayer({
          id: `cluster-count-${type}`,
          type: 'symbol',
          source: `points-${type}`,
          filter: ['has', 'point_count'],
          layout: { 'text-field': '{point_count_abbreviated}', 'text-size': 12 },
          paint: { 'text-color': '#ffffff' },
        });
        map.addLayer({
          id: `points-${type}`,
          type: 'circle',
          source: `points-${type}`,
          filter: ['!', ['has', 'point_count']],
          paint: {
            'circle-color': TYPE_COLORS[type],
            'circle-radius': 6,
            'circle-stroke-width': 2,
            'circle-stroke-color': '#000000',
          },
        });
      });
      map.addSource('coverage', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      });
      map.addLayer({
        id: 'coverage-line',
        type: 'line',
        source: 'coverage',
        paint: {
          'line-color': '#f59e0b',
          'line-width': 2,
          'line-opacity': 0.7,
        },
      });
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
      map.setStyle(styleUrl);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [styleUrl]);

  // Push live points into each type's clustered source.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !map.isStyleLoaded()) return;
    for (const type of TYPES) {
      const source = map.getSource(`points-${type}`) as maplibregl.GeoJSONSource | undefined;
      if (!source) continue;
      source.setData({
        type: 'FeatureCollection',
        features: byType[type].map(toFeature),
      });
    }
  }, [byType]);

  // Coverage overlay.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !map.isStyleLoaded()) return;
    const source = map.getSource('coverage') as maplibregl.GeoJSONSource | undefined;
    if (!source) return;
    if (coverage && coverage.points.length > 0) {
      source.setData({
        type: 'FeatureCollection',
        features: [
          {
            type: 'Feature',
            geometry: { type: 'LineString', coordinates: coverage.points.map((p) => [p.lon, p.lat]) },
            properties: {},
          },
        ],
      });
    } else {
      source.setData({ type: 'FeatureCollection', features: [] });
    }
  }, [coverage]);

  return <div ref={containerRef} className="h-full w-full" />;
}
