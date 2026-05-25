#!/bin/bash
set -e

echo "=== Setup Hidrômetro BRK ==="

# Copiar .env
if [ ! -f .env ]; then
  cp .env.example .env
  echo ".env criado — edite com suas credenciais antes de continuar."
  exit 1
fi

# Carregar variáveis
export $(grep -v '^#' .env | xargs)

# Criar diretórios
mkdir -p storage/fotos storage/logs storage/backup_local

# Subir banco com Docker
docker compose up -d db
echo "Aguardando PostgreSQL..."
sleep 5

# Migrations
cd backend
dotnet restore
dotnet ef database update --project src/HidrometroApp.Infrastructure --startup-project src/HidrometroApp.Api
cd ..

echo "=== Setup concluído! ==="
echo "Rode: cd backend && dotnet run --project src/HidrometroApp.Api"
