# PROGRESS — Hidrômetro BRK

> Tracker vivo de pendências. Fonte de verdade verificada **no código** em 16/07/2026.
> Legenda: ✅ feito · 🟡 parcial · ⛔ pendente · 🔧 manual (você executa)

---

## Baseline atual

- 4 semanas de roadmap concluídas (auth, leituras, anomalias P1+P2, relatórios, web, flutter).
- Sprint 5 (confidence threshold OCR, GCS storage, blur detection mobile, SSO Google backend)
  commitada em `9bd07f7`. Higiene de repo, EXIF auto-orient e overlay de enquadramento
  commitados em `3fb5edb`.
- Testes backend: `dotnet build` limpo (0 erros, 0 warnings) e suíte **completa** —
  64/64 testes passando (unitários + integração, incluindo SSO) confirmado em 16/07/2026.
- Testes mobile: `flutter analyze` limpo (0 issues) e `flutter test` — 18/18 passando,
  confirmado em 16/07/2026.
- Vulnerabilidade `SixLabors.ImageSharp` 3.1.7 (NU1902, CVE-2025-54575, DoS no decoder
  GIF) corrigida — bump para 3.1.11 em `HidrometroApp.Infrastructure.csproj`.
- Índice do git corrompido (race entre sync de pasta e git) resolvido via backup +
  `git reset` em 16/07/2026 — sem perda de trabalho, índice reconstruído do HEAD.
- CI "CI — Mobile Flutter" quebrado desde 14/06/2026 (nunca passou) — corrigido em
  16/07/2026, ver seção 8.

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
| 2.7 | Confirmar `dotnet restore && dotnet build && dotnet test` (0 warnings, tudo verde) | ✅ (16/07/2026) — 64/64 testes |

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

### 8. CI "CI — Mobile Flutter" ✅ — corrigido em 16/07/2026 (quebrado desde 14/06)

| # | Item | Status |
|---|------|--------|
| 8.1 | `mobile/android/.gradle/` (cache Gradle) commitado sem querer, sem `.gitignore` | ✅ nunca chegou a ser commitado de fato (só staged); resolvido junto com o fix do índice do git |
| 8.2 | Gradle wrapper (`gradlew`, `gradlew.bat`, `gradle-wrapper.jar/properties`) nunca existiu no repo — build de APK não roda no CI | ✅ gerado via `flutter` (Gradle 8.0, compatível com AGP 8.1.0 do projeto) |
| 8.3 | `flutter analyze` roda com `--fatal-infos`/`--fatal-warnings` (default) — qualquer lint trava o CI; havia 13 issues (1 error real: `test/widget_test.dart` referenciava a classe boilerplate `MyApp` do `flutter create`, nunca adaptada) | ✅ todos corrigidos |
| 8.4 | `flutter test` — 5 falhas pré-existentes: overflow de layout em `resultado_screen.dart` (banner de vazamento sem scroll), testes de `LoginScreen` procurando campo `'CPF'` que virou `'Email'`, e 2 testes de loading incapazes de capturar o estado transitório (sem client HTTP injetável em `AuthService`) | ✅ `resultado_screen.dart` usa `Expanded(SingleChildScrollView(...))`; testes atualizados para `'Email'`; `AuthService.client` agora injetável, testes usam `MockClient`+`Completer` |
| 8.5 | `mobile/android/app/build.gradle` referenciava `flutterVersionCode`/`flutterVersionName` nunca definidos (falta bloco de leitura de `local.properties`, típico de template antigo) | ✅ removido — plugin novo do Flutter injeta a partir de `pubspec.yaml` (`version: 1.0.0+1`) |
| 8.6 | `minSdkVersion` do projeto abaixo do exigido pelo plugin `camera_android` (mín. 21) | ✅ elevado para 21 |
| 8.7 | `AndroidManifest.xml` referencia `@mipmap/ic_launcher`, mas nenhuma pasta `mipmap-*` jamais existiu no repo | 🟡 resolvido com placeholder (ícone padrão do Flutter) só para destravar o CI — **precisa ser substituído pelo ícone real da marca (Hidrômetro BRK / Prolar AGE) antes de qualquer release** |
| 8.8 | `local.properties` (específico de máquina) estava commitado | ✅ removido do controle de versão, coberto pelo `.gitignore` |

---

## Ordem de ataque sugerida

**Foco atual: só localhost. Deploy em servidor real (#7) fica para depois.**

1. ~~Rodar a suíte completa de `dotnet test`~~ — feito, 64/64 (#2.7).
2. ~~SSO: domínio Workspace + testes~~ — feito (#3.2–#3.4).
3. ~~Reverificar `GAPS_IMPLEMENTATION.md`~~ — feito (#5.3).
4. ~~CI "CI — Mobile Flutter"~~ — feito, verde de ponta a ponta (#8).
5. Substituir o ícone placeholder do Android pelo ícone real da marca (#8.7).
6. Calibrar blur detection com mais fotos reais (#4.3) — depende de amostras de campo, não dá para fazer sem fotos reais.
7. Sazonalidade na detecção de anomalia (#5.2) — fora de escopo por ora (Fase 4).
8. Deploy em produção com o script novo (#7.3–7.5) — retomar quando sair do localhost.

---

*Última atualização: 16/07/2026*
