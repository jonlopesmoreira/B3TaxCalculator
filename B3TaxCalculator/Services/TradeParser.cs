using System.Text.RegularExpressions;
using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TradeParser
{
    public static List<Trade> ParseFromText(PdfReadResult pdf)
    {
        var trades = new List<Trade>();

        var flatText = pdf.FlatText;
        var rowText = pdf.RowText;

        var notaPattern = @"Nr\.\s*nota\s*(\d+).*?Data preg[ãa]o\s*(\d{2}/\d{2}/\d{4})";
        var flatNoteMatches = Regex.Matches(flatText, notaPattern, RegexOptions.Singleline);
        var notaCostsDict = ExtractNotaCosts(rowText, flatNoteMatches);

        var vistaPattern = @"([CV])VISTA([A-Z]+[0-9]*)\s+(?:ON|PN|PNA|PNB|PNC|UNT)(?:\s+(?:N1|N2|NM|EJ))?\s+@\s*(\d+)\s+(\d+),(\d{2})\s+(\d{1,3}[\d\.]*)[,](\d{2})[DC]";
        var vistaMatches = Regex.Matches(flatText, vistaPattern);

        var tesouroDiretoPattern = @"\d+-BOVESPACVISTA([A-Z]+)\s+(LFTB\s+[A-Z0-9]+)\s*@[#]?(\d{1})(\d{3}),(\d{2})(\d{3}),(\d{2})[DC]";
        var tesouroDiretoMatches = Regex.Matches(flatText, tesouroDiretoPattern);

        var optionPattern = @"([CV])OPCAO DE (COMPRA|VENDA)\d{2}/\d{2}([A-Z0-9W]+)\s+[A-Z]+\s+[\d,]+\s+[A-Z]+[A-Z0-9/#\s]*?(\d{3})(\d),(\d{2})(\d+),(\d{2})[DC]";
        var optionMatches = Regex.Matches(flatText, optionPattern);

        Console.WriteLine($"  [{flatNoteMatches.Count}] nota(s) | [{vistaMatches.Count}] acoes | [{tesouroDiretoMatches.Count}] RF | [{optionMatches.Count}] opcoes");

        var allOperations = new List<(int Position, string Type, Match Match)>();

        foreach (Match match in vistaMatches)
        {
            allOperations.Add((match.Index, "VISTA_ON", match));
        }

        foreach (Match match in tesouroDiretoMatches)
        {
            allOperations.Add((match.Index, "TESOURO", match));
        }

        foreach (Match match in optionMatches)
        {
            allOperations.Add((match.Index, "OPCAO", match));
        }

        allOperations = allOperations.OrderBy(o => o.Position).ToList();

        foreach (var op in allOperations)
        {
            var nota = FindClosestNotaAfter(flatNoteMatches, op.Position);
            if (!nota.HasValue)
            {
                continue;
            }

            if (op.Type == "VISTA_ON")
            {
                var match = op.Match;
                var asset = match.Groups[2].Value.Trim();
                var side = match.Groups[1].Value;
                var quantity = int.Parse(match.Groups[3].Value);
                var priceInt = int.Parse(match.Groups[4].Value);
                var priceCents = int.Parse(match.Groups[5].Value);
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = nota.Value.Date,
                    Side = side,
                    Market = "VISTA",
                    Asset = asset,
                    Quantity = quantity,
                    Price = price,
                    NotaNumber = nota.Value.NotaNum
                });
            }
            else if (op.Type == "TESOURO")
            {
                var match = op.Match;
                var asset = Regex.Replace($"{match.Groups[1].Value.Trim()} {match.Groups[2].Value.Trim()}", @"\s+", " ");
                var quantity = int.Parse(match.Groups[3].Value);
                var priceInt = int.Parse(match.Groups[4].Value);
                var priceCents = int.Parse(match.Groups[5].Value);
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = nota.Value.Date,
                    Side = "C",
                    Market = "VISTA",
                    Asset = asset,
                    Quantity = quantity,
                    Price = price,
                    NotaNumber = nota.Value.NotaNum
                });
            }
            else if (op.Type == "OPCAO")
            {
                var match = op.Match;
                var quantity = int.Parse(match.Groups[4].Value);
                var priceInt = int.Parse(match.Groups[5].Value);
                var priceCents = int.Parse(match.Groups[6].Value);
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = nota.Value.Date,
                    Side = match.Groups[1].Value,
                    Market = "OPCAO_" + match.Groups[2].Value,
                    Asset = match.Groups[3].Value.Trim(),
                    Quantity = quantity,
                    Price = price,
                    NotaNumber = nota.Value.NotaNum
                });
            }
        }

        RatearCustos(trades, notaCostsDict);

        return trades
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Asset)
            .ToList();
    }

    private static (string NotaNum, DateTime Date)? FindClosestNotaAfter(MatchCollection notaMatches, int position)
    {
        int closestDistance = int.MaxValue;
        (string NotaNum, DateTime Date)? closestNota = null;

        foreach (Match notaMatch in notaMatches)
        {
            if (notaMatch.Index > position)
            {
                var distance = notaMatch.Index - position;
                if (distance < closestDistance && DateTime.TryParse(notaMatch.Groups[2].Value, out var date))
                {
                    closestNota = (notaMatch.Groups[1].Value, date);
                    closestDistance = distance;
                }
            }
        }

        if (!closestNota.HasValue)
        {
            closestDistance = int.MaxValue;
            foreach (Match notaMatch in notaMatches)
            {
                if (notaMatch.Index < position)
                {
                    var distance = position - notaMatch.Index;
                    if (distance < closestDistance && DateTime.TryParse(notaMatch.Groups[2].Value, out var date))
                    {
                        closestNota = (notaMatch.Groups[1].Value, date);
                        closestDistance = distance;
                    }
                }
            }
        }

        return closestNota;
    }

    private static void RatearCustos(List<Trade> trades, Dictionary<string, NotaCosts> costsDict)
    {
        foreach (var group in trades.GroupBy(t => t.NotaNumber))
        {
            if (string.IsNullOrEmpty(group.Key) || !costsDict.TryGetValue(group.Key, out var costs) || costs.TotalCustos <= 0)
            {
                continue;
            }

            var totalVolume = group.Sum(t => t.Total);
            if (totalVolume <= 0)
            {
                continue;
            }

            foreach (var trade in group)
            {
                trade.Fees = costs.TotalCustos * (trade.Total / totalVolume);
            }
        }
    }

    private static Dictionary<string, NotaCosts> ExtractNotaCosts(string rowText, MatchCollection notaMatches)
    {
        var costsDict = new Dictionary<string, NotaCosts>();

        for (var i = 0; i < notaMatches.Count; i++)
        {
            var notaMatch = notaMatches[i];
            var notaNum = notaMatch.Groups[1].Value;

            if (!DateTime.TryParse(notaMatch.Groups[2].Value, out var date))
            {
                continue;
            }

            var start = rowText.IndexOf(notaNum, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            var end = rowText.Length;
            if (i + 1 < notaMatches.Count)
            {
                var nextNotaNum = notaMatches[i + 1].Groups[1].Value;
                var nextIndex = rowText.IndexOf(nextNotaNum, start + notaNum.Length, StringComparison.Ordinal);
                if (nextIndex > start)
                {
                    end = nextIndex;
                }
            }

            var block = rowText.Substring(start, end - start);
            var lines = block
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var costs = new NotaCosts
            {
                Date = date,
                NotaNumber = notaNum,
                TaxaLiquidacao = ExtractDecimalFromRows(lines, "Taxa de liquidação"),
                TaxaRegistro = ExtractDecimalFromRows(lines, "Taxa de Registro"),
                TaxaTermoOpcoes = ExtractDecimalFromRows(lines, "Taxa de termo/opções"),
                TaxaANA = ExtractDecimalFromRows(lines, "Taxa A.N.A."),
                Emolumentos = ExtractDecimalFromRows(lines, "Emolumentos"),
                TaxaTransferenciaAtivos = ExtractDecimalFromRows(lines, "Taxa de Transf. de Ativos"),
                TaxaOperacional = ExtractDecimalFromRows(lines, "Taxa Operacional"),
                Execucao = ExtractDecimalFromRows(lines, "Execução"),
                TaxaCustodia = ExtractDecimalFromRows(lines, "Taxa de Custódia"),
                Impostos = ExtractDecimalFromRows(lines, "Impostos"),
                Outros = ExtractDecimalFromRows(lines, "Outros")
            };

            if (costs.TotalCustos > 0)
            {
                costsDict[notaNum] = costs;
                Console.WriteLine($"     Nota {notaNum}: Custos R$ {costs.TotalCustos:N2}");
            }
        }

        return costsDict;
    }

    private static decimal ExtractDecimalFromRows(List<string> lines, string label)
    {
        foreach (var line in lines)
        {
            var labelIndex = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (labelIndex < 0)
            {
                continue;
            }

            var slice = line[labelIndex..];
            var match = Regex.Match(slice, $@"{Regex.Escape(label)}\s+(?<value>\d+,\d{{2}})(?:\s+[DC])?", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var valueText = match.Groups["value"].Value.Replace(',', '.');
            if (decimal.TryParse(
                valueText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            {
                return value;
            }
        }

        return 0m;
    }
}
