# Message contracts

Source of truth: `contracts/proto/*.proto` (all packages `sigmap`,
streaming in `sigmap.live`). Generated for C#, TypeScript and Kotlin; there is
no hand-maintained mirror.

## Envelope

Every message crossing a transport boundary (RabbitMQ, HTTPS ingest) is an
`Envelope`:

| Field | Type | Notes |
|---|---|---|
| `message_id` | string | Unique per logical operation; idempotency key for batches/pushes |
| `schema_version` | string | `"1"`. Consumers reject unknown versions |
| `sent_at_unix_ms` | uint64 | UTC epoch ms |
| `payload` | oneof | `detection_batch` \| `config_push` \| `config_ack` \| `heartbeat` \| `pairing_request` \| `pairing_response` |

## DetectionBatch (scanner → backend, RabbitMQ `sigmap.ingest` / Android HTTPS)

| Field | Type | Notes |
|---|---|---|
| `device_id` | string | Scanner device UUID |
| `batch_id` | string | Unique per batch; Postgres unique constraint |
| `has_gps` | bool | Device has a GPS receiver |
| `gps_samples` | repeated GpsSample | Device GPS track for the batch window |
| `detections` | repeated Detection | Raw detections |

`Detection`: `device_type` (AP/BLUETOOTH/BT_LE/CLIENT), `mac`, `ssid`,
`bt_name`, `bt_uuid`, `signal_dbm`, `channel`, `encryption` (AP only),
`detected_at_unix_ms`, `vendor_oui` (first 3 MAC bytes).

`GpsSample`: `lat`, `lon`, `accuracy_m`, `at_unix_ms`.

**Locations are resolved server-side** from `gps_samples` + swarm context —
detections never carry a location.

## ConfigPush (backend → scanner, RabbitMQ `sigmap.config`)

`push_id` (ack target), `device_id`, `session_id`, `preset_id` (when pushed
from a preset), `config`, `issued_at_unix_ms`.

`ScanConfig`: `interfaces[]` (`name`, `driver`, `monitor_mode`, `enabled`,
`channels[]`), `channel_hop_ms`, `scan_wifi`, `scan_bluetooth`, `scan_bt_le`,
`scan_clients_promiscuous`, `batch_interval_ms` (default 1500).

The built-in **"Off" preset** is a `ScanConfig` with no interfaces and all
toggles false. Newly paired devices always start here.

## ConfigAck (scanner → backend, RabbitMQ `sigmap.ingest`)

`push_id`, `device_id`, `status` (APPLIED/REJECTED/FAILED), `error`,
`applied_at_unix_ms`.

## Heartbeat (scanner → backend, RabbitMQ `sigmap.heartbeat`)

`device_id`, `at_unix_ms`, `status` (ONLINE/BUSY/ERROR), `interfaces[]`
(`name`, `current_channel`, `in_monitor_mode`, `enabled`), `battery_pct`,
`gps_fix`, `detections_buffered`, `detections_sent_total`.

## Pairing (REST only — never RabbitMQ)

`PairingRequest`: `device_id`, `device_name`, `token_hash` (SHA-256 of the
short-lived QR token), `capabilities` (`has_wifi_monitor`, `has_bluetooth`,
`has_gps`, `has_battery`, `platform`), `sent_at_unix_ms`.

`PairingResponse`: `status` (PENDING/APPROVED/REJECTED/TOKEN_EXPIRED),
`device_id`, `session_id`, `initial_config` (always "Off"), `server_time_unix_ms`.

## Live (BFF → browser, gRPC-Web server-streaming)

`SubscribeRequest { session_id, client_id }` →
`stream LiveEvent` with oneof `detections` (re-broadcast of an ingested,
already-located batch), `device` (heartbeat), `config` (push status), `fleet`
(snapshot).
