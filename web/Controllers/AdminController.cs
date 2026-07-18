using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HidrometroApp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IHttpClientFactory _http;

    public AdminController(IHttpClientFactory http) => _http = http;

    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var client = CriarClient();
            var data   = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
            ViewBag.Operadores = null;
            return View(data);
        }
        catch
        {
            ViewBag.Operadores = MockOperadores();
            return View(MockDashboard());
        }
    }

    public async Task<IActionResult> Auditoria(
        string? tabela, string? acao, int? usuarioId,
        DateTime? de, DateTime? ate, int pagina = 1)
    {
        try
        {
            var client = CriarClient();
            var qs = $"?pagina={pagina}";
            if (!string.IsNullOrEmpty(tabela)) qs += $"&tabela={tabela}";
            if (!string.IsNullOrEmpty(acao))   qs += $"&acao={acao}";
            if (usuarioId.HasValue)            qs += $"&usuarioId={usuarioId}";
            if (de.HasValue)                   qs += $"&de={de:yyyy-MM-dd}";
            if (ate.HasValue)                  qs += $"&ate={ate:yyyy-MM-dd}";

            var registros = await client.GetFromJsonAsync<List<JsonElement>>($"/api/admin/auditoria{qs}");
            ViewBag.Pagina = pagina;
            return View(registros ?? new());
        }
        catch
        {
            ViewBag.Pagina = pagina;
            return View(MockAuditoria());
        }
    }

    public async Task<IActionResult> Condominios()
    {
        try
        {
            var client = CriarClient();
            var lista  = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/condominios");
            return View(lista ?? new());
        }
        catch
        {
            return View(MockCondominios());
        }
    }

    [HttpPost]
    public async Task<IActionResult> CriarCondominio(
        string nome, string? endereco, int qtdUnidades, string tipoMedidor, string? numerosTexto)
    {
        try
        {
            var numeros = string.IsNullOrWhiteSpace(numerosTexto)
                ? null
                : numerosTexto
                    .Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .ToList();

            var client = CriarClient();
            var resp = await client.PostAsJsonAsync("/api/admin/condominios", new
            {
                nome,
                endereco,
                qtdUnidades = numeros?.Count ?? qtdUnidades,
                tipoMedidor,
                numeros
            });

            if (resp.IsSuccessStatusCode)
            {
                TempData["Sucesso"] = $"Condomínio \"{nome}\" criado com sucesso.";
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                try
                {
                    var doc = JsonDocument.Parse(body);
                    TempData["Erro"] = doc.RootElement.TryGetProperty("title", out var t)
                        ? t.GetString()
                        : $"Erro {(int)resp.StatusCode}";
                }
                catch { TempData["Erro"] = $"Erro ao criar condomínio ({(int)resp.StatusCode})."; }
            }
        }
        catch
        {
            TempData["Erro"] = "Não foi possível conectar à API.";
        }

        return RedirectToAction(nameof(Condominios));
    }

    public async Task<IActionResult> Usuarios()
    {
        try
        {
            var client = CriarClient();
            var lista  = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/usuarios");
            return View(lista ?? new());
        }
        catch
        {
            return View(MockUsuarios());
        }
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

    // ── Mock data ────────────────────────────────────────────────────────

    private static JsonElement MockDashboard()
    {
        var raw = new
        {
            totalCondominios   = 60,
            totalUnidades      = 2340,
            ordensAbertas      = 12,
            suspeitasVazamento = 23,
            leiturasNoMes      = 1847,
            leiturasIa         = 1631,
            leiturasManual     = 216,
            totalUsuarios      = 18,
            ordensNoMes        = 12,
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(raw)).RootElement;
    }

    private static List<dynamic> MockOperadores() => new()
    {
        new { nome = "Ádna Ferreira", condominiosMin =  1, condominiosMax = 20, progresso = 72 },
        new { nome = "Júnior Costa",  condominiosMin = 21, condominiosMax = 40, progresso = 91 },
        new { nome = "Lucas Mendes",  condominiosMin = 41, condominiosMax = 60, progresso = 45 },
    };

    private static List<JsonElement> MockAuditoria()
    {
        var raw = new[]
        {
            new { criadoEm = DateTime.Now.AddMinutes(-5),  usuario = "Ádna Ferreira", tabela = "leituras_hidrometro",
                  acao = "VALIDATE", registroId = "847",           origem = "Web",    motivo = "" },
            new { criadoEm = DateTime.Now.AddMinutes(-18), usuario = "Lucas Mendes",  tabela = "leituras_hidrometro",
                  acao = "REJECT",   registroId = "846",           origem = "Web",    motivo = "Foto ilegível" },
            new { criadoEm = DateTime.Now.AddHours(-1),    usuario = "Admin",         tabela = "ordens_servico",
                  acao = "INSERT",   registroId = "OS-062026-004", origem = "Web",    motivo = "Nova OS — Parque das Dunas" },
            new { criadoEm = DateTime.Now.AddHours(-3),    usuario = "Júnior Costa",  tabela = "leituras_hidrometro",
                  acao = "CORRECT",  registroId = "831",           origem = "Web",    motivo = "Valor OCR incorreto — corrigido para 12.4 m³" },
            new { criadoEm = DateTime.Now.AddHours(-5),    usuario = "José Fiscal",   tabela = "leituras_hidrometro",
                  acao = "MANUAL",   registroId = "815",           origem = "Mobile", motivo = "Entrada manual — hidrômetro sem visibilidade" },
        };
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<List<JsonElement>>(json)!;
    }

    private static List<JsonElement> MockCondominios()
    {
        var raw = new[]
        {
            new { id = 1, nome = "Residencial Atlântico", endereco = "Av. Fernandes Lima, 1200 — Farol",
                  totalUnidades = 40, tipoMedidor = "AguaFria",    osAtiva = true,  progresso = 62 },
            new { id = 2, nome = "Edifício Coral",        endereco = "R. Comendador Palmeira, 340 — Centro",
                  totalUnidades = 20, tipoMedidor = "AguaFria",    osAtiva = true,  progresso = 100 },
            new { id = 3, nome = "Torres do Sol",         endereco = "Av. Gustavo Paiva, 5600 — Cruz das Almas",
                  totalUnidades = 30, tipoMedidor = "AguaQuente",  osAtiva = true,  progresso = 100 },
            new { id = 4, nome = "Parque das Dunas",      endereco = "R. Engenheiro Paulo Brandão, 80 — Jatiúca",
                  totalUnidades = 25, tipoMedidor = "AguaFria",    osAtiva = true,  progresso = 0 },
            new { id = 5, nome = "Brisa do Mar",          endereco = "Av. Álvaro Otacílio, 2400 — Pajuçara",
                  totalUnidades = 18, tipoMedidor = "Gas",         osAtiva = false, progresso = 0 },
            new { id = 6, nome = "Ville Blanc",           endereco = "R. Deputado Silvio Lamenha Filho, 190 — Gruta",
                  totalUnidades = 32, tipoMedidor = "AguaFria",    osAtiva = true,  progresso = 41 },
        };
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<List<JsonElement>>(json)!;
    }

    private static List<JsonElement> MockUsuarios()
    {
        var raw = new[]
        {
            new { id = 1, nome = "Administrador", email = "admin@prolar.com",  perfil = "Admin",
                  ativo = true,  ultimoAcesso = DateTime.Now.AddHours(-1) },
            new { id = 2, nome = "Ádna Ferreira", email = "adna@prolar.com",   perfil = "Operador",
                  ativo = true,  ultimoAcesso = DateTime.Now.AddMinutes(-5) },
            new { id = 3, nome = "Júnior Costa",  email = "junior@prolar.com", perfil = "Operador",
                  ativo = true,  ultimoAcesso = DateTime.Now.AddHours(-2) },
            new { id = 4, nome = "Lucas Mendes",  email = "lucas@prolar.com",  perfil = "Operador",
                  ativo = true,  ultimoAcesso = DateTime.Now.AddHours(-3) },
            new { id = 5, nome = "José Fiscal",   email = "jose@prolar.com",   perfil = "Fiscal",
                  ativo = true,  ultimoAcesso = DateTime.Now.AddHours(-6) },
            new { id = 6, nome = "Maria Fiscal",  email = "maria@prolar.com",  perfil = "Fiscal",
                  ativo = false, ultimoAcesso = DateTime.Now.AddDays(-3) },
        };
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<List<JsonElement>>(json)!;
    }
}
