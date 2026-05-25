using HidrometroApp.Core.Models;

namespace HidrometroApp.Core.Interfaces;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        int? usuarioId,
        string tabela,
        string acao,
        int? registroId,
        object? dadosAntes = null,
        object? dadosDepois = null,
        string? origem = null,
        string? motivo = null);

    Task<IEnumerable<Auditoria>> ListarAsync(
        string? tabela = null,
        string? acao = null,
        int? usuarioId = null,
        DateTime? de = null,
        DateTime? ate = null,
        int pagina = 1,
        int tamanhoPagina = 50);
}
