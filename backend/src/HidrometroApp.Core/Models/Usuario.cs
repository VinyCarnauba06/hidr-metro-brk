namespace HidrometroApp.Core.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<OrdemServico> OrdensServico { get; set; } = new List<OrdemServico>();
    public ICollection<LeituraHidrometro> LeiturasRegistradas { get; set; } = new List<LeituraHidrometro>();
    public ICollection<LeituraHidrometro> LeiturasValidadas { get; set; } = new List<LeituraHidrometro>();
}

public enum PerfilUsuario
{
    Fiscal,
    Operador,
    Admin
}
