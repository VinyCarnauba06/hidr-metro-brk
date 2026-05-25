using System.Text.Json;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HidrometroApp.Core.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly HidrometroApp.Infrastructure.Data.HidrometroDbContext _db;

    public AuditoriaService(HidrometroApp.Infrastructure.Data.HidrometroDbContext db)
    {
        _db = db;
    }

    public async Task RegistrarAsync(
        int? usuarioId,
        string tabela,
        string acao,
        int? registroId,
        object? dadosAntes = null,
        object? dadosDepois = null,
        string? origem = null,
        string? motivo = null)
    {
        var audit = new Auditoria
        {
            UsuarioId = usuarioId,
            Tabela = tabela,
            Acao = acao,
            RegistroId = registroId,
            DadosAntes = dadosAntes != null ? JsonSerializer.Serialize(dadosAntes) : null,
            DadosDepois = dadosDepois != null ? JsonSerializer.Serialize(dadosDepois) : null,
            Origem = origem,
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow
        };

        _db.Auditorias.Add(audit);
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Auditoria>> ListarAsync(
        string? tabela = null,
        string? acao = null,
        int? usuarioId = null,
        DateTime? de = null,
        DateTime? ate = null,
        int pagina = 1,
        int tamanhoPagina = 50)
    {
        var query = _db.Auditorias
            .Include(a => a.Usuario)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(tabela))
            query = query.Where(a => a.Tabela == tabela);
        if (!string.IsNullOrEmpty(acao))
            query = query.Where(a => a.Acao == acao);
        if (usuarioId.HasValue)
            query = query.Where(a => a.UsuarioId == usuarioId);
        if (de.HasValue)
            query = query.Where(a => a.CriadoEm >= de.Value);
        if (ate.HasValue)
            query = query.Where(a => a.CriadoEm <= ate.Value);

        return await query
            .OrderByDescending(a => a.CriadoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();
    }
}
