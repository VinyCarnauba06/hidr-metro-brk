using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
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
        var client = CriarClient();
        var ordens = await client.GetFromJsonAsync<List<dynamic>>("/api/operador/ordens-aguardando");
        return View(ordens ?? new());
    }

    public async Task<IActionResult> Validar(int osId)
    {
        var client = CriarClient();
        var leituras = await client.GetFromJsonAsync<List<dynamic>>($"/api/operador/ordens/{osId}/leituras");
        var progresso = await client.GetFromJsonAsync<dynamic>($"/api/operador/os/{osId}/progresso");

        ViewBag.OsId = osId;
        ViewBag.Progresso = progresso;
        return View(leituras ?? new());
    }

    [HttpPost]
    public async Task<IActionResult> AprovarLeitura(int id, int osId)
    {
        var client = CriarClient();
        await client.PatchAsJsonAsync($"/api/operador/leituras/{id}/validar", new { });
        return RedirectToAction("Validar", new { osId });
    }

    [HttpPost]
    public async Task<IActionResult> CorrigirLeitura(int id, int osId, decimal valorCorrigido, string? observacao)
    {
        var client = CriarClient();
        await client.PatchAsJsonAsync($"/api/operador/leituras/{id}/corrigir",
            new { ValorM3Corrigido = valorCorrigido, Observacao = observacao });
        return RedirectToAction("Validar", new { osId });
    }

    [HttpPost]
    public async Task<IActionResult> RejeitarLeitura(int id, int osId, string motivoRejeicao)
    {
        var client = CriarClient();
        await client.PatchAsJsonAsync($"/api/operador/leituras/{id}/rejeitar",
            new { MotivoRejeicao = motivoRejeicao });
        return RedirectToAction("Validar", new { osId });
    }

    public async Task<IActionResult> GerarExcel(int osId)
    {
        var client = CriarClient();
        var resp = await client.PostAsync($"/api/operador/relatorio/{osId}/excel", null);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"leituras_os{osId}.xlsx");
    }

    public async Task<IActionResult> GerarPdf(int osId)
    {
        var client = CriarClient();
        var resp = await client.PostAsync($"/api/operador/relatorio/{osId}/pdf", null);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        return File(bytes, "application/pdf", $"leituras_os{osId}.pdf");
    }

    private HttpClient CriarClient()
    {
        var client = _http.CreateClient("api");
        var token = User.FindFirst("token")?.Value;
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
