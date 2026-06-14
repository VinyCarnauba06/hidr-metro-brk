using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Core.Services;

public class LeituraService : ILeituraService
{
    private readonly HidrometroDbContext _db;
    private readonly IAzureVisionService _vision;
    private readonly IAuditoriaService _auditoria;
    private readonly AnomaliaService _anomalia;
    private readonly IConfiguration _config;
    private readonly ILogger<LeituraService> _logger;

    public LeituraService(
        HidrometroDbContext db,
        IAzureVisionService vision,
        IAuditoriaService auditoria,
        AnomaliaService anomalia,
        IConfiguration config,
        ILogger<LeituraService> logger)
    {
        _db = db;
        _vision = vision;
        _auditoria = auditoria;
        _anomalia = anomalia;
        _config = config;
        _logger = logger;
    }

    public async Task<LeituraResponse> UploadFotoAsync(int osId, int unidadeId, int fiscalId, byte[] fotoBytes, string nomeArquivo)
    {
        var os = await _db.OrdensServico
            .Include(o => o.Condominio)
            .FirstOrDefaultAsync(o => o.Id == osId)
            ?? throw new NotFoundException($"OS {osId} não encontrada");

        var unidade = await _db.Unidades.FirstOrDefaultAsync(u => u.Id == unidadeId && u.Ativa)
            ?? throw new NotFoundException($"Unidade {unidadeId} não encontrada");

        // Verificar tentativas anteriores
        var tentativasAnteriores = await _db.LeiturasHidrometro
            .Where(l => l.OsId == osId && l.UnidadeId == unidadeId && l.Status == StatusLeitura.Rejeitado)
            .CountAsync();

        if (tentativasAnteriores >= 3)
            throw new FotoRejeitadaException("Máximo de 3 tentativas atingido. Use o recurso manual.");

        // Salvar foto localmente
        var fotoPath = await SalvarFotoAsync(fotoBytes, nomeArquivo, os.CondominioId);

        // Analisar com Azure Vision
        LeituraResultadoIa resultado;
        try
        {
            resultado = await _vision.AnalisarFotoAsync(fotoBytes);
        }
        catch (HidrometroApp.Core.Exceptions.OcrSemLeituraValidaException ex)
        {
            // OCR não encontrou padrão de visor — foto provavelmente focou o número de série.
            // Registra como rejeitado e devolve PermiteRecurso=true para o fiscal fotografar novamente.
            _logger.LogWarning("OCR sem leitura válida para OS={OsId} Unidade={UnidadeId}: {Msg}",
                osId, unidadeId, ex.Message);
            resultado = new LeituraResultadoIa
            {
                Sucesso = false,
                Confianca = 0,
                Motivo = ex.Message,
                PermiteRecurso = true
            };
        }

        var qualidade = resultado.Sucesso ? QualidadeFoto.Ok
            : resultado.PermiteRecurso ? QualidadeFoto.BaixaConfianca
            : tentativasAnteriores >= 2 ? QualidadeFoto.Rejeitado3x
            : QualidadeFoto.BaixaConfianca;

        var leitura = new LeituraHidrometro
        {
            OsId = osId,
            UnidadeId = unidadeId,
            FotoPath = fotoPath,
            ValorM3 = resultado.HidrometroM3,
            ValorLitros = resultado.Litros ?? 0,
            Origem = OrigemLeitura.Ia,
            ConfiancaIa = resultado.Confianca,
            Tentativas = tentativasAnteriores + 1,
            Status = resultado.Sucesso ? StatusLeitura.Pendente : StatusLeitura.Rejeitado,
            QualidadeFoto = qualidade,
            CriadoPorId = fiscalId
        };

        // Verificar suspeita de vazamento se leitura válida
        if (resultado.Sucesso && resultado.HidrometroM3.HasValue)
        {
            var ultimaLeitura = await ObterUltimaLeituraValidadaAsync(unidadeId);
            if (ultimaLeitura > 0)
            {
                var consumo = resultado.HidrometroM3.Value - ultimaLeitura;
                leitura.SuspeitaVazamento = await _anomalia.VerificarSuspeitaVazamentoAsync(unidadeId, consumo);
                leitura.RecomendacaoRevisao = await _anomalia.VerificarOutlierAsync(unidadeId, consumo);
            }
        }

        _db.LeiturasHidrometro.Add(leitura);

        // Atualizar status da OS
        if (os.Status == StatusOS.Aberta)
        {
            os.Status = StatusOS.EmProgresso;
        }

        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(fiscalId, "leituras_hidrometro", "INSERT", leitura.Id,
            dadosDepois: new { leitura.ValorM3, leitura.ConfiancaIa, leitura.Origem },
            origem: "app_fiscal");

        return MapearResponse(leitura, unidade.Numero, resultado.PermiteRecurso);
    }

    public async Task<LeituraResponse> RegistrarManualAsync(int leituraId, int fiscalId, RecursoManualRequest request)
    {
        var leitura = await _db.LeiturasHidrometro
            .Include(l => l.Unidade)
            .FirstOrDefaultAsync(l => l.Id == leituraId)
            ?? throw new NotFoundException($"Leitura {leituraId} não encontrada");

        var (valida, motivo) = await _anomalia.ValidarLeituraAsync(leitura.UnidadeId, request.ValorM3);
        if (!valida) throw new LeituraInvalidaException(motivo!);

        var anterior = new { leitura.ValorM3, leitura.Origem, leitura.Status };

        leitura.ValorM3 = request.ValorM3;
        leitura.ValorLitros = request.ValorLitros;
        leitura.Origem = OrigemLeitura.Manual;
        leitura.Status = StatusLeitura.Pendente;
        leitura.QualidadeFoto = QualidadeFoto.Manual;
        leitura.Observacao = Sanitizar(request.Observacao);

        var ultimaLeitura = await ObterUltimaLeituraValidadaAsync(leitura.UnidadeId);
        if (ultimaLeitura > 0)
        {
            var consumo = request.ValorM3 - ultimaLeitura;
            leitura.SuspeitaVazamento = await _anomalia.VerificarSuspeitaVazamentoAsync(leitura.UnidadeId, consumo);
        }

        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(fiscalId, "leituras_hidrometro", "MANUAL", leituraId,
            dadosAntes: anterior,
            dadosDepois: new { leitura.ValorM3, leitura.Origem },
            origem: "app_fiscal");

        return MapearResponse(leitura, leitura.Unidade.Numero, false);
    }

    public async Task<LeituraResponse> ValidarAsync(int leituraId, int operadorId, ValidarLeituraRequest request)
    {
        var leitura = await _db.LeiturasHidrometro
            .Include(l => l.Unidade)
            .FirstOrDefaultAsync(l => l.Id == leituraId)
            ?? throw new NotFoundException($"Leitura {leituraId} não encontrada");

        var anterior = new { leitura.Status, leitura.ValorM3Validado };

        leitura.ValorM3Validado = leitura.ValorM3;
        leitura.Status = StatusLeitura.Validado;
        leitura.ValidadoPorId = operadorId;
        leitura.ValidadoEm = DateTime.UtcNow;
        leitura.Observacao = Sanitizar(request.Observacao);

        await _db.SaveChangesAsync();
        await RegistrarHistoricoConsumoAsync(leitura);
        await VerificarOsCompleta(leitura.OsId);

        await _auditoria.RegistrarAsync(operadorId, "leituras_hidrometro", "VALIDATE", leituraId,
            dadosAntes: anterior, dadosDepois: new { leitura.Status, leitura.ValorM3Validado },
            origem: "web_operador");

        return MapearResponse(leitura, leitura.Unidade.Numero, false);
    }

    public async Task<LeituraResponse> CorrigirAsync(int leituraId, int operadorId, ValidarLeituraRequest request)
    {
        var leitura = await _db.LeiturasHidrometro
            .Include(l => l.Unidade)
            .FirstOrDefaultAsync(l => l.Id == leituraId)
            ?? throw new NotFoundException($"Leitura {leituraId} não encontrada");

        if (!request.ValorM3Corrigido.HasValue)
            throw new HidrometroValidationException("Informe o valor corrigido");

        var (valida, motivo) = await _anomalia.ValidarLeituraAsync(leitura.UnidadeId, request.ValorM3Corrigido.Value);
        if (!valida) throw new LeituraInvalidaException(motivo!);

        var anterior = new { leitura.ValorM3, leitura.ValorM3Validado, leitura.Status };

        leitura.ValorM3Validado = request.ValorM3Corrigido;
        leitura.Status = StatusLeitura.Validado;
        leitura.ValidadoPorId = operadorId;
        leitura.ValidadoEm = DateTime.UtcNow;
        leitura.Observacao = Sanitizar(request.Observacao);

        await _db.SaveChangesAsync();
        await RegistrarHistoricoConsumoAsync(leitura);
        await VerificarOsCompleta(leitura.OsId);

        await _auditoria.RegistrarAsync(operadorId, "leituras_hidrometro", "CORRECT", leituraId,
            dadosAntes: anterior, dadosDepois: new { leitura.ValorM3Validado, leitura.Status },
            origem: "web_operador", motivo: request.Observacao);

        return MapearResponse(leitura, leitura.Unidade.Numero, false);
    }

    public async Task<LeituraResponse> RejeitarAsync(int leituraId, int operadorId, ValidarLeituraRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MotivoRejeicao))
            throw new HidrometroValidationException("Motivo de rejeição é obrigatório");

        var leitura = await _db.LeiturasHidrometro
            .Include(l => l.Unidade)
            .FirstOrDefaultAsync(l => l.Id == leituraId)
            ?? throw new NotFoundException($"Leitura {leituraId} não encontrada");

        var anterior = new { leitura.Status };

        leitura.Status = StatusLeitura.Rejeitado;
        leitura.MotivoRejeicao = Sanitizar(request.MotivoRejeicao);
        leitura.ValidadoPorId = operadorId;
        leitura.ValidadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(operadorId, "leituras_hidrometro", "REJECT", leituraId,
            dadosAntes: anterior, dadosDepois: new { leitura.Status, leitura.MotivoRejeicao },
            origem: "web_operador", motivo: request.MotivoRejeicao);

        return MapearResponse(leitura, leitura.Unidade.Numero, false);
    }

    public async Task<ProgressoOsResponse> ObterProgressoAsync(int osId)
    {
        var os = await _db.OrdensServico
            .Include(o => o.Condominio)
            .ThenInclude(c => c.Unidades.Where(u => u.Ativa))
            .FirstOrDefaultAsync(o => o.Id == osId)
            ?? throw new NotFoundException($"OS {osId} não encontrada");

        var unidades = os.Condominio.Unidades.ToList();
        var leiturasOk = await _db.LeiturasHidrometro
            .Where(l => l.OsId == osId && l.Status != StatusLeitura.Rejeitado)
            .Select(l => l.UnidadeId)
            .Distinct()
            .ToListAsync();

        var validadas = await _db.LeiturasHidrometro
            .Where(l => l.OsId == osId && l.Status == StatusLeitura.Validado)
            .CountAsync();

        var unidadesFaltando = unidades
            .Where(u => !leiturasOk.Contains(u.Id))
            .Select(u => new UnidadePendente { Id = u.Id, Numero = u.Numero })
            .ToList();

        return new ProgressoOsResponse
        {
            OsId = osId,
            TotalUnidades = unidades.Count,
            LeiturasRegistradas = leiturasOk.Count,
            LeiturasValidadas = validadas,
            FaltandoRegistrar = unidadesFaltando.Count,
            PercentualConcluido = unidades.Count > 0
                ? Math.Round((decimal)leiturasOk.Count / unidades.Count * 100, 1)
                : 0,
            UnidadesFaltando = unidadesFaltando
        };
    }

    public async Task<IEnumerable<LeituraResponse>> ListarPorOsAsync(int osId)
    {
        var leituras = await _db.LeiturasHidrometro
            .Include(l => l.Unidade)
            .Where(l => l.OsId == osId)
            .OrderBy(l => l.Unidade.Numero)
            .ToListAsync();

        return leituras.Select(l => MapearResponse(l, l.Unidade.Numero, false));
    }

    public async Task<byte[]?> ObterFotoAsync(int leituraId)
    {
        var leitura = await _db.LeiturasHidrometro
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leituraId);

        if (leitura?.FotoPath == null) return null;

        if (File.Exists(leitura.FotoPath))
            return await File.ReadAllBytesAsync(leitura.FotoPath);

        return null;
    }

    private async Task<string> SalvarFotoAsync(byte[] bytes, string nomeArquivo, int condominioId)
    {
        var basePath = _config["STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "storage", "fotos");
        var mesAno = DateTime.Now.ToString("yyyyMM");
        var pasta = Path.Combine(basePath, mesAno, $"condo_{condominioId}");
        Directory.CreateDirectory(pasta);

        var ext = Path.GetExtension(nomeArquivo).ToLower();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") ext = ".jpg";

        var nomeSeguro = $"{Guid.NewGuid()}{ext}";
        var caminho = Path.Combine(pasta, nomeSeguro);
        await File.WriteAllBytesAsync(caminho, bytes);
        return caminho;
    }

    private async Task<decimal> ObterUltimaLeituraValidadaAsync(int unidadeId)
    {
        return await _db.LeiturasHidrometro
            .Where(l => l.UnidadeId == unidadeId && l.Status == StatusLeitura.Validado && l.ValorM3Validado.HasValue)
            .OrderByDescending(l => l.ValidadoEm)
            .Select(l => l.ValorM3Validado!.Value)
            .FirstOrDefaultAsync();
    }

    private async Task RegistrarHistoricoConsumoAsync(LeituraHidrometro leitura)
    {
        var ultimaLeitura = await _db.LeiturasHidrometro
            .Where(l => l.UnidadeId == leitura.UnidadeId
                && l.Status == StatusLeitura.Validado
                && l.Id != leitura.Id
                && l.ValorM3Validado.HasValue)
            .OrderByDescending(l => l.ValidadoEm)
            .Select(l => l.ValorM3Validado!.Value)
            .FirstOrDefaultAsync();

        var os = await _db.OrdensServico.FindAsync(leitura.OsId);

        _db.HistoricoConsumo.Add(new HistoricoConsumo
        {
            UnidadeId = leitura.UnidadeId,
            OsId = leitura.OsId,
            LeituraAnterior = ultimaLeitura > 0 ? ultimaLeitura : null,
            LeituraAtual = leitura.ValorM3Validado,
            ConsumoM3 = ultimaLeitura > 0 ? leitura.ValorM3Validado - ultimaLeitura : null,
            Mes = os?.Mes ?? DateTime.Now.Month,
            Ano = os?.Ano ?? DateTime.Now.Year
        });

        await _db.SaveChangesAsync();
    }

    private async Task VerificarOsCompleta(int osId)
    {
        var progresso = await ObterProgressoAsync(osId);
        if (progresso.FaltandoRegistrar == 0 && progresso.LeiturasValidadas == progresso.TotalUnidades)
        {
            var os = await _db.OrdensServico.FindAsync(osId);
            if (os != null)
            {
                os.Status = StatusOS.Validada;
                await _db.SaveChangesAsync();
            }
        }
    }

    private static string? Sanitizar(string? s, int max = 500) =>
        s == null ? null : s.Length > max ? s[..max] : s;

    private static LeituraResponse MapearResponse(LeituraHidrometro l, string numeroUnidade, bool permiteRecurso) => new()
    {
        Id = l.Id,
        UnidadeId = l.UnidadeId,
        NumeroUnidade = numeroUnidade,
        Sucesso = l.Status != StatusLeitura.Rejeitado,
        ValorM3 = l.ValorM3Validado ?? l.ValorM3,
        ValorLitros = l.ValorLitros,
        ConfiancaIa = l.ConfiancaIa,
        Origem = l.Origem.ToString(),
        Status = l.Status.ToString(),
        QualidadeFoto = l.QualidadeFoto.ToString(),
        SuspeitaVazamento = l.SuspeitaVazamento,
        PermiteRecurso = permiteRecurso,
        Motivo = l.MotivoRejeicao,
        CriadoEm = l.CriadoEm
    };
}
