using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using MarketProHunter.Models;
using MarketProHunter.Scoring;

namespace MarketProHunter.Export;

public sealed class ExcelExporter
{
    public async Task WriteWorkbookAsync(
        string path,
        IReadOnlyList<ProductResult> allProducts,
        SmartQueueResult smartQueue,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path)) File.Delete(path);

        await using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        await AddTextAsync(archive, "[Content_Types].xml", BuildContentTypes(), cancellationToken);
        await AddTextAsync(archive, "_rels/.rels", BuildRootRels(), cancellationToken);
        await AddTextAsync(archive, "xl/workbook.xml", BuildWorkbookXml(), cancellationToken);
        await AddTextAsync(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRels(), cancellationToken);
        await AddTextAsync(archive, "xl/styles.xml", BuildStyles(), cancellationToken);
        await AddTextAsync(archive, "xl/worksheets/sheet1.xml", BuildProductsSheet("Daily Winners", smartQueue.Items.Select(x => x.Product).ToList(), includeRank: true, smartQueue), cancellationToken);
        await AddTextAsync(archive, "xl/worksheets/sheet2.xml", BuildProductsSheet("All Products", allProducts, includeRank: false, smartQueue: null), cancellationToken);
        await AddTextAsync(archive, "xl/worksheets/sheet3.xml", BuildSummarySheet(allProducts, smartQueue), cancellationToken);
    }

    private static async Task AddTextAsync(ZipArchive archive, string entryName, string content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    private static string BuildProductsSheet(string sheetName, IReadOnlyList<ProductResult> products, bool includeRank, SmartQueueResult? smartQueue)
    {
        var headers = new List<string>();
        if (includeRank) headers.Add("Rank");
        headers.AddRange(new[]
        {
            "UploadScore", "Decision", "Tier", "ASIN", "Brand", "Title", "AmazonCost", "eBaySell", "NetProfit", "Margin%",
            "TitleQ", "ImageQ", "ContentQ", "BulletQ", "DescriptionQ", "SpecsQ", "BulletCount", "SpecCount", "A+Content",
            "Competition", "Confidence", "Rating", "Reviews", "Keyword", "ProductUrl", "Image1", "QualityNotes", "PageNotes"
        });

        var rows = new List<IReadOnlyList<object?>> { headers };
        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var row = new List<object?>();
            if (includeRank) row.Add(i + 1);
            row.AddRange(new object?[]
            {
                p.UploadScore,
                p.UploadDecision,
                includeRank && smartQueue is not null && i < smartQueue.Items.Count ? smartQueue.Items[i].Tier : string.Empty,
                p.Asin,
                p.Brand,
                p.Title,
                p.Price,
                p.RecommendedSalePrice,
                p.NetProfit,
                p.NetMarginPercent,
                p.TitleQualityScore,
                p.ImageQualityScore,
                p.ContentQualityScore,
                p.BulletPointQualityScore,
                p.DescriptionQualityScore,
                p.SpecificationQualityScore,
                p.BulletPointCount,
                p.SpecificationCount,
                p.HasAPlusContent ? "Yes" : "No",
                p.CompetitionScore,
                p.ConfidenceScore,
                p.Rating,
                p.ReviewCount,
                p.SearchKeyword,
                p.ProductUrl,
                p.ImageUrl1,
                p.ListingQualityNotes,
                p.ProductPageQualityNotes
            });
            rows.Add(row);
        }

        return BuildWorksheetXml(rows, freezeTopRow: true, autoFilter: true);
    }

    private static string BuildSummarySheet(IReadOnlyList<ProductResult> products, SmartQueueResult smartQueue)
    {
        var upload90 = products.Count(x => x.UploadScore >= 90);
        var avgNet = products.Count == 0 ? 0m : Math.Round(products.Average(x => x.NetProfit), 2);
        var avgMargin = products.Count == 0 ? 0m : Math.Round(products.Average(x => x.NetMarginPercent), 2);
        var avgQuality = products.Count == 0 ? 0m : Math.Round(products.Average(AverageQuality), 2);
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Metric", "Value" },
            new object?[] { "Accepted Products", products.Count },
            new object?[] { "Smart Queue Target", smartQueue.RequestedCount },
            new object?[] { "Smart Queue Selected", smartQueue.SelectedCount },
            new object?[] { "Upload Score 90+", upload90 },
            new object?[] { "Expected Net Profit", smartQueue.ExpectedNetProfit },
            new object?[] { "Average Net Profit", avgNet },
            new object?[] { "Average Margin %", avgMargin },
            new object?[] { "Average Upload Score", smartQueue.AverageUploadScore },
            new object?[] { "Average Confidence %", smartQueue.AverageConfidenceScore },
            new object?[] { "Average Listing Quality", avgQuality },
            new object?[] { "Generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
            new object?[] { "", "" },
            new object?[] { "Top Keywords", "Accepted Count" }
        };

        foreach (var group in products.GroupBy(x => x.SearchKeyword).OrderByDescending(g => g.Count()).Take(20))
        {
            rows.Add(new object?[] { group.Key, group.Count() });
        }

        rows.Add(new object?[] { "", "" });
        rows.Add(new object?[] { "Top Brands", "Accepted Count" });
        foreach (var group in products.Where(x => !string.IsNullOrWhiteSpace(x.Brand)).GroupBy(x => x.Brand).OrderByDescending(g => g.Count()).Take(20))
        {
            rows.Add(new object?[] { group.Key, group.Count() });
        }

        return BuildWorksheetXml(rows, freezeTopRow: true, autoFilter: false);
    }

    private static decimal AverageQuality(ProductResult p)
    {
        return Math.Round((p.TitleQualityScore + p.ImageQualityScore + p.ContentQualityScore + p.BulletPointQualityScore + p.DescriptionQualityScore + p.SpecificationQualityScore) / 6m, 2);
    }

    private static string BuildWorksheetXml(IReadOnlyList<IReadOnlyList<object?>> rows, bool freezeTopRow, bool autoFilter)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        builder.Append("<sheetViews><sheetView workbookViewId=\"0\">");
        if (freezeTopRow) builder.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        builder.Append("</sheetView></sheetViews>");
        builder.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
        builder.Append("<cols><col min=\"1\" max=\"1\" width=\"12\" customWidth=\"1\"/><col min=\"2\" max=\"30\" width=\"18\" customWidth=\"1\"/></cols>");
        builder.Append("<sheetData>");

        for (var r = 0; r < rows.Count; r++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{r + 1}\">");
            var row = rows[r];
            for (var c = 0; c < row.Count; c++)
            {
                AppendCell(builder, r + 1, c + 1, row[c], isHeader: r == 0);
            }
            builder.Append("</row>");
        }

        builder.Append("</sheetData>");
        if (autoFilter && rows.Count > 1)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<autoFilter ref=\"A1:{ColumnName(rows[0].Count)}{rows.Count}\"/>");
        }
        builder.Append("</worksheet>");
        return builder.ToString();
    }

    private static void AppendCell(StringBuilder builder, int row, int column, object? value, bool isHeader)
    {
        var reference = $"{ColumnName(column)}{row}";
        var style = isHeader ? " s=\"1\"" : string.Empty;

        switch (value)
        {
            case null:
                builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"{style}/>");
                break;
            case int or decimal or double or float:
                builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"{style}><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>");
                break;
            default:
                builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t>{EscapeXml(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)}</t></is></c>");
                break;
        }
    }

    private static string ColumnName(int index)
    {
        var dividend = index;
        var name = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            dividend = (dividend - modulo) / 26;
        }
        return name;
    }

    private static string EscapeXml(string value) => XmlConvert.EncodeName(value) == value
        ? SecurityElementEscape(value)
        : SecurityElementEscape(value);

    private static string SecurityElementEscape(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string BuildContentTypes() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private static string BuildRootRels() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string BuildWorkbookXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Daily Winners" sheetId="1" r:id="rId1"/>
            <sheet name="All Products" sheetId="2" r:id="rId2"/>
            <sheet name="Summary" sheetId="3" r:id="rId3"/>
          </sheets>
        </workbook>
        """;

    private static string BuildWorkbookRels() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>
          <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string BuildStyles() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
          <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs>
        </styleSheet>
        """;
}
