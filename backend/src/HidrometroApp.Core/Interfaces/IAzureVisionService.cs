namespace HidrometroApp.Core.Interfaces;

public class LeituraResultadoIa
{
    public bool Sucesso { get; set; }
    public decimal? HidrometroM3 { get; set; }
    public int? Litros { get; set; }
    public decimal Confianca { get; set; }
    public bool PermiteRecurso { get; set; }
    public string? Motivo { get; set; }
}

public interface IAzureVisionService
{
    Task<LeituraResultadoIa> AnalisarFotoAsync(byte[] fotoBytes);
    bool ValidarQualidadeFoto(byte[] fotoBytes);
}
