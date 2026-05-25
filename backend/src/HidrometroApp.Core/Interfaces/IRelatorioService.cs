using HidrometroApp.Core.Entities.DTOs;

namespace HidrometroApp.Core.Interfaces;

public interface IRelatorioService
{
    Task<byte[]> GerarExcelAsync(int osId);
    Task<byte[]> GerarPdfAsync(int osId);
    Task<RelatorioOsResponse> ObterDadosRelatorioAsync(int osId);
}
