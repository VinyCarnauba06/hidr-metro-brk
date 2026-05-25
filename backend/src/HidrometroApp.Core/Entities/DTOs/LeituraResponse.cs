namespace HidrometroApp.Core.Entities.DTOs;

public class UploadFotoRequest
{
    public int OsId { get; set; }
    public int UnidadeId { get; set; }
    public IFormFile Foto { get; set; } = null!;
}

public class LeituraResponse
{
    public int Id { get; set; }
    public int UnidadeId { get; set; }
    public string NumeroUnidade { get; set; } = string.Empty;
    public bool Sucesso { get; set; }
    public decimal? ValorM3 { get; set; }
    public int? ValorLitros { get; set; }
    public decimal? ConfiancaIa { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string QualidadeFoto { get; set; } = string.Empty;
    public bool SuspeitaVazamento { get; set; }
    public bool PermiteRecurso { get; set; }
    public string? Motivo { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class RecursoManualRequest
{
    public decimal ValorM3 { get; set; }
    public int ValorLitros { get; set; }
    public string? Observacao { get; set; }
}

public class ValidarLeituraRequest
{
    public decimal? ValorM3Corrigido { get; set; }
    public string? MotivoRejeicao { get; set; }
    public string? Observacao { get; set; }
}

public class ProgressoOsResponse
{
    public int OsId { get; set; }
    public int TotalUnidades { get; set; }
    public int LeiturasRegistradas { get; set; }
    public int LeiturasValidadas { get; set; }
    public int FaltandoRegistrar { get; set; }
    public decimal PercentualConcluido { get; set; }
    public List<UnidadePendente> UnidadesFaltando { get; set; } = new();
}

public class UnidadePendente
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
}
