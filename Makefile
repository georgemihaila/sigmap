SHELL := /bin/bash
.PHONY: help build test gen-proto gen-proto-check fmt compose-up compose-down compose-prod-up ci

help:
	@echo "make build          build all .NET projects"
	@echo "make test           run all backend + scanner tests"
	@echo "make gen-proto      regenerate TS types from contracts/proto into frontend/src/generated/proto"
	@echo "make compose-up     docker compose up (dev)"
	@echo "make compose-down   docker compose down"
	@echo "make ci             CI-style: build + test + gen-proto-check"

build:
	dotnet build Sigmap.slnx -c Release

test:
	dotnet test backend/Sigmap.Backend.IntegrationTests -c Release
	dotnet test scanner/Sigmap.Scanner.Tests -c Release

# Regenerate TypeScript bindings from the single source of truth.
gen-proto:
	npm --prefix frontend ci
	npm --prefix frontend run gen:proto

# Fail if the committed generated TS differs from what protoc would emit.
gen-proto-check: gen-proto
	git diff --exit-code frontend/src/generated/proto

compose-up:
	docker compose -f deploy/docker-compose.yml up -d --build

compose-down:
	docker compose -f deploy/docker-compose.yml down

compose-prod-up:
	docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml up -d --build

ci: build gen-proto-check test
