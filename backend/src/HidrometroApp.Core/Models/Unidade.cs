namespace HidrometroApp.Core.Models;

public class Unidade
{
    public int Id { get; set; }
    public int CondominioId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public bool Ativa { get; set; } = true;

    public Condominio Condominio { get; set; } = null!;
    public ICollection<LeituraHidrometro> Leituras { get; set; } = new List<LeituraHidrometro>();
    public ICollection<HistoricoConsumo> HistoricoConsumo { get; set; } = new List<HistoricoConsumo>();
    public ICollection<HistoricoTrocaHidrometro> HistoricoTrocas { get; set; } = new List<HistoricoTrocaHidrometro>();
}
