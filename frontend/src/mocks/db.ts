import type {
  ConfigPushResult, Coverage, DetectedDevice, DetectionSource,
  DetectedDevicePage, Device, DeviceConfigDto, DeviceType, Encryption, ExportFormat,
  ExportRecord, ExportStatus, FleetDevice, Heartbeat, LiveSessionState, LocationFlag,
  PairingRequest, ScanPreset, Session, SessionStats, SignalPoint, Swarm, User,
  WigleSettings,
} from '@/lib/domain';
import { encodeCursor, keyIsBefore } from '@/lib/cursor';
import { boundingArea, CITIES, mulberry32, normalizeMac, ouiFor, pick, randomBtName, randomCapabilities, randomClientSsid, randomInt, randomMac, randomSsid, signalFor, uuid, weightedPick, channelFor, pointInArea, FLEET_NAMES } from './generators';
import { defaultScanConfig, offScanConfig, parseScanConfig, stringifyScanConfig } from '@/lib/scanConfig';

// ---------------------------------------------------------------------------
// Internal row types
// ---------------------------------------------------------------------------

export interface DetectionRow {
  id: number;
  batchId: string;
  sessionId: string;
  deviceId: string;
  swarmId: string | null;
  deviceType: DeviceType;
  mac: string;
  ssid: string | null;
  btName: string | null;
  channel: number;
  signalDbm: number;
  encryption: Encryption;
  detectedAt: string;
  locationFlag: LocationFlag;
  lat: number | null;
  lon: number | null;
  source: DetectionSource;
}

interface SessionDeviceRow {
  sessionId: string;
  deviceId: string;
  swarmId: string | null;
  role: string;
  joinedAt: string;
}

export interface MockDb {
  users: User[];
  currentUser: User | null;
  sessions: Session[];
  swarms: Swarm[];
  devices: Device[];
  sessionDevices: SessionDeviceRow[];
  configs: Map<string, DeviceConfigDto>;
  presets: ScanPreset[];
  detectedDevices: DetectedDevice[];
  detections: DetectionRow[];
  exports: ExportRecord[];
  pairings: PairingRequest[];
  wigleSettings: WigleSettings;
  rng: () => number;
  nextDetectionId: number;
}

// ---------------------------------------------------------------------------
// Generation
// ---------------------------------------------------------------------------

const PRESET_CONFIGS: Array<[string, string, () => string]> = [
  ['Off', 'All radios off — new devices start here.', () => stringifyScanConfig(offScanConfig())],
  ['Urban Hopper', 'WiFi + BLE hopping, fast dwell, for dense city ops.', () =>
    stringifyScanConfig({
      ...defaultScanConfig(),
      channelHopMs: 250,
      scanBluetooth: true,
      scanClientsPromiscuous: true,
      interfaces: [
        { name: 'wlan0', driver: 'nl80211', monitorMode: true, enabled: true, channels: [1, 6, 11, 36, 40, 44, 149, 153, 157] },
        { name: 'hci0', driver: 'bluez', monitorMode: false, enabled: true, channels: [] },
      ],
    }),
  ],
  ['BLE Beacon Sweep', 'BLE-focused sweep for asset-tag hunting.', () =>
    stringifyScanConfig({
      ...defaultScanConfig(),
      scanWifi: false,
      scanBluetooth: true,
      scanBtLe: true,
      scanClientsPromiscuous: false,
      channelHopMs: 1000,
      interfaces: [
        { name: 'hci0', driver: 'bluez', monitorMode: false, enabled: true, channels: [] },
      ],
    }),
  ],
  ['Drone Chase', 'Two-card simultaneous capture for drone ops.', () =>
    stringifyScanConfig({
      ...defaultScanConfig(),
      channelHopMs: 500,
      interfaces: [
        { name: 'wlan0', driver: 'nl80211', monitorMode: true, enabled: true, channels: [] },
        { name: 'wlan1', driver: 'nl80211', monitorMode: true, enabled: true, channels: [] },
      ],
    }),
  ],
  ['Deep WiFi', 'Long dwell per channel, clients included.', () =>
    stringifyScanConfig({
      ...defaultScanConfig(),
      channelHopMs: 2000,
      scanClientsPromiscuous: true,
      batchIntervalMs: 2500,
      interfaces: [
        { name: 'wlan0', driver: 'nl80211', monitorMode: true, enabled: true, channels: [1, 6, 11] },
      ],
    }),
  ],
  ['Airport Sweep', '5GHz-leaning sweep for terminal surveys.', () =>
    stringifyScanConfig({
      ...defaultScanConfig(),
      channelHopMs: 400,
      interfaces: [
        { name: 'wlan0', driver: 'nl80211', monitorMode: true, enabled: true, channels: [36, 40, 44, 48, 149, 153, 157, 161] },
      ],
    }),
  ],
  ['Rural Low-Power', 'Battery-friendly slow scan for rural coverage.', () =>
    stringifyScanConfig({
      ...defaultScanConfig(),
      channelHopMs: 1500,
      batchIntervalMs: 5000,
      interfaces: [
        { name: 'wlan0', driver: 'nl80211', monitorMode: true, enabled: true, channels: [1, 6, 11] },
      ],
    }),
  ],
];

function msAgo(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

function isoInDays(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString();
}

export function generateDb(seed = 20260812): MockDb {
  const rng = mulberry32(seed);
  const db: MockDb = {
    users: [
      { id: uuid(rng), username: 'operator', role: 'operator' },
      { id: uuid(rng), username: 'viewer', role: 'viewer' },
    ],
    currentUser: null,
    sessions: [],
    swarms: [],
    devices: [],
    sessionDevices: [],
    configs: new Map(),
    presets: [],
    detectedDevices: [],
    detections: [],
    exports: [],
    pairings: [],
    wigleSettings: { apiName: 'wigle.net', apiKeySet: false, username: null, passwordSet: false },
    rng,
    nextDetectionId: 1,
  };

  // Presets
  db.presets = PRESET_CONFIGS.map(([name, description, factory], i) => ({
    id: uuid(rng),
    name,
    description,
    ownerId: i === 0 ? null : db.users[0].id,
    configJson: factory(),
    isBuiltin: i === 0,
    createdAt: msAgo(randomInt(rng, 5000, 40000)),
    updatedAt: msAgo(randomInt(rng, 0, 5000)),
  }));

  // Fleet devices
  const platforms = ['linux-x64', 'linux-arm64', 'raspberry-pi', 'android-14', 'android-13'];
  const statuses: Device['status'][] = ['online', 'online', 'online', 'offline', 'error'];
  db.devices = FLEET_NAMES.map((name, i) => ({
    id: uuid(rng),
    name,
    platform: pick(rng, platforms),
    capabilities: randomCapabilities(rng, pick(rng, platforms)),
    status: i < 3 ? 'pending' : pick(rng, statuses),
    lastHeartbeatAt: i < 3 ? null : msAgo(randomInt(rng, 0, 45)),
    lastKnownIp: `10.10.${randomInt(rng, 1, 9)}.${randomInt(rng, 2, 254)}`,
    pairedAt: msAgo(randomInt(rng, 1000, 30000)),
  }));

  // Sessions
  const activeCities = CITIES.slice(0, 4);
  const archivedCities = CITIES.slice(4, 7);
  const plannedCities = CITIES.slice(7, 8);

  const mkSession = (cityIdx: number, status: Session['status'], idx: number): Session => {
    const city = CITIES[cityIdx];
    return {
      id: uuid(rng),
      name: `${city.name} ${['Downtown', 'North', 'Airport', 'University', 'Harbor', 'Old Town', 'Tech Park', 'Central'][idx]} Sweep`,
      description: status === 'archived' ? 'Completed op — see export center for results.' : `Sensor coverage of the ${city.name} ${['downtown core', 'northern districts', 'airport perimeter', 'university campus', 'industrial zone', 'old town', 'tech park'][idx]}.`,
      startsAt: status === 'planned' ? isoInDays(randomInt(rng, 1, 7)) : msAgo(randomInt(rng, 60, 3000)),
      endsAt: status === 'archived' ? msAgo(randomInt(rng, 0, 2000)) : status === 'planned' ? isoInDays(randomInt(rng, 8, 14)) : null,
      status,
      boundingArea: boundingArea(city, 0.05),
      createdAt: msAgo(randomInt(rng, 3000, 40000)),
      updatedAt: msAgo(randomInt(rng, 0, 2000)),
    };
  };

  let sIdx = 0;
  activeCities.forEach((_c, i) => { db.sessions.push(mkSession(i, 'active', sIdx++)); });
  archivedCities.forEach((_c, i) => { db.sessions.push(mkSession(i + 4, 'archived', sIdx++)); });
  plannedCities.forEach((_c, i) => { db.sessions.push(mkSession(i + 7, 'planned', sIdx++)); });

  const activeOrArchived = db.sessions.filter((s) => s.status !== 'planned');

  // Swarms + device assignment + configs for operational sessions
  for (const session of activeOrArchived) {
    const isActive = session.status === 'active';
    const pool = db.devices.filter((d) => d.status !== 'pending' || isActive);
    const memberCount = isActive ? randomInt(rng, 5, 9) : randomInt(rng, 3, 6);
    const members = [...pool].sort(() => rng() - 0.5).slice(0, Math.min(memberCount, pool.length));
    const swarmCount = isActive ? Math.min(3, Math.max(2, Math.floor(members.length / 3))) : 2;
    const swarms = Array.from({ length: swarmCount }, (_, i) => ({
      id: uuid(rng),
      sessionId: session.id,
      name: ['Alpha', 'Bravo', 'Charlie'][i],
    }));
    db.swarms.push(...swarms);

    members.forEach((device, i) => {
      const swarm = swarms[Math.min(Math.floor(i / 3), swarms.length - 1)];
      db.sessionDevices.push({
        sessionId: session.id,
        deviceId: device.id,
        swarmId: swarm.id,
        role: i % 4 === 0 ? 'lead' : 'scout',
        joinedAt: session.createdAt,
      });
      const preset = pick(rng, db.presets.slice(1));
      const cfg = parseScanConfig(preset.configJson);
      if (rng() > 0.3 && session.status === 'active') cfg.channelHopMs = Math.max(100, cfg.channelHopMs + randomInt(rng, -200, 200));
      db.configs.set(`${session.id}:${device.id}`, {
        sessionId: session.id,
        deviceId: device.id,
        configJson: stringifyScanConfig(cfg),
        configRev: randomInt(rng, 1, 8),
        presetId: preset.id,
        source: 'preset',
        pushState: 'acked',
        lastPushId: uuid(rng),
        updatedAt: msAgo(randomInt(rng, 1, 600)),
      });
    });
  }

  // Per-session region: detected devices + detection history
  for (const session of activeOrArchived) {
    const area = session.boundingArea ?? boundingArea(CITIES[0]);
    const members = db.sessionDevices.filter((sd) => sd.sessionId === session.id);
    const memberDevices = members.map((m) => db.devices.find((d) => d.id === m.deviceId)!).filter(Boolean);
    const perSession = session.status === 'active' ? randomInt(rng, 380, 520) : randomInt(rng, 120, 220);

    const regionDevices: Array<{ mac: string; macNormalized: string; oui: string; vendor: string; deviceType: DeviceType }> = [];
    for (let i = 0; i < perSession; i++) {
      const type = weightedPick(rng, [
        ['AP', 0.58],
        ['BLUETOOTH', 0.14],
        ['BT_LE', 0.18],
        ['CLIENT', 0.1],
      ] as Array<[DeviceType, number]>);
      const { oui, vendor } = ouiFor(rng);
      const mac = randomMac(rng, oui);
      regionDevices.push({ mac, macNormalized: normalizeMac(mac), oui, vendor, deviceType: type });
    }

    const detectionCount = session.status === 'active' ? randomInt(rng, 700, 1300) : randomInt(rng, 150, 400);
    const devicePool = memberDevices.length ? memberDevices : db.devices.slice(0, 3);
    const batchId = uuid(rng);
    for (let i = 0; i < detectionCount; i++) {
      const region = pick(rng, regionDevices);
      const detector = pick(rng, devicePool);
      const locationRoll = rng();
      const locationFlag: LocationFlag = locationRoll < 0.55 ? 'gps' : locationRoll < 0.8 ? 'inferred' : 'unlocated';
      const point = locationFlag === 'unlocated' ? null : pointInArea(rng, area);
      const btName = region.deviceType !== 'AP' && region.deviceType !== 'CLIENT' ? randomBtName(rng) : null;
      const ssid = region.deviceType === 'AP' ? randomSsid(rng) : region.deviceType === 'CLIENT' ? randomClientSsid(rng) : null;
      const detectedAt = new Date(
        new Date(session.startsAt ?? Date.now()).getTime() + rng() * Math.min(86_400_000, Date.now() - new Date(session.startsAt ?? Date.now()).getTime()),
      ).toISOString();
      db.detections.push({
        id: db.nextDetectionId++,
        batchId,
        sessionId: session.id,
        deviceId: detector.id,
        swarmId: members.find((m) => m.deviceId === detector.id)?.swarmId ?? null,
        deviceType: region.deviceType,
        mac: region.mac,
        ssid,
        btName,
        channel: channelFor(region.deviceType, rng),
        signalDbm: signalFor(region.deviceType, rng),
        encryption: region.deviceType === 'AP' ? weightedPick(rng, [
          ['open', 0.15], ['wep', 0.05], ['wpa', 0.1], ['wpa2', 0.6], ['wpa3', 0.1],
        ] as Array<[Encryption, number]>) : 'unspecified',
        detectedAt,
        locationFlag,
        lat: point?.lat ?? null,
        lon: point?.lon ?? null,
        source: 'scanner',
      });
    }
  }

  // Build detected_devices aggregate heads from detections
  const aggregate = new Map<string, DetectedDevice>();
  for (const d of db.detections) {
    const existing = aggregate.get(d.mac);
    if (existing) {
      if (d.detectedAt > existing.lastSeenAt) {
        existing.lastSeenAt = d.detectedAt;
        if (d.ssid) existing.ssidLatest = d.ssid;
        if (d.btName) existing.btNameLatest = d.btName;
        if (d.lat != null) { existing.latitude = d.lat; existing.longitude = d.lon; }
      }
      if (d.detectedAt < existing.firstSeenAt) existing.firstSeenAt = d.detectedAt;
      existing.detectionCount += 1;
      existing.channelLatest = d.channel;
    } else {
      const region = db.detections.find((x) => x.mac === d.mac)!;
      const vendor = region.mac.startsWith('B8:27') || region.mac.startsWith('DC:A6') ? 'Raspberry Pi Foundation' : ouiFor(rng).vendor;
      const oui = region.mac.slice(0, 8);
      aggregate.set(d.mac, {
        id: uuid(rng),
        mac: d.mac,
        macNormalized: normalizeMac(d.mac),
        vendorOui: oui,
        vendorName: vendor,
        deviceType: d.deviceType,
        ssidLatest: d.ssid,
        btNameLatest: d.btName,
        channelLatest: d.channel,
        firstSeenAt: d.detectedAt,
        lastSeenAt: d.detectedAt,
        latitude: d.lat,
        longitude: d.lon,
        detectionCount: 1,
      });
    }
  }
  db.detectedDevices = [...aggregate.values()];

  // Exports
  const formats: ExportFormat[] = ['wigle_csv', 'csv', 'geojson'];
  const eStatuses: ExportStatus[] = ['done', 'done', 'done', 'done', 'failed', 'queued', 'running'];
  for (let i = 0; i < 26; i++) {
    const session = rng() > 0.35 ? pick(rng, activeOrArchived) : null;
    const format = pick(rng, formats);
    const status = pick(rng, eStatuses);
    db.exports.push({
      id: uuid(rng),
      sessionId: session?.id ?? null,
      sessionName: session?.name ?? null,
      format,
      status,
      fileName: status === 'done' ? `${(session?.name ?? 'all').replace(/\s+/g, '-')}-${format}.${format === 'wigle_csv' ? 'csv' : format === 'geojson' ? 'geojson' : 'csv'}` : null,
      createdAt: msAgo(randomInt(rng, 20, 5000)),
      ownerId: db.users[0].id,
      sizeBytes: status === 'done' ? randomInt(rng, 1000, 900000) : null,
      rowCount: status === 'done' ? randomInt(rng, 50, 3000) : null,
    });
  }

  // Pairings
  const pendingCaps = [
    { hasWifiMonitor: true, hasBluetooth: false, hasGps: true, hasBattery: false, platform: 'raspberry-pi' },
    { hasWifiMonitor: true, hasBluetooth: true, hasGps: true, hasBattery: true, platform: 'android-14' },
    { hasWifiMonitor: false, hasBluetooth: true, hasGps: false, hasBattery: false, platform: 'linux-arm64' },
    { hasWifiMonitor: true, hasBluetooth: false, hasGps: true, hasBattery: false, platform: 'linux-x64' },
    { hasWifiMonitor: true, hasBluetooth: true, hasGps: true, hasBattery: true, platform: 'android-13' },
    { hasWifiMonitor: true, hasBluetooth: true, hasGps: true, hasBattery: false, platform: 'raspberry-pi' },
  ];
  db.pairings = pendingCaps.map((cap, i) => ({
    id: uuid(rng),
    deviceId: uuid(rng),
    deviceName: `Scanner-${i + 1}`,
    platform: cap.platform,
    capabilities: cap,
    status: i < 4 ? 'pending' : i === 4 ? 'approved' : 'rejected',
    requestedAt: msAgo(randomInt(rng, 2, 600)),
    sessionId: null,
  }));

  return db;
}

// ---------------------------------------------------------------------------
// DB singleton
// ---------------------------------------------------------------------------

let state: MockDb = generateDb();

export function getDb(): MockDb {
  return state;
}

export function resetDb(seed?: number): MockDb {
  state = generateDb(seed ?? 20260812);
  return state;
}

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export function authUser(): User | null {
  return state.currentUser;
}

export function setAuthUser(user: User | null): void {
  state.currentUser = user;
}

export function login(username: string, password: string): User {
  const user = state.users.find((u) => u.username === username);
  if (!user || password !== 'sigmap-dev') throw new Error('invalid_credentials');
  state.currentUser = user;
  return user;
}

// ---------------------------------------------------------------------------
// Sessions
// ---------------------------------------------------------------------------

export function sessionById(id: string): Session | undefined {
  return state.sessions.find((s) => s.id === id);
}

export function sessionStats(sessionId: string): SessionStats {
  const rows = state.detections.filter((d) => d.sessionId === sessionId);
  const members = state.sessionDevices.filter((sd) => sd.sessionId === sessionId);
  const encryption: SessionStats['encryptionBreakdown'] = {};
  const vendor: Record<string, number> = {};
  const types: SessionStats['deviceTypeBreakdown'] = {};
  let located = 0;
  for (const d of rows) {
    if (d.encryption !== 'unspecified') encryption[d.encryption] = (encryption[d.encryption] ?? 0) + 1;
    if (d.locationFlag !== 'unlocated') located += 1;
    types[d.deviceType] = (types[d.deviceType] ?? 0) + 1;
    const dd = state.detectedDevices.find((x) => x.mac === d.mac);
    if (dd?.vendorName) vendor[dd.vendorName] = (vendor[dd.vendorName] ?? 0) + 1;
  }
  const onlineDevices = members.filter((m) => {
    const dev = state.devices.find((d) => d.id === m.deviceId);
    return dev?.status === 'online';
  }).length;
  return {
    totalDetections: rows.length,
    locatedDetections: located,
    unlocatedDetections: rows.length - located,
    deviceCount: members.length,
    onlineDevices,
    encryptionBreakdown: encryption,
    vendorBreakdown: Object.entries(vendor).sort((a, b) => b[1] - a[1]).slice(0, 12).reduce((acc, [k, v]) => ({ ...acc, [k]: v }), {}),
    deviceTypeBreakdown: types,
  };
}

export function sessionCoverage(sessionId: string, limit = 5000): Coverage {
  const rows = state.detections
    .filter((d) => d.sessionId === sessionId && d.lat != null && d.lon != null)
    .slice(-limit);
  return {
    sessionId,
    points: rows.map((r) => ({ lat: r.lat as number, lon: r.lon as number })),
    count: rows.length,
  };
}

// ---------------------------------------------------------------------------
// Fleet
// ---------------------------------------------------------------------------

export function fleetDeviceFor(device: Device, sessionId?: string): FleetDevice {
  const membership = state.sessionDevices.find(
    (sd) => sd.deviceId === device.id && (!sessionId || sd.sessionId === sessionId),
  );
  const session = membership ? state.sessions.find((s) => s.id === membership.sessionId) : undefined;
  const swarm = membership ? state.swarms.find((w) => w.id === membership.swarmId) : undefined;
  const config = membership ? state.configs.get(`${membership.sessionId}:${device.id}`) ?? null : null;
  const preset = config?.presetId ? state.presets.find((p) => p.id === config.presetId) : undefined;
  return {
    device,
    sessionId: membership?.sessionId ?? null,
    sessionName: session?.name ?? null,
    swarmId: membership?.swarmId ?? null,
    swarmName: swarm?.name ?? null,
    role: membership?.role ?? null,
    drift: config?.pushState === 'pending',
    presetId: config?.presetId ?? null,
    presetName: preset?.name ?? null,
    config,
  };
}

export function listFleet(sessionId?: string): FleetDevice[] {
  return state.devices
    .map((d) => fleetDeviceFor(d, sessionId))
    .sort((a, b) => {
      const rank = { online: 0, offline: 1, error: 2, pending: 3 }[a.device.status] - { online: 0, offline: 1, error: 2, pending: 3 }[b.device.status];
      if (rank !== 0) return rank;
      return a.device.name.localeCompare(b.device.name);
    });
}

export function deviceConfig(sessionId: string, deviceId: string): DeviceConfigDto | null {
  return state.configs.get(`${sessionId}:${deviceId}`) ?? null;
}

export function applyConfigToDevice(
  sessionId: string,
  deviceId: string,
  configJson: string,
  presetId: string | null,
): ConfigPushResult {
  const existing = state.configs.get(`${sessionId}:${deviceId}`);
  const config: DeviceConfigDto = existing ?? {
    sessionId,
    deviceId,
    configJson: null,
    configRev: 0,
    presetId: null,
    source: 'custom',
    pushState: 'acked',
    lastPushId: null,
    updatedAt: new Date().toISOString(),
  };
  config.configJson = configJson;
  config.configRev = existing?.configRev ?? 0;
  config.presetId = presetId;
  config.source = presetId ? 'preset' : 'custom';
  config.pushState = 'pending';
  config.lastPushId = uuid(state.rng);
  config.updatedAt = new Date().toISOString();
  state.configs.set(`${sessionId}:${deviceId}`, config);

  // Simulate the device ACKing after a round-trip (drift clears).
  const pushId = config.lastPushId;
  setTimeout(() => {
    const live = state.configs.get(`${sessionId}:${deviceId}`);
    if (live && live.lastPushId === pushId) {
      live.pushState = state.rng() < 0.1 ? 'failed' : 'acked';
    }
  }, 1400);

  return {
    sessionId,
    deviceId,
    pushId: config.lastPushId as string,
    configRev: config.configRev,
    pushState: 'pending',
  };
}

export function applyConfigToMany(
  sessionId: string,
  configJson: string,
  deviceIds?: string[],
): ConfigPushResult[] {
  const members = state.sessionDevices.filter((sd) => sd.sessionId === sessionId);
  const targets = deviceIds && deviceIds.length ? deviceIds : members.map((m) => m.deviceId);
  return targets.map((deviceId) => applyConfigToDevice(sessionId, deviceId, configJson, null));
}

// ---------------------------------------------------------------------------
// Presets
// ---------------------------------------------------------------------------

export function presetById(id: string): ScanPreset | undefined {
  return state.presets.find((p) => p.id === id);
}

// ---------------------------------------------------------------------------
// Detected devices / detections
// ---------------------------------------------------------------------------

export interface DetectedQuery {
  cursor?: string | null;
  limit?: number;
  deviceType?: string | null;
  search?: string | null;
}

function detectedQueryRows(q: DetectedQuery): DetectedDevice[] {
  let rows = state.detectedDevices;
  if (q.deviceType) rows = rows.filter((d) => d.deviceType === q.deviceType);
  if (q.search) {
    const needle = q.search.trim().toLowerCase();
    rows = rows.filter(
      (d) =>
        d.mac.toLowerCase().includes(needle) ||
        (d.ssidLatest ?? '').toLowerCase().includes(needle) ||
        (d.vendorName ?? '').toLowerCase().includes(needle) ||
        (d.btNameLatest ?? '').toLowerCase().includes(needle),
    );
  }
  return [...rows].sort((a, b) => {
    if (a.lastSeenAt !== b.lastSeenAt) return a.lastSeenAt < b.lastSeenAt ? 1 : -1;
    return a.id < b.id ? 1 : -1;
  });
}

export function listDetectedDevices(q: DetectedQuery): DetectedDevicePage {
  const sorted = detectedQueryRows(q);
  const cursor = q.cursor ? JSON.parse(atob(q.cursor)) as { sortKey: string; id: string } : null;
  let start = 0;
  if (cursor) {
    start = sorted.findIndex((row) => keyIsBefore({ sortKey: row.lastSeenAt, id: row.id }, cursor));
    if (start === -1) return { items: [], nextCursor: null, count: sorted.length };
  }
  const limit = q.limit ?? 50;
  const page = sorted.slice(start, start + limit);
  const next = page.length === limit ? sorted[start + limit] : undefined;
  return {
    items: page,
    nextCursor: next ? encodeCursor(next.lastSeenAt, next.id) : null,
    count: sorted.length,
  };
}

export function detectedDeviceByMac(mac: string): DetectedDevice | undefined {
  return state.detectedDevices.find((d) => d.macNormalized === normalizeMac(mac));
}

export function signalSeries(mac: string): SignalPoint[] {
  const rows = state.detections
    .filter((d) => normalizeMac(d.mac) === normalizeMac(mac))
    .sort((a, b) => (a.detectedAt < b.detectedAt ? -1 : 1));
  return rows.map((r) => ({
    at: r.detectedAt,
    signalDbm: r.signalDbm,
    channel: r.channel,
    locationFlag: r.locationFlag,
    lat: r.lat,
    lon: r.lon,
  }));
}

// ---------------------------------------------------------------------------
// Live snapshot
// ---------------------------------------------------------------------------

export function liveSnapshot(sessionId: string): LiveSessionState {
  const members = state.sessionDevices.filter((sd) => sd.sessionId === sessionId);
  const deviceIds = new Set(members.map((m) => m.deviceId));
  const devices: Record<string, Heartbeat> = {};
  for (const d of state.devices) {
    if (!deviceIds.has(d.id)) continue;
    devices[d.id] = {
      deviceId: d.id,
      at: d.lastHeartbeatAt ?? new Date().toISOString(),
      status: d.status === 'error' ? 'error' : d.status === 'online' ? 'online' : d.status === 'pending' ? 'error' : 'online',
      currentChannel: d.status === 'online' ? randomInt(state.rng, 1, 11) : null,
      batteryPct: d.capabilities.hasBattery ? randomInt(state.rng, 40, 100) : null,
      gpsFix: d.capabilities.hasGps,
      detectionsBuffered: randomInt(state.rng, 0, 200),
      detectionsSentTotal: randomInt(state.rng, 500, 50000),
    };
  }
  const located = state.detections
    .filter((d) => d.sessionId === sessionId && d.lat != null && d.lon != null)
    .slice(-800);
  const points: LiveSessionState['points'] = {};
  for (const d of located) {
    points[d.mac] = {
      mac: d.mac,
      lat: d.lat as number,
      lon: d.lon as number,
      type: d.deviceType,
      ssid: d.ssid,
      signalDbm: d.signalDbm,
      lastSeen: d.detectedAt,
    };
  }
  return {
    sessionId,
    points,
    unlocated: [],
    batchCount: 0,
    lastBatchAt: null,
    devices,
  };
}

// ---------------------------------------------------------------------------
// Live append (used by the mock live source)
// ---------------------------------------------------------------------------

export function appendLiveBatch(sessionId: string, deviceId: string, count: number): Array<{
  type: 'detections';
  sessionId: string;
  batchId: string;
  deviceId: string;
  detections: import('@/lib/domain').LocatedDetection[];
}> {
  const session = sessionById(sessionId);
  if (!session) return [];
  const area = session.boundingArea ?? boundingArea(CITIES[0]);
  const membership = state.sessionDevices.find((sd) => sd.sessionId === sessionId && sd.deviceId === deviceId);
  const swarmId = membership?.swarmId ?? null;
  const batchId = uuid(state.rng);
  const locatedDetections: import('@/lib/domain').LocatedDetection[] = [];

  for (let i = 0; i < count; i++) {
    const { oui, vendor } = ouiFor(state.rng);
    const mac = randomMac(state.rng, oui);
    const type = weightedPick(state.rng, [
      ['AP', 0.5], ['BLUETOOTH', 0.16], ['BT_LE', 0.22], ['CLIENT', 0.12],
    ] as Array<[DeviceType, number]>);
    const roll = state.rng();
    const locationFlag: LocationFlag = roll < 0.55 ? 'gps' : roll < 0.8 ? 'inferred' : 'unlocated';
    const point = locationFlag === 'unlocated' ? null : pointInArea(state.rng, area);
    const detectedAt = new Date().toISOString();
    const ssid = type === 'AP' ? randomSsid(state.rng) : type === 'CLIENT' ? randomClientSsid(state.rng) : null;
    const btName = type !== 'AP' && type !== 'CLIENT' ? randomBtName(state.rng) : null;
    const encryption = type === 'AP' ? weightedPick(state.rng, [
      ['open', 0.15], ['wep', 0.05], ['wpa', 0.1], ['wpa2', 0.6], ['wpa3', 0.1],
    ] as Array<[Encryption, number]>) : 'unspecified';
    const channel = channelFor(type, state.rng);
    const signalDbm = signalFor(type, state.rng);

    // Upsert the detected-devices aggregate.
    const normalized = normalizeMac(mac);
    const head = state.detectedDevices.find((d) => d.macNormalized === normalized);
    if (head) {
      head.lastSeenAt = detectedAt;
      head.detectionCount += 1;
      head.channelLatest = channel;
      if (ssid) head.ssidLatest = ssid;
      if (btName) head.btNameLatest = btName;
      if (point) { head.latitude = point.lat; head.longitude = point.lon; }
    } else {
      state.detectedDevices.push({
        id: uuid(state.rng),
        mac,
        macNormalized: normalized,
        vendorOui: mac.slice(0, 8),
        vendorName: vendor,
        deviceType: type,
        ssidLatest: ssid,
        btNameLatest: btName,
        channelLatest: channel,
        firstSeenAt: detectedAt,
        lastSeenAt: detectedAt,
        latitude: point?.lat ?? null,
        longitude: point?.lon ?? null,
        detectionCount: 1,
      });
    }

    state.detections.push({
      id: state.nextDetectionId++,
      batchId,
      sessionId,
      deviceId,
      swarmId,
      deviceType: type,
      mac,
      ssid,
      btName,
      channel,
      signalDbm,
      encryption,
      detectedAt,
      locationFlag,
      lat: point?.lat ?? null,
      lon: point?.lon ?? null,
      source: 'scanner',
    });

    locatedDetections.push({
      mac,
      deviceType: type,
      ssid,
      btName,
      signalDbm,
      channel,
      encryption,
      detectedAt,
      locationFlag,
      lat: point?.lat ?? null,
      lon: point?.lon ?? null,
      vendorName: vendor,
    });
  }

  return [{ type: 'detections', sessionId, batchId, deviceId, detections: locatedDetections }];
}
