// backend/src/HidrometroApp.Infrastructure/Services/GeminiVisionService.cs
using System.Globalization;
using System.Text;
using System.Text.Json;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Infrastructure.Services;

public class GeminiVisionService : IGeminiVisionService
{
    private static readonly HttpClient _http = new();
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiVisionService> _logger;

    private const decimal CONFIANCA_MINIMA  = 0.50m;
    private const decimal CONFIANCA_RECURSO = 0.40m;

    private const string PROMPT = """
        You are an OCR specialist for utility meter reading. Analyze the meter display in the image and extract the reading.

        METER DISPLAY RULES:
        - The display has two sections: BLACK digits (on black/dark background) = integer part in m³; RED digits (on red/pink background) = decimal part in dm³ (liters)
        - Read ONLY digits shown on the rolling drum/odometer display
        - The final reading is: [black digits].[red digits] m³

        IGNORE completely:
        - Any handwritten numbers on the glass or casing (written in pen/marker by the field agent to identify the unit)
        - Serial numbers (they contain letters mixed with digits, e.g. "C12I0004630D", "D23L0006747D")
        - Barcodes
        - Labels/stickers on pipes or casing
        - The spinning star/impeller indicator
        - The analog sub-dial (small circular gauge)
        - Brand names (Itron, LAO, Akvometer, Hidrometer, Aquarius, Sappel, Diehl)

        METER FORMATS you may encounter:
        - Rectangular display (Itron, LAO): horizontal strip with 5 black + 3 red digits
        - Circular display (Akvometer, Hidrometer, Aquarius): round face with 5-6 black + 2-3 red digits

        CONFIDENCE RULES:
        - Return confidence 0.0 to 1.0
        - High confidence (≥0.85): all digits clearly visible, no blur, no obstruction
        - Medium confidence (0.50-0.84): some digits uncertain, minor blur, partial obstruction
        - Low confidence (<0.50): severe blur, display obstructed by seal/lock, image too dark/bright, display rotated >30°

        Respond ONLY with this JSON (no markdown, no explanation):
        {
          "leitura_inteira": "XXXXX",
          "leitura_decimal": "XXX",
          "leitura_completa": "XXXXX.XXX",
          "confianca": 0.00,
          "problema": "describe issue or null if none",
          "digitos_pretos_visiveis": true,
          "digitos_vermelhos_visiveis": true
        }
        """;

    public GeminiVisionService(IConfiguration config, ILogger<GeminiVisionService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool ValidarQualidadeFoto(byte[] fotoBytes)
    {
        if (fotoBytes.Length < 10_000)
        {
            _logger.LogWarning("Foto rejeitada: tamanho {Size} bytes abaixo do mínimo", fotoBytes.Length);
            return false;
        }

        var isJpeg = fotoBytes[0] == 0xFF && fotoBytes[1] == 0xD8 && fotoBytes[2] == 0xFF;
        var isPng  = fotoBytes.Length >= 4
            && fotoBytes[0] == 0x89 && fotoBytes[1] == 0x50
            && fotoBytes[2] == 0x4E && fotoBytes[3] == 0x47;

        if (!isJpeg && !isPng)
        {
            _logger.LogWarning("Foto rejeitada: formato inválido (não é JPEG nem PNG)");
            return false;
        }

        return true;
    }

    public async Task<LeituraResultadoIa> AnalisarFotoAsync(byte[] fotoBytes)
    {
        if (!ValidarQualidadeFoto(fotoBytes))
            return new LeituraResultadoIa
            {
                Sucesso        = false,
                Confianca      = 0,
                Motivo         = "Foto inválida ou muito pequena",
                PermiteRecurso = true
            };

        var apiKey = _config["GEMINI_API_KEY"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("GEMINI_API_KEY não configurada. Usando modo simulado.");
            return SimularLeitura();
        }

        try
        {
            var base64   = Convert.ToBase64String(fotoBytes);
            var mimeType = fotoBytes[0] == 0x89 ? "image/png" : "image/jpeg";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = PROMPT },
                            new { inline_data = new { mime_type = mimeType, data = base64 } }
                        }
                    }
                },
                generationConfig = new { temperature = 0.1, maxOutputTokens = 200 }
            };

            var url     = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            return ParsearResposta(await response.Content.ReadAsStringAsync());
        }
        catch (OcrSemLeituraValidaException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar Gemini Vision API");
            return new LeituraResultadoIa
            {
                Sucesso        = false,
                Confianca      = 0,
                Motivo         = "Erro ao processar imagem",
                PermiteRecurso = true
            };
        }
    }

    private LeituraResultadoIa ParsearResposta(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        var texto = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        _logger.LogDebug("Gemini resposta bruta: {Texto}", texto);

        using var resultado = JsonDocument.Parse(texto.Trim());
        var root = resultado.RootElement;

        var confianca = root.TryGetProperty("confianca", out var c) ? c.GetDecimal() : 0m;
        var problema  = root.TryGetProperty("problema", out var p) && p.ValueKind != JsonValueKind.Null
            ? p.GetString()
            : null;

        if (confianca < CONFIANCA_MINIMA)
        {
            var motivo = problema ?? "Baixa confiança — verifique o valor";
            _logger.LogWarning("Gemini baixa confiança ({Confianca:F2}): {Motivo}", confianca, motivo);
            return new LeituraResultadoIa
            {
                Sucesso        = false,
                Confianca      = confianca,
                PermiteRecurso = confianca >= CONFIANCA_RECURSO,
                Motivo         = motivo
            };
        }

        var leituraInteira  = root.TryGetProperty("leitura_inteira",  out var li) ? li.GetString() : null;
        var leituraDecimal  = root.TryGetProperty("leitura_decimal",  out var ld) ? ld.GetString() : null;

        if (string.IsNullOrEmpty(leituraInteira))
            throw new OcrSemLeituraValidaException("Nenhum dígito lido no visor");

        var m3     = decimal.Parse(leituraInteira, CultureInfo.InvariantCulture);
        var litros = !string.IsNullOrEmpty(leituraDecimal) && int.TryParse(leituraDecimal, out var l) ? l : 0;

        return new LeituraResultadoIa
        {
            Sucesso        = true,
            HidrometroM3   = m3,
            Litros         = litros,
            Confianca      = confianca,
            PermiteRecurso = true,
            Motivo         = problema
        };
    }

    private LeituraResultadoIa SimularLeitura()
    {
        var rng = new Random();
        return new LeituraResultadoIa
        {
            Sucesso        = true,
            HidrometroM3   = rng.Next(100, 9999),
            Litros         = rng.Next(0, 999),
            Confianca      = Math.Round((decimal)(rng.NextDouble() * 0.3 + 0.7), 2),
            PermiteRecurso = true,
            Motivo         = "[MODO SIMULADO — sem Gemini configurado]"
        };
    }
}
