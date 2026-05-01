# D2-WORX — Docker Compose Helpers
# Usage: make <target>
#
# All compose commands use --env-file .env.local (+ .env.secrets) for variable interpolation.

COMPOSE := docker compose -f infra/compose/compose.yml --env-file .env.local --env-file .env.secrets

.PHONY: up down build logs ps infra otel clean restart

## Start all services
up:
	$(COMPOSE) up -d

## Stop all services (preserves volumes)
down:
	$(COMPOSE) down

## Build/rebuild all images
build:
	$(COMPOSE) build

## Tail logs for a service: make logs s=d2-web
logs:
	$(COMPOSE) logs -f $(s)

## Show running services
ps:
	$(COMPOSE) ps

## Start infrastructure only (PG, Redis, RMQ, Dkron, MinIO, ClamAV)
infra:
	$(COMPOSE) up -d d2-postgres d2-redis d2-rabbitmq d2-dkron d2-minio d2-minio-init d2-clamav

## Start observability stack only
otel:
	$(COMPOSE) up -d d2-loki d2-tempo d2-mimir d2-cadvisor d2-alloy d2-grafana

## Stop everything and remove volumes + local images (DESTRUCTIVE)
clean:
	$(COMPOSE) down -v --rmi local

## Restart a service: make restart s=d2-web
restart:
	$(COMPOSE) restart $(s)
