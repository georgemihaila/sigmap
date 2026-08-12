import { describe, expect, it } from 'vitest';
import { applyConfigToDevice, fleetDeviceFor, resetDb } from './db';

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
