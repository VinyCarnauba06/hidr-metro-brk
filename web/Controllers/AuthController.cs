using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HidrometroApp.Web.Controllers;

public class AuthController : Controller
{
    private readonly IHttpClientFactory  _http;
    private readonly IWebHostEnvironment _env;

    public AuthController(IHttpClientFactory http, IWebHostEnvironment env)
    {
        _http = http;
        _env  = env;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated != true) return View();

        return User.IsInRole("Admin")
            ? RedirectToAction("Dashboard", "Admin")
            : RedirectToAction("Index", "Operador");
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        LoginApiResponse? data = null;

        try
        {
            var client   = _http.CreateClient("api");
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { model.Email, model.Senha });

            if (response.IsSuccessStatusCode)
                data = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        }
        catch { /* API indisponível – recai no mock abaixo */ }

        if (data == null)
        {
            if (_env.IsDevelopment())
                return await MockLogin(model.Email);

            ModelState.AddModelError("", "Email ou senha inválidos");
            return View(model);
        }

        await AssinarCookie(data.Nome, data.Perfil, data.Token);

        return data.Perfil == "Admin"
            ? RedirectToAction("Dashboard", "Admin")
            : RedirectToAction("Index", "Operador");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task AssinarCookie(string nome, string perfil, string token)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,  nome),
            new(ClaimTypes.Email, nome),          // email ou nome derivado
            new(ClaimTypes.Role,  perfil),        // "Admin" ou "Operador"
            new("token",          token),
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private async Task<IActionResult> MockLogin(string email)
    {
        var isAdmin = email.Trim().Equals("admin@prolar.com", StringComparison.OrdinalIgnoreCase);
        var perfil  = isAdmin ? "Admin" : "Operador";
        var nome    = email.Contains('@') ? email.Split('@')[0] : email;

        await AssinarCookie(nome, perfil, "mock-jwt-token");

        return isAdmin
            ? RedirectToAction("Dashboard", "Admin")
            : RedirectToAction("Index", "Operador");
    }
}

public record LoginViewModel
{
    public string Email { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}

public record LoginApiResponse(string Token, string Nome, string Perfil, DateTime ExpiraEm);
