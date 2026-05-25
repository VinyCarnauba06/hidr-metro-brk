namespace HidrometroApp.Core.Models;

public class HistoricoTrocaHidrometro
{
    public int Id { get; set; }
    public int UnidadeId { get; set; }
    public DateOnly? DataTroca { get; set; }
    public string? NumeroSerieAnterior { get; set; }
    public string? NumeroSerieNovo { get; set; }
    public string? Motivo { get; set; }
    public int? CriadoPorId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Unidade Unidade { get; set; } = null!;
    public Usuario? CriadoPor { get; set; }
}
