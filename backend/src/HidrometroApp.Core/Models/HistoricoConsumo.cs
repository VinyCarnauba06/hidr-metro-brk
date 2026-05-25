namespace HidrometroApp.Core.Models;

public class HistoricoConsumo
{
    public int Id { get; set; }
    public int UnidadeId { get; set; }
    public int? OsId { get; set; }

    public decimal? ConsumoM3 { get; set; }
    public decimal? LeituraAnterior { get; set; }
    public decimal? LeituraAtual { get; set; }

    public int Mes { get; set; }
    public int Ano { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Unidade Unidade { get; set; } = null!;
    public OrdemServico? Os { get; set; }
}
