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
[Route("api/fiscal")]
[Authorize(Roles = "Fiscal,Admin")]
public class FiscalController : ControllerBase
{
    private readonly ILeituraService _leitura;
    private readonly HidrometroDbContext _db;

    public FiscalController(ILeituraService leitura, HidrometroDbContext db)
    {
        _leitura = leitura;
        _db = db;
    }

    [HttpGet("ordens-abertas")]
    public async Task<IActionResult> OrdensAbertas()
    {
        var fiscalId = ObterFiscalId();
        var ordens = await _db.OrdensServico
            .Include(o => o.Condominio)
            .Where(o => o.Status == Core.Models.StatusOS.Aberta || o.Status == Core.Models.StatusOS.EmProgresso)
            .Select(o => new
            {
                o.Id,
                o.Mes,
                o.Ano,
                Condominio = o.Condominio.Nome,
                Endereco = o.Condominio.Endereco,
                TotalUnidades = o.Condominio.QtdUnidades,
                Status = o.Status.ToString(),
                o.DataInicio,
                o.DataLimite
            })
            .OrderBy(o => o.Condominio)
            .ToListAsync();

        return Ok(ordens);
    }

    [HttpGet("os/{osId}")]
    public async Task<IActionResult> DetalhesOs(int osId)
    {
        var os = await _db.OrdensServico
            .Include(o => o.Condominio)
            .ThenInclude(c => c.Unidades.Where(u => u.Ativa))
            .FirstOrDefaultAsync(o => o.Id == osId);

        if (os == null) return NotFound();

        return Ok(new
        {
            os.Id,
            os.Mes,
            os.Ano,
            Condominio = os.Condominio.Nome,
            Endereco = os.Condominio.Endereco,
            Status = os.Status.ToString(),
            Unidades = os.Condominio.Unidades.Select(u => new { u.Id, u.Numero, u.Tipo })
        });
    }

    [HttpPost("leitura/upload")]
    public async Task<IActionResult> UploadFoto([FromForm] int osId, [FromForm] int unidadeId, IFormFile foto)
    {
        if (!Request.HasFormContentType ||
            Request.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true)
            return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                new { message = "Content-Type deve ser multipart/form-data" });

        if (foto == null || foto.Length == 0)
            return BadRequest(new { message = "Foto obrigatória" });

        try
        {
            using var ms = new MemoryStream();
            await foto.CopyToAsync(ms);

            var resultado = await _leitura.UploadFotoAsync(
                osId, unidadeId, ObterFiscalId(), ms.ToArray(), foto.FileName);

            return Ok(resultado);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (FotoRejeitadaException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPost("leitura/{id}/recurso")]
    public async Task<IActionResult> RecursoManual(int id, [FromBody] RecursoManualRequest request)
    {
        try
        {
            var resultado = await _leitura.RegistrarManualAsync(id, ObterFiscalId(), request);
            return Ok(resultado);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (LeituraInvalidaException ex) { return UnprocessableEntity(new { message = ex.Message }); }
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

    [HttpGet("os/{osId}/faltando")]
    public async Task<IActionResult> Faltando(int osId)
    {
        try
        {
            var progresso = await _leitura.ObterProgressoAsync(osId);
            return Ok(progresso.UnidadesFaltando);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private int ObterFiscalId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
