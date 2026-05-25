# Arquitetura — Sistema Hidrômetro BRK

## Visão Geral

```
Flutter App (Fiscal)
       │ HTTPS + JWT
       ▼
.NET 8 API (REST)
       │
       ├── PostgreSQL (dados)
       ├── Azure Computer Vision (IA)
       ├── Azure Blob Storage (backup fotos)
       └── Sistema de arquivos local (fotos)
       
ASP.NET Core MVC (Operador/Admin)
       │ HTTP interno
       └── .NET 8 API
```

## Camadas

| Camada | Projeto | Responsabilidade |
|--------|---------|-----------------|
| API | `HidrometroApp.Api` | Controllers, roteamento, autenticação JWT |
| Core | `HidrometroApp.Core` | Models, Services, Interfaces, DTOs, Exceptions |
| Infrastructure | `HidrometroApp.Infrastructure` | DbContext, Azure, Segurança, Repositórios |

## Fluxo principal

1. **Fiscal** → tira foto no app Flutter
2. **Flutter** → valida qualidade (tamanho, magic bytes)
3. **API** → salva foto local + envia ao Azure Vision
4. **Azure Vision** → extrai número do hidrômetro
5. **API** → valida leitura (GAPs #1-4), persiste, retorna resultado
6. **Flutter** → exibe resultado, oferece recurso manual se falhou
7. **Operador** → acessa web, vê grid de leituras com fotos
8. **Operador** → aprova / corrige / rejeita
9. **Sistema** → registra histórico de consumo, verifica OS completa
10. **Operador** → gera Excel/PDF → importa no Cond21

## Autenticação

- JWT Bearer Token
- Roles: `Fiscal`, `Operador`, `Admin`
- Expiração: 8h (configurável via `JWT_EXPIRATION_HOURS`)

## Armazenamento de fotos

```
storage/fotos/
  └── YYYYMM/
        └── condo_{id}/
              └── {uuid}.jpg
```

Backup automático: Azure Blob + rede local (job mensal).
