using System.Text;
using System.Text.Json;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HidrometroApp.Infrastructure.Services;

public class GeminiVisionService : IAzureVisionService
{
    private static readonly HttpClient _http = new();
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiVisionService> _logger;

    private const decimal CONFIANCA_MINIMA  = 0.50m;
    private const decimal CONFIANCA_RECURSO = 0.40m;

    private const string PROMPT = """
        Você é um leitor especializado em visores de hidrômetros (medidores de água).
        Analise a imagem e extraia a leitura do visor principal.

        Regras:
        - O visor mostra metros cúbicos (m³) com dígitos pretos, e litros com dígitos vermelhos
        - Retorne APENAS um JSON válido, sem markdown, sem explicações
        - Se não conseguir ler com certeza, retorne sucesso=false

        Formato obrigatório:
        {"sucesso": true, "m3": 123, "litros": 456, "confianca": 0.95}
        ou
        {"sucesso": false, "motivo": "imagem desfocada"}
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
        var isPng  = fotoBytes[0] == 0x89 && fotoBytes[1] == 0x50 && fotoBytes[2] == 0x4E && fotoBytes[3] == 0x47;

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
                Sucesso = false,
                Motivo = "Foto inválida ou muito pequena",
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
            var base64 = Convert.ToBase64String(fotoBytes);
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
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 100
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return ParsearResposta(responseJson);
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
                Sucesso = false,
                Motivo = "Erro ao processar imagem",
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

        var sucesso = root.GetProperty("sucesso").GetBoolean();

        if (!sucesso)
        {
            var motivo = root.TryGetProperty("motivo", out var m) ? m.GetString() : "Leitura não reconhecida";
            _logger.LogWarning("Gemini não reconheceu visor: {Motivo}", motivo);
            throw new OcrSemLeituraValidaException(motivo ?? "Imagem ilegível");
        }

        var m3 = root.GetProperty("m3").GetDecimal();
        var litros = root.TryGetProperty("litros", out var l) ? l.GetInt32() : 0;
        var confianca = root.TryGetProperty("confianca", out var c) ? c.GetDecimal() : 0.85m;

        return new LeituraResultadoIa
        {
            Sucesso        = confianca >= CONFIANCA_MINIMA,
            HidrometroM3   = m3,
            Litros         = litros,
            Confianca      = confianca,
            PermiteRecurso = confianca >= CONFIANCA_RECURSO,
            Motivo         = confianca < CONFIANCA_MINIMA ? "Baixa confiança — verifique o valor" : null
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
