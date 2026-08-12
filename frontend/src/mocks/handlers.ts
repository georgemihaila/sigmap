import { http, HttpResponse } from 'msw';

import type { ConfigDiff, ExportFormat, ExportRecord } from '@/lib/domain';
import { parseScanConfig, diffScanConfigs, stringifyScanConfig, mergeInterfaces } from '@/lib/scanConfig';
import {
  applyConfigToDevice, applyConfigToMany, authUser, deviceConfig, detectedDeviceByMac,
  getDb, listDetectedDevices, listFleet, liveSnapshot, login, presetById,
  sessionById, sessionCoverage, sessionStats, setAuthUser, signalSeries,
} from './db';

const JSON401 = () => HttpResponse.json({ error: 'unauthorized' }, { status: 401 });

function requireAuth(): boolean {
  return authUser() !== null;
}

export const handlers = [
  // -------------------------------------------------------------------------
  // Auth
  // -------------------------------------------------------------------------
  http.get('/api/auth/me', () => {
    const user = authUser();
    return user ? HttpResponse.json({ user }) : JSON401();
  }),
  http.post('/api/auth/login', async ({ request }) => {
    const body = (await request.json()) as { username?: string; password?: string };
    try {
      const user = login(body.username ?? '', body.password ?? '');
      return HttpResponse.json({ user });
    } catch {
      return HttpResponse.json({ error: 'invalid_credentials' }, { status: 401 });
    }
  }),
  http.post('/api/auth/logout', () => {
    setAuthUser(null);
    return new HttpResponse(null, { status: 204 });
  }),

  // -------------------------------------------------------------------------
  // Sessions
  // -------------------------------------------------------------------------
  http.get('/api/sessions', () => {
    if (!requireAuth()) return JSON401();
    const sessions = [...getDb().sessions].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return HttpResponse.json(sessions);
  }),
  http.post('/api/sessions', async ({ request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const body = (await request.json()) as { name?: string; description?: string; startsAt?: string | null; endsAt?: string | null };
    const session = {
      id: crypto.randomUUID(),
      name: body.name ?? 'Untitled session',
      description: body.description ?? null,
      startsAt: body.startsAt ?? null,
      endsAt: body.endsAt ?? null,
      status: 'planned' as const,
      boundingArea: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    db.sessions.push(session);
    return HttpResponse.json(session, { status: 201 });
  }),
  http.get('/api/sessions/:id', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const session = sessionById(String(params.id));
    return session ? HttpResponse.json(session) : HttpResponse.json({ error: 'not_found' }, { status: 404 });
  }),
  http.put('/api/sessions/:id', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const session = sessionById(String(params.id));
    if (!session) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    const body = (await request.json()) as { name?: string; description?: string; startsAt?: string | null; endsAt?: string | null };
    if (body.name !== undefined) session.name = body.name;
    if (body.description !== undefined) session.description = body.description;
    if (body.startsAt !== undefined) session.startsAt = body.startsAt;
    if (body.endsAt !== undefined) session.endsAt = body.endsAt;
    session.updatedAt = new Date().toISOString();
    void db;
    return HttpResponse.json(session);
  }),
  http.post('/api/sessions/:id/archive', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const session = sessionById(String(params.id));
    if (!session) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    session.status = 'archived';
    session.endsAt = new Date().toISOString();
    session.updatedAt = new Date().toISOString();
    return HttpResponse.json(session);
  }),
  http.get('/api/sessions/:id/stats', ({ params }) => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(sessionStats(String(params.id)));
  }),
  http.get('/api/sessions/:id/swarms', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const swarms = getDb().swarms.filter((w) => w.sessionId === String(params.id));
    return HttpResponse.json(swarms);
  }),
  http.get('/api/sessions/:id/coverage', ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const url = new URL(request.url);
    const limit = Number(url.searchParams.get('limit') ?? 5000);
    return HttpResponse.json(sessionCoverage(String(params.id), limit));
  }),
  http.get('/api/sessions/:id/live', ({ params }) => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(liveSnapshot(String(params.id)));
  }),

  // -------------------------------------------------------------------------
  // Fleet
  // -------------------------------------------------------------------------
  http.get('/api/fleet', () => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(listFleet());
  }),
  http.get('/api/sessions/:id/fleet', ({ params }) => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(listFleet(String(params.id)));
  }),

  // -------------------------------------------------------------------------
  // Config (session bulk + per-device)
  // -------------------------------------------------------------------------
  http.get('/api/sessions/:id/devices/:deviceId/config/push-status', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const config = deviceConfig(String(params.id), String(params.deviceId));
    return HttpResponse.json({
      pushState: config?.pushState ?? 'acked',
      lastPushId: config?.lastPushId ?? null,
      configRev: config?.configRev ?? 0,
    });
  }),
  http.get('/api/sessions/:id/devices/:deviceId/config', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const config = deviceConfig(String(params.id), String(params.deviceId));
    if (!config) return HttpResponse.json({ error: 'no_config' }, { status: 404 });
    return HttpResponse.json(config);
  }),
  http.put('/api/sessions/:id/devices/:deviceId/config', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const body = (await request.json()) as { configJson?: string; presetId?: string | null };
    const result = applyConfigToDevice(
      String(params.id),
      String(params.deviceId),
      body.configJson ?? '',
      body.presetId ?? null,
    );
    return HttpResponse.json(result);
  }),

  http.post('/api/sessions/:id/config/preview', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const sessionId = String(params.id);
    const body = (await request.json()) as { configJson?: string };
    const target = parseScanConfig(body.configJson);
    const diffs: ConfigDiff[] = db.sessionDevices
      .filter((sd) => sd.sessionId === sessionId)
      .map((sd) => {
        const device = db.devices.find((d) => d.id === sd.deviceId);
        const current = parseScanConfig(deviceConfig(sessionId, sd.deviceId)?.configJson);
        const changes = diffScanConfigs(current, target);
        return {
          deviceId: sd.deviceId,
          deviceName: device?.name ?? sd.deviceId,
          changes,
          equal: changes.length === 0,
        };
      });
    return HttpResponse.json(diffs);
  }),
  http.put('/api/sessions/:id/config', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const body = (await request.json()) as { configJson?: string; deviceIds?: string[] };
    const results = applyConfigToMany(
      String(params.id),
      body.configJson ?? '',
      body.deviceIds,
    );
    return HttpResponse.json(results);
  }),

  http.get('/api/sessions/:id/presets/:presetId/preview', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const sessionId = String(params.id);
    const preset = presetById(String(params.presetId));
    if (!preset) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    const target = parseScanConfig(preset.configJson);
    const diffs: ConfigDiff[] = db.sessionDevices
      .filter((sd) => sd.sessionId === sessionId)
      .map((sd) => {
        const device = db.devices.find((d) => d.id === sd.deviceId);
        const current = parseScanConfig(deviceConfig(sessionId, sd.deviceId)?.configJson);
        const merged = mergeInterfaces(target, current.interfaces.map((i) => i.name));
        const changes = diffScanConfigs(current, merged);
        return {
          deviceId: sd.deviceId,
          deviceName: device?.name ?? sd.deviceId,
          changes,
          equal: changes.length === 0,
        };
      });
    return HttpResponse.json(diffs);
  }),
  http.post('/api/sessions/:id/presets/:presetId/apply', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const sessionId = String(params.id);
    const preset = presetById(String(params.presetId));
    if (!preset) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    const body = (await request.json()) as { deviceIds?: string[] };
    const db = getDb();
    const members = db.sessionDevices.filter((sd) => sd.sessionId === sessionId);
    const targets = body.deviceIds?.length ? body.deviceIds : members.map((m) => m.deviceId);
    const results = targets.map((deviceId) => {
      const current = parseScanConfig(deviceConfig(sessionId, deviceId)?.configJson);
      const merged = mergeInterfaces(parseScanConfig(preset.configJson), current.interfaces.map((i) => i.name));
      return applyConfigToDevice(sessionId, deviceId, stringifyScanConfig(merged), preset.id);
    });
    return HttpResponse.json(results);
  }),

  // -------------------------------------------------------------------------
  // Presets
  // -------------------------------------------------------------------------
  http.get('/api/presets', () => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(getDb().presets);
  }),
  http.post('/api/presets', async ({ request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const body = (await request.json()) as { name?: string; description?: string; configJson?: string };
    const preset = {
      id: crypto.randomUUID(),
      name: body.name ?? 'Untitled preset',
      description: body.description ?? null,
      ownerId: authUser()?.id ?? null,
      configJson: body.configJson ?? stringifyScanConfig(parseScanConfig(undefined)),
      isBuiltin: false,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    db.presets.push(preset);
    return HttpResponse.json(preset, { status: 201 });
  }),
  http.put('/api/presets/:id', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const preset = db.presets.find((p) => p.id === String(params.id));
    if (!preset) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    const body = (await request.json()) as { name?: string; description?: string; configJson?: string };
    if (body.name !== undefined) preset.name = body.name;
    if (body.description !== undefined) preset.description = body.description;
    if (body.configJson !== undefined) preset.configJson = body.configJson;
    preset.updatedAt = new Date().toISOString();
    return HttpResponse.json(preset);
  }),
  http.delete('/api/presets/:id', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const idx = db.presets.findIndex((p) => p.id === String(params.id));
    if (idx === -1) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    db.presets.splice(idx, 1);
    return new HttpResponse(null, { status: 204 });
  }),

  // -------------------------------------------------------------------------
  // Detected devices (keyset paginated)
  // -------------------------------------------------------------------------
  http.get('/api/detected-devices', ({ request }) => {
    if (!requireAuth()) return JSON401();
    const url = new URL(request.url);
    const page = listDetectedDevices({
      cursor: url.searchParams.get('cursor'),
      limit: Number(url.searchParams.get('limit') ?? 50),
      deviceType: url.searchParams.get('deviceType'),
      search: url.searchParams.get('search'),
    });
    return HttpResponse.json(page);
  }),
  http.get('/api/devices/detected/:mac/signal-series', ({ params }) => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(signalSeries(String(params.mac)));
  }),
  http.get('/api/devices/detected/:mac', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const device = detectedDeviceByMac(String(params.mac));
    return device ? HttpResponse.json(device) : HttpResponse.json({ error: 'not_found' }, { status: 404 });
  }),

  // -------------------------------------------------------------------------
  // Exports / WiGLE
  // -------------------------------------------------------------------------
  http.get('/api/exports', () => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json([...getDb().exports].sort((a, b) => b.createdAt.localeCompare(a.createdAt)));
  }),
  http.post('/api/exports', async ({ request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const body = (await request.json()) as { sessionId?: string | null; format?: string };
    const format: ExportFormat = body.format === 'csv' || body.format === 'geojson' ? body.format : 'wigle_csv';
    const record: ExportRecord = {
      id: crypto.randomUUID(),
      sessionId: body.sessionId ?? null,
      sessionName: body.sessionId ? sessionById(body.sessionId)?.name ?? null : null,
      format,
      status: 'queued',
      fileName: null,
      createdAt: new Date().toISOString(),
      ownerId: authUser()?.id ?? '',
      sizeBytes: null,
      rowCount: null,
    };
    db.exports.unshift(record);
    // Simulate the export job completing.
    setTimeout(() => {
      record.status = 'done';
      record.fileName = `sigmap-${record.id.slice(0, 8)}.${format === 'geojson' ? 'geojson' : 'csv'}`;
      record.rowCount = Math.floor(Math.random() * 3900) + 100;
      record.sizeBytes = Math.floor(Math.random() * 895000) + 5000;
    }, 1800);
    return HttpResponse.json(record, { status: 202 });
  }),
  http.get('/api/exports/:id/download', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const record = getDb().exports.find((e) => e.id === String(params.id));
    if (!record || record.status !== 'done') {
      return HttpResponse.json({ error: 'not_ready' }, { status: 409 });
    }
    const lines = [
      'MAC,SSID,AuthMode,FirstTime,LastTime,Channel,RSSI,CurrentLatitude,CurrentLongitude,AltitudeMeters,AccuracyMeters,Type',
      '00:1A:2B:00:00:01,MyWiFi,[WPA2-PSK-CCMP][ESS],2026-08-11 10:00:00,2026-08-12 09:00:00,6,-52,48.137,11.575,0,8,WIFI',
    ];
    return new HttpResponse(lines.join('\n'), {
      headers: { 'Content-Type': 'text/csv' },
    });
  }),
  http.post('/api/wigle/upload', async ({ request }) => {
    if (!requireAuth()) return JSON401();
    const body = (await request.json()) as { sessionId?: string };
    return HttpResponse.json({
      status: 'queued',
      message: body.sessionId ? `Uploading session observations to WiGLE…` : 'Upload requires a session.',
    });
  }),
  http.post('/api/wigle/import', () => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json({ imported: 14 });
  }),
  http.get('/api/settings/wigle', () => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(getDb().wigleSettings);
  }),
  http.put('/api/settings/wigle', async ({ request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const body = (await request.json()) as { apiName?: string; apiKey?: string; username?: string; password?: string };
    if (body.apiName !== undefined) db.wigleSettings.apiName = body.apiName;
    if (body.apiKey !== undefined) db.wigleSettings.apiKeySet = body.apiKey.length > 0;
    if (body.username !== undefined) db.wigleSettings.username = body.username;
    if (body.password !== undefined) db.wigleSettings.passwordSet = body.password.length > 0;
    return HttpResponse.json(db.wigleSettings);
  }),

  // -------------------------------------------------------------------------
  // Pairing
  // -------------------------------------------------------------------------
  http.get('/api/pairing/pending', () => {
    if (!requireAuth()) return JSON401();
    return HttpResponse.json(
      getDb().pairings.filter((p) => p.status === 'pending').sort((a, b) => a.requestedAt.localeCompare(b.requestedAt)),
    );
  }),
  http.post('/api/pairing/:deviceId/approve', async ({ params, request }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const pairing = db.pairings.find((p) => p.deviceId === String(params.deviceId));
    if (!pairing) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    const body = (await request.json()) as { sessionId?: string };
    pairing.status = 'approved';
    pairing.sessionId = body.sessionId ?? null;
    return new HttpResponse(null, { status: 204 });
  }),
  http.post('/api/pairing/:deviceId/reject', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const db = getDb();
    const pairing = db.pairings.find((p) => p.deviceId === String(params.deviceId));
    if (!pairing) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    pairing.status = 'rejected';
    return new HttpResponse(null, { status: 204 });
  }),
  http.get('/api/pairing/qr/:sessionId', ({ params }) => {
    if (!requireAuth()) return JSON401();
    const session = sessionById(String(params.sessionId));
    if (!session) return HttpResponse.json({ error: 'not_found' }, { status: 404 });
    const token = crypto.randomUUID().replace(/-/g, '').slice(0, 8);
    return HttpResponse.json({
      sessionId: session.id,
      token,
      payload: `sigmap-pair://${session.id}?token=${token}`,
    });
  }),
];
