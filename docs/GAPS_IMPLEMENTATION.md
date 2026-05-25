# GAPs de Implementação — Status

## P1 — Implementados

| GAP | Descrição | Arquivo |
|-----|-----------|---------|
| #1 | Detecção de vazamento (150% da média) | `AnomaliaService.cs:VerificarSuspeitaVazamentoAsync` |
| #2 | Validação de limites (não pode regredir, máx 999999) | `AnomaliaService.cs:ValidarLeituraAsync` |
| #3 | Troca de hidrômetro (leitura regressiva com troca = OK) | `AnomaliaService.cs:ValidarLeituraAsync` |
| #8 | Ver foto na validação web | `OperadorController.cs:ObterFoto` |
| #10 | Motivo de rejeição obrigatório | `RejeitarAsync` em `LeituraService.cs` |

## P2 — Implementados

| GAP | Descrição | Arquivo |
|-----|-----------|---------|
| #4 | Outliers via Z-score | `AnomaliaService.cs:VerificarOutlierAsync` |
| #6 | Quality flag (Ok/BaixaConfianca/Manual/Rejeitado3x) | `LeituraHidrometro.cs:QualidadeFoto` |
| #7 | Histórico de consumo | `LeituraService.cs:RegistrarHistoricoConsumoAsync` |

## P3 — Pendentes

| GAP | Descrição | Status |
|-----|-----------|--------|
| #5 | Sazonalidade | Fase 4 |
| #9 | Rotação automática de foto | Fase 4 — Azure Vision detecta orientação |

## Como adicionar GAP #9 (Rotação)

```csharp
// Em AzureVisionService.cs, antes de enviar pra IA:
var metadata = await ReadExifOrientation(fotoBytes);
if (metadata.Rotation != 0)
    fotoBytes = RotateImage(fotoBytes, metadata.Rotation);
```

Use `ImageSharp` ou `SkiaSharp` para rotacionar.
