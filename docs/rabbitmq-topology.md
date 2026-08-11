# RabbitMQ topology

All exchanges/queues durable, all messages persistent (delivery mode 2),
publisher confirms on the scanner side.

## Exchanges (all topic, durable)

| Exchange | Routing key | Bound queue | Direction |
|---|---|---|---|
| `sigmap.ingest` | `detection.batch.<device_id>` | `ingest.detection_batches` | scanner → backend |
| `sigmap.config` | `config.push.<device_id>` | `config.push.<device_id>` | backend → scanner |
| `sigmap.heartbeat` | `heartbeat.<device_id>` | `heartbeat.all` | scanner → backend |
| `sigmap.events` | `event.*` | `bff.live` | backend → BFF |
| `sigmap.dlx` | (dead-letter) | `*.dlq` | poison batches |

## Ingestion (scanner → backend)

* Scanner flushes a batch every `batch_interval_ms` (default 1.5s) to
  `sigmap.ingest` with routing key `detection.batch.<device_id>`.
* Backend binds one durable queue `ingest.detection_batches`
  (`detection.batch.*`), single consumer, **manual ack after the Postgres tx
  commits**. Redelivery dedupes via the `detection_batches` unique constraint.
* Failed batches (schema mismatch, poison) go to `sigmap.dlx`.

## Config push (backend → scanner)

* Per-device durable queue `config.push.<device_id>`, declared and bound when
  the device pairs, deleted on unpair. A push published while the device is
  offline sits in its queue until it reconnects (offline-first by design).
* Push lifecycle: publish + set `push_state=pending` → scanner applies →
  `ConfigAck` → `push_state=acked|failed`. A staleness sweep re-publishes
  `pending` pushes that never acked.

## Heartbeat (scanner → backend)

* `sigmap.heartbeat`, routing key `heartbeat.<device_id>`, bound by the
  backend queue `heartbeat.all`. Drives fleet status and the config-push
  staleness sweep. Heartbeats are also forwarded to `sigmap.events` so the BFF
  can stream them.

## Internal events (backend → BFF)

* `sigmap.events` (`event.*`) carries post-ingest events (located batches,
  device status, config-push state) consumed by the BFF's `bff.live` queue and
  re-broadcast over gRPC-Web. This keeps the browser streaming path decoupled
  from Postgres and from the ingest path.
