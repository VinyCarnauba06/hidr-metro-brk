namespace HidrometroApp.Core.Models;

public class LeituraHidrometro
{
    public int Id { get; set; }
    public int OsId { get; set; }
    public int UnidadeId { get; set; }

    // Foto
    public string? FotoPath { get; set; }

    // Valores
    public decimal? ValorM3 { get; set; }
    public int ValorLitros { get; set; } = 0;
    public decimal? ValorM3Validado { get; set; }

    // Origem e confiança
    public OrigemLeitura Origem { get; set; } = OrigemLeitura.Ia;
    public decimal? ConfiancaIa { get; set; }
    public int Tentativas { get; set; } = 0;

    // Status e flags
    public StatusLeitura Status { get; set; } = StatusLeitura.Pendente;
    public QualidadeFoto QualidadeFoto { get; set; } = QualidadeFoto.Ok;
    public bool SuspeitaVazamento { get; set; } = false;
    public bool RecomendacaoRevisao { get; set; } = false;

    // Observações
    public string? Observacao { get; set; }
    public string? MotivoRejeicao { get; set; }

    // Auditoria
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public int? CriadoPorId { get; set; }
    public int? ValidadoPorId { get; set; }
    public DateTime? ValidadoEm { get; set; }

    // Navegação
    public OrdemServico Os { get; set; } = null!;
    public Unidade Unidade { get; set; } = null!;
    public Usuario? CriadoPor { get; set; }
    public Usuario? ValidadoPor { get; set; }
}

public enum OrigemLeitura { Ia, Manual }

public enum StatusLeitura { Pendente, Validado, Rejeitado }

public enum QualidadeFoto
{
    Ok,
    BaixaConfianca,
    Manual,
    Rejeitado3x
}
