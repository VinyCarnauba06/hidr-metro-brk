using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        var client = CriarClient();
        var stats = await client.GetFromJsonAsync<dynamic>("/api/admin/dashboard");
        return View(stats);
    }

    public async Task<IActionResult> Auditoria(
        string? tabela, string? acao, int? usuarioId,
        DateTime? de, DateTime? ate, int pagina = 1)
    {
        var client = CriarClient();
        var qs = $"?pagina={pagina}";
        if (!string.IsNullOrEmpty(tabela)) qs += $"&tabela={tabela}";
        if (!string.IsNullOrEmpty(acao)) qs += $"&acao={acao}";
        if (usuarioId.HasValue) qs += $"&usuarioId={usuarioId}";
        if (de.HasValue) qs += $"&de={de:yyyy-MM-dd}";
        if (ate.HasValue) qs += $"&ate={ate:yyyy-MM-dd}";

        var registros = await client.GetFromJsonAsync<List<dynamic>>($"/api/admin/auditoria{qs}");
        ViewBag.Pagina = pagina;
        return View(registros ?? new());
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
