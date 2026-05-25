# Sistema Leitor de Hidrômetro BRK

**Empresa:** Prolar AGE (Maceió, AL)  
**Objetivo:** Substituir processo manual de leitura + Excel por sistema automatizado com IA

## Stack
- **Backend:** .NET 8 (API REST)
- **Frontend:** ASP.NET Core MVC + Tailwind CSS
- **Mobile:** Flutter (Android/iOS/Windows)
- **Banco:** PostgreSQL + Entity Framework Core
- **IA:** Azure Computer Vision

## Como começar

```bash
# 1. Clone e configure
cp .env.example .env
# edite .env com suas credenciais

# 2. Setup banco de dados
psql -U postgres -f database/scripts/create_database.sql
psql -U postgres -d hidrometro_brk -f database/schema.sql

# 3. Backend
cd backend
dotnet restore
dotnet ef database update --project src/HidrometroApp.Infrastructure --startup-project src/HidrometroApp.Api

# 4. Rodar API
cd src/HidrometroApp.Api
dotnet run

# 5. App mobile
cd mobile
flutter pub get
flutter run
```

## Usuários e papéis

| Perfil    | Acesso                                      |
|-----------|---------------------------------------------|
| fiscal    | App mobile — tira fotos, envia leituras     |
| operador  | Web — valida leituras, gera relatórios      |
| admin     | Web — dashboard, auditoria, configurações   |

## Documentação
- [Arquitetura](docs/ARCHITECTURE.md)
- [API Specification](docs/API_SPECIFICATION.md)
- [Database Schema](docs/DATABASE_SCHEMA.md)
- [Deploy Guide](docs/DEPLOYMENT.md)
- [GAPs de Implementação](docs/GAPS_IMPLEMENTATION.md)

## Fases
- **Fase 1** (Semanas 1-2): Backend Core + Azure Vision
- **Fase 2** (Semanas 3-4): Operador Web + Relatórios
- **Fase 3** (Semanas 5-6): App Flutter
- **Fase 4** (Semanas 7-8): Testes + Deploy
