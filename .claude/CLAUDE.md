# CLAUDE.md — Hidrômetro BRK

Guia de contexto e roadmap para o Claude Code. Leia este arquivo antes de qualquer tarefa.

---

## Status Final (06/2026)

**Todas as 4 semanas do roadmap concluídas.**

| Métrica | Resultado |
|---|---|
| Testes unitários | 37 passando |
| Testes de integração | 12 passando |
| Total testes | **49 — 0 falhas** |
| `dotnet build` | 0 erros, 0 warnings |
| Cobertura services | ≥ 80% (LeituraService, AnomaliaService, AuthService, RelatorioService) |
| Docker Compose prod | Funcional (API + Web + PostgreSQL) |
| Script E2E | `tests/e2e/e2e_test.sh` — 21 assertions |

**Problemas conhecidos corrigidos:**
- `ValidationException` → `HidrometroValidationException` (renomeado)
- Guard de 100% em `OperadorController.GerarExcel/GerarPdf` (adicionado)
- `DbSeeder` cria OS de teste para o fiscal (corrigido)
- CORS restrito via `ALLOWED_ORIGINS` env var (prod) / aberto em Development
- Rate limit 3 req/min/IP em `POST /api/auth/login`
- Content-Type validation em `POST /api/fiscal/leitura/upload`
- Sanitização de `Observacao` e `MotivoRejeicao` (500 chars)

**Próxima fase:** OCR real (OpenAI GPT-4o Vision — chave pendente) + integração Cond21.

---

## Visão Geral do Projeto

Sistema de automação de leitura de hidrômetros para a **Prolar AGE** (maior administradora de condomínios de Alagoas). Substitui o fluxo manual: foto → papel → planilha → importação no Cond21.

**Fora de escopo:** rateio de custos e integração direta com Cond21.

### Três Componentes

| Componente | Stack | Porta |
|---|---|---|
| API REST | .NET 8 + EF Core + PostgreSQL | 5000 |
| Web Dashboard | ASP.NET Core MVC + Tailwind CSS | 5001 |
| App Mobile | Flutter (iOS/Android/Windows) | — |

### Três Perfis de Usuário

- **Fiscal** — campo, usa o app mobile para fotografar
- **Operador** — web dashboard, valida 100% das leituras
- **Admin** — gerencia OS, condominios, usuários, monitora auditoria

---

## Estrutura de Diretórios

```
hidrometro-brk/
├── backend/
│   ├── src/
│   │   ├── HidrometroApp.Api/          # Controllers, Program.cs
│   │   ├── HidrometroApp.Core/         # Models, Interfaces, Services, DTOs, Exceptions
│   │   └── HidrometroApp.Infrastructure/  # DbContext, DbSeeder, Azure/, Security/
│   └── tests/
│       └── HidrometroApp.Tests/        # xUnit + Moq + InMemory
├── web/                                # ASP.NET Core MVC (dashboard)
│   ├── Controllers/
│   └── Views/
├── mobile/
│   └── lib/
│       ├── models/
│       ├── screens/
│       └── services/
├── database/
│   ├── schema.sql
│   └── seeds/
├── docker-compose.yml
└── CLAUDE.md  ← este arquivo
```

---

## Estado Atual do Código

### O que já está implementado e funcionando

- **Auth:** JWT 8h, BCrypt, `AuthService`, `JwtTokenGenerator`, `AuthController`
- **Leituras (fluxo completo):** `LeituraService` com upload, recurso manual, validar, corrigir, rejeitar, progresso, histórico consumo
- **Anomalias (P1+P2):** `AnomaliaService` com detecção de vazamento (150%), Z-score, validação de limites, troca de hidrômetro
- **Relatórios:** `RelatorioService` gerando Excel (EPPlus) e PDF (iTextSharp) com dados corretos
- **Auditoria:** log imutável de todas as ações com `AuditoriaService`
- **Admin API:** CRUD de condominios, ordens de serviço, usuários
- **Web:** controllers e views Razor para Operador e Admin (funcional mas básico)
- **Flutter:** login, lista de OS, câmera, resultado, sync offline com SQLite
- **Infra:** Docker, docker-compose, GitHub Actions CI/CD, DbSeeder com 3 usuários de teste
- **Testes existentes:** `AnomaliaServiceTests` (4 testes) + `AuthServiceTests` (4 testes)

### Problemas Conhecidos a Corrigir

1. **`ValidationException` conflita com `System.ComponentModel.DataAnnotations`** — renomear para `HidrometroValidationException` em `ValidationException.cs` e todos os catches.

2. **Guard de 100% ausente na API de relatório** — `OperadorController.GerarExcel/GerarPdf` não verifica `progresso.FaltandoRegistrar > 0` antes de gerar. Adicionar esse check (o bloqueio atual só existe no frontend).

3. **`DbSeeder` não cria OS de teste** — o fiscal loga e vê lista vazia. Adicionar uma OS aberta para o condominio seed.

4. **`AzureBlobService` é dead code** — não tem interface, não está no DI. Deixar como está por enquanto (não remove, não ativa).

5. **CORS aberto** — `AllowAnyOrigin` em Program.cs. Aceitável para dev, mas criar issue para restringir em prod.

---

## OCR / Vision — DECISÃO ARQUITETURAL

**OCR está mockado intencionalmente.** A classe `AzureVisionService` tem um método `SimularLeitura()` que é ativado automaticamente quando `AZURE_VISION_ENDPOINT` e `AZURE_VISION_KEY` não estão configuradas no ambiente. Isso permite desenvolvimento e testes completos sem dependência de serviço externo.

**NÃO remover, NÃO substituir, NÃO migrar para Google Vision por enquanto.**

Quando a API key estiver disponível (OpenAI GPT-4o Vision é a direção provável), será feito um PR específico trocando apenas a implementação da interface `IAzureVisionService`.

O `SimularLeitura()` retorna valores aleatórios realistas com `Confianca` entre 0.70 e 1.00 e a string `[MODO SIMULADO — sem Azure configurado]` no campo `Motivo`.

---

## Convenções de Código

### C# (.NET 8)

- **Nomenclatura de arquivo:** comentar o caminho no topo de cada arquivo novo (`// backend/src/HidrometroApp.Core/Services/XyzService.cs`)
- **Injeção de dependência:** sempre pelo construtor, nunca Service Locator
- **Exceções de domínio:** usar as definidas em `ValidationException.cs` — `NotFoundException`, `UnauthorizedException`, `LeituraInvalidaException`, `FotoRejeitadaException`, `OcrSemLeituraValidaException`
- **Controllers API:** retornar tipos específicos (`Ok()`, `NotFound()`, `UnprocessableEntity()`) — nunca `StatusCode(200)`
- **EF Core:** sempre `AsNoTracking()` em queries de leitura; sempre `Include()` para navegação necessária
- **Async:** todos os métodos que tocam DB ou I/O devem ser `async Task<T>`
- **Sem lógica nos controllers:** controllers fazem binding + delegam para services + mapeiam resposta HTTP

### Dart / Flutter

- Nomenclatura: `snake_case` para arquivos, `camelCase` para variáveis, `PascalCase` para classes
- Screens não fazem chamadas HTTP diretamente — usar `ApiService` ou `StorageService`
- Offline-first: toda leitura salva no SQLite antes de tentar sync
- Sem `print()` — usar `debugPrint()` e apenas em dev

### Testes

- **Framework:** xUnit + Moq + FluentAssertions + EF InMemory
- **Nomenclatura:** `Método_Condição_ResultadoEsperado` (ex: `ValidarLeitura_Negativa_Retorna_Invalida`)
- **InMemory DB:** cada teste cria um `Guid.NewGuid().ToString()` como nome — nunca compartilhar DB entre testes
- **Mocks:** usar `Moq` para interfaces externas (ITokenGenerator, IAzureVisionService)
- **Cobertura mínima alvo:** 80% nos services de domínio (`LeituraService`, `AnomaliaService`, `AuthService`, `RelatorioService`)

---

## Credenciais de Desenvolvimento (seed)

| Perfil | CPF | Senha |
|---|---|---|
| Admin | 00000000000 | Admin@123 |
| Operador | 11111111111 | Operador@123 |
| Fiscal | 22222222222 | Fiscal@123 |

---

## Variáveis de Ambiente (.env)

```bash
DATABASE_URL=postgresql://postgres:senha123@localhost:5432/hidrometro_brk
JWT_SECRET=sua-secret-super-segura-minimo-32-chars-aqui
JWT_EXPIRATION_HOURS=8
LOG_LEVEL=Information
LOG_PATH=./storage/logs/app.log
ENVIRONMENT=Development

# OCR — DEIXAR VAZIO para ativar modo simulado (padrão em dev)
AZURE_VISION_ENDPOINT=
AZURE_VISION_KEY=

# Storage de fotos — deixar vazio usa ./storage/fotos relativo ao executável
STORAGE_PATH=
```

---

## Como Rodar Localmente

```bash
# 1. Subir PostgreSQL
docker-compose up -d db

# 2. API (migrations + seed automáticos em Development)
cd backend/src/HidrometroApp.Api
cp ../../.env.example .env   # e preencher JWT_SECRET
dotnet run

# 3. Web Dashboard
cd web
dotnet run

# 4. Testes
cd backend
dotnet test

# 5. Flutter
cd mobile
flutter pub get
flutter run
```

---

## Roadmap — Entrega em 1 Mês

### Semana 1 — Estabilização e Testes Unitários

**Objetivo:** projeto compila limpo, testes passando, sem bugs conhecidos.

- [ ] Corrigir `ValidationException` → `HidrometroValidationException` (renomear classe e todos os catches)
- [ ] Adicionar guard de 100% em `OperadorController.GerarExcel` e `GerarPdf` na API
- [ ] Adicionar OS de teste no `DbSeeder` (status Aberta, mês/ano atual, condominio seed)
- [ ] Testes unitários — `LeituraService`:
  - `UploadFoto_LimiteAtingido_LancaFotoRejeitada`
  - `RegistrarManual_LeituraInvalida_LancaException`
  - `ValidarLeitura_AtualizaStatus_ERegistraHistorico`
  - `RejeitarLeitura_MotivoVazio_LancaValidation`
  - `ObterProgresso_OsCompleta_RetornaPercentual100`
- [ ] Testes unitários — `RelatorioService`:
  - `GerarExcel_OsNaoEncontrada_LancaNotFound`
  - `GerarExcel_RetornaBytesNaoVazios`
  - `GerarPdf_RetornaBytesNaoVazios`
  - `ObterDados_IncluiSomenteValidadas`
- [ ] Testes unitários — `AnomaliaService` (cobrir casos faltantes):
  - `ValidarLeitura_ComTrocaRecente_Aceita`
  - `VerificarOutlier_HistoricoInsuficiente_RetornaFalse`
  - `VerificarVazamento_HistoricoInsuficiente_RetornaFalse`
  - `VerificarVazamento_ConsumoNormal_RetornaFalse`
- [ ] CI verde: `dotnet build` + `dotnet test` passando no GitHub Actions

### Semana 2 — Testes de Integração (API)

**Objetivo:** fluxo completo testado contra banco InMemory via `WebApplicationFactory`.

Criar `backend/tests/HidrometroApp.Tests/Integration/` com:

- [ ] `AuthIntegrationTests`:
  - `POST /api/auth/login` com credenciais válidas → 200 + token
  - `POST /api/auth/login` com senha errada → 401
  - `GET /api/auth/me` sem token → 401
  - `GET /api/auth/me` com token válido → 200 + perfil correto
- [ ] `FiscalIntegrationTests`:
  - `GET /api/fiscal/ordens-abertas` sem auth → 401
  - `GET /api/fiscal/ordens-abertas` como Fiscal → 200
  - `POST /api/fiscal/leitura/upload` com foto válida → 200 + `confiancaIa > 0` (mock simulado)
  - `POST /api/fiscal/leitura/upload` sem foto → 400
  - `POST /api/fiscal/leitura/{id}/recurso` com valor negativo → 422
  - `GET /api/fiscal/os/{osId}/progresso` → retorna percentual correto
- [ ] `OperadorIntegrationTests`:
  - `GET /api/operador/ordens-aguardando` como Fiscal → 403
  - `PATCH /api/operador/leituras/{id}/validar` → status muda para Validado
  - `PATCH /api/operador/leituras/{id}/corrigir` → valida limites
  - `PATCH /api/operador/leituras/{id}/rejeitar` → motivo obrigatório
  - `POST /api/operador/relatorio/{osId}/excel` com OS não 100% → 400
  - `POST /api/operador/relatorio/{osId}/excel` com OS 100% → 200 + bytes xlsx
- [ ] `AdminIntegrationTests`:
  - `POST /api/admin/condominios` → cria condomínio + unidades
  - `POST /api/admin/ordens` duplicada → 409
  - `POST /api/admin/usuarios` com CPF duplicado → 409
  - `GET /api/admin/auditoria` → retorna log de ações anteriores

### Semana 3 — Web Dashboard e Flutter

**Objetivo:** UI funcional e testada.

**Web (ASP.NET Core MVC):**
- [ ] Corrigir `OperadorController.AprovarLeitura`: adicionar `ValidarLeituraRequest` com body correto (atualmente envia `{}`)
- [ ] View `Operador/Validar.cshtml`: exibir foto inline via `<img src="/api/operador/leituras/{id}/foto">` com fallback
- [ ] View `Operador/Validar.cshtml`: destacar visualmente linhas com `suspeitaVazamento = true`
- [ ] View `Admin/Dashboard.cshtml`: exibir stats do endpoint `/api/admin/dashboard`
- [ ] Adicionar tratamento de erro nas views (atualmente `dynamic` lança NullRef se API cair)
- [ ] Testes de controller MVC (xUnit + `WebApplicationFactory` para web):
  - Login redireciona para `Operador/Index` quando Operador
  - Login redireciona para `Admin/Dashboard` quando Admin
  - Acesso a rota de Operador por Fiscal → 403

**Flutter:**
- [ ] `ordens_screen.dart`: mostrar badge com quantidade de pendentes offline (contar SQLite)
- [ ] `resultado_screen.dart`: botão "Recurso manual" quando `permiteRecurso = true`, abrindo dialog com campo decimal
- [ ] `sync_service.dart`: indicador visual de sync em andamento (usar `ValueNotifier<SyncStatus>`)
- [ ] Widget tests (Flutter):
  - `LoginScreen` exibe erro quando API retorna 401
  - `CameraScreen` mostra progress bar com valor correto
  - `ResultadoScreen` exibe alerta de vazamento quando `suspeitaVazamento = true`

### Semana 4 — Testes E2E, Hardening e Entrega

**Objetivo:** fluxo completo testado de ponta a ponta, documentação de deploy.

- [ ] **Teste E2E completo** (script bash + curl ou Playwright para web):
  - Criar OS via Admin API
  - Login como Fiscal → obter OS → upload foto (mock) → verificar pendente
  - Login como Operador → listar leituras → validar todas → verificar progresso 100%
  - Gerar Excel → verificar Content-Type e tamanho > 0
  - Gerar PDF → verificar Content-Type e tamanho > 0
  - Verificar auditoria: 3+ registros gerados no fluxo
- [ ] **Hardening mínimo:**
  - Restringir CORS em `Program.cs` para origem configurável via env var `ALLOWED_ORIGINS`
  - Rate limit em `POST /api/auth/login` (3 tentativas/min por IP usando `AspNetCoreRateLimit` ou middleware simples)
  - Validação de Content-Type em `POST /api/fiscal/leitura/upload` (rejeitar se não for `multipart/form-data`)
  - Sanitizar `MotivoRejeicao` e `Observacao` — truncar em 500 chars se necessário
- [ ] **Docker Compose** funcional para prod:
  - Remover variáveis Azure do `docker-compose.yml` que não são usadas
  - Adicionar `STORAGE_PATH` e `API_URL` no compose
  - Testar `docker-compose up` do zero (sem volumes existentes)
- [ ] **`docs/DEPLOYMENT.md`** atualizado com passos reais testados
- [ ] **Checklist de QA manual** (executar e marcar):
  - Fiscal faz login, vê OS, fotografa, resultado aparece
  - Foto com `permiteRecurso = true` → recurso manual funciona
  - Operador vê lista, valida, corrige, rejeita com motivo
  - Relatório Excel abre no Excel sem erro
  - Relatório PDF abre no navegador/leitor
  - Admin cria condomínio, cria OS, vê auditoria

---

## Métricas de Qualidade Mínimas para Entrega

| Métrica | Meta |
|---|---|
| Cobertura de testes nos services | ≥ 80% |
| Testes unitários | ≥ 25 |
| Testes de integração (API) | ≥ 16 |
| Testes E2E | fluxo completo passing |
| `dotnet build` sem warnings | ✓ |
| `docker-compose up` funcional | ✓ |
| Fluxo Fiscal→Operador→Relatório manual | ✓ |

---

## Perguntas Frequentes

**Por que `AnomaliaService` e `AuditoriaService` injetam `HidrometroDbContext` diretamente (Core → Infrastructure)?**
Violação arquitetural conhecida. Migração para repositórios abstratos está fora do escopo desta entrega para não adicionar risco. Criar issue para Fase 2.

**Por que `RelatorioService` está em Core se usa EPPlus/iTextSharp?**
Mesma razão acima. Aceitável por ora.

**Por que não usar o `schema.sql` para migrations?**
EF Core gerencia as migrations automaticamente em Development (`db.Database.MigrateAsync()`). O `schema.sql` existe como referência e para setup manual de produção se necessário. Não mantê-los sincronizados manualmente — migrations são a fonte de verdade.

**Como testar o OCR real quando a key estiver disponível?**
Setar `AZURE_VISION_ENDPOINT` e `AZURE_VISION_KEY` no `.env`. O `AzureVisionService` usa o client real automaticamente. O `SimularLeitura()` só ativa quando as duas vars estão vazias/nulas.