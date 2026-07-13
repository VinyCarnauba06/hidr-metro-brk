# PROGRESS — Hidrômetro BRK

> Tracker vivo de pendências. Fonte de verdade verificada **no código** em 11/07/2026.
> Legenda: ✅ feito · 🟡 parcial · ⛔ pendente · 🔧 manual (você executa)

---

## Baseline atual

- 4 semanas de roadmap concluídas (auth, leituras, anomalias P1+P2, relatórios, web, flutter).
- Sprint 5 (confidence threshold OCR, GCS storage, blur detection mobile, SSO Google backend)
  commitada em `9bd07f7`. Higiene de repo, EXIF auto-orient e overlay de enquadramento
  commitados em `3fb5edb`.
- Testes: `dotnet build` limpo (0 erros) e `AuthServiceTests` (9/9) confirmados na sua
  máquina em 11/07/2026. **Falta rodar a suíte completa** (inclui os novos testes de
  integração de SSO) — este ambiente não tem .NET/Flutter instalado, só edita arquivos.
- Vulnerabilidade `SixLabors.ImageSharp` 3.1.7 (NU1902, CVE-2025-54575, DoS no decoder
  GIF) corrigida — bump para 3.1.11 em `HidrometroApp.Infrastructure.csproj`.
- Working tree tem mudanças não commitadas de rotina (migrations, alguns arquivos mobile) —
  não relacionadas às pendências abaixo, revisar com `git status` antes de commitar.

---

## Pendências

### 1. Higiene de repo ✅ — resolvido em `3fb5edb`

Sprint 5 commitada, tombstones Azure removidos.

### 2. GCS Storage real ✅

| # | Item | Status |
|---|------|--------|
| 2.1 | `IFotoStorage` (Core/Interfaces) — `SalvarAsync`/`ObterAsync` | ✅ |
| 2.2 | `LocalFotoStorage` (default, filesystem) + `GcsFotoStorage` (SDK `Google.Cloud.Storage.V1`, ativa com `GCS_BUCKET_NAME`) | ✅ |
| 2.3 | `LeituraService` injeta `IFotoStorage`; grava/lê via storage | ✅ |
| 2.4 | DI em `Program.cs` escolhe Local/GCS por env var; pacote NuGet adicionado | ✅ |
| 2.5 | `GcsStorageService.cs` (stub tombstone antigo) removido do repo | ✅ (11/07/2026) |
| 2.6 | `GOOGLE_APPLICATION_CREDENTIALS` documentado explicitamente em `.env.example` | ✅ (11/07/2026) |
| 2.7 | **Confirmar `dotnet restore && dotnet build && dotnet test` (0 warnings, tudo verde)** | ⛔ você roda |

### 3. SSO Google Workspace 🟡

| # | Item | Status |
|---|------|--------|
| 3.1 | `IGoogleTokenValidator` / `GoogleTokenValidator.cs` + rota em `AuthController` + `AuthService` | ✅ |
| 3.2 | Restringir login ao domínio Workspace da Prolar via `GOOGLE_ALLOWED_DOMAIN` — `AuthService.LoginGoogleAsync` valida sufixo do email antes de checar o usuário; vazio = sem restrição (dev) | ✅ (11/07/2026) |
| 3.3 | Testes unitários cobrindo domínio autorizado/negado, sem restrição, usuário não cadastrado, normalização de `@dominio` na config — `AuthServiceTests.cs` (6 novos testes) | ✅ (11/07/2026) |
| 3.4 | Testes de **integração** do fluxo SSO via `WebApplicationFactory` (`POST /api/auth/google`) | ✅ (11/07/2026) — `AuthGoogleIntegrationTests.cs`, 5 testes com `FakeGoogleTokenValidator` (sem domínio, domínio negado, domínio autorizado, usuário não cadastrado, id_token vazio) |

### 4. Mobile 🟡

| # | Item | Status |
|---|------|--------|
| 4.1 | Blur detection (Laplaciano, threshold 100) — `photo_validator.dart` | ✅ |
| 4.2 | Guia de enquadramento visual (overlay `CustomPainter`) | ✅ (commitado em `3fb5edb`) |
| 4.3 | Calibrar threshold de nitidez com fotos reais (amostra: só 3/10 aceitáveis na última calibração) | 🟡 precisa mais amostras reais de campo |
| 4.4 | Badge de pendentes offline, indicador de sync, recurso manual | ✅ |

### 5. GAPs P3 / antigos 🟡

| # | Item | Status |
|---|------|--------|
| 5.1 | GAP #9 — rotação automática de foto via EXIF antes do OCR | ✅ (commitado em `3fb5edb`) |
| 5.2 | GAP #5 — sazonalidade na detecção de anomalia | ⛔ (Fase 4, fora de escopo por ora) |
| 5.3 | `docs/GAPS_IMPLEMENTATION.md` — confirmar se ainda cita `AzureVisionService` | ✅ (11/07/2026) — reverificado, doc já reflete `SixLabors.ImageSharp`/`CorrigirOrientacaoExif`, nenhuma menção a Azure |

### 6. Dívida arquitetural conhecida 🟡 — aceita por ora, Fase 2

| # | Item | Status |
|---|------|--------|
| 6.1 | Core→Infrastructure: `AnomaliaService`/`AuditoriaService` injetam `DbContext`; `RelatorioService` usa EPPlus/iText em Core | 🟡 aceito, não bloqueia entrega |
| 6.2 | Tombstones Azure/GCS antigos removidos do repo | ✅ (11/07/2026) |

### 7. Deploy produção Prolar 🟡 — script pronto, execução manual pendente

| # | Item | Status |
|---|------|--------|
| 7.1 | Script `scripts/deploy/install-services.ps1` (versionado, idempotente, resolve conflito de porta 5432, lê env de um `-EnvFile` externo, health check embutido) | ✅ (11/07/2026, substitui o script solto em `C:\Deploy`) |
| 7.2 | `docs/DEPLOYMENT.md` atualizado com o novo script + tabela de env vars completa (GCS, SSO) | ✅ (11/07/2026) |
| 7.3 | Rodar `install-services.ps1 -EnvFile ... -PostgresMode docker\|native` como admin no servidor | 🔧 você executa |
| 7.4 | Criar o `.env.prod` real fora do repo com `JWT_SECRET`, `DATABASE_URL`, `ALLOWED_ORIGINS`, e opcionalmente `GCS_BUCKET_NAME`+`GOOGLE_APPLICATION_CREDENTIALS`, `GEMINI_API_KEY`, `GOOGLE_CLIENT_ID` | 🔧 você executa |
| 7.5 | Health check `GET /api/health` → 200 em produção | 🔧 validado pelo script na primeira execução |

---

## Ordem de ataque sugerida

**Foco atual: só localhost. Deploy em servidor real (#7) fica para depois.**

1. Rodar a suíte **completa** de `dotnet test` (não só `AuthServiceTests`) para confirmar
   0 falhas com tudo somado — SSO unitário + integração (11 testes novos) + o resto (#2.7).
2. ~~SSO: domínio Workspace + testes~~ — feito (#3.2–#3.4).
3. ~~Reverificar `GAPS_IMPLEMENTATION.md`~~ — feito (#5.3).
4. Calibrar blur detection com mais fotos reais (#4.3) — depende de amostras de campo, não dá para fazer sem fotos reais.
5. Sazonalidade na detecção de anomalia (#5.2) — fora de escopo por ora (Fase 4).
6. Deploy em produção com o script novo (#7.3–7.5) — retomar quando sair do localhost.

---

*Última atualização: 11/07/2026*
