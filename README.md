# Sigmap

Distributed wardriving platform: a fleet of scanner devices (laptop + adapter,
Raspberry Pi, Android phone) ingest WiFi/BLE/client detections into a central
PostGIS backend over RabbitMQ, surfaced live on a map and queryable through a
WiGLE-equivalent device database.

See `docs/architecture.md` for the design decisions (transport, monitor-mode
control, durability model) and `docs/postgres-schema.md`,
`docs/message-contracts.md`, `docs/rabbitmq-topology.md`, `docs/openapi.md`
for the contracts.

## Quick start

```bash
make build                  # build all .NET projects
make gen-proto              # generate TS bindings from contracts/proto
make compose-up             # postgres + rabbitmq + backend-api + bff + frontend + simulator scanner
```

Then open http://localhost:5173 and sign in (`operator` / `sigmap-dev` for dev).
A simulator scanner agent runs by default and feeds synthetic detections so the
live map works without any wireless hardware or elevated privileges. Pair a
real scanner (or this repo's Android app) from the **Pairing** page.

## Verified end-to-end (as built)

* Scanner simulator → RabbitMQ → backend ingest → PostGIS → events → BFF →
  gRPC-Web streaming → live map (scanner ↔ config-push ↔ ack round-trip works).
* Config pushes are durable + acked; `push_state` tracks pending/acked/failed.
* Batch redelivery is deduplicated by `(device_id, batch_id)`.
* Keyset pagination, WiGLE CSV/CSV/GeoJSON exports, WiGLE upload/import,
  cookie auth with operator/viewer roles, coverage heatmap, session
  comparison, GPX route import.
* Test totals: 18 backend integration (Testcontainers), 30 scanner unit,
  12 frontend (Vitest/RTL), Android unit tests.

## Layout

```
contracts/     shared protobuf schemas + generated C# + shared location logic
backend/       core domain API (Domain/Application/Infrastructure/Api)
bff/           UI-facing service: auth, shaping, gRPC-Web live stream
scanner/       field agent: SharpPcap capture, iw control, simulator source
frontend/      React 18 + TS + Mantine + Redux Toolkit / RTK Query
android/       Kotlin + Jetpack Compose client
deploy/        docker-compose.yml (+ prod/test overrides)
```

## Testing

* Backend: xUnit integration tests against real Postgres + RabbitMQ via
  Testcontainers (`backend/Sigmap.Backend.IntegrationTests`).
* Scanner: unit tests for channel-hopping, buffering and location
  interpolation (`scanner/Sigmap.Scanner.Tests`).
* Frontend: Vitest + React Testing Library (config-diff, live-map slices).
* Android: instrumented pairing tests.

`deploy/docker-compose.test.yml` spins up the dependencies for integration
tests when you don't want Testcontainers to manage them.

## Production

`docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml up -d`
builds the frontend statically behind nginx and uses persisted volumes for
Postgres and RabbitMQ. See `deploy/README.md`.

## Host requirements for real scanning

The scanner agent needs `CAP_NET_RAW` + `CAP_NET_ADMIN` (or root) for monitor
mode. In Docker that means `network_mode: host`, `--cap-add=NET_ADMIN
--cap-add=NET_RAW` and the wireless adapter passed through — monitor mode does
not work on a default bridge network. The simulator source needs none of this.
