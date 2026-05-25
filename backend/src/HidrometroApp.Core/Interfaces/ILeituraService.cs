using HidrometroApp.Core.Entities.DTOs;

namespace HidrometroApp.Core.Interfaces;

public interface ILeituraService
{
    Task<LeituraResponse> UploadFotoAsync(int osId, int unidadeId, int fiscalId, byte[] fotoBytes, string nomeArquivo);
    Task<LeituraResponse> RegistrarManualAsync(int leituraId, int fiscalId, RecursoManualRequest request);
    Task<LeituraResponse> ValidarAsync(int leituraId, int operadorId, ValidarLeituraRequest request);
    Task<LeituraResponse> CorrigirAsync(int leituraId, int operadorId, ValidarLeituraRequest request);
    Task<LeituraResponse> RejeitarAsync(int leituraId, int operadorId, ValidarLeituraRequest request);
    Task<ProgressoOsResponse> ObterProgressoAsync(int osId);
    Task<IEnumerable<LeituraResponse>> ListarPorOsAsync(int osId);
    Task<byte[]?> ObterFotoAsync(int leituraId);
}
