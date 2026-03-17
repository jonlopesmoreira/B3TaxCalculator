using System.Text.RegularExpressions;
using B3TaxCalculator.Models;
using System.IO;

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

        var exercPattern = @"EXERC(?:\s+OPC)?\s+(COMPRA|VENDA)\s*\d{2}/\d{2}\s+([A-Z0-9W]{4,})[\s\S]{0,60}?(\d{1,3},\d{2})\s*@\s*(\d+)";
        var exercMatches = Regex.Matches(flatText, exercPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Console.WriteLine($"  [{flatNoteMatches.Count}] nota(s) | [{vistaMatches.Count}] acoes | [{tesouroDiretoMatches.Count}] RF | [{optionMatches.Count}] opcoes | [{exercMatches.Count}] exercicios");

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

        foreach (Match match in exercMatches)
        {
            allOperations.Add((match.Index, "EXERC_OPCAO", match));
        }

        var rowExercPattern = @"EXERC(?:\s+OPC)?\s+(COMPRA|VENDA).{0,80}?([A-Z0-9W]{4,}).{0,80}?(\d{1,3},\d{2})\s*@\s*(\d+)";
        var rowExercMatches = Regex.Matches(rowText, rowExercPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match m in rowExercMatches)
        {
            var rowAsset = m.Groups.Count > 2 ? m.Groups[2].Value.Trim() : "";
            var rowPrice = m.Groups.Count > 3 ? m.Groups[3].Value.Trim() : "";
            var rowQty = m.Groups.Count > 4 ? m.Groups[4].Value.Trim() : "";

            var duplicate = exercMatches.Cast<Match>().Any(em =>
            {
                var flatAsset = em.Groups.Count > 2 ? em.Groups[2].Value.Trim() : "";
                var flatPrice = em.Groups.Count > 3 ? em.Groups[3].Value.Trim() : "";
                var flatQty = em.Groups.Count > 4 ? em.Groups[4].Value.Trim() : "";

                return flatAsset == rowAsset && flatPrice == rowPrice && flatQty == rowQty;
            });

            if (!duplicate)
            {
                var posInFlat = flatText.IndexOf(m.Value, StringComparison.OrdinalIgnoreCase);
                if (posInFlat < 0 && !string.IsNullOrEmpty(rowAsset))
                {
                    posInFlat = flatText.IndexOf(rowAsset, StringComparison.OrdinalIgnoreCase);
                }

                var pos = posInFlat >= 0 ? posInFlat : m.Index;
                allOperations.Add((pos, "EXERC_OPCAO", m));
            }
        }

        if (exercMatches.Count > 0 || rowExercMatches.Count > 0)
        {
            foreach (Match m in exercMatches)
            {
                Console.WriteLine($"     Exerc match (flat): '{m.Value.Replace('\n',' ')}'");
            }
            foreach (Match m in rowExercMatches)
            {
                Console.WriteLine($"     Exerc match (row): '{m.Value.Replace('\n',' ')}'");
            }
        }

        var lines = rowText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var existingExercTexts = exercMatches.Cast<Match>().Select(m => m.Value.Trim()).Concat(rowExercMatches.Cast<Match>().Select(m => m.Value.Trim())).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fallbackPattern = new Regex(@"EXERC(?:\s+OPC)?\s+(COMPRA|VENDA).*?([A-Z0-9]{4,})[\s\S]{0,60}?(\d{1,3},\d{2})\s*@\s*(\d+)", RegexOptions.IgnoreCase);
        for (int li = 0; li < lines.Length; li++)
        {
            var line = lines[li].Trim();
            if (line.Length == 0) continue;
            if (!line.Contains("EXERC", StringComparison.OrdinalIgnoreCase)) continue;

            if (existingExercTexts.Contains(line))
            {
                continue;
            }

            var fm = fallbackPattern.Match(line);
            if (fm.Success)
            {
                var assetCode = fm.Groups.Count > 2 ? fm.Groups[2].Value : string.Empty;
                if (string.IsNullOrEmpty(assetCode) || assetCode.Length < 4 || (assetCode.All(c => char.IsDigit(c))))
                {
                    continue;
                }

                Console.WriteLine($"     Exerc fallback match (row line): '{line}'");
                var pos = rowText.IndexOf(line, StringComparison.Ordinal);
                allOperations.Add((pos >= 0 ? pos : 0, "EXERC_OPCAO", fm));
            }
        }

        allOperations = allOperations.OrderBy(o => o.Position).ToList();

        foreach (var op in allOperations)
        {
            var nota = FindNotaContainingMatch(flatNoteMatches, op.Match.Value, rowText);
            var foundByContaining = nota.HasValue;
            if (!nota.HasValue)
            {
                nota = FindClosestNotaAfter(flatNoteMatches, op.Position);
            }

            if (!nota.HasValue)
            {
                continue;
            }

            var searchMethod = foundByContaining ? "FindNotaContainingMatch" : "FindClosestNotaAfter";
            var assetCode = op.Type == "OPCAO" && op.Match.Groups.Count > 3 ? op.Match.Groups[3].Value : 
                           op.Type == "VISTA_ON" && op.Match.Groups.Count > 2 ? op.Match.Groups[2].Value : "?";
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory ?? ".", "DebugPdf");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "parser_log.txt");
                File.AppendAllText(logPath, $"DEBUG: {op.Type} {assetCode} encontrado por {searchMethod} -> Nota {nota.Value.NotaNum} ({nota.Value.Date:dd/MM/yyyy})" + Environment.NewLine);
            }
            catch { }

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
            if (op.Type == "OPCAO")
            {
                var match = op.Match;
                var quantity = int.Parse(match.Groups[4].Value);
                var priceInt = int.Parse(match.Groups[5].Value);
                var priceCents = int.Parse(match.Groups[6].Value);
                var price = priceInt + (priceCents / 100m);

                var trade = new Trade
                {
                    Date = nota.Value.Date,
                    Side = match.Groups[1].Value,
                    Market = "OPCAO_" + match.Groups[2].Value,
                    Asset = match.Groups[3].Value.Trim(),
                    Quantity = quantity,
                    Price = price,
                    NotaNumber = nota.Value.NotaNum
                };

                // Log para debug
                try
                {
                    var logDir = Path.Combine(AppContext.BaseDirectory ?? ".", "DebugPdf");
                    Directory.CreateDirectory(logDir);
                    var logPath = Path.Combine(logDir, "parser_log.txt");
                    File.AppendAllText(logPath, $"OPCAO Trade: {trade.Asset} {trade.Side} {quantity}x @ {price:N2} NotaNumber={nota.Value.NotaNum} Date={trade.Date:dd/MM/yyyy}" + Environment.NewLine);
                }
                catch { }

                trades.Add(trade);
            }
            else if (op.Type == "EXERC_OPCAO")
            {
                var match = op.Match;
                var sideText = match.Groups[1].Value.ToUpperInvariant();
                var side = sideText.StartsWith("C") ? "C" : "V";
                var asset = match.Groups[2].Value.Trim();
                var priceText = match.Groups[3].Value.Replace(',', '.');
                decimal.TryParse(priceText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price);
                var quantity = int.Parse(match.Groups[4].Value);

                trades.Add(new Trade
                {
                    Date = nota.Value.Date,
                    Side = side,
                    Market = "OPCAO_EXERC",
                    Asset = asset,
                    Quantity = quantity,
                    Price = price,
                    NotaNumber = nota.Value.NotaNum,
                    IsExercise = true
                });
            }
        }

        RatearCustos(trades, notaCostsDict);

        var deduplicatedTrades = new List<Trade>();
        var exercisesSeen = new HashSet<string>();

        foreach (var trade in trades.OrderBy(t => t.Date).ThenBy(t => t.Asset))
        {
            if (trade.IsExercise)
            {
                var key = $"{trade.Date:yyyyMMdd}_{trade.Asset}_{trade.Quantity}_{trade.Price}";
                if (exercisesSeen.Contains(key))
                {
                    continue;
                }
                exercisesSeen.Add(key);
            }
            deduplicatedTrades.Add(trade);
        }

        return deduplicatedTrades;
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

    private static (string NotaNum, DateTime Date)? FindNotaContainingMatch(MatchCollection notaMatches, string matchText, string rowText)
    {
        var assetMatch = Regex.Match(matchText, @"([A-Z0-9W]{4,})");
        var assetCode = assetMatch.Success ? assetMatch.Groups[1].Value : "";

        var quantityPriceMatch = Regex.Match(matchText, @"(\d{2,3})\s*[\.,]\s*(\d+)");
        var searchStr = quantityPriceMatch.Success ? 
            $"{quantityPriceMatch.Groups[1].Value}.*{quantityPriceMatch.Groups[2].Value}" : 
            "";

        for (var i = 0; i < notaMatches.Count; i++)
        {
            var notaMatch = notaMatches[i];
            var notaNum = notaMatch.Groups[1].Value;
            if (!DateTime.TryParse(notaMatch.Groups[2].Value, out var date))
            {
                continue;
            }

            var start = rowText.IndexOf(notaNum, StringComparison.Ordinal);
            if (start < 0) continue;

            var end = rowText.Length;
            if (i + 1 < notaMatches.Count)
            {
                var nextNotaNum = notaMatches[i + 1].Groups[1].Value;
                var nextIndex = rowText.IndexOf(nextNotaNum, start + notaNum.Length, StringComparison.Ordinal);
                if (nextIndex > start) end = nextIndex;
            }

            var block = rowText.Substring(start, end - start);

            if (block.IndexOf(matchText, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (notaNum, date);
            }

            if (!string.IsNullOrEmpty(searchStr) && Regex.IsMatch(block, searchStr))
            {
                if (!string.IsNullOrEmpty(assetCode) && block.IndexOf(assetCode, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (notaNum, date);
                }
            }
            else if (!string.IsNullOrEmpty(assetCode) && block.IndexOf(assetCode, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (notaNum, date);
            }
        }

        return null;
    }

    private static void RatearCustos(List<Trade> trades, Dictionary<string, NotaCosts> costsDict)
    {
        foreach (var group in trades.GroupBy(t => t.NotaNumber))
        {
            if (string.IsNullOrEmpty(group.Key) || !costsDict.TryGetValue(group.Key, out var costs) || costs.TotalCustos <= 0)
            {
                continue;
            }

            var nonExerciseTrades = group.Where(t => !t.IsExercise).ToList();
            var tradesForRateio = nonExerciseTrades.Any() ? nonExerciseTrades : group.ToList();

            var totalVolume = tradesForRateio.Sum(t => t.Total);
            if (totalVolume <= 0)
            {
                continue;
            }

            foreach (var trade in tradesForRateio)
            {
                if (trade.IsExercise)
                {
                    trade.Fees = costs.TotalCustos;
                }
                else
                {
                    trade.Fees = costs.TotalCustos * (trade.Total / totalVolume);
                }
            }
        }

        var assignedNotaKeys = trades.Select(t => t.NotaNumber).Where(k => !string.IsNullOrEmpty(k)).ToHashSet();
        var missingNotas = costsDict.Keys.Except(assignedNotaKeys).ToList();
        foreach (var nota in missingNotas)
        {
            if (!costsDict.TryGetValue(nota, out var costs)) continue;

            // Procurar APENAS trades sem NotaNumber atribuído na mesma data
            var tradesOnDate = trades.Where(t => t.Date.Date == costs.Date.Date && string.IsNullOrEmpty(t.NotaNumber)).ToList();
            if (!tradesOnDate.Any()) continue;

            var totalVolume = tradesOnDate.Sum(t => t.Total);
            if (totalVolume <= 0) continue;

            Console.WriteLine($"     Aviso: nota {nota} com custos R$ {costs.TotalCustos:N2} não atribuida por NotaNumber; rateando entre {tradesOnDate.Count} trades na data {costs.Date:dd/MM/yyyy}.");
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory ?? ".", "DebugPdf");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "parser_log.txt");
                File.AppendAllText(logPath, $"Aviso: nota {nota} nao atribuida por NotaNumber; rateando entre {tradesOnDate.Count} trades na data {costs.Date:dd/MM/yyyy}." + Environment.NewLine);
            }
            catch { }

            foreach (var trade in tradesOnDate)
            {
                trade.Fees += costs.TotalCustos * (trade.Total / totalVolume);
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

            var explicitTotal = ExtractDecimalFromRows(lines, "Total Custos / Despesas");
            if (explicitTotal <= 0)
            {
                explicitTotal = ExtractDecimalFromRows(lines, "Total Custos") + ExtractDecimalFromRows(lines, "Total Custos Despesas");
            }
            if (explicitTotal > 0)
            {
                costs.TotalCustosExplicit = explicitTotal;
            }

            var liquidoFinal = ExtractDecimalFromRows(lines, "Líquido para");

            var valorLiquidoOperacoes = ExtractDecimalFromRows(lines, "Valor líquido das operações");

            if (valorLiquidoOperacoes > 0 && liquidoFinal > 0)
            {
                var custosCalculados = valorLiquidoOperacoes - liquidoFinal;
                var custoAbsoluto = Math.Abs(custosCalculados);
                if (custoAbsoluto > 0)
                {
                    costs.TotalCustosExplicit = custoAbsoluto;
                    Console.WriteLine($"     Nota {notaNum}: Valor líquido operações R$ {valorLiquidoOperacoes:N2} - Líquido para R$ {liquidoFinal:N2} = Custos R$ {custoAbsoluto:N2}");
                }
            }
            else if (explicitTotal > 0)
            {
                costs.TotalCustosExplicit = explicitTotal;
            }

            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory ?? ".", "DebugPdf");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "parser_log.txt");
                if (liquidoFinal > 0)
                {
                    File.AppendAllText(logPath, $"     Nota {notaNum}: Líquido R$ {liquidoFinal:N2}, Custos aplicados R$ {costs.TotalCustos:N2}" + Environment.NewLine);
                }
                else
                {
                    File.AppendAllText(logPath, $"     Nota {notaNum}: Custos R$ {costs.TotalCustos:N2}" + Environment.NewLine);
                }
            }
            catch
            {
            }

            if (costs.TotalCustos > 0)
            {
                costsDict[notaNum] = costs;
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

            // Para "Líquido para", o padrão pode ser "Líquido para 23/02/2026  1,97 C"
            // Procurar por datas e pular elas
            var sliceNoDate = Regex.Replace(slice, @"\d{2}/\d{2}/\d{4}", " ");

            // Procurar por um número no formato brasileiro (ex.: 11,36 ou 1.946,00) após o label.
            var numberMatch = Regex.Match(sliceNoDate, @"(?<value>\d{1,3}(?:[\.\d{3}])*,\d{2})");
            if (!numberMatch.Success)
            {
                // fallback: tentar pegar dígitos simples com vírgula
                numberMatch = Regex.Match(sliceNoDate, @"(?<value>\d+,\d{2})");
            }

            if (!numberMatch.Success)
            {
                continue;
            }

            var valueText = numberMatch.Groups["value"].Value;
            valueText = valueText.Replace(".", string.Empty).Replace(',', '.');

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
