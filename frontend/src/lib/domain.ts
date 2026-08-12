/**
 * Domain types — the single source of truth for the frontend contract.
 *
 * Field names/shapes mirror the backend Postgres schema and message contracts
 * (camelCased at the BFF boundary). Enums are string literals matching the
 * Postgres enum values.
 */

export type UserRole = 'viewer' | 'operator';
export type SessionStatus = 'planned' | 'active' | 'archived';
export type DeviceStatus = 'pending' | 'offline' | 'online' | 'error';
export type PushState = 'pending' | 'acked' | 'failed';
export type ConfigSource = 'preset' | 'custom';
export type LocationFlag = 'gps' | 'inferred' | 'unlocated';
export type DetectionSource = 'scanner' | 'wigle_import';
export type DeviceType = 'AP' | 'BLUETOOTH' | 'BT_LE' | 'CLIENT';
export type Encryption = 'unspecified' | 'open' | 'wep' | 'wpa' | 'wpa2' | 'wpa3';
export type ExportFormat = 'wigle_csv' | 'csv' | 'geojson';
export type ExportStatus = 'queued' | 'running' | 'done' | 'failed';
export type PairingStatus = 'pending' | 'approved' | 'rejected' | 'expired';

// ---------------------------------------------------------------------------
// Users / auth
// ---------------------------------------------------------------------------

export interface User {
  id: string;
  username: string;
  role: UserRole;
}

// ---------------------------------------------------------------------------
// Sessions, swarms
// ---------------------------------------------------------------------------

export interface BoundingArea {
  latMin: number;
  lonMin: number;
  latMax: number;
  lonMax: number;
}

export interface Session {
  id: string;
  name: string;
  description: string | null;
  startsAt: string | null;
  endsAt: string | null;
  status: SessionStatus;
  boundingArea: BoundingArea | null;
  createdAt: string;
  updatedAt: string;
}

export interface Swarm {
  id: string;
  sessionId: string;
  name: string;
  deviceCount?: number;
}

export interface SessionStats {
  totalDetections: number;
  locatedDetections: number;
  unlocatedDetections: number;
  deviceCount: number;
  onlineDevices: number;
  encryptionBreakdown: Partial<Record<Encryption, number>>;
  vendorBreakdown: Record<string, number>;
  deviceTypeBreakdown: Partial<Record<DeviceType, number>>;
}

export interface Coverage {
  sessionId: string;
  points: Array<{ lat: number; lon: number }>;
  count: number;
}

// ---------------------------------------------------------------------------
// Fleet (scanner hardware / devices)
// ---------------------------------------------------------------------------

export interface DeviceCapabilities {
  hasWifiMonitor: boolean;
  hasBluetooth: boolean;
  hasGps: boolean;
  hasBattery: boolean;
  platform: string;
}

export interface Device {
  id: string;
  name: string;
  platform: string;
  capabilities: DeviceCapabilities;
  status: DeviceStatus;
  lastHeartbeatAt: string | null;
  lastKnownIp: string | null;
  pairedAt: string;
}

/** BFF-shaped fleet row: device + live membership + config-drift summary. */
export interface FleetDevice {
  device: Device;
  sessionId: string | null;
  sessionName: string | null;
  swarmId: string | null;
  swarmName: string | null;
  role: string | null;
  /** True when the live config is not yet acked / differs from the target. */
  drift: boolean;
  presetId: string | null;
  presetName: string | null;
  config: DeviceConfigDto | null;
}

// ---------------------------------------------------------------------------
// Scan config / presets
// ---------------------------------------------------------------------------

export interface InterfaceConfig {
  name: string;
  driver: string;
  monitorMode: boolean;
  enabled: boolean;
  channels: number[];
}

export interface ScanConfig {
  interfaces: InterfaceConfig[];
  channelHopMs: number;
  scanWifi: boolean;
  scanBluetooth: boolean;
  scanBtLe: boolean;
  scanClientsPromiscuous: boolean;
  batchIntervalMs: number;
}

export interface DeviceConfigDto {
  sessionId: string;
  deviceId: string;
  configJson: string | null;
  configRev: number;
  presetId: string | null;
  source: ConfigSource;
  pushState: PushState;
  lastPushId: string | null;
  updatedAt: string;
}

export interface ConfigPushResult {
  sessionId: string;
  deviceId: string;
  pushId: string;
  configRev: number;
  pushState: PushState;
}

export interface ScanPreset {
  id: string;
  name: string;
  description: string | null;
  ownerId: string | null;
  configJson: string;
  isBuiltin: boolean;
  createdAt: string;
  updatedAt: string;
}

/** One discrete change between two scan configs (for the diff/preview UI). */
export interface ConfigChange {
  path: string;
  before: string;
  after: string;
}

export interface ConfigDiff {
  deviceId: string;
  deviceName: string;
  changes: ConfigChange[];
  equal: boolean;
}

// ---------------------------------------------------------------------------
// Detections / detected devices (the WiGLE-equivalent table)
// ---------------------------------------------------------------------------

export interface DetectedDevice {
  id: string;
  mac: string;
  macNormalized: string;
  vendorOui: string | null;
  vendorName: string | null;
  deviceType: DeviceType;
  ssidLatest: string | null;
  btNameLatest: string | null;
  channelLatest: number | null;
  firstSeenAt: string;
  lastSeenAt: string;
  latitude: number | null;
  longitude: number | null;
  detectionCount: number;
}

export interface DetectedDevicePage {
  items: DetectedDevice[];
  nextCursor: string | null;
  count: number;
}

export interface SignalPoint {
  at: string;
  signalDbm: number;
  channel: number;
  locationFlag: LocationFlag;
  lon: number | null;
  lat: number | null;
}

// ---------------------------------------------------------------------------
// Live stream (mirrors the BFF gRPC-Web LiveEvent oneof)
// ---------------------------------------------------------------------------

export interface LocatedDetection {
  mac: string;
  deviceType: DeviceType;
  ssid: string | null;
  btName: string | null;
  signalDbm: number;
  channel: number;
  encryption: Encryption;
  detectedAt: string;
  locationFlag: LocationFlag;
  lat: number | null;
  lon: number | null;
  vendorName: string | null;
}

export interface Heartbeat {
  deviceId: string;
  at: string;
  status: 'online' | 'busy' | 'error';
  currentChannel: number | null;
  batteryPct: number | null;
  gpsFix: boolean;
  detectionsBuffered: number;
  detectionsSentTotal: number;
}

export type LiveEvent =
  | {
      type: 'detections';
      sessionId: string;
      batchId: string;
      deviceId: string;
      detections: LocatedDetection[];
    }
  | { type: 'device'; sessionId: string; heartbeat: Heartbeat }
  | {
      type: 'config';
      sessionId: string;
      deviceId: string;
      pushId: string;
      status: PushState;
    }
  | { type: 'fleet'; sessionId: string; devices: Heartbeat[] };

// ---------------------------------------------------------------------------
// Pairing
// ---------------------------------------------------------------------------

export interface PairingRequest {
  id: string;
  deviceId: string;
  deviceName: string;
  platform: string;
  capabilities: DeviceCapabilities;
  status: PairingStatus;
  requestedAt: string;
  sessionId: string | null;
}

export interface PairingQr {
  sessionId: string;
  token: string;
  payload: string;
}

// ---------------------------------------------------------------------------
// Exports / WiGLE
// ---------------------------------------------------------------------------

export interface ExportRecord {
  id: string;
  sessionId: string | null;
  sessionName: string | null;
  format: ExportFormat;
  status: ExportStatus;
  fileName: string | null;
  createdAt: string;
  ownerId: string;
  sizeBytes: number | null;
  rowCount: number | null;
}

export interface WigleSettings {
  apiName: string | null;
  apiKeySet: boolean;
  username: string | null;
  passwordSet: boolean;
}

// ---------------------------------------------------------------------------
// Live map cache shape
// ---------------------------------------------------------------------------

export interface LiveMapPoint {
  mac: string;
  lat: number;
  lon: number;
  type: DeviceType;
  ssid: string | null;
  signalDbm: number;
  lastSeen: string;
}

export interface LiveSessionState {
  sessionId: string;
  /** Located detections keyed by MAC. */
  points: Record<string, LiveMapPoint>;
  /** MACs seen since subscribe that had no location. */
  unlocated: LocatedDetection[];
  batchCount: number;
  lastBatchAt: string | null;
  devices: Record<string, Heartbeat>;
}
