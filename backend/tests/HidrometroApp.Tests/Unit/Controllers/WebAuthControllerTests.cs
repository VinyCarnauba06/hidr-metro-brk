// backend/tests/HidrometroApp.Tests/Unit/Controllers/WebAuthControllerTests.cs
// Testes unitários dos controllers MVC (Web): login com redirecionamento por perfil e autorização por role.
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;
using HidrometroApp.Web.Controllers;

namespace HidrometroApp.Tests.Unit.Controllers;

/// <summary>
/// Mock HttpMessageHandler que retorna respostas configuráveis.
/// Permite simular a API REST para testes do controller MVC sem necessidade de um servidor real.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly HttpContent? _content;

    public MockHttpMessageHandler(HttpStatusCode statusCode, HttpContent? content = null)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage
        {
            StatusCode = _statusCode,
            Content = _content
        });
    }
}

/// <summary>
/// Fábrica de HttpClient com resposta mockada para uso com IHttpClientFactory.
/// </summary>
public static class MockHttpClientFactoryHelper
{
    public static Mock<IHttpClientFactory> CriarFactory(HttpStatusCode status, object? body = null)
    {
        HttpContent? content = null;
        if (body != null)
        {
            content = JsonContent.Create(body);
        }

        var handler = new MockHttpMessageHandler(status, content);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };

        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient("api")).Returns(httpClient);
        return mock;
    }
}

public class WebAuthControllerTests
{
    /// <summary>
    /// Cria um ControllerContext com HttpContext mockado para suportar:
    /// - SignInAsync (cookie authentication)
    /// - TempData
    /// - Url helper
    /// </summary>
    private static ControllerContext CriarControllerContext()
    {
        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock.Setup(s => s.SignInAsync(
            It.IsAny<HttpContext>(),
            It.IsAny<string>(),
            It.IsAny<ClaimsPrincipal>(),
            It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/mock");

        var urlHelperFactoryMock = new Mock<IUrlHelperFactory>();
        urlHelperFactoryMock
            .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
            .Returns(urlHelperMock.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IAuthenticationService)))
            .Returns(authServiceMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IUrlHelperFactory)))
            .Returns(urlHelperFactoryMock.Object);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProviderMock.Object
        };

        return new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
    }

    private static AuthController CriarController(Mock<IHttpClientFactory> httpFactory)
    {
        var controller = new AuthController(httpFactory.Object)
        {
            ControllerContext = CriarControllerContext()
        };

        // TempData precisa de provider mockado
        controller.TempData = new TempDataDictionary(
            controller.HttpContext,
            Mock.Of<ITempDataProvider>());

        return controller;
    }

    // ─── ITEM 10: Login Operador redireciona para Operador/Index ───

    [Fact]
    public async Task Login_Operador_RedirecionaPara_Operador_Index()
    {
        // Arrange
        var httpFactory = MockHttpClientFactoryHelper.CriarFactory(HttpStatusCode.OK, new
        {
            Token = "jwt-operador",
            Nome = "Operador Teste",
            Perfil = "Operador",
            ExpiraEm = DateTime.UtcNow.AddHours(8)
        });

        var controller = CriarController(httpFactory);
        var model = new LoginViewModel { Email = "operador@prolar.com", Senha = "Operador@123" };

        // Act
        var result = await controller.Login(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Operador", redirect.ControllerName);
    }

    // ─── ITEM 11: Login Admin redireciona para Admin/Dashboard ───

    [Fact]
    public async Task Login_Admin_RedirecionaPara_Admin_Dashboard()
    {
        // Arrange
        var httpFactory = MockHttpClientFactoryHelper.CriarFactory(HttpStatusCode.OK, new
        {
            Token = "jwt-admin",
            Nome = "Admin Teste",
            Perfil = "Admin",
            ExpiraEm = DateTime.UtcNow.AddHours(8)
        });

        var controller = CriarController(httpFactory);
        var model = new LoginViewModel { Email = "admin@prolar.com", Senha = "Admin@123" };

        // Act
        var result = await controller.Login(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
    }

    // ─── Teste extra: Login Fiscal redireciona para Operador/Index ───

    [Fact]
    public async Task Login_Fiscal_RedirecionaPara_Operador_Index()
    {
        // Arrange — a API retorna perfil "Fiscal", o web AuthController redireciona para Operador/Index
        var httpFactory = MockHttpClientFactoryHelper.CriarFactory(HttpStatusCode.OK, new
        {
            Token = "jwt-fiscal",
            Nome = "Fiscal Teste",
            Perfil = "Fiscal",
            ExpiraEm = DateTime.UtcNow.AddHours(8)
        });

        var controller = CriarController(httpFactory);
        var model = new LoginViewModel { Email = "fiscal@prolar.com", Senha = "Fiscal@123" };

        // Act
        var result = await controller.Login(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Operador", redirect.ControllerName);
    }

    // ─── Login com credenciais inválidas retorna View com erro ───

    [Fact]
    public async Task Login_CredenciaisInvalidas_RetornaView_ComModelError()
    {
        // Arrange — API retorna 401
        var httpFactory = MockHttpClientFactoryHelper.CriarFactory(HttpStatusCode.Unauthorized);
        var controller = CriarController(httpFactory);
        var model = new LoginViewModel { Email = "invalido@teste.com", Senha = "SenhaErrada" };

        // Act
        var result = await controller.Login(model);

        // Assert — retorna View (não redireciona)
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values,
            v => v.Errors.Any(e => e.ErrorMessage.Contains("Email ou senha inválidos")));
    }

    // ─── Login GET quando já autenticado redireciona para Home/Index ───

    [Fact]
    public void Login_Get_UsuarioAutenticado_RedirecionaParaHome()
    {
        // Arrange — usuário já autenticado
        var httpFactory = MockHttpClientFactoryHelper.CriarFactory(HttpStatusCode.OK);
        var controller = CriarController(httpFactory);

        // Simula usuário autenticado
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Admin Teste"),
            new Claim(ClaimTypes.Role, "Admin"),
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        controller.HttpContext.User = new ClaimsPrincipal(identity);

        // Act
        var result = controller.Login();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }
}

// ─── ITEM 12: Acesso a rota de Operador por Fiscal → 403 ───
// Este teste verifica que o atributo [Authorize(Roles = "Operador,Admin")]
// está corretamente aplicado no OperadorController, impedindo acesso por Fiscal.

public class WebOperadorControllerAuthTests
{
    [Fact]
    public void OperadorController_TemAuthorizeAttribute_ComRoles_OperadorAdmin()
    {
        // Assert — verifica que o controller tem [Authorize(Roles = "Operador,Admin")]
        var controllerType = typeof(OperadorController);
        var authAttr = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);
        Assert.Contains("Operador", authAttr.Roles);
        Assert.Contains("Admin", authAttr.Roles);
        Assert.DoesNotContain("Fiscal", authAttr.Roles);
    }

    [Fact]
    public void AdminController_TemAuthorizeAttribute_ComRoles_Admin()
    {
        // Assert — verifica que o controller tem [Authorize(Roles = "Admin")]
        var controllerType = typeof(AdminController);
        var authAttr = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);
        Assert.Contains("Admin", authAttr.Roles);
        Assert.DoesNotContain("Operador", authAttr.Roles);
        Assert.DoesNotContain("Fiscal", authAttr.Roles);
    }

    [Fact]
    public void AuthController_Login_TemAllowAnonymous()
    {
        // Assert — verifica que os métodos Login (GET e POST) têm [AllowAnonymous]
        var controllerType = typeof(AuthController);

        var loginGet = controllerType.GetMethod("Login", Type.EmptyTypes);
        Assert.NotNull(loginGet);
        var anonGet = loginGet!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
        Assert.NotEmpty(anonGet);

        var loginPost = controllerType.GetMethod("Login", new[] { typeof(LoginViewModel) });
        Assert.NotNull(loginPost);
        var anonPost = loginPost!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
        Assert.NotEmpty(anonPost);
    }
}
