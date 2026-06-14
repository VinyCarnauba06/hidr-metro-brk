using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace HidrometroApp.Core.Services;

public class RelatorioService : IRelatorioService
{
    private readonly HidrometroDbContext _db;

    public RelatorioService(HidrometroDbContext db)
    {
        _db = db;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<RelatorioOsResponse> ObterDadosRelatorioAsync(int osId)
    {
        var os = await _db.OrdensServico
            .Include(o => o.Condominio)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == osId)
            ?? throw new NotFoundException($"OS {osId} não encontrada");

        var leituras = await _db.LeiturasHidrometro
            .Include(l => l.Unidade)
            .Where(l => l.OsId == osId && l.Status == StatusLeitura.Validado)
            .OrderBy(l => l.Unidade.Numero)
            .ToListAsync();

        var historico = await _db.HistoricoConsumo
            .Where(h => h.OsId == osId)
            .ToDictionaryAsync(h => h.UnidadeId);

        var itens = leituras.Select(l =>
        {
            historico.TryGetValue(l.UnidadeId, out var hist);
            return new RelatorioItemResponse
            {
                Unidade = l.Unidade.Numero,
                LeituraAnterior = hist?.LeituraAnterior,
                LeituraAtual = l.ValorM3Validado ?? 0,
                Consumo = hist?.ConsumoM3 ?? 0,
                Origem = l.Origem.ToString(),
                SuspeitaVazamento = l.SuspeitaVazamento,
                Observacao = l.Observacao
            };
        }).ToList();

        return new RelatorioOsResponse
        {
            OsId = osId,
            Condominio = os.Condominio.Nome,
            Mes = os.Mes,
            Ano = os.Ano,
            TotalUnidades = leituras.Count,
            Itens = itens
        };
    }

    public async Task<byte[]> GerarExcelAsync(int osId)
    {
        var dados = await ObterDadosRelatorioAsync(osId);

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Leituras");

        // Cabeçalho
        ws.Cells[1, 1].Value = $"Leituras — {dados.Condominio} — {dados.Mes:D2}/{dados.Ano}";
        ws.Cells[1, 1, 1, 7].Merge = true;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Size = 14;

        var headers = new[] { "Unidade", "Leitura Anterior (m³)", "Leitura Atual (m³)", "Consumo (m³)", "Origem", "Vazamento?", "Observação" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[3, i + 1].Value = headers[i];
            ws.Cells[3, i + 1].Style.Font.Bold = true;
            ws.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(255, 0, 112, 192);
            ws.Cells[3, i + 1].Style.Font.Color.SetColor(255, 255, 255, 255);
        }

        int row = 4;
        foreach (var item in dados.Itens)
        {
            ws.Cells[row, 1].Value = item.Unidade;
            ws.Cells[row, 2].Value = item.LeituraAnterior;
            ws.Cells[row, 3].Value = item.LeituraAtual;
            ws.Cells[row, 4].Value = item.Consumo;
            ws.Cells[row, 5].Value = item.Origem;
            ws.Cells[row, 6].Value = item.SuspeitaVazamento ? "SIM" : "não";
            ws.Cells[row, 7].Value = item.Observacao;

            if (item.SuspeitaVazamento)
            {
                ws.Cells[row, 1, row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(255, 255, 230, 230);
            }
            row++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.Cells[3, 1, row - 1, 7].Style.Border.BorderAround(ExcelBorderStyle.Thin);

        return package.GetAsByteArray();
    }

    public async Task<byte[]> GerarPdfAsync(int osId)
    {
        var dados = await ObterDadosRelatorioAsync(osId);

        using var memStream = new MemoryStream();
        var document = new Document(PageSize.A4.Rotate(), 25f, 25f, 30f, 30f);
        var writer = PdfWriter.GetInstance(document, memStream);

        document.Open();

        // Título
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
        var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

        document.Add(new Paragraph($"Leituras de Hidrômetro — {dados.Condominio}", titleFont));
        document.Add(new Paragraph($"Competência: {dados.Mes:D2}/{dados.Ano}  |  Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}", cellFont));
        document.Add(new Paragraph(" "));

        // Tabela
        var table = new PdfPTable(7) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 10, 18, 18, 15, 12, 12, 25 });

        var bgHeader = new BaseColor(0, 112, 192);
        string[] colunas = { "Unidade", "Leitura Ant. (m³)", "Leitura Atual (m³)", "Consumo (m³)", "Origem", "Vazamento", "Observação" };

        foreach (var col in colunas)
        {
            var cell = new PdfPCell(new Phrase(col, headerFont))
            {
                BackgroundColor = bgHeader,
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 5
            };
            cell.Phrase.Font.Color = BaseColor.White;
            table.AddCell(cell);
        }

        var bgAlert = new BaseColor(255, 230, 230);
        foreach (var item in dados.Itens)
        {
            var bg = item.SuspeitaVazamento ? bgAlert : BaseColor.White;
            void AddCell(string val) => table.AddCell(new PdfPCell(new Phrase(val, cellFont)) { BackgroundColor = bg, Padding = 4 });

            AddCell(item.Unidade);
            AddCell(item.LeituraAnterior?.ToString("F2") ?? "—");
            AddCell(item.LeituraAtual.ToString("F2"));
            AddCell(item.Consumo.ToString("F2"));
            AddCell(item.Origem);
            AddCell(item.SuspeitaVazamento ? "SIM" : "não");
            AddCell(item.Observacao ?? "");
        }

        document.Add(table);
        document.Add(new Paragraph($"\nTotal de unidades: {dados.TotalUnidades}", cellFont));
        document.Close();

        return memStream.ToArray();
    }
}
