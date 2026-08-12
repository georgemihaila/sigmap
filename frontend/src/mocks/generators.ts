import type { BoundingArea, DeviceCapabilities, DeviceType } from '@/lib/domain';

/** Deterministic PRNG (mulberry32) so fixture data is stable across reloads/tests. */
export function mulberry32(seed: number) {
  let a = seed >>> 0;
  return function () {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export type Rng = () => number;

export function pick<T>(rng: Rng, arr: readonly T[]): T {
  return arr[Math.floor(rng() * arr.length)];
}

export function weightedPick<T>(rng: Rng, entries: Array<[T, number]>): T {
  const total = entries.reduce((s, [, w]) => s + w, 0);
  let roll = rng() * total;
  for (const [value, weight] of entries) {
    roll -= weight;
    if (roll <= 0) return value;
  }
  return entries[entries.length - 1][0];
}

export function randomInt(rng: Rng, min: number, max: number): number {
  return Math.floor(rng() * (max - min + 1)) + min;
}

export function randomFloat(rng: Rng, min: number, max: number): number {
  return rng() * (max - min) + min;
}

export function uuid(rng: Rng): string {
  const hex = '0123456789abcdef';
  let out = '';
  for (let i = 0; i < 32; i++) out += hex[Math.floor(rng() * 16)];
  return `${out.slice(0, 8)}-${out.slice(8, 12)}-4${out.slice(13, 16)}-${'89ab'[Math.floor(rng() * 4)]}${out.slice(17, 20)}-${out.slice(20)}`;
}

// ---------------------------------------------------------------------------
// OUI / vendor table (real-world prefixes; first 3 bytes of the MAC)
// ---------------------------------------------------------------------------

export const OUI_TABLE: Array<[string, string]> = [
  ['00:1A:2B', 'Intel Corporate'],
  ['00:1E:58', 'Intel Corporate'],
  ['34:AA:8B', 'Intel Corporate'],
  ['00:24:BE', 'TP-Link Technologies'],
  ['5C:63:BF', 'TP-Link Technologies'],
  ['5C:87:9C', 'TP-Link Technologies'],
  ['00:50:F1', 'ASUSTek Computer'],
  ['00:0C:29', 'VMware'],
  ['00:23:24', 'Dell'],
  ['00:18:4D', 'Hewlett Packard'],
  ['00:1B:63', 'Cisco-Linksys'],
  ['84:D8:1B', 'Cisco Systems'],
  ['20:2B:3D', 'Cisco Meraki'],
  ['00:22:2D', 'Micro-Star International'],
  ['00:1C:AB', 'Giga-Byte Technology'],
  ['00:23:8E', 'Belkin International'],
  ['00:26:5A', 'Samsung Electronics'],
  ['08:3E:8E', 'Samsung Electronics'],
  ['48:8F:5A', 'Samsung Electronics'],
  ['00:1F:5B', 'Netgear'],
  ['B0:BE:76', 'Netgear'],
  ['8C:3A:E3', 'D-Link'],
  ['6C:0B:84', 'Huawei Technologies'],
  ['70:4F:57', 'Apple'],
  ['00:15:6D', 'Apple'],
  ['00:0D:67', 'Apple'],
  ['04:02:1F', 'Amazon Technologies'],
  ['68:05:CA', 'Amazon Technologies'],
  ['48:F8:B3', 'Roku'],
  ['90:9A:4A', 'Google'],
  ['B8:27:EB', 'Raspberry Pi Foundation'],
  ['DC:A6:32', 'Raspberry Pi Foundation'],
  ['00:1A:11', 'Sony'],
  ['D8:32:14', 'Espressif'],
  ['3C:6A:9D', 'Espressif'],
  ['24:4B:FE', 'Liteon Technology'],
  ['00:13:EF', 'Sagemcom'],
  ['2C:30:33', 'Wistron Neweb'],
  ['00:15:0F', 'LG Electronics'],
  ['7C:11:BE', 'LG Electronics'],
  ['44:6C:9D', 'Liteon'],
  ['5C:02:14', 'Motorola Mobility'],
  ['F4:81:39', 'Xiaomi Communications'],
  ['00:5A:39', 'Ubiquiti'],
  ['24:A4:3C', 'Ubiquiti'],
  ['04:18:D6', 'MikroTik'],
  ['24:0A:C4', 'Acer'],
  ['00:1E:10', 'Hewlett Packard'],
];

export function ouiFor(rng: Rng): { oui: string; vendor: string } {
  const [oui, vendor] = pick(rng, OUI_TABLE);
  return { oui, vendor };
}

// ---------------------------------------------------------------------------
// SSID / device-name generators
// ---------------------------------------------------------------------------

const RESIDENTIAL: Array<[string, number]> = [
  ['HOME', 3], ['NETGEAR', 2], ['TP-LINK_', 2], ['MyWiFi', 2], ['FRITZ!Box', 2],
  ['BTHomeHub', 1], ['SKY', 1], ['O2-WLAN', 1], ['Speedtest', 1],
];

const ENTERPRISE = ['eduroam', 'eduroam - Radsec', 'corp-5GHz', 'corp-guest', 'Starbucks WiFi', 'Airport Free WiFi', 'HotelGuest', 'Uni-Guest', 'xfinitywifi', 'Vodafone Hotspot'];

const IOT = ['TP-Link_SmartPlug', 'SmartLife', 'Hue Bridge', 'Sonos', 'TuyaSmart', 'Galaxy-', 'esp-', 'MiBand', 'Ring-', 'RingCam'];

export function randomSsid(rng: Rng): string {
  const style = weightedPick(rng, [
    ['residential', 10],
    ['enterprise', 3],
    ['iot', 2],
  ]);
  if (style === 'enterprise') return pick(rng, ENTERPRISE);
  if (style === 'iot') {
    const base = pick(rng, IOT);
    return base + (rng() > 0.5 ? String(randomInt(rng, 10, 999)) : '');
  }
  const template = weightedPick(rng, RESIDENTIAL);
  return `${template}${randomInt(rng, 1000, 9999)}`;
}

export function randomBtName(rng: Rng): string {
  return pick(rng, [
    'AirPods Pro', 'Galaxy Buds2', 'Fitbit Versa 3', 'JBL Flip 5', 'MacBook Pro',
    'iPhone 15', 'Samsung TV', 'Xbox Series X', 'PlayStation 5', 'Fitbit Charge 5',
    'Tile Mate', 'Beats Solo', 'Pixel Buds', 'Garmin Forerunner', 'Withings Body+',
    'ESP32-BLE', 'iTag', 'Mi Band 6', 'Logitech MX Master', 'Bose QC45',
  ]);
}

export function randomClientSsid(rng: Rng): string | null {
  return rng() > 0.3 ? null : pick(rng, ['DESKTOP-', 'LAPTOP-', 'ANDROID-', 'iPhone', 'PC-']);
}

// ---------------------------------------------------------------------------
// MAC addresses
// ---------------------------------------------------------------------------

export function randomMac(rng: Rng, oui?: string): string {
  const prefix = oui ?? ouiFor(rng).oui;
  const bytes: string[] = [];
  const parts = prefix.split(':');
  while (parts.length < 6) {
    parts.push(randomInt(rng, 0, 255).toString(16).padStart(2, '0').toUpperCase());
  }
  void bytes;
  return parts.join(':');
}

export function normalizeMac(mac: string): string {
  return mac.replace(/[:-]/g, '').toLowerCase();
}

// ---------------------------------------------------------------------------
// Fleet devices
// ---------------------------------------------------------------------------

export const FLEET_NAMES = [
  'Sierra-1', 'Sierra-2', 'Tango-1', 'Foxtrot-3', 'RPi-Beta', 'RPi-Gamma',
  'Aurora-1', 'Pixel-8', 'Pixel-6a', 'Galaxy-S23', 'ThinkPad-X1', 'MacBook-M2',
  'Nuc-5', 'OrangePi-1', 'RPi-Zero2', 'Xiaomi-11', 'Vega-2', 'Nimbus-4',
];

export function randomCapabilities(rng: Rng, platform: string): DeviceCapabilities {
  return {
    hasWifiMonitor: rng() > 0.25,
    hasBluetooth: rng() > 0.35,
    hasGps: rng() > 0.2,
    hasBattery: platform.startsWith('android'),
    platform,
  };
}

// ---------------------------------------------------------------------------
// Geography
// ---------------------------------------------------------------------------

export interface City {
  name: string;
  lat: number;
  lon: number;
}

export const CITIES: City[] = [
  { name: 'Munich', lat: 48.1371, lon: 11.5754 },
  { name: 'Berlin', lat: 52.52, lon: 13.405 },
  { name: 'Prague', lat: 50.0755, lon: 14.4378 },
  { name: 'Vienna', lat: 48.2082, lon: 16.3738 },
  { name: 'Zürich', lat: 47.3769, lon: 8.5417 },
  { name: 'Salzburg', lat: 47.8095, lon: 13.055 },
  { name: 'Stuttgart', lat: 48.7758, lon: 9.1829 },
  { name: 'Freiburg', lat: 47.999, lon: 7.8421 },
];

export function boundingArea(city: City, radiusDeg = 0.045): BoundingArea {
  return {
    latMin: city.lat - radiusDeg,
    lonMin: city.lon - radiusDeg * 1.6,
    latMax: city.lat + radiusDeg,
    lonMax: city.lon + radiusDeg * 1.6,
  };
}

export function pointInArea(rng: Rng, area: BoundingArea): { lat: number; lon: number } {
  return {
    lat: randomFloat(rng, area.latMin, area.latMax),
    lon: randomFloat(rng, area.lonMin, area.lonMax),
  };
}

// ---------------------------------------------------------------------------
// Signals / channels by device type
// ---------------------------------------------------------------------------

export function signalFor(type: DeviceType, rng: Rng): number {
  switch (type) {
    case 'AP':
      return -randomInt(rng, 28, 92);
    case 'CLIENT':
      return -randomInt(rng, 35, 88);
    default:
      return -randomInt(rng, 40, 90);
  }
}

export function channelFor(type: DeviceType, rng: Rng): number {
  switch (type) {
    case 'AP':
    case 'CLIENT':
      return pick(rng, [1, 6, 11, 36, 40, 44, 48, 149, 153, 157, 161]);
    case 'BLUETOOTH':
      return randomInt(rng, 0, 39);
    default:
      return randomInt(rng, 37, 39);
  }
}
