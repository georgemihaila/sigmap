# Postgres schema

PostGIS (`geometry` columns, SRID 4326). EF Core owns the schema
(`backend/Sigmap.Backend.Infrastructure`); migrations applied on startup.

## Tables

### users
`id uuid pk`, `username` unique, `password_hash`, `role` enum
`viewer|operator`, `created_at`. Single-owner default; operator role can push
config/manage devices.

### sessions
`id uuid pk`, `name`, `description`, `starts_at`, `ends_at`,
`bounding_area geometry(POLYGON,4326) null`, `status` enum
`planned|active|archived`, `created_at`, `updated_at`.

### swarms
`id uuid pk`, `session_id fk`, `name`. A session's devices are grouped into
swarms for location inference and coverage.

### devices
`id uuid pk`, `name`, `device_type`, `capabilities jsonb`, `status` enum
`offline|online|error`, `last_heartbeat_at`, `last_known_ip`, `paired_at`.
Physical scanner units.

### session_devices
`session_id fk`, `device_id fk`, `swarm_id fk null`, `role`, `joined_at`.
Membership of a device in a session.

### session_device_config
Per-(session, device) config + push-state tracking:
`session_id fk`, `device_id fk`, `config jsonb`, `config_rev`,
`preset_id fk null`, `source` enum `preset|custom`,
`push_state` enum `pending|acked|failed` default `acked`,
`last_push_id uuid null`, `updated_at`, unique `(session_id, device_id)`.

### scan_presets
Reusable, non-tied config templates: `id uuid pk`, `name`, `owner_id fk null`,
`config jsonb`, `is_builtin bool`, `created_at`, `updated_at`. Seeded with the
built-in **"Off"** preset.

### detection_batches
Idempotency gate: `device_id`, `batch_id`, `session_id`, `received_at`,
unique `(device_id, batch_id)`.

### detections
`id bigserial pk`, `batch_id fk`, `session_id`, `device_id`, `swarm_id`,
`device_type` enum, `mac`, `ssid`, `bt_name`, `channel`, `signal_dbm`,
`encryption`, `detected_at`, `location_flag` enum `gps|inferred|unlocated`,
`geom geometry(POINT,4326) null`, `source` enum `scanner|wigle_import`.
Indexes: `(detected_at desc, id desc)` (keyset pagination), `session_id`.

### detected_devices
The WiGLE-equivalent aggregate head, unique per MAC:
`id uuid pk`, `mac` unique, `mac_normalized`, `vendor_oui`, `vendor_name`,
`device_type`, `ssid_latest`, `first_seen_at`, `last_seen_at`,
`geom geometry(POINT,4326) null`. Index `(last_seen_at desc, id desc)`.
All observed points live in `detections`.

### wigle_credentials
`id`, `owner_id`, `api_name`, `api_key`, `username`, `password`, `is_default`.

### watchlist
`id`, `owner_id`, `mac` or `ssid`, `tag`, `action` enum `highlight|exclude`,
`notes`.

### alert_rules
`id`, `owner_id`, `rule_type`, `config jsonb`, `enabled`, `last_triggered_at`.
Built-ins: watched BSSID reappears; swarm member offline mid-session.

### exports
`id`, `session_id`, `format` enum `wigle_csv|csv|geojson`, `status`,
`file_path`, `created_at`, `owner_id`.

### session_gps_samples
Heatmap/coverage: `session_id`, `device_id`, `geom geometry(POINT,4326)`,
`at_unix_ms`. Indexed on `session_id`.

## Location model

`detections.location_flag` is one of:
* `gps` — resolved from the device's own GPS track.
* `inferred` — time-interpolated from a GPS-equipped device in the same swarm.
* `unlocated` — no GPS evidence within the window; **never fabricated**.

## Pagination

Keyset/cursor pagination everywhere unbounded:
`WHERE (last_seen_at, id) < (@cursor_last_seen, @cursor_id)
 ORDER BY last_seen_at DESC, id DESC LIMIT @limit` — never `OFFSET`.
