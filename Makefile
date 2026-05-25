.PHONY: setup dev test build docker-up docker-down migrate seed

# Setup inicial
setup:
	cp -n .env.example .env || true
	mkdir -p storage/fotos storage/logs storage/backup_local

# Dev
dev:
	bash scripts/start-dev.sh

# Testes
test:
	cd backend && dotnet test HidrometroApp.sln

# Build
build:
	cd backend && dotnet build HidrometroApp.sln -c Release

# Docker
docker-up:
	docker compose up -d

docker-down:
	docker compose down

# Migrations
migrate:
	cd backend && dotnet ef database update \
		--project src/HidrometroApp.Infrastructure \
		--startup-project src/HidrometroApp.Api

# Seed
seed:
	psql $$DATABASE_URL -f database/seeds/usuarios_seed.sql
	psql $$DATABASE_URL -f database/seeds/condominios_seed.sql

# Flutter
flutter-run:
	cd mobile && flutter run

flutter-build-apk:
	cd mobile && flutter build apk --release
