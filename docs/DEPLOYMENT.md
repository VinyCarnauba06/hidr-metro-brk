# Deploy — Hidrômetro BRK

Guia de produção testado. Cobre Docker (recomendado) e Windows sem Docker.

---

## Pré-requisitos

| Ferramenta | Versão mínima |
|------------|---------------|
| Docker     | 24.x          |
| Docker Compose | v2.x      |
| curl + jq  | qualquer      |

Para build local sem Docker: .NET 8 SDK e PostgreSQL 16.

---

## Opção A: Docker Compose (recomendado)

### 1. Configurar variáveis de ambiente

```bash
cp .env.example .env
```

Editar `.env` com valores reais:

```bash
# Obrigatório — gerar com: openssl rand -base64 32
JWT_SECRET=sua-secret-super-segura-minimo-32-chars

# Obrigatório — senha do PostgreSQL
DATABASE_PASSWORD=senha-segura-aqui

# Origens permitidas para CORS (vírgula-separado)
# Deixar vazio apenas para desenvolvimento local
ALLOWED_ORIGINS=https://hidrometro.suaempresa.com.br,https://app.suaempresa.com.br

# URL da API para o dashboard web (nome do serviço Docker = "api")
API_URL=http://api:5000

# Opcional — deixar vazio para OCR simulado (Google Gemini Vision)
GEMINI_API_KEY=
```

### 2. Subir os serviços

```bash
# Primeira vez (sem volumes existentes)
docker compose up -d

# Verificar logs
docker compose logs -f api
docker compose logs -f web
```

### 3. Verificar saúde

```bash
# API health
curl http://localhost:5000/api/health

# Login de teste (deve retornar token)
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"cpf":"00000000000","senha":"Admin@123"}' | jq .

# Dashboard web
curl -I http://localhost:5001
```

### 4. Executar suite E2E (opcional, após subir)

```bash
API_URL=http://localhost:5000 bash tests/e2e/e2e_test.sh
```

Todos os 21 assertions devem passar antes de considerar o deploy saudável.

---

## Opção B: Windows sem Docker (NSSM)

### Pré-requisitos

- .NET 8 SDK + Runtime (`winget install Microsoft.DotNet.SDK.8`)
- PostgreSQL 16 — nativo **ou** via Docker (`docker compose up -d db`), não os dois na porta 5432 ao mesmo tempo
- NSSM: `winget install NSSM.NSSM`
- PowerShell rodando como Administrador

### Script automatizado (recomendado)

`scripts/deploy/install-services.ps1` cobre os passos manuais abaixo, é idempotente
(pode rodar de novo para atualizar) e resolve o conflito de porta 5432 entre
Postgres nativo e Docker.

1. Crie um arquivo de env de produção **fora do repo** (nunca commitar), ex.
   `C:\hidrometro\.env.prod`, com as chaves obrigatórias de `.env.example`
   (`DATABASE_URL`, `JWT_SECRET`, `ALLOWED_ORIGINS`, `API_URL`) e as
   opcionais que quiser ativar (`GCS_BUCKET_NAME` + `GOOGLE_APPLICATION_CREDENTIALS`
   para storage no GCS, `GEMINI_API_KEY`, `GOOGLE_CLIENT_ID` para SSO).

2. Rode como Administrador:

   ```powershell
   cd C:\Dev\hidrometro-brk\scripts\deploy
   .\install-services.ps1 -EnvFile C:\hidrometro\.env.prod -PostgresMode docker
   ```

   Use `-PostgresMode native` se o Postgres 16 já roda nativo no servidor.
   Use `-SkipPublish` em execuções seguintes para só reconfigurar os serviços
   sem rodar `dotnet publish` de novo.

3. O script publica API e Web em `C:\hidrometro\api` e `C:\hidrometro\web`,
   registra `HidrometroAPI` (porta 5000) e `HidrometroWeb` (porta 5001) via NSSM
   com logs rotacionados em `C:\hidrometro\logs`, e faz health check em
   `GET /api/health` e na raiz do dashboard antes de terminar.

### Passos manuais equivalentes (se preferir não usar o script)

```powershell
# 1. Publicar API
cd backend
dotnet publish src/HidrometroApp.Api -c Release -o C:\hidrometro\api

# 2. Publicar Web
cd ..\web
dotnet publish -c Release -o C:\hidrometro\web

# 3. Registrar API como serviço
nssm install HidrometroAPI "C:\Program Files\dotnet\dotnet.exe"
nssm set HidrometroAPI AppParameters "C:\hidrometro\api\HidrometroApp.Api.dll"
nssm set HidrometroAPI AppDirectory "C:\hidrometro\api"
nssm set HidrometroAPI AppEnvironmentExtra `
  "ASPNETCORE_URLS=http://+:5000" `
  "ASPNETCORE_ENVIRONMENT=Production" `
  "DATABASE_URL=postgresql://postgres:SENHA@localhost:5432/hidrometro_brk" `
  "JWT_SECRET=sua-secret-super-segura-32-chars" `
  "STORAGE_PATH=C:\hidrometro\storage\fotos" `
  "GCS_BUCKET_NAME=" `
  "GOOGLE_APPLICATION_CREDENTIALS=" `
  "LOG_PATH=C:\hidrometro\logs\app.log" `
  "ALLOWED_ORIGINS=http://localhost:5001"

# 4. Registrar Web como serviço
nssm install HidrometroWeb "C:\Program Files\dotnet\dotnet.exe"
nssm set HidrometroWeb AppParameters "C:\hidrometro\web\HidrometroApp.Web.dll"
nssm set HidrometroWeb AppDirectory "C:\hidrometro\web"
nssm set HidrometroWeb AppEnvironmentExtra `
  "ASPNETCORE_URLS=http://+:5001" `
  "ASPNETCORE_ENVIRONMENT=Production" `
  "API_URL=http://localhost:5000"

# 5. Iniciar
nssm start HidrometroAPI
nssm start HidrometroWeb
```

### Storage de fotos em produção (GCS vs local)

- `GCS_BUCKET_NAME` vazio → `LocalFotoStorage`, grava em `STORAGE_PATH`. Simples,
  mas não sobrevive a reinstalação do servidor nem escala horizontalmente.
- `GCS_BUCKET_NAME` setado → `GcsFotoStorage` (`Google.Cloud.Storage.V1`), requer
  `GOOGLE_APPLICATION_CREDENTIALS` apontando para o JSON do Service Account com
  permissão `roles/storage.objectAdmin` no bucket. A troca é automática via DI em
  `Program.cs` — nenhum outro código muda.

---

## Variáveis de ambiente — referência completa

| Variável | Obrigatório | Padrão | Descrição |
|----------|------------|--------|-----------|
| `DATABASE_URL` | Sim | — | String de conexão PostgreSQL |
| `JWT_SECRET` | Sim | — | Mínimo 32 chars, gerado aleatoriamente |
| `JWT_EXPIRATION_HOURS` | Não | `8` | Validade do token |
| `STORAGE_PATH` | Não | `./storage/fotos` | Diretório de fotos |
| `LOG_PATH` | Não | `./storage/logs/app.log` | Arquivo de log |
| `LOG_LEVEL` | Não | `Information` | Nível Serilog |
| `ALLOWED_ORIGINS` | Não | `*` em dev | Origens CORS permitidas (vírgula) |
| `API_URL` | Sim (web) | — | URL da API (usado pelo dashboard) |
| `GEMINI_API_KEY` | Não | — | Vazio = OCR simulado (Google Gemini Vision) |
| `GCS_BUCKET_NAME` | Não | — | Vazio = storage local; setado = ativa `GcsFotoStorage` |
| `GOOGLE_APPLICATION_CREDENTIALS` | Sim (se GCS ativo) | — | Path do JSON do Service Account com `roles/storage.objectAdmin` |
| `GOOGLE_CLIENT_ID` | Não | — | Client ID OAuth 2.0 para SSO Google Workspace |

---

## Backup do banco

```bash
# Dump manual
docker exec hidrometro_db pg_dump -U postgres hidrometro_brk > backup_$(date +%Y%m%d).sql

# Restaurar
docker exec -i hidrometro_db psql -U postgres hidrometro_brk < backup_20260601.sql
```

Agendar dump automático via cron (Linux) ou Agendador de Tarefas (Windows):

```bash
# crontab -e
0 2 * * * docker exec hidrometro_db pg_dump -U postgres hidrometro_brk > /backups/hidrometro_$(date +\%Y\%m\%d).sql
```

---

## Checklist de QA Manual

Execute antes de cada release. Marque cada item.

### Autenticação

- [ ] Login como Admin com CPF `00000000000` / `Admin@123` → redireciona para `/Admin/Dashboard`
- [ ] Login como Operador com CPF `11111111111` / `Operador@123` → redireciona para `/Operador`
- [ ] Login como Fiscal com CPF `22222222222` / `Fiscal@123` → redireciona para `/Operador` (default)
- [ ] Acesso direto a `/Admin` sem login → redirect para `/Auth/Login`
- [ ] Login com senha errada → mensagem de erro visível na tela

### Fluxo do Fiscal (App Mobile ou curl)

- [ ] Fiscal faz login, vê lista de OS abertas (≥1 OS do seed)
- [ ] Fiscal abre OS, vê unidades listadas
- [ ] Fiscal fotografa hidrômetro → resultado aparece com valor e confiança
- [ ] Foto com `permiteRecurso = true` → botão "Recurso Manual" visível
- [ ] Recurso manual: digitar valor m³ positivo → confirmação sem erro
- [ ] Leituras ficam visíveis no progresso da OS

### Fluxo do Operador (Web Dashboard)

- [ ] Operador acessa `/Operador` → lista de OS com leituras pendentes
- [ ] Clicar na OS abre página de validação com foto inline
- [ ] Leituras com `suspeitaVazamento=true` têm banner vermelho visível
- [ ] Validar leitura → status muda para "Validado"
- [ ] Corrigir leitura: inserir valor corrigido → salvo sem erro
- [ ] Rejeitar leitura sem motivo → erro na tela (motivo obrigatório)
- [ ] Rejeitar leitura com motivo → status muda para "Rejeitado"

### Relatórios (OS 100% completa)

- [ ] Gerar Excel: arquivo `.xlsx` baixado, abre no Excel sem erro de formato
- [ ] Gerar Excel com OS incompleta → mensagem de bloqueio (não permite baixar)
- [ ] Gerar PDF: arquivo `.pdf` baixado, abre no leitor sem erro

### Admin

- [ ] Admin cria condomínio com 3 unidades → condomínio visível na listagem
- [ ] Admin cria OS para o condomínio → OS aparece na lista do Fiscal
- [ ] Admin tenta criar OS duplicada (mesmo condomínio + mês + ano) → erro 409
- [ ] Admin acessa `/Admin/Auditoria` → log de ações aparece (≥1 registro)

### Hardening

- [ ] POST `/api/auth/login` 4x seguidas com credenciais inválidas → 4ª retorna HTTP 429
- [ ] POST `/api/fiscal/leitura/upload` com `Content-Type: application/json` → HTTP 415
- [ ] CORS: requisição de origem não listada em `ALLOWED_ORIGINS` em produção → bloqueada

---

## Troubleshooting

**API não inicia — "DATABASE_URL não configurada"**
Verificar que `.env` está no diretório raiz e que `docker compose` o lê (`docker compose config | grep DATABASE`).

**Migrations falham — "relation already exists"**
O banco tem schema antigo. Rodar: `docker exec hidrometro_api dotnet ef database drop --force` e reiniciar. Apenas em ambientes de desenvolvimento/staging.

**Fotos não aparecem na validação**
Verificar que o volume `fotos_data` está montado no mesmo path de `STORAGE_PATH`. O endpoint `/api/operador/leituras/{id}/foto` deve retornar `200`.

**OCR retorna `[MODO SIMULADO]`**
Comportamento esperado quando `GEMINI_API_KEY` está vazio. Para ativar OCR real, preencher `GEMINI_API_KEY` no `.env` e reiniciar o container.
