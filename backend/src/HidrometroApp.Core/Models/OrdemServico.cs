namespace HidrometroApp.Core.Models;

public class OrdemServico
{
    public int Id { get; set; }
    public int CondominioId { get; set; }
    public int? FiscalId { get; set; }
    public int Mes { get; set; }
    public int Ano { get; set; }
    public DateTime DataInicio { get; set; } = DateTime.UtcNow;
    public DateTime? DataConclusao { get; set; }
    // Data limite pro fiscal visitar o condomínio — não é obrigatório ir exatamente
    // nesse dia (pode ir antes), o problema é ultrapassar essa data sem ter ido.
    public DateTime? DataLimite { get; set; }
    public StatusOS Status { get; set; } = StatusOS.Aberta;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Condominio Condominio { get; set; } = null!;
    public Usuario? Fiscal { get; set; }
    public ICollection<LeituraHidrometro> Leituras { get; set; } = new List<LeituraHidrometro>();
}

public enum StatusOS
{
    Aberta,
    EmProgresso,
    Validada,
    Finalizada
}
