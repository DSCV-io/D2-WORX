<!--
Copyright (c) DCSV. All rights reserved.
-->

# infra/ — Deployment + Observability

## Layout

```
infra/
  compose/                    Docker Compose files
    compose.yml               Base compose — infrastructure + application services
    compose.prod.yml          Production overrides — Swarm deploy: blocks, dev-tool profiles disabled
  docker/                     Per-service Dockerfiles (one per service)
  observability/              LGTM stack configs (Loki / Grafana / Tempo / Mimir + Alloy collector)
    alloy/config/             Alloy collector config — scrape jobs, log routing, trace forwarding
    grafana/provisioning/     Datasources + dashboards (auto-provisioned at boot)
    loki/config/              Loki config (object storage backend = MinIO buckets)
    mimir/config/             Mimir config
    tempo/config/             Tempo config
```

## Operations (via Makefile from repo root)

```bash
make up                    # Start all infra services
make down                  # Stop all (preserves volumes)
make logs s=d2-postgres    # Tail logs for a specific service
make ps                    # Show running services
make infra                 # Start infrastructure only (PG, Redis, RMQ, Dkron, MinIO, ClamAV)
make otel                  # Start observability stack only (Loki, Tempo, Mimir, cAdvisor, Alloy, Grafana)
make restart s=d2-web      # Restart a specific service
make clean                 # Stop everything + remove volumes + local images (DESTRUCTIVE)
make build                 # Rebuild all images
```

Compose CLI direct (if you need flags Makefile doesn't pass):

```bash
docker compose -f infra/compose/compose.yml --env-file .env.local --env-file .env.secrets <subcommand>
```

Production / staging (Swarm or layered Compose):

```bash
docker compose -f infra/compose/compose.yml -f infra/compose/compose.prod.yml --env-file .env.local --env-file .env.secrets up -d
docker stack deploy -c infra/compose/compose.yml -c infra/compose/compose.prod.yml d2-worx   # Swarm
```

## Observability access

| Service | Port | URL | Credentials |
|---|---|---|---|
| Grafana | 3000 | http://localhost:3000 | `OTEL_USERNAME` / `OTEL_PASSWORD` from `.env.secrets` |
| Portainer | 9443 | https://localhost:9443 | bcrypt hash from `.env.secrets` |
| pgAdmin | 5533 | http://localhost:5533 | `DBA_EMAIL` / `DBA_PASSWORD` from `.env.secrets` |
| RedisInsight | 5540 | http://localhost:5540 | (no auth) |
| Alloy UI | 12345 | http://localhost:12345 | (no auth) |
| RabbitMQ Mgmt | 15672 | http://localhost:15672 | `MQ_USERNAME` / `MQ_PASSWORD` from `.env.secrets` |
| MinIO Console | 9001 | http://localhost:9001 | `S3_USERNAME` / `S3_PASSWORD` from `.env.secrets` |
| Dkron Dashboard | 8888 | http://localhost:8888 | (no auth) |

## Production topology

Eventually Docker Swarm + Portainer (until ~$50K/month MRR), then K8s migration unlocks SPIFFE / NetworkPolicy / sophisticated autoscaling. Pre-launch: Compose on a single VPS.

Two overlay networks (Swarm-time):
- `ingress` — Edge instances attach here; faces L7 LB (Cloudflare)
- `internal` — everyone else (SvelteKit, Files, Courier, Notifications, Audit, future services, infra)

Edge attaches to both. SvelteKit on `internal` only → cannot be reached from outside Edge.

## Migrating between environments

`compose.yml` is the base — same shape between dev and prod. `compose.prod.yml` overlays:
- Pre-built images from `ghcr.io` (instead of `build:` from source)
- `deploy:` blocks for Swarm (resource limits + restart policies)
- Dev-only services disabled via `profiles: [dev-tools]` (pgAdmin, RedisInsight, Portainer)

Maximum dev↔prod parity is a deliberate design choice — Compose syntax IS Swarm `stack.yml` syntax, just different overlay files.
