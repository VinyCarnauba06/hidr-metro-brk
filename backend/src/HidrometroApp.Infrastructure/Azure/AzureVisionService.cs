using System.Text.RegularExpressions;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Infrastructure.Azure;

public class AzureVisionService : IAzureVisionService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureVisionService> _logger;

    private const decimal CONFIANCA_MINIMA = 0.80m;
    private const decimal CONFIANCA_RECURSO = 0.40m;

    public AzureVisionService(IConfiguration config, ILogger<AzureVisionService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<LeituraResultadoIa> AnalisarFotoAsync(byte[] fotoBytes)
    {
        try
        {
            if (!ValidarQualidadeFoto(fotoBytes))
            {
                return new LeituraResultadoIa
                {
                    Sucesso = false,
                    Motivo = "Foto muito escura, desfocada ou pequena demais",
                    PermiteRecurso = true
                };
            }

            var endpoint = _config["AZURE_VISION_ENDPOINT"] ?? _config["Azure:VisionEndpoint"];
            var key = _config["AZURE_VISION_KEY"] ?? _config["Azure:VisionKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Azure Vision não configurado. Usando modo simulado.");
                return SimularLeitura();
            }

            // Integração real com Azure Computer Vision
            using var client = new Azure.AI.Vision.ImageAnalysis.ImageAnalysisClient(
                new Uri(endpoint),
                new Azure.AzureKeyCredential(key)
            );

            using var stream = new MemoryStream(fotoBytes);
            var result = await client.AnalyzeAsync(
                Azure.AI.Vision.ImageAnalysis.BinaryData.FromBytes(fotoBytes),
                Azure.AI.Vision.ImageAnalysis.VisualFeatures.Read
            );

            return ParsearResultado(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar foto no Azure Vision");
            return new LeituraResultadoIa
            {
                Sucesso = false,
                Motivo = "Erro ao processar imagem",
                PermiteRecurso = true
            };
        }
    }

    public bool ValidarQualidadeFoto(byte[] fotoBytes)
    {
        // Tamanho mínimo: 50KB
        if (fotoBytes.Length < 50 * 1024) return false;

        // Tamanho máximo: 10MB
        if (fotoBytes.Length > 10 * 1024 * 1024) return false;

        // Verificar se é JPEG ou PNG pelo magic bytes
        var isJpeg = fotoBytes.Length > 3 && fotoBytes[0] == 0xFF && fotoBytes[1] == 0xD8;
        var isPng = fotoBytes.Length > 4 && fotoBytes[0] == 0x89 && fotoBytes[1] == 0x50;
        if (!isJpeg && !isPng) return false;

        return true;
    }

    // Regex ancorada para padrão de visor: 4-7 dígitos + separador decimal obrigatório.
    // Ancorada com ^ e $ para não casar substrings de seriais alfanuméricos mais longos.
    private static readonly Regex ReVisorDecimal =
        new(@"^(\d{4,7})[,\.](\d{1,3})$", RegexOptions.Compiled);

    // Padrão secundário (sem casas decimais): confiança reduzida pois pode ser um código.
    private static readonly Regex ReVisorInteiro =
        new(@"^\d{4,7}$", RegexOptions.Compiled);

    // Qualquer letra indica número de série, rótulo ou unidade de medida — descarta a linha.
    private static readonly Regex ReContemLetra =
        new(@"[A-Za-zÀ-ÿ]", RegexOptions.Compiled);

    private LeituraResultadoIa ParsearResultado(Azure.AI.Vision.ImageAnalysis.ImageAnalysisResult result)
    {
        var linhasBrutas = result.Read?.Blocks
            .SelectMany(b => b.Lines)
            .Select(l => l.Text.Trim())
            .ToList() ?? new List<string>();

        // Descartar linhas que contenham letras (seriais, labels, unidades)
        var candidatos = linhasBrutas
            .Where(l => !ReContemLetra.IsMatch(l))
            .ToList();

        _logger.LogDebug("OCR: {Total} linhas brutas → {Candidatos} candidatos sem letras",
            linhasBrutas.Count, candidatos.Count);

        decimal? valorM3 = null;
        int litros = 0;
        decimal confianca = 0;

        foreach (var linha in candidatos)
        {
            // Preferência máxima: visor com decimal (ex: "00123,456" ou "00123.456")
            var mDecimal = ReVisorDecimal.Match(linha);
            if (mDecimal.Success
                && decimal.TryParse(mDecimal.Groups[1].Value, out var m3Dec)
                && int.TryParse(mDecimal.Groups[2].Value, out var l))
            {
                valorM3 = m3Dec;
                litros = l;
                confianca = 0.90m;
                break; // melhor candidato possível — para aqui
            }

            // Fallback: somente dígitos sem separador (confiança menor)
            if (valorM3 == null)
            {
                var mInteiro = ReVisorInteiro.Match(linha);
                if (mInteiro.Success && decimal.TryParse(mInteiro.Value, out var m3Int))
                {
                    valorM3 = m3Int;
                    litros = 0;
                    confianca = 0.60m;
                    // não break — continua buscando padrão decimal de maior confiança
                }
            }
        }

        if (valorM3 == null || confianca < CONFIANCA_RECURSO)
        {
            _logger.LogWarning(
                "OCR: nenhum padrão de visor encontrado em {N} candidatos (linhas brutas: {T})",
                candidatos.Count, linhasBrutas.Count);

            // Lança exceção para impedir que lixo (seriais, labels) seja salvo no banco.
            throw new OcrSemLeituraValidaException(
                "Nenhum padrão de visor numérico detectado na foto. " +
                "Tire uma nova foto focando diretamente no display do hidrômetro.");
        }

        return new LeituraResultadoIa
        {
            Sucesso = confianca >= CONFIANCA_MINIMA,
            HidrometroM3 = valorM3,
            Litros = litros,
            Confianca = confianca,
            PermiteRecurso = confianca >= CONFIANCA_RECURSO,
            Motivo = confianca < CONFIANCA_MINIMA ? "Baixa confiança — verifique o valor extraído" : null
        };
    }

    private LeituraResultadoIa SimularLeitura()
    {
        // Modo simulado para desenvolvimento sem Azure
        var random = new Random();
        var valor = random.Next(100, 9999);
        var confianca = (decimal)(random.NextDouble() * 0.3 + 0.7);

        return new LeituraResultadoIa
        {
            Sucesso = true,
            HidrometroM3 = valor,
            Litros = random.Next(0, 999),
            Confianca = Math.Round(confianca, 2),
            PermiteRecurso = true,
            Motivo = "[MODO SIMULADO — sem Azure configurado]"
        };
    }
}
