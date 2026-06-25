// backend/tests/HidrometroApp.Tests/Unit/Services/LeituraServiceTests.cs
using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Data;
using HidrometroApp.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace HidrometroApp.Tests.Unit.Services;

public class LeituraServiceTests
{
    private HidrometroDbContext CriarDb() =>
        new(new DbContextOptionsBuilder<HidrometroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private LeituraService CriarServico(HidrometroDbContext db,
        IGeminiVisionService? vision = null,
        IAuditoriaService? auditoria = null,
        IFotoStorage? storage = null)
    {
        vision ??= new Mock<IGeminiVisionService>().Object;
        auditoria ??= new Mock<IAuditoriaService>().Object;
        storage ??= CriarStorageTemp();
        return new LeituraService(db, vision, auditoria, new AnomaliaService(db), storage,
            NullLogger<LeituraService>.Instance);
    }

    private static IFotoStorage CriarStorageTemp()
    {
        var cfg = new Mock<IConfiguration>();
        cfg.Setup(c => c["STORAGE_PATH"]).Returns(Path.Combine(Path.GetTempPath(), "hidro_tests"));
        return new LocalFotoStorage(cfg.Object, NullLogger<LocalFotoStorage>.Instance);
    }

    private static Mock<IGeminiVisionService> MockGemini(decimal confianca, bool sucesso = true,
        decimal m3 = 1234m, int litros = 567)
    {
        var mock = new Mock<IGeminiVisionService>();
        mock.Setup(v => v.ValidarQualidadeFoto(It.IsAny<byte[]>())).Returns(true);
        mock.Setup(v => v.AnalisarFotoAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new LeituraResultadoIa
            {
                Sucesso        = sucesso,
                Confianca      = confianca,
                HidrometroM3   = sucesso ? m3 : null,
                Litros         = sucesso ? litros : null,
                PermiteRecurso = confianca >= 0.40m,
                Motivo         = sucesso ? null : "Baixa confiança simulada",
            });
        return mock;
    }

    private async Task<(HidrometroDbContext db, Condominio condo, Unidade unidade, OrdemServico os)> CriarBaseAsync()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Condo Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        var os = new OrdemServico
        {
            CondominioId = condo.Id,
            Mes = DateTime.Now.Month,
            Ano = DateTime.Now.Year,
            Status = StatusOS.Aberta
        };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        return (db, condo, unidade, os);
    }

    [Fact]
    public async Task UploadFoto_LimiteAtingido_LancaFotoRejeitada()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        for (int i = 0; i < 3; i++)
        {
            db.LeiturasHidrometro.Add(new LeituraHidrometro
            {
                OsId = os.Id,
                UnidadeId = unidade.Id,
                Status = StatusLeitura.Rejeitado
            });
        }
        await db.SaveChangesAsync();

        var svc = CriarServico(db);

        await Assert.ThrowsAsync<FotoRejeitadaException>(() =>
            svc.UploadFotoAsync(os.Id, unidade.Id, 1, Array.Empty<byte>(), "foto.jpg"));
    }

    [Fact]
    public async Task RegistrarManual_LeituraInvalida_LancaException()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        var leitura = new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Rejeitado
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();

        var svc = CriarServico(db);

        await Assert.ThrowsAsync<LeituraInvalidaException>(() =>
            svc.RegistrarManualAsync(leitura.Id, 1, new RecursoManualRequest { ValorM3 = -5m }));
    }

    [Fact]
    public async Task ValidarLeitura_AtualizaStatus_ERegistraHistorico()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        var leitura = new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Pendente,
            ValorM3 = 100m
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();

        var mockAuditoria = new Mock<IAuditoriaService>();
        var svc = CriarServico(db, auditoria: mockAuditoria.Object);

        var result = await svc.ValidarAsync(leitura.Id, 1, new ValidarLeituraRequest());

        Assert.Equal("Validado", result.Status);

        var historico = await db.HistoricoConsumo.Where(h => h.OsId == os.Id).ToListAsync();
        Assert.Single(historico);
    }

    [Fact]
    public async Task RejeitarLeitura_MotivoVazio_LancaValidation()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        var leitura = new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Pendente
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();

        var svc = CriarServico(db);

        await Assert.ThrowsAsync<HidrometroValidationException>(() =>
            svc.RejeitarAsync(leitura.Id, 1, new ValidarLeituraRequest { MotivoRejeicao = "" }));
    }

    [Fact]
    public async Task ObterProgresso_OsCompleta_RetornaPercentual100()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Condo", QtdUnidades = 2 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var u1 = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        var u2 = new Unidade { CondominioId = condo.Id, Numero = "102", Ativa = true };
        db.Unidades.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var os = new OrdemServico { CondominioId = condo.Id, Mes = DateTime.Now.Month, Ano = DateTime.Now.Year };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        db.LeiturasHidrometro.AddRange(
            new LeituraHidrometro { OsId = os.Id, UnidadeId = u1.Id, Status = StatusLeitura.Validado, ValorM3Validado = 100m },
            new LeituraHidrometro { OsId = os.Id, UnidadeId = u2.Id, Status = StatusLeitura.Validado, ValorM3Validado = 200m }
        );
        await db.SaveChangesAsync();

        var svc = CriarServico(db);
        var progresso = await svc.ObterProgressoAsync(os.Id);

        Assert.Equal(100m, progresso.PercentualConcluido);
        Assert.Equal(0, progresso.FaltandoRegistrar);
    }

    // ── Confidence threshold — 3 níveis ──────────────────────────────────────

    [Fact]
    public async Task UploadFoto_ConfiancaAlta_RetornaStatusAceita()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();
        var svc = CriarServico(db, MockGemini(confianca: 0.92m).Object);

        var result = await svc.UploadFotoAsync(os.Id, unidade.Id, 1, Array.Empty<byte>(), "foto.jpg");

        Assert.Equal("Aceita", result.Status);
        Assert.True(result.Sucesso);
        Assert.False(result.PrioridadeOperador ?? false);
    }

    [Fact]
    public async Task UploadFoto_ConfiancaMedia_RetornaStatusAguardandoRevisao()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();
        var svc = CriarServico(db, MockGemini(confianca: 0.65m).Object);

        var result = await svc.UploadFotoAsync(os.Id, unidade.Id, 1, Array.Empty<byte>(), "foto.jpg");

        Assert.Equal("AguardandoRevisao", result.Status);
        Assert.True(result.Sucesso);
        Assert.True(result.PrioridadeOperador);
    }

    [Fact]
    public async Task UploadFoto_ConfiancaBaixa_LancaFotoRejeitada_EGravaStatusRejeitada()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();
        var svc = CriarServico(db, MockGemini(confianca: 0.30m, sucesso: false).Object);

        await Assert.ThrowsAsync<FotoRejeitadaException>(() =>
            svc.UploadFotoAsync(os.Id, unidade.Id, 1, Array.Empty<byte>(), "foto.jpg"));

        var leituraGravada = await db.LeiturasHidrometro
            .Where(l => l.OsId == os.Id && l.UnidadeId == unidade.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(leituraGravada);
        Assert.Equal(StatusLeitura.Rejeitada, leituraGravada.Status);
        Assert.Equal(0.30m, leituraGravada.ConfiancaIa);
    }

    // ── EXIF AutoOrient ──────────────────────────────────────────────────────

    [Fact]
    public void UploadFoto_ImagemRotacionada90_OcrRecebeImagemCorreta()
    {
        // Cria imagem 100x50 (landscape) com EXIF indicando rotação 90° (Orientation=6)
        using var image = new Image<Rgb24>(100, 50);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6); // 90° clockwise
        image.Metadata.ExifProfile = exif;

        byte[] bytesRotacionados;
        using (var ms = new MemoryStream())
        {
            image.SaveAsJpeg(ms);
            bytesRotacionados = ms.ToArray();
        }

        var corrigidos = LeituraService.CorrigirOrientacaoExif(bytesRotacionados);

        using var resultado = SixLabors.ImageSharp.Image.Load(corrigidos);
        // Após AutoOrient, uma imagem 100x50 com orientação 6 deve ter dimensões invertidas (50x100)
        Assert.Equal(50, resultado.Width);
        Assert.Equal(100, resultado.Height);
    }

    [Fact]
    public void UploadFoto_ImagemSemExif_NaoAlteraBytes()
    {
        // Bytes inválidos (não JPEG) devem ser retornados sem modificação
        var bytesInvalidos = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var resultado = LeituraService.CorrigirOrientacaoExif(bytesInvalidos);

        Assert.Equal(bytesInvalidos, resultado);
    }
}
