namespace HidrometroApp.Core.Models;

public class Condominio
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public int QtdUnidades { get; set; }
    public TipoMedidor TipoMedidor { get; set; } = TipoMedidor.AguaFria;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Unidade> Unidades { get; set; } = new List<Unidade>();
    public ICollection<OrdemServico> OrdensServico { get; set; } = new List<OrdemServico>();
    public ICollection<OperadorCondominio> Operadores { get; set; } = new List<OperadorCondominio>();
}

public enum TipoMedidor
{
    Gas,
    AguaFria,
    AguaQuente,
    AguaQuenteEFria
}
