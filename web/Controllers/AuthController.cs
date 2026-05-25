using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HidrometroApp.Web.Controllers;

public class AuthController : Controller
{
    private readonly IHttpClientFactory _http;

    public AuthController(IHttpClientFactory http) => _http = http;

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home")
        : View();

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var client = _http.CreateClient("api");
        var response = await client.PostAsJsonAsync("/api/auth/login", new { model.Cpf, model.Senha });

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "CPF ou senha inválidos");
            return View(model);
        }

        var data = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        if (data == null) return View(model);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, data.Nome),
            new(ClaimTypes.Role, data.Perfil),
            new("token", data.Token)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return data.Perfil switch
        {
            "Fiscal" => RedirectToAction("Index", "Operador"),
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            _ => RedirectToAction("Index", "Operador")
        };
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}

public record LoginViewModel
{
    public string Cpf { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}

public record LoginApiResponse(string Token, string Nome, string Perfil, DateTime ExpiraEm);
