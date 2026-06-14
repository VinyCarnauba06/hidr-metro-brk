using HidrometroApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HidrometroApp.Infrastructure.Data;

public class HidrometroDbContext : DbContext
{
    public HidrometroDbContext(DbContextOptions<HidrometroDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Condominio> Condominios => Set<Condominio>();
    public DbSet<Unidade> Unidades => Set<Unidade>();
    public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();
    public DbSet<LeituraHidrometro> LeiturasHidrometro => Set<LeituraHidrometro>();
    public DbSet<HistoricoConsumo> HistoricoConsumo => Set<HistoricoConsumo>();
    public DbSet<HistoricoTrocaHidrometro> HistoricoTrocaHidrometro => Set<HistoricoTrocaHidrometro>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<OperadorCondominio> OperadorCondominios => Set<OperadorCondominio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired().HasMaxLength(255);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Nome).IsRequired().HasMaxLength(255);
            e.Property(x => x.SenhaHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.Perfil).HasConversion<string>();
        });

        modelBuilder.Entity<Condominio>(e =>
        {
            e.ToTable("condominios");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(255);
            e.Property(x => x.Endereco).HasMaxLength(500);
            e.Property(x => x.TipoMedidor).HasConversion<string>();
        });

        modelBuilder.Entity<Unidade>(e =>
        {
            e.ToTable("unidades");
            e.HasKey(x => x.Id);
            e.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            e.HasIndex(x => new { x.CondominioId, x.Numero }).IsUnique();
            e.HasOne(x => x.Condominio).WithMany(x => x.Unidades)
                .HasForeignKey(x => x.CondominioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrdemServico>(e =>
        {
            e.ToTable("ordens_servico");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CondominioId, x.Mes, x.Ano }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Condominio).WithMany(x => x.OrdensServico)
                .HasForeignKey(x => x.CondominioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Fiscal).WithMany(x => x.OrdensServico)
                .HasForeignKey(x => x.FiscalId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeituraHidrometro>(e =>
        {
            e.ToTable("leituras_hidrometro");
            e.HasKey(x => x.Id);
            e.Property(x => x.Origem).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.QualidadeFoto).HasConversion<string>();
            e.Property(x => x.FotoPath).HasMaxLength(500);
            e.Property(x => x.ValorM3).HasPrecision(8, 2);
            e.Property(x => x.ValorM3Validado).HasPrecision(8, 2);
            e.Property(x => x.ConfiancaIa).HasPrecision(3, 2);
            e.HasOne(x => x.Os).WithMany(x => x.Leituras)
                .HasForeignKey(x => x.OsId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Unidade).WithMany(x => x.Leituras)
                .HasForeignKey(x => x.UnidadeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CriadoPor).WithMany(x => x.LeiturasRegistradas)
                .HasForeignKey(x => x.CriadoPorId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ValidadoPor).WithMany(x => x.LeiturasValidadas)
                .HasForeignKey(x => x.ValidadoPorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HistoricoConsumo>(e =>
        {
            e.ToTable("historico_consumo");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConsumoM3).HasPrecision(8, 2);
            e.Property(x => x.LeituraAnterior).HasPrecision(8, 2);
            e.Property(x => x.LeituraAtual).HasPrecision(8, 2);
            e.HasOne(x => x.Unidade).WithMany(x => x.HistoricoConsumo)
                .HasForeignKey(x => x.UnidadeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HistoricoTrocaHidrometro>(e =>
        {
            e.ToTable("historico_troca_hidrometro");
            e.HasKey(x => x.Id);
            e.Property(x => x.NumeroSerieAnterior).HasMaxLength(50);
            e.Property(x => x.NumeroSerieNovo).HasMaxLength(50);
            e.HasOne(x => x.Unidade).WithMany(x => x.HistoricoTrocas)
                .HasForeignKey(x => x.UnidadeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CriadoPor).WithMany()
                .HasForeignKey(x => x.CriadoPorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OperadorCondominio>(e =>
        {
            e.ToTable("operador_condominios");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OperadorId, x.CondominioId }).IsUnique();
            e.HasOne(x => x.Operador).WithMany(x => x.CondominiosAtribuidos)
                .HasForeignKey(x => x.OperadorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Condominio).WithMany(x => x.Operadores)
                .HasForeignKey(x => x.CondominioId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Auditoria>(e =>
        {
            e.ToTable("auditoria");
            e.HasKey(x => x.Id);
            e.Property(x => x.Acao).IsRequired().HasMaxLength(50);
            e.Property(x => x.Tabela).HasMaxLength(100);
            e.Property(x => x.Origem).HasMaxLength(50);
            e.Property(x => x.Motivo).HasMaxLength(255);
            e.HasOne(x => x.Usuario).WithMany()
                .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
