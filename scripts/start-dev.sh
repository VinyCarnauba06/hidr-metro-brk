#!/bin/bash
# Start do ambiente de desenvolvimento

echo "Subindo banco de dados..."
docker compose up -d db

echo "Iniciando API (.NET 8)..."
cd backend/src/HidrometroApp.Api
dotnet run &
API_PID=$!

echo ""
echo "=== Dev iniciado ==="
echo "API:     http://localhost:5000"
echo "Swagger: http://localhost:5000/swagger"
echo ""
echo "Pressione Ctrl+C para encerrar."
trap "kill $API_PID; docker compose stop db" EXIT
wait $API_PID
