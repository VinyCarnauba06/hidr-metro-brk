# PROGRESS — Hidrômetro BRK

> Tracker vivo de pendências. Fonte de verdade verificada **no código** em 23/06/2026
> (não no memory.md importado, que estava desatualizado em vários pontos).
> Legenda: ✅ feito · 🟡 parcial · ⛔ pendente · 🔧 manual (você executa)

---

## Baseline atual

- 4 semanas de roadmap concluídas (auth, leituras, anomalias P1+P2, relatórios, web, flutter).
- Sprint 5 (confidence threshold OCR, cleanup Azure→GCS parcial, blur detection mobile) **implementado mas SEM COMMIT**.
- Testes: memory aponta 52/52; CLAUDE.md aponta 49. **Precisa confirmar com `dotnet test` na sua máquina** (sandbox não tem .NET/Flutter, sem rede para instalar).
- Build/test de .NET e Flutter rodam **na sua máquina** — aqui eu só escrevo o código.

---

## Pendências

### 1. Higiene de repo ⛔ — *bloqueante leve, fazer primeiro*

| # | Item | Status |
|---|------|--------|
| 1.1 | Sprint 5 inteiro sem commit (~24 modificados + untracked: migrations, `GcsStorageService`, `photo_validator`) | ⛔ |
| 1.2 | Logs e binários sujando o repo: `api*.log`, `web_run.log`, `*.err`, fotos `.jpg` em `backend/**/storage/fotos/` e `backend/tests/**/storage/fotos/` | ⛔ |
| 1.3 | Adicionar esses padrões ao `.gitignore` e `git rm --cached` os já versionados | ⛔ |

### 2. GCS Storage real 🟡 — *código pronto, aguardando `dotnet test` na máquina do Viny*

| # | Item | Status |
|---|------|--------|
| 2.1 | `IFotoStorage` (Core/Interfaces) — `SalvarAsync`/`ObterAsync` | ✅ |
| 2.2 | `LocalFotoStorage` (default, filesystem) + `GcsFotoStorage` (SDK `Google.Cloud.Storage.V1`, ativa com `GCS_BUCKET_NAME`) | ✅ |
| 2.3 | `LeituraService` injeta `IFotoStorage` (removido `IConfiguration` morto); grava/lê via storage | ✅ |
| 2.4 | DI em `Program.cs` escolhe Local/GCS por env var; pacote NuGet adicionado; stub antigo → tombstone; testes atualizados | ✅ |
| 2.5 | **Confirmar `dotnet restore && dotnet build && dotnet test` (0 warnings, tudo verde)** | ⛔ você roda |

### 3. SSO Google Workspace ⛔

| # | Item | Status |
|---|------|--------|
| 3.1 | Hoje só JWT (`Program.cs:57`). Adicionar Google OAuth + callback | ⛔ |
| 3.2 | Mapear `email` Google → `Usuario`; restringir ao domínio Workspace da Prolar; emitir JWT interno pós-login | ⛔ |

### 4. Mobile 🟡

| # | Item | Status |
|---|------|--------|
| 4.1 | Blur detection (variância do Laplaciano, threshold 100) + rejeição imediata — `photo_validator.dart` + `camera_screen.dart:58` | ✅ |
| 4.2 | Guia de enquadramento visual (overlay `CustomPainter`) na câmera | ⛔ |
| 4.3 | Calibrar threshold de nitidez com fotos reais (amostra real: só 3/10 aceitáveis) | 🟡 |
| 4.4 | Badge de pendentes offline, indicador de sync, recurso manual no resultado | ✅ (verificado em `ordens_screen.dart`) |

### 5. GAPs P3 / antigos ⛔

| # | Item | Status |
|---|------|--------|
| 5.1 | GAP #9 — rotação automática de foto via EXIF antes do OCR (ImageSharp/SkiaSharp) | ⛔ |
| 5.2 | GAP #5 — sazonalidade na detecção de anomalia | ⛔ (Fase 4) |
| 5.3 | `docs/GAPS_IMPLEMENTATION.md` desatualizado (ainda cita `AzureVisionService` na rotação) | ⛔ |

### 6. Dívida arquitetural conhecida 🟡 — *Fase 2, não bloqueia entrega*

| # | Item | Status |
|---|------|--------|
| 6.1 | Core→Infrastructure: `AnomaliaService`/`AuditoriaService` injetam `DbContext`; `RelatorioService` usa EPPlus/iText em Core (gambiarra `<Compile Include>` cross-project no csproj) | 🟡 aceito por ora |
| 6.2 | Remover `AzureBlobService.cs` + pasta `Azure/` (tombstones) de vez após GCS no lugar | ⛔ |

### 7. Deploy produção Prolar 🔧 — *você executa (admin no servidor)*

| # | Item | Status |
|---|------|--------|
| 7.1 | Rodar `C:\Deploy\install-services.ps1` como admin (NSSM) | 🔧 |
| 7.2 | Resolver conflito porta 5432 (Postgres nativo vs Docker) — escolher um | 🔧 |
| 7.3 | Setar `GEMINI_API_KEY` real, `GCS_BUCKET_NAME`, `GOOGLE_APPLICATION_CREDENTIALS` no `.env` de prod | 🔧 |
| 7.4 | Health check `GET /api/health` → 200 (já validado em dev) | ✅ |

---

## Ordem de ataque

1. Higiene de repo (rápido, protege o trabalho) → **#1**
2. GCS Storage real → **#2**
3. SSO Google Workspace → **#3**
4. Guia de enquadramento mobile → **#4.2**
5. GAPs P3 + docs → **#5**
6. Remover tombstones Azure → **#6.2**
7. Deploy prod → **#7** (manual, por último)

---

*Última atualização: 23/06/2026*
