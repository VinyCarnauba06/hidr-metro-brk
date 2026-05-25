using HidrometroApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HidrometroApp.Core.Services;

public class AnomaliaService
{
    private readonly HidrometroApp.Infrastructure.Data.HidrometroDbContext _db;

    private const decimal PERCENTUAL_VAZAMENTO = 1.5m; // 150% da média = suspeita
    private const int MESES_HISTORICO = 6;

    public AnomaliaService(HidrometroApp.Infrastructure.Data.HidrometroDbContext db)
    {
        _db = db;
    }

    // GAP #1: Detecção de vazamento por comparação com média histórica
    public async Task<bool> VerificarSuspeitaVazamentoAsync(int unidadeId, decimal consumoAtual)
    {
        var historico = await _db.HistoricoConsumo
            .Where(h => h.UnidadeId == unidadeId && h.ConsumoM3.HasValue)
            .OrderByDescending(h => h.Ano).ThenByDescending(h => h.Mes)
            .Take(MESES_HISTORICO)
            .Select(h => h.ConsumoM3!.Value)
            .ToListAsync();

        if (historico.Count < 2) return false;

        var media = historico.Average();
        return media > 0 && consumoAtual > media * PERCENTUAL_VAZAMENTO;
    }

    // GAP #4: Detecção de outliers via Z-score
    public async Task<bool> VerificarOutlierAsync(int unidadeId, decimal leituraAtual)
    {
        var historico = await _db.HistoricoConsumo
            .Where(h => h.UnidadeId == unidadeId && h.ConsumoM3.HasValue)
            .OrderByDescending(h => h.Ano).ThenByDescending(h => h.Mes)
            .Take(12)
            .Select(h => h.ConsumoM3!.Value)
            .ToListAsync();

        if (historico.Count < 4) return false;

        var media = historico.Average();
        var desvioPadrao = (decimal)Math.Sqrt(historico.Select(v => Math.Pow((double)(v - media), 2)).Average());

        if (desvioPadrao == 0) return false;

        var zScore = Math.Abs((leituraAtual - media) / desvioPadrao);
        return zScore > 3; // Z-score > 3 = outlier
    }

    // GAP #2: Validação de limites (leitura não pode regredir sem troca)
    public async Task<(bool valida, string? motivo)> ValidarLeituraAsync(int unidadeId, decimal novaLeitura)
    {
        if (novaLeitura < 0)
            return (false, "Leitura não pode ser negativa");

        if (novaLeitura > 999999)
            return (false, "Leitura acima do limite máximo do hidrômetro");

        var ultimaLeitura = await _db.LeiturasHidrometro
            .Where(l => l.UnidadeId == unidadeId && l.Status == StatusLeitura.Validado && l.ValorM3Validado.HasValue)
            .OrderByDescending(l => l.ValidadoEm)
            .Select(l => l.ValorM3Validado!.Value)
            .FirstOrDefaultAsync();

        if (ultimaLeitura > 0 && novaLeitura < ultimaLeitura)
        {
            // GAP #3: Verificar se houve troca de hidrômetro recente (30 dias)
            var trocaRecente = await _db.HistoricoTrocaHidrometro
                .Where(t => t.UnidadeId == unidadeId && t.DataTroca >= DateOnly.FromDateTime(DateTime.Today.AddDays(-30)))
                .AnyAsync();

            if (!trocaRecente)
                return (false, $"Leitura {novaLeitura} é inferior à última leitura validada {ultimaLeitura}. Registre uma troca de hidrômetro se aplicável.");
        }

        return (true, null);
    }
}
