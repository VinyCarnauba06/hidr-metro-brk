# Deploy — Guia de Produção

## Opção A: Docker (recomendado)

```bash
# 1. Configure o .env
cp .env.example .env
nano .env  # edite as variáveis

# 2. Suba tudo
docker compose up -d

# 3. Verifique
curl http://localhost:5000/api/health
```

## Opção B: Windows + NSSM (sem Docker)

### Pré-requisitos
- .NET 8 Runtime instalado
- PostgreSQL 16 instalado e rodando
- NSSM (Non-Sucking Service Manager)

### Deploy da API

```powershell
# 1. Publicar
cd backend
dotnet publish src/HidrometroApp.Api -c Release -o C:\hidrometro\api

# 2. Criar service com NSSM
nssm install HidrometroAPI "C:\Program Files\dotnet\dotnet.exe"
nssm set HidrometroAPI AppParameters "C:\hidrometro\api\HidrometroApp.Api.dll"
nssm set HidrometroAPI AppDirectory "C:\hidrometro\api"
nssm set HidrometroAPI AppEnvironmentExtra "ASPNETCORE_URLS=http://+:5000" "ASPNETCORE_ENVIRONMENT=Production"

# 3. Definir variáveis de ambiente no NSSM
nssm set HidrometroAPI AppEnvironmentExtra `
  "DATABASE_URL=postgresql://postgres:SENHA@localhost:5432/hidrometro_brk" `
  "JWT_SECRET=seu-secret-super-seguro-aqui" `
  "AZURE_VISION_ENDPOINT=..." `
  "AZURE_VISION_KEY=..."

# 4. Iniciar
nssm start HidrometroAPI
```

### Backup automático

```
Agendador de Tarefas > Criar Tarefa:
  - Programa: C:\hidrometro\scripts\backup.bat
  - Gatilho: Todo mês, dia 1, às 02:00
  - Executar como: SYSTEM (ou conta com acesso ao PostgreSQL)
```

## Variáveis obrigatórias em produção

| Variável | Descrição |
|----------|-----------|
| `DATABASE_URL` | String de conexão PostgreSQL |
| `JWT_SECRET` | Secret JWT (mín. 32 chars, aleatório) |
| `AZURE_VISION_ENDPOINT` | Endpoint do Azure Vision |
| `AZURE_VISION_KEY` | Chave do Azure Vision |

## Verificação pós-deploy

```bash
# Health check
curl http://seu-servidor:5000/api/health

# Login de teste
curl -X POST http://seu-servidor:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"cpf":"00000000000","senha":"Admin@123"}'
```
