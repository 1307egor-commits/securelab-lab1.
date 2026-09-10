#!/usr/bin/env bash
# Піднімає PostgreSQL через Docker Compose і виконує dotnet test.
# Не замінює reset після ручних експериментів із даними.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

ENV_FILE="infra/.env.example"
[ -f "infra/.env" ] && ENV_FILE="infra/.env"

echo "==> PostgreSQL up (--wait), env: $ENV_FILE"
docker compose --env-file "$ENV_FILE" -f infra/compose.yaml up -d --wait
docker compose --env-file "$ENV_FILE" -f infra/compose.yaml ps

echo "==> dotnet test"
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release

echo "==> Done. Stop the stack with:"
echo "    docker compose --env-file $ENV_FILE -f infra/compose.yaml down"
