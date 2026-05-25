using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HidrometroApp.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly HidrometroDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AdminController(HidrometroDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var hoje = DateTime.Today;
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);

        var stats = new
        {
            TotalCondominios = await _db.Condominios.CountAsync(),
            TotalUnidades = await _db.Unidades.CountAsync(u => u.Ativa),
            TotalUsuarios = await _db.Usuarios.CountAsync(u => u.Ativo),
            OrdensNoMes = await _db.OrdensServico.CountAsync(o => o.CriadoEm >= inicioMes),
            LeiturasNoMes = await _db.LeiturasHidrometro.CountAsync(l => l.CriadoEm >= inicioMes),
            LeiturasIa = await _db.LeiturasHidrometro.CountAsync(l => l.CriadoEm >= inicioMes && l.Origem == OrigemLeitura.Ia),
            LeiturasManual = await _db.LeiturasHidrometro.CountAsync(l => l.CriadoEm >= inicioMes && l.Origem == OrigemLeitura.Manual),
            SuspeitasVazamento = await _db.LeiturasHidrometro.CountAsync(l => l.CriadoEm >= inicioMes && l.SuspeitaVazamento),
            OrdensAbertas = await _db.OrdensServico.CountAsync(o => o.Status == StatusOS.Aberta || o.Status == StatusOS.EmProgresso),
        };

        return Ok(stats);
    }

    [HttpGet("auditoria")]
    public async Task<IActionResult> Auditoria(
        [FromQuery] string? tabela,
        [FromQuery] string? acao,
        [FromQuery] int? usuarioId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1)
    {
        var registros = await _auditoria.ListarAsync(tabela, acao, usuarioId, de, ate, pagina);
        return Ok(registros.Select(a => new
        {
            a.Id,
            a.Tabela,
            a.Acao,
            a.RegistroId,
            Usuario = a.Usuario?.Nome,
            a.Origem,
            a.Motivo,
            a.CriadoEm
        }));
    }

    [HttpPost("condominios")]
    public async Task<IActionResult> CriarCondominio([FromBody] CriarCondominioRequest request)
    {
        var condo = new Condominio
        {
            Nome = request.Nome,
            Endereco = request.Endereco,
            QtdUnidades = request.QtdUnidades,
            TipoMedidor = Enum.Parse<TipoMedidor>(request.TipoMedidor, ignoreCase: true)
        };
        _db.Condominios.Add(condo);
        await _db.SaveChangesAsync();

        for (int i = 1; i <= request.QtdUnidades; i++)
        {
            _db.Unidades.Add(new Unidade
            {
                CondominioId = condo.Id,
                Numero = i.ToString().PadLeft(3, '0'),
                Ativa = true
            });
        }
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(CriarCondominio), new { id = condo.Id }, condo);
    }

    [HttpPost("ordens")]
    public async Task<IActionResult> CriarOrdem([FromBody] CriarOrdemRequest request)
    {
        var existe = await _db.OrdensServico.AnyAsync(o =>
            o.CondominioId == request.CondominioId && o.Mes == request.Mes && o.Ano == request.Ano);

        if (existe) return Conflict(new { message = "Já existe uma OS para este condomínio nesta competência" });

        var os = new OrdemServico
        {
            CondominioId = request.CondominioId,
            Mes = request.Mes,
            Ano = request.Ano,
            Status = StatusOS.Aberta
        };
        _db.OrdensServico.Add(os);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(CriarOrdem), new { id = os.Id }, os);
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> Usuarios()
    {
        return Ok(await _db.Usuarios
            .Where(u => u.Ativo)
            .Select(u => new { u.Id, u.Nome, u.Cpf, Perfil = u.Perfil.ToString(), u.CriadoEm })
            .ToListAsync());
    }

    [HttpPost("usuarios")]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioRequest request)
    {
        if (await _db.Usuarios.AnyAsync(u => u.Cpf == request.Cpf))
            return Conflict(new { message = "CPF já cadastrado" });

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Cpf = request.Cpf.Replace(".", "").Replace("-", ""),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
            Perfil = Enum.Parse<PerfilUsuario>(request.Perfil, ignoreCase: true),
            Ativo = true
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            ObterUsuarioId(), "usuarios", "INSERT", usuario.Id,
            dadosDepois: new { usuario.Nome, usuario.Perfil },
            origem: "web_admin");

        return CreatedAtAction(nameof(CriarUsuario), new { id = usuario.Id }, new { usuario.Id, usuario.Nome, usuario.Perfil });
    }

    private int ObterUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }
}

public record CriarCondominioRequest(string Nome, string? Endereco, int QtdUnidades, string TipoMedidor);
public record CriarOrdemRequest(int CondominioId, int Mes, int Ano);
public record CriarUsuarioRequest(string Nome, string Cpf, string Senha, string Perfil);
