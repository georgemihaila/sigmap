import { describe, expect, it } from 'vitest';
import { applyConfigToDevice, approvePairing, fleetDeviceFor, locatedDetectedDevices, pairingCode, rejectPairing, resetDb } from './db';

describe('fleet drift indicator', () => {
  it('a device with an acked config is not in drift', () => {
    const db = resetDb();
    const session = db.sessions.find((s) => s.status === 'active')!;
    const membership = db.sessionDevices.find((sd) => sd.sessionId === session.id)!;
    const device = db.devices.find((d) => d.id === membership.deviceId)!;
    const config = db.configs.get(`${session.id}:${device.id}`)!;
    config.pushState = 'acked';
    expect(fleetDeviceFor(device, session.id).drift).toBe(false);
  });

  it('a device with a pending push is in drift', () => {
    const db = resetDb();
    const session = db.sessions.find((s) => s.status === 'active')!;
    const membership = db.sessionDevices.find((sd) => sd.sessionId === session.id)!;
    const device = db.devices.find((d) => d.id === membership.deviceId)!;
    const config = db.configs.get(`${session.id}:${device.id}`)!;
    config.pushState = 'pending';
    expect(fleetDeviceFor(device, session.id).drift).toBe(true);
  });

  it('a device with a failed push is in drift (not synced)', () => {
    const db = resetDb();
    const session = db.sessions.find((s) => s.status === 'active')!;
    const membership = db.sessionDevices.find((sd) => sd.sessionId === session.id)!;
    const device = db.devices.find((d) => d.id === membership.deviceId)!;
    const config = db.configs.get(`${session.id}:${device.id}`)!;
    config.pushState = 'failed';
    expect(fleetDeviceFor(device, session.id).drift).toBe(true);
  });

  it('a device without any config is not in drift', () => {
    const db = resetDb();
    const unassigned = db.devices.find(
      (d) => !db.sessionDevices.some((sd) => sd.deviceId === d.id),
    );
    if (!unassigned) return;
    expect(fleetDeviceFor(unassigned).drift).toBe(false);
  });

  it('a fresh push reports pending and therefore drift until acked', () => {
    const db = resetDb();
    const session = db.sessions.find((s) => s.status === 'active')!;
    const membership = db.sessionDevices.find((sd) => sd.sessionId === session.id)!;
    const device = db.devices.find((d) => d.id === membership.deviceId)!;
    const result = applyConfigToDevice(session.id, device.id, '{"scanWifi":true}', null);
    expect(result.pushState).toBe('pending');
    expect(fleetDeviceFor(device, session.id).drift).toBe(true);
  });
});

describe('pairing — permanent, platform-level', () => {
  it('serves a stable pairing code that is not scoped to a session', () => {
    resetDb();
    const first = pairingCode();
    expect(first.token).toMatch(/^[0-9a-f]{8}$/);
    expect(first.payload).toMatch(/^sigmap-pair:\/\/\?host=.+&token=/);
    expect(first.payload).toContain(`token=${first.token}`);
    // The code is cached, so the QR does not churn between requests.
    expect(pairingCode()).toEqual(first);
  });

  it('approving without a session still pairs the device permanently into the fleet', () => {
    const db = resetDb();
    const pending = db.pairings.find((p) => p.status === 'pending')!;
    approvePairing(pending.deviceId, null);
    expect(db.pairings.find((p) => p.deviceId === pending.deviceId)?.status).toBe('approved');
    const device = db.devices.find((d) => d.id === pending.deviceId);
    expect(device).toBeDefined();
    expect(device?.status).toBe('offline');
    expect(db.sessionDevices.some((sd) => sd.deviceId === pending.deviceId)).toBe(false);
  });

  it('approving with a session assigns the paired device to that session', () => {
    const db = resetDb();
    const pending = db.pairings.find((p) => p.status === 'pending')!;
    const session = db.sessions.find((s) => s.status === 'active')!;
    approvePairing(pending.deviceId, session.id);
    expect(
      db.sessionDevices.some((sd) => sd.deviceId === pending.deviceId && sd.sessionId === session.id),
    ).toBe(true);
    expect(db.devices.find((d) => d.id === pending.deviceId)?.status).toBe('offline');
  });

  it('rejecting a pairing marks it rejected and does not create a fleet device', () => {
    const db = resetDb();
    const pending = db.pairings.find((p) => p.status === 'pending')!;
    rejectPairing(pending.deviceId);
    expect(db.pairings.find((p) => p.deviceId === pending.deviceId)?.status).toBe('rejected');
    expect(db.devices.some((d) => d.id === pending.deviceId)).toBe(false);
  });
});

describe('locatedDetectedDevices — dashboard map source', () => {
  it('returns only detected devices that carry a coordinate', () => {
    const db = resetDb();
    const located = locatedDetectedDevices();
    const expected = db.detectedDevices.filter((d) => d.latitude != null && d.longitude != null);
    expect(located).toHaveLength(expected.length);
    expect(located.length).toBeGreaterThan(0);
    for (const d of located) {
      expect(typeof d.latitude).toBe('number');
      expect(typeof d.longitude).toBe('number');
      expect(d.mac).toMatch(/^([0-9A-F]{2}:){5}[0-9A-F]{2}$/);
    }
  });
});
