using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HidrometroApp.Api.Controllers;

[ApiController]
[Route("api/operador")]
[Authorize(Roles = "Operador,Admin")]
public class OperadorController : ControllerBase
{
    private readonly ILeituraService _leitura;
    private readonly IRelatorioService _relatorio;
    private readonly HidrometroDbContext _db;

    public OperadorController(ILeituraService leitura, IRelatorioService relatorio, HidrometroDbContext db)
    {
        _leitura = leitura;
        _relatorio = relatorio;
        _db = db;
    }

    [HttpGet("ordens-aguardando")]
    public async Task<IActionResult> OrdensAguardando()
    {
        var ordens = await _db.OrdensServico
            .Include(o => o.Condominio)
            .Where(o => o.Status == Core.Models.StatusOS.EmProgresso || o.Status == Core.Models.StatusOS.Validada)
            .Select(o => new
            {
                o.Id,
                o.Mes,
                o.Ano,
                Condominio = o.Condominio.Nome,
                Status = o.Status.ToString(),
                TotalLeituras = o.Leituras.Count,
                LeiturasValidadas = o.Leituras.Count(l => l.Status == Core.Models.StatusLeitura.Validado),
                LeiturasPendentes = o.Leituras.Count(l => l.Status == Core.Models.StatusLeitura.Pendente),
                SuspeitasVazamento = o.Leituras.Count(l => l.SuspeitaVazamento)
            })
            .OrderByDescending(o => o.SuspeitasVazamento)
            .ThenBy(o => o.Condominio)
            .ToListAsync();

        return Ok(ordens);
    }

    [HttpGet("ordens/{osId}/leituras")]
    public async Task<IActionResult> ListarLeituras(int osId)
    {
        try
        {
            return Ok(await _leitura.ListarPorOsAsync(osId));
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("leituras/{id}/foto")]
    public async Task<IActionResult> ObterFoto(int id)
    {
        var foto = await _leitura.ObterFotoAsync(id);
        if (foto == null) return NotFound(new { message = "Foto não encontrada" });
        return File(foto, "image/jpeg");
    }

    [HttpPatch("leituras/{id}/validar")]
    public async Task<IActionResult> Validar(int id, [FromBody] ValidarLeituraRequest request)
    {
        try
        {
            return Ok(await _leitura.ValidarAsync(id, ObterUsuarioId(), request));
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (LeituraInvalidaException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPatch("leituras/{id}/corrigir")]
    public async Task<IActionResult> Corrigir(int id, [FromBody] ValidarLeituraRequest request)
    {
        try
        {
            return Ok(await _leitura.CorrigirAsync(id, ObterUsuarioId(), request));
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (LeituraInvalidaException ex) { return UnprocessableEntity(new { message = ex.Message }); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("leituras/{id}/rejeitar")]
    public async Task<IActionResult> Rejeitar(int id, [FromBody] ValidarLeituraRequest request)
    {
        try
        {
            return Ok(await _leitura.RejeitarAsync(id, ObterUsuarioId(), request));
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("os/{osId}/progresso")]
    public async Task<IActionResult> Progresso(int osId)
    {
        try
        {
            return Ok(await _leitura.ObterProgressoAsync(osId));
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("relatorio/{osId}/excel")]
    public async Task<IActionResult> GerarExcel(int osId)
    {
        try
        {
            var bytes = await _relatorio.GerarExcelAsync(osId);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"leituras_os{osId}_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("relatorio/{osId}/pdf")]
    public async Task<IActionResult> GerarPdf(int osId)
    {
        try
        {
            var bytes = await _relatorio.GerarPdfAsync(osId);
            return File(bytes, "application/pdf", $"leituras_os{osId}_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private int ObterUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
