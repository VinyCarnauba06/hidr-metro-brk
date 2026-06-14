using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HidrometroApp.Web.Controllers;

[Authorize(Roles = "Operador,Admin")]
public class OperadorController : Controller
{
    private readonly IHttpClientFactory _http;

    public OperadorController(IHttpClientFactory http) => _http = http;

    public async Task<IActionResult> Index()
    {
        try
        {
            var client = CriarClient();
            var ordens = await client.GetFromJsonAsync<List<JsonElement>>("/api/operador/ordens-aguardando");
            return View(ordens ?? new());
        }
        catch
        {
            return View(MockOrdens());
        }
    }

    public async Task<IActionResult> Validar(int osId)
    {
        try
        {
            var client    = CriarClient();
            var leituras  = await client.GetFromJsonAsync<List<JsonElement>>($"/api/operador/ordens/{osId}/leituras");
            var progresso = await client.GetFromJsonAsync<JsonElement>($"/api/operador/os/{osId}/progresso");
            ViewBag.OsId      = osId;
            ViewBag.Progresso = progresso;
            return View(leituras ?? new());
        }
        catch
        {
            ViewBag.OsId      = osId;
            ViewBag.Progresso = MockProgresso();
            return View(MockLeituras());
        }
    }

    [HttpPost]
    public async Task<IActionResult> AprovarLeitura(int id, int osId)
    {
        try
        {
            var client = CriarClient();
            var resp   = await client.PatchAsJsonAsync($"/api/operador/leituras/{id}/validar",
                new { ValorM3Corrigido = (decimal?)null, MotivoRejeicao = (string?)null, Observacao = (string?)null });

            if (!resp.IsSuccessStatusCode)
                TempData["Erro"] = await ExtrairMensagemErro(resp);
        }
        catch { /* modo mock – redireciona silenciosamente */ }

        return RedirectToAction("Validar", new { osId });
    }

    [HttpPost]
    public async Task<IActionResult> CorrigirLeitura(int id, int osId, decimal valorCorrigido, string? observacao)
    {
        try
        {
            var client = CriarClient();
            var resp   = await client.PatchAsJsonAsync($"/api/operador/leituras/{id}/corrigir",
                new { ValorM3Corrigido = valorCorrigido, Observacao = observacao });

            if (!resp.IsSuccessStatusCode)
                TempData["Erro"] = await ExtrairMensagemErro(resp);
        }
        catch { /* modo mock */ }

        return RedirectToAction("Validar", new { osId });
    }

    [HttpPost]
    public async Task<IActionResult> RejeitarLeitura(int id, int osId, string motivoRejeicao)
    {
        try
        {
            var client = CriarClient();
            var resp   = await client.PatchAsJsonAsync($"/api/operador/leituras/{id}/rejeitar",
                new { MotivoRejeicao = motivoRejeicao });

            if (!resp.IsSuccessStatusCode)
                TempData["Erro"] = await ExtrairMensagemErro(resp);
        }
        catch { /* modo mock */ }

        return RedirectToAction("Validar", new { osId });
    }

    public async Task<IActionResult> GerarExcel(int osId)
    {
        try
        {
            var client = CriarClient();
            var resp   = await client.PostAsync($"/api/operador/relatorio/{osId}/excel", null);

            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"leituras_os{osId}.xlsx");
            }

            TempData["Erro"] = await ExtrairMensagemErro(resp);
        }
        catch { /* gera mock abaixo */ }

        return File(MockXlsx(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"leituras_os{osId}_mock.xlsx");
    }

    public async Task<IActionResult> GerarPdf(int osId)
    {
        try
        {
            var client = CriarClient();
            var resp   = await client.PostAsync($"/api/operador/relatorio/{osId}/pdf", null);

            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                return File(bytes, "application/pdf", $"leituras_os{osId}.pdf");
            }

            TempData["Erro"] = await ExtrairMensagemErro(resp);
        }
        catch { /* gera mock abaixo */ }

        return File(MockPdf(), "application/pdf", $"leituras_os{osId}_mock.pdf");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private HttpClient CriarClient()
    {
        var client = _http.CreateClient("api");
        var token  = User.FindFirst("token")?.Value;
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> ExtrairMensagemErro(HttpResponseMessage resp)
    {
        try
        {
            var json = await resp.Content.ReadAsStringAsync();
            var doc  = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? $"Erro {(int)resp.StatusCode}";
        }
        catch { }
        return $"Erro ao processar requisição (HTTP {(int)resp.StatusCode})";
    }

    // ── Mock data ────────────────────────────────────────────────────────

    private static List<JsonElement> MockOrdens()
    {
        var raw = new[]
        {
            new { id = 1, condominio = "Residencial Atlântico", mes = 6, ano = 2026,
                  totalLeituras = 40, leiturasValidadas = 25, leiturasPendentes = 15, suspeitasVazamento = 1 },
            new { id = 2, condominio = "Edifício Coral",        mes = 6, ano = 2026,
                  totalLeituras = 20, leiturasValidadas = 20, leiturasPendentes = 0,  suspeitasVazamento = 0 },
            new { id = 3, condominio = "Torres do Sol",         mes = 6, ano = 2026,
                  totalLeituras = 30, leiturasValidadas = 30, leiturasPendentes = 0,  suspeitasVazamento = 0 },
            new { id = 4, condominio = "Parque das Dunas",      mes = 6, ano = 2026,
                  totalLeituras = 25, leiturasValidadas = 0,  leiturasPendentes = 25, suspeitasVazamento = 0 },
        };
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<List<JsonElement>>(json)!;
    }

    private static JsonElement MockProgresso()
    {
        var raw = new { osId = 0, totalUnidades = 40, leiturasRegistradas = 25, leiturasValidadas = 20,
                        faltandoRegistrar = 15, percentualConcluido = 62.5, unidadesFaltando = new int[0] };
        return JsonDocument.Parse(JsonSerializer.Serialize(raw)).RootElement;
    }

    private static List<JsonElement> MockLeituras()
    {
        var raw = new[]
        {
            new { id = 101, unidadeId = 1, numeroUnidade = "Apto 301", sucesso = true, valorM3 = 24.7m,
                  valorLitros = 350, confiancaIa = 0.94, origem = "Ia", status = "Pendente",
                  qualidadeFoto = "Ok", suspeitaVazamento = true,  permiteRecurso = true,
                  motivo = (string?)null, criadoEm = DateTime.Now.AddHours(-2) },
            new { id = 102, unidadeId = 2, numeroUnidade = "Apto 402", sucesso = true, valorM3 = 8.2m,
                  valorLitros = 120, confiancaIa = 0.87, origem = "Ia", status = "Pendente",
                  qualidadeFoto = "Ok", suspeitaVazamento = false, permiteRecurso = true,
                  motivo = (string?)null, criadoEm = DateTime.Now.AddHours(-1) },
            new { id = 103, unidadeId = 3, numeroUnidade = "Apto 115", sucesso = true, valorM3 = 11.5m,
                  valorLitros = 200, confiancaIa = 0.91, origem = "Ia", status = "Validado",
                  qualidadeFoto = "Ok", suspeitaVazamento = false, permiteRecurso = false,
                  motivo = (string?)null, criadoEm = DateTime.Now.AddHours(-3) },
        };
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<List<JsonElement>>(json)!;
    }

    // Gera um .xlsx mínimo e válido (ZIP com Open XML)
    private static byte[] MockXlsx()
    {
        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        static void Add(ZipArchive z, string entry, string xml)
        {
            using var w = new StreamWriter(z.CreateEntry(entry).Open());
            w.Write(xml);
        }

        Add(zip, "[Content_Types].xml",
            @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
            @"<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" +
            @"<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" +
            @"<Default Extension=""xml"" ContentType=""application/xml""/>" +
            @"<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>" +
            @"<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>" +
            @"</Types>");

        Add(zip, "_rels/.rels",
            @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>" +
            @"</Relationships>");

        Add(zip, "xl/workbook.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
            @"<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">" +
            @"<sheets><sheet name=""Leituras"" sheetId=""1"" r:id=""rId1""/></sheets>" +
            @"</workbook>");

        Add(zip, "xl/_rels/workbook.xml.rels",
            @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>" +
            @"</Relationships>");

        Add(zip, "xl/worksheets/sheet1.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
            @"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData>" +
            @"<row r=""1""><c r=""A1"" t=""inlineStr""><is><t>Unidade</t></is></c><c r=""B1"" t=""inlineStr""><is><t>Leitura (m3)</t></is></c><c r=""C1"" t=""inlineStr""><is><t>Status</t></is></c></row>" +
            @"<row r=""2""><c r=""A2"" t=""inlineStr""><is><t>Apto 301</t></is></c><c r=""B2""><v>24.7</v></c><c r=""C2"" t=""inlineStr""><is><t>Pendente</t></is></c></row>" +
            @"<row r=""3""><c r=""A3"" t=""inlineStr""><is><t>Apto 402</t></is></c><c r=""B3""><v>8.2</v></c><c r=""C3"" t=""inlineStr""><is><t>Pendente</t></is></c></row>" +
            @"<row r=""4""><c r=""A4"" t=""inlineStr""><is><t>Apto 115</t></is></c><c r=""B4""><v>11.5</v></c><c r=""C4"" t=""inlineStr""><is><t>Validado</t></is></c></row>" +
            @"</sheetData></worksheet>");

        zip.Dispose();
        return ms.ToArray();
    }

    // Gera um PDF mínimo e válido
    private static byte[] MockPdf()
    {
        // Calcula offsets dinamicamente para garantir xref correto
        var header = "%PDF-1.4\n";
        var obj1   = "1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n";
        var obj2   = "2 0 obj\n<</Type /Pages /Kids [3 0 R] /Count 1>>\nendobj\n";
        var obj3   = "3 0 obj\n<</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]>>\nendobj\n";

        int o1 = header.Length;
        int o2 = o1 + obj1.Length;
        int o3 = o2 + obj2.Length;
        int xr = o3 + obj3.Length;

        var xref =
            $"xref\n0 4\n" +
            $"0000000000 65535 f \n" +
            $"{o1:D10} 00000 n \n" +
            $"{o2:D10} 00000 n \n" +
            $"{o3:D10} 00000 n \n" +
            $"trailer\n<</Size 4 /Root 1 0 R>>\n" +
            $"startxref\n{xr}\n%%EOF\n";

        return System.Text.Encoding.ASCII.GetBytes(header + obj1 + obj2 + obj3 + xref);
    }
}
