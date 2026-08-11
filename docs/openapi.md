# API surface

Two services expose HTTP:

* **Core backend** (`backend-api`, `/api/v1`) — clean domain API. OpenAPI
  document served at `/openapi/v1.json`. Devices talk to it directly
  (pairing, config endpoints, HTTPS ingest).
* **BFF** (`bff`, `/api`) — UI glue: cookie session, shaped/aggregated
  responses, gRPC-Web streaming endpoint. The frontend talks only to the BFF.

All list endpoints are keyset-paginated: `?cursor=<base64>&limit=100`; the
cursor encodes `(sort_key, id)`.

## Core backend

### Pairing
| Method | Path | Notes |
|---|---|---|
| POST | `/api/v1/pairing/request` | `PairingRequest` → `PairingResponse`; registers + admits device |
| GET | `/api/v1/pairing/pending` | approval queue |
| POST | `/api/v1/pairing/{id}/approve` | admit device to a session |
| POST | `/api/v1/pairing/{id}/reject` | |
| GET | `/api/v1/pairing/qr/{sessionId}` | QR payload for a session |

### Sessions / swarms
`GET|POST /api/v1/sessions`, `GET|PUT|DELETE /api/v1/sessions/{id}`,
`POST /api/v1/sessions/{id}/archive`, `POST /api/v1/sessions/{id}/swarms`,
`PUT /api/v1/sessions/{id}/swarms/{swarmId}`, plus device assignment
(`POST /api/v1/sessions/{id}/devices/{deviceId}` with swarm_id + role).

### Devices
`GET /api/v1/devices`, `GET /api/v1/devices/{id}`,
`GET /api/v1/devices/{id}/config`.

### Session-device config
`GET /api/v1/sessions/{id}/devices/{deviceId}/config`,
`PUT /api/v1/sessions/{id}/devices/{deviceId}/config` (push via RabbitMQ),
`GET /api/v1/sessions/{id}/devices/{deviceId}/config/push-status`.

### Presets
`GET|POST|PUT|DELETE /api/v1/presets`, `GET /api/v1/presets/builtin`.
Applying a preset = `PUT` to the session-device config (same pipeline).

### Detections / detected devices
`GET /api/v1/detections?session=&cursor=&limit=`,
`GET /api/v1/devices/detected?cursor=&limit=&bbox=&session=`,
`GET /api/v1/devices/detected/{mac}`,
`GET /api/v1/devices/detected/{mac}/signal-series`.

### Ingest (Android)
`POST /api/v1/ingest/batch` — protobuf `Envelope` body (`DetectionBatch`),
published to `sigmap.ingest` internally.

### Exports / WiGLE
`POST /api/v1/exports`, `GET /api/v1/exports/{id}`,
`GET /api/v1/exports/{id}/download`, `POST /api/v1/wigle/upload`,
`POST /api/v1/wigle/import`, `GET|PUT /api/v1/settings/wigle`.

### Watchlist / alerts / stats
`GET|POST|PUT|DELETE /api/v1/watchlist`,
`GET|POST|PUT|DELETE /api/v1/alerts`,
`GET /api/v1/sessions/{id}/stats` (encryption + vendor breakdown).

## BFF

| Method | Path | Notes |
|---|---|---|
| POST | `/api/auth/login` | cookie session set |
| POST | `/api/auth/logout` | |
| GET | `/api/auth/me` | current user + active session bootstrap |
| GET | `/api/fleet` | devices + heartbeat + drift + preset name |
| GET | `/api/detected-devices` | shaped, keyset-paginated |
| GET | `/api/map/coverage` | session coverage polygons |
| gRPC-Web | `/LiveStream/Subscribe` | server-streaming `LiveEvent` |
