namespace HidrometroApp.Core.Entities.DTOs;

public class RelatorioItemResponse
{
    public string Unidade { get; set; } = string.Empty;
    public decimal? LeituraAnterior { get; set; }
    public decimal LeituraAtual { get; set; }
    public decimal Consumo { get; set; }
    public string Origem { get; set; } = string.Empty;
    public bool SuspeitaVazamento { get; set; }
    public string? Observacao { get; set; }
}

public class RelatorioOsResponse
{
    public int OsId { get; set; }
    public string Condominio { get; set; } = string.Empty;
    public int Mes { get; set; }
    public int Ano { get; set; }
    public int TotalUnidades { get; set; }
    public List<RelatorioItemResponse> Itens { get; set; } = new();
    public DateTime GeradoEm { get; set; } = DateTime.UtcNow;
}
