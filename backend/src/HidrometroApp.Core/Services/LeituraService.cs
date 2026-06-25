using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HidrometroApp.Core.Services;

public class LeituraService : ILeituraService
{
    private readonly HidrometroDbContext _db;
    private readonly IGeminiVisionService _vision;
    private readonly IAuditoriaService _auditoria;
    private readonly AnomaliaService _anomalia;
    private readonly IFotoStorage _fotoStorage;
    private readonly ILogger<LeituraService> _logger;

    public LeituraService(
        HidrometroDbContext db,
        IGeminiVisionService vision,
        IAuditoriaService auditoria,
        AnomaliaService anomalia,
        IFotoStorage fotoStorage,
        ILogger<LeituraService> logger)
    {
        _db = db;
        _vision = vision;
        _auditoria = auditoria;
        _anomalia = anomalia;
        _fotoStorage = fotoStorage;
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
            .Where(l => l.OsId == osId && l.UnidadeId == unidadeId
                && (l.Status == StatusLeitura.Rejeitado || l.Status == StatusLeitura.Rejeitada))
            .CountAsync();

        if (tentativasAnteriores >= 3)
            throw new FotoRejeitadaException("Máximo de 3 tentativas atingido. Use o recurso manual.");

        // Corrigir orientação EXIF antes de salvar e enviar ao OCR
        fotoBytes = CorrigirOrientacaoExif(fotoBytes);

        // Salvar foto localmente
        var fotoPath = await SalvarFotoAsync(fotoBytes, nomeArquivo, os.CondominioId);

        // Analisar com Gemini Vision
        LeituraResultadoIa resultado;
        try
        {
            resultado = await _vision.AnalisarFotoAsync(fotoBytes);
        }
        catch (HidrometroApp.Core.Exceptions.OcrSemLeituraValidaException ex)
        {
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

        // Confiança insuficiente → salva registro para auditoria e retorna 422
        if (!resultado.Sucesso)
        {
            var leituraRej = new LeituraHidrometro
            {
                OsId          = osId,
                UnidadeId     = unidadeId,
                FotoPath      = fotoPath,
                Origem        = OrigemLeitura.Ia,
                ConfiancaIa   = resultado.Confianca,
                Tentativas    = tentativasAnteriores + 1,
                Status        = StatusLeitura.Rejeitada,
                QualidadeFoto = QualidadeFoto.BaixaConfianca,
                CriadoPorId   = fiscalId
            };
            _db.LeiturasHidrometro.Add(leituraRej);
            if (os.Status == StatusOS.Aberta) os.Status = StatusOS.EmProgresso;
            await _db.SaveChangesAsync();
            await _auditoria.RegistrarAsync(fiscalId, "leituras_hidrometro", "INSERT", leituraRej.Id,
                dadosDepois: new { leituraRej.ConfiancaIa, leituraRej.Origem, Status = "Rejeitado" },
                origem: "app_fiscal");
            throw new FotoRejeitadaException("Qualidade insuficiente. Refaça a foto.");
        }

        // Triagem por confiança: ≥0.85 aceita auto, 0.50–0.84 aguarda revisão
        var status = resultado.Confianca >= 0.85m
            ? StatusLeitura.Aceita
            : StatusLeitura.AguardandoRevisao;

        var leitura = new LeituraHidrometro
        {
            OsId               = osId,
            UnidadeId          = unidadeId,
            FotoPath           = fotoPath,
            ValorM3            = resultado.HidrometroM3,
            ValorLitros        = resultado.Litros ?? 0,
            Origem             = OrigemLeitura.Ia,
            ConfiancaIa        = resultado.Confianca,
            Tentativas         = tentativasAnteriores + 1,
            Status             = status,
            QualidadeFoto      = QualidadeFoto.Ok,
            PrioridadeOperador = resultado.Confianca < 0.85m,
            CriadoPorId        = fiscalId
        };

        // Verificar suspeita de vazamento
        if (resultado.HidrometroM3.HasValue)
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

        if (os.Status == StatusOS.Aberta)
            os.Status = StatusOS.EmProgresso;

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
            .Where(l => l.OsId == osId
                && l.Status != StatusLeitura.Rejeitado
                && l.Status != StatusLeitura.Rejeitada)
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

        return await _fotoStorage.ObterAsync(leitura.FotoPath);
    }

    public static byte[] CorrigirOrientacaoExif(byte[] bytes)
    {
        try
        {
            using var input = new MemoryStream(bytes);
            using var image = SixLabors.ImageSharp.Image.Load(input);
            image.Mutate(x => x.AutoOrient());
            using var output = new MemoryStream();
            image.SaveAsJpeg(output);
            return output.ToArray();
        }
        catch
        {
            // Se não for imagem reconhecida ou não tiver EXIF, retornar bytes originais
            return bytes;
        }
    }

    private async Task<string> SalvarFotoAsync(byte[] bytes, string nomeArquivo, int condominioId)
    {
        var mesAno = DateTime.Now.ToString("yyyyMM");

        var ext = Path.GetExtension(nomeArquivo).ToLower();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") ext = ".jpg";

        var objectName = $"{mesAno}/condo_{condominioId}/{Guid.NewGuid()}{ext}";
        var contentType = ext == ".png" ? "image/png" : "image/jpeg";
        return await _fotoStorage.SalvarAsync(bytes, objectName, contentType);
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
        Id                 = l.Id,
        UnidadeId          = l.UnidadeId,
        NumeroUnidade      = numeroUnidade,
        Sucesso            = l.Status != StatusLeitura.Rejeitado && l.Status != StatusLeitura.Rejeitada,
        ValorM3            = l.ValorM3Validado ?? l.ValorM3,
        ValorLitros        = l.ValorLitros,
        ConfiancaIa        = l.ConfiancaIa,
        Origem             = l.Origem.ToString(),
        Status             = l.Status.ToString(),
        QualidadeFoto      = l.QualidadeFoto.ToString(),
        SuspeitaVazamento  = l.SuspeitaVazamento,
        PermiteRecurso     = permiteRecurso,
        PrioridadeOperador = l.PrioridadeOperador,
        Motivo             = l.MotivoRejeicao,
        CriadoEm          = l.CriadoEm
    };
}
