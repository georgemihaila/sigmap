# Sigmap — deploy guide

`docker compose -f deploy/docker-compose.yml up -d` brings up the whole dev
stack: Postgres (PostGIS), RabbitMQ, core API, BFF, Vite frontend, and a
simulator scanner agent.

## Dev vs prod

| | Dev (`docker-compose.yml`) | Prod (add `-f docker-compose.prod.yml`) |
|---|---|---|
| frontend | Vite dev server on :5173 | static build behind nginx on :8080 |
| data | named volumes (persist across recreate) | same volumes |
| restart | `unless-stopped` | `always` |
| scanner | simulator (no privileges) | simulator by default; swap to `wifi` + caps for real capture |

## Real monitor-mode scanning

WiFi monitor mode **cannot** run on a default bridge network. On a host with a
wireless adapter:

```yaml
scanner-agent:
  network_mode: host
  cap_add: [NET_ADMIN, NET_RAW]
  devices:
    - /dev/bus/usb/001:/dev/bus/usb/001   # your wireless adapter
  environment:
    SCANNER__SOURCE_TYPE: wifi
    SCANNER__DEVICE_ID: <stable-uuid>
```

The scanner agent needs `CAP_NET_RAW` (libpcap) + `CAP_NET_ADMIN` (iw) or root.
See `docs/architecture.md` for the `iw`-vs-netlink decision.

## Integration tests

`deploy/docker-compose.test.yml` runs ephemeral Postgres+PostGIS and RabbitMQ.
The test suite uses **Testcontainers** by default (real containers, no mocks);
run with:

```bash
make test        # backend + scanner tests (Testcontainers)
npm --prefix frontend run test -- --run
cd android && ./gradlew :app:testDebugUnitTest   # needs JDK 17 + Android SDK
```

## Configuration

| Variable | Default | Where |
|---|---|---|
| `POSTGRES_PASSWORD` | `sigmap` | compose |
| `RABBITMQ_PASSWORD` | `sigmap` | compose |
| `PAIRING_TOKEN` | `sigmap-dev-token` | backend (QR pairing) |
| `PUBLIC_HOST` | `http://localhost:5080` | backend (QR payload host) |
| `AUTH__OPERATOR_USERNAME` / `PASSWORD` | `operator` / `sigmap-dev` | BFF |
| `AUTH__VIEWER_USERNAME` / `PASSWORD` | `viewer` / `viewer-dev` | BFF |
| `RETENTION__DETECTIONSDAYS` | `365` | backend retention job |
| `RETENTION__ANONYMIZECLIENTMACS` | `false` | backend (privacy) |
| `EXPORT__DIRECTORY` | `<app>/exports` | backend |
| `VITE_BFF_URL` | `http://localhost:5090` | frontend gRPC-Web target |

Set these via an `.env` file next to the compose file or shell exports.
