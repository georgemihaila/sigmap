# Sigmap — Distributed Wardriving Platform

Architecture and design decisions. Companion docs: `message-contracts.md`,
`postgres-schema.md`, `rabbitmq-topology.md`, `openapi.md`.

## System overview

```
                    ┌──────────────────────────────┐
                    │           Frontend           │
                    │ React 18 + Mantine + RTK     │
                    └──────────────┬───────────────┘
                                   │ gRPC-Web (stream) + REST (shaped)
                    ┌──────────────▼───────────────┐
                    │   BFF (Sigmap.Bff)           │  auth session, aggregation,
                    │   gRPC-Web server-streaming  │  LiveStream re-broadcast
                    └──────┬──────────────┬────────┘
                           │ REST         │ RabbitMQ (sigmap.events)
               ┌───────────▼───────┐   ┌──▼─────────────┐
               │ Core API backend  │◄──┤  RabbitMQ      │
               │ (Sigmap.Backend)  │──►│  exchanges     │
               │ EF Core / PostGIS │   └──┬───────┬─────┘
               └─────────┬─────────┘      │       │
                         │ SQL            │ AMQP  │ AMQP
                  ┌──────▼──────┐  ┌──────▼──┐  ┌▼─────────────┐
                  │  PostgreSQL │  │Scanner  │  │   Android    │
                  │  + PostGIS  │  │ agent   │  │  client      │
                  └─────────────┘  └─────────┘  └──────────────┘
```

* **Scanner agent** (C#, net8.0, self-contained/AOT): raw AF_PACKET capture
  with in-process radiotap/802.11 parsing, `iw` for monitor/channel control,
  BlueZ over D-Bus for BT/BLE, RabbitMQ publisher with confirms, SQLite offline
  buffer.
* **Core API backend** (.NET 10): domain API, EF Core + PostGIS, RabbitMQ
  consumers, config-push pipeline, WiGLE integration.
* **BFF** (.NET 10): session auth, response shaping, the only gRPC-Web endpoint.
* **Contracts** (`Sigmap.Contracts`, net8.0;net10.0): protobuf schemas (the
  single source of truth) + shared Geo/location logic.
* **Android** (Kotlin + Compose): pairs as a device, reports via HTTPS ingest
  using the same protobuf `Envelope`.

## Decision: monitor-mode / channel control via `iw` (v1)

For v1 the scanner shells out to `iw` for monitor-mode setup and channel
hopping, isolated behind the `ILinuxWireless` interface in
`scanner/Sigmap.Scanner.Agent/Linux`. Frame capture is a raw `AF_PACKET`
socket (`scanner/Sigmap.Scanner.Agent/Capture.Wifi/RawPacketSocket.cs`) with
radiotap and 802.11 parsing done in-process — no Python tooling and no
libpcap/SharpPcap dependency.

**Why not raw nl80211 over netlink for v1:** a native `AF_NETLINK` +
`NL80211_CMD_SET_CHANNEL` implementation requires genl attribute encoding,
sequence tracking, and async netlink ack handling with no maintained managed
library to build on — meaningful, risky work with zero product payoff at this
stage. `iw` is stable and ubiquitous on the target platforms (Raspberry Pi OS,
Debian, Fedora). The capture path (the part that actually produces data) is
native from day one.

`ILinuxWireless` is a thin seam (`SetMonitorMode`, `SetChannel`,
`GetCurrentChannel`, `GetInterfaces`); a raw-netlink implementation can replace
the `iw` adapter later without touching the hop loop or capture code.

**Host requirements:** `CAP_NET_RAW` (capture) and `CAP_NET_ADMIN` (monitor
mode / channel control) or root. In Docker, monitor mode requires
`network_mode: host` + `--cap-add=NET_ADMIN --cap-add=NET_RAW` + the wireless
adapter passed through; it does **not** work on a default bridge network. The
agent ships a simulator source (behind the same `IScanSource` interface) that
needs no privileges at all.

## Decision: real-time transport — gRPC-Web server-streaming

Native gRPC (HTTP/2 trailers-based streaming) is not directly usable from a
browser, so the choice is strictly between the two standard options:

1. **gRPC-Web with server-streaming RPCs**, proxied by the ASP.NET Core
   gRPC-Web middleware (in-process, no Envoy).
2. A WebSocket transport carrying protobuf-framed messages.

**Chosen: option 1 — gRPC-Web.** Rationale: it is the "proper" gRPC-in-browser
path, reuses the exact `.proto` contracts already required for the message bus,
and the BFF middleware serves it in-process. It does not use WebSockets at all
(HTTP/1.1 + `application/grpc-web+proto` framing). The tradeoff vs WebSockets:
gRPC-Web has no client-to-server streaming and less flow control than a raw
socket — acceptable because live data here is essentially server→browser.

Consequently **SignalR is not used anywhere**. This deliberately resolves the
conflict between the "live map via WebSocket/SignalR" wording and the transport
section: one transport, one set of contracts.

**Streaming contract:** `contracts/proto/live.proto` (`LiveStream.Subscribe`,
server-streaming `LiveEvent`). The BFF subscribes to the internal
`sigmap.events` RabbitMQ exchange and re-broadcasts to connected browsers.
Generated TypeScript comes from the same `.proto` files (protoc →
`frontend/src/generated/proto`); there is no hand-maintained duplicate.

## Message contracts

Single source of truth: `contracts/proto/*.proto`. All language targets are
generated:
* C# (backend, BFF, scanner): Grpc.Tools at build time.
* TypeScript (frontend): protoc + `@bufbuild/protoc-gen-es` /
  `protoc-gen-connect-es` (`make gen-proto`).
* Kotlin (Android): protobuf-gradle-plugin at build time.

Every message rides an `Envelope { message_id, schema_version, sent_at_unix_ms,
oneof payload }`. The `message_id` doubles as the idempotency key for batches
and pushes. See `message-contracts.md` for the field-level reference.

## Durability model (no data loss on crash/restart)

1. **RabbitMQ**: durable exchanges/queues, persistent messages (delivery mode
   2). The scanner uses publisher confirms and only deletes a batch from its
   SQLite buffer after the confirm.
2. **Manual ack, not auto-ack**: the backend consumer acks a detection batch
   only after the Postgres transaction writing it commits. Crash between
   receive and commit ⇒ RabbitMQ redelivers on restart.
3. **Idempotent writes**: `detection_batches(device_id, batch_id)` has a unique
   constraint; the consumer inserts the batch row in the same transaction as
   the detections, so a redelivered batch is a no-op.
4. **Config-push acks**: `session_device_config.push_state` is
   `pending|acked|failed`; the backend only considers a push applied after the
   explicit `ConfigAck`. A staleness sweep re-publishes `pending` pushes, so a
   crash mid-propagation is visible and resumable.
5. **Volumes**: `postgres` and `rabbitmq` data live in named Docker volumes.
6. **Restart policy**: `restart: unless-stopped` on `backend-api` and `bff`.

## Scanner agent offline-first

Detections are buffered to local SQLite and batched; if RabbitMQ is
unreachable (e.g. no backhaul while driving), the agent keeps buffering and
flushes on reconnect. Batch boundaries are preserved so idempotency holds
across reconnects.

## Repository layout

```
contracts/      shared protobuf schemas + generated C# + shared Geo logic
backend/        core domain API (Api / Application / Infrastructure / Domain)
bff/            UI-facing service, auth, gRPC-Web streaming
scanner/        field agent (agent + tests)
frontend/       React 18 + TS + Mantine + Redux Toolkit
android/        Kotlin + Jetpack Compose client
deploy/         docker-compose.{yml,prod,test}
docs/           this and companion documents
```
