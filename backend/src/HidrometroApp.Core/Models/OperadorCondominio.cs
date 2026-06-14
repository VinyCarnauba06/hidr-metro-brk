namespace HidrometroApp.Core.Models;

public class OperadorCondominio
{
    public int Id { get; set; }
    public int OperadorId { get; set; }
    public int CondominioId { get; set; }

    public Usuario Operador { get; set; } = null!;
    public Condominio Condominio { get; set; } = null!;
}
