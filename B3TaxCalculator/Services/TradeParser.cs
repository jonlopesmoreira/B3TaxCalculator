using System.Text.RegularExpressions;
using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TradeParser
{
    public static List<Trade> ParseFromText(string pdfText)
    {
        var trades = new List<Trade>();

        // Buscar padrão "Nr. nota [NUMERO]" seguido de "Data pregão [DATA]"
        // Isso identifica cada nota de corretagem e sua data
        var notaPattern = @"Nr\.\s*nota\s*(\d+).*?Data preg[ãa]o\s*(\d{2}/\d{2}/\d{4})";
        var notaMatches = Regex.Matches(pdfText, notaPattern, RegexOptions.Singleline);

        // Buscar ações à vista (com ON)
        var vistaPattern = @"([CV])VISTA([A-Z]+[0-9]*)\s*ON\s+[A-Z0-9]*@(\d{3})(\d+),(\d{2})(\d+)[.,]?(\d{2,3}),(\d{2})[DC]";
        var vistaMatches = Regex.Matches(pdfText, vistaPattern);

        // Buscar Tesouro Direto
        var tesouroDiretoPattern = @"\d+-BOVESPACVISTA([A-Z]+)\s+(LFTB\s+[A-Z0-9]+)\s*@[#]?(\d{1})(\d{3}),(\d{2})(\d{3}),(\d{2})[DC]";
        var tesouroDiretoMatches = Regex.Matches(pdfText, tesouroDiretoPattern);

        // Buscar opções - código completo
        var optionPattern = @"([CV])OPCAO DE (COMPRA|VENDA)\d{2}/\d{2}([A-Z0-9W]+)\s+[A-Z]+\s+[\d,]+\s+[A-Z]+[A-Z0-9/#\s]*?(\d{3})(\d),(\d{2})(\d+),(\d{2})[DC]";
        var optionMatches = Regex.Matches(pdfText, optionPattern);

        Console.WriteLine($"  [{notaMatches.Count}] nota(s) | [{vistaMatches.Count}] acoes | [{tesouroDiretoMatches.Count}] RF | [{optionMatches.Count}] opcoes");

        // Criar lista de todas as operações com suas posições
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

        // Ordenar por posição
        allOperations = allOperations.OrderBy(o => o.Position).ToList();

        // Processar cada operação e associar à nota de corretagem
        foreach (var op in allOperations)
        {
            // Encontrar a nota de corretagem mais próxima DEPOIS da operação
            var nota = FindClosestNotaAfter(notaMatches, op.Position);

            if (nota == null)
                continue;

            if (op.Type == "VISTA_ON")
            {
                var match = op.Match;
                var asset = match.Groups[2].Value.Trim();
                var side = match.Groups[1].Value;

                var quantity = int.Parse(match.Groups[3].Value);
                var priceInt = int.Parse(match.Groups[4].Value);
                var priceCents = int.Parse(match.Groups[5].Value);
                var totalIntStr = match.Groups[6].Value + match.Groups[7].Value.TrimStart('.');
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = nota.Value.Date,
                    Side = side,
                    Market = "VISTA",
                    Asset = asset,
                    Quantity = quantity,
                    Price = price,
                    Fees = 0
                });
            }
            else if (op.Type == "TESOURO")
            {
                var match = op.Match;
                var assetBase = match.Groups[1].Value.Trim();
                var assetDetail = match.Groups[2].Value.Trim();

                // Normalizar espaços múltiplos para um único espaço
                var asset = $"{assetBase} {assetDetail}".Trim();
                asset = Regex.Replace(asset, @"\s+", " ");

                var side = "C";

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
                    Fees = 0
                });
            }
            else if (op.Type == "OPCAO")
            {
                var match = op.Match;
                var asset = match.Groups[3].Value.Trim();
                var side = match.Groups[1].Value;
                var type = match.Groups[2].Value;

                var quantity = int.Parse(match.Groups[4].Value);
                var priceInt = int.Parse(match.Groups[5].Value);
                var priceCents = int.Parse(match.Groups[6].Value);
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = nota.Value.Date,
                    Side = side,
                    Market = "OPCAO_" + type,
                    Asset = asset,
                    Quantity = quantity,
                    Price = price,
                    Fees = 0
                });
            }
        }

        // Ordenar por data
        trades = trades.OrderBy(t => t.Date).ThenBy(t => t.Asset).ToList();

        return trades;
    }

    private static (string NotaNum, DateTime Date)? FindClosestNotaAfter(MatchCollection notaMatches, int position)
    {
        int closestDistance = int.MaxValue;
        (string NotaNum, DateTime Date)? closestNota = null;

        // Encontrar a nota mais próxima DEPOIS da posição da operação
        foreach (Match notaMatch in notaMatches)
        {
            if (notaMatch.Index > position)
            {
                int distance = notaMatch.Index - position;
                if (distance < closestDistance)
                {
                    var notaNum = notaMatch.Groups[1].Value;
                    if (DateTime.TryParse(notaMatch.Groups[2].Value, out var date))
                    {
                        closestNota = (notaNum, date);
                        closestDistance = distance;
                    }
                }
            }
        }

        // Se não encontrou depois, pegar a mais próxima antes (fallback)
        if (!closestNota.HasValue)
        {
            closestDistance = int.MaxValue;
            foreach (Match notaMatch in notaMatches)
            {
                if (notaMatch.Index < position)
                {
                    int distance = position - notaMatch.Index;
                    if (distance < closestDistance)
                    {
                        var notaNum = notaMatch.Groups[1].Value;
                        if (DateTime.TryParse(notaMatch.Groups[2].Value, out var date))
                        {
                            closestNota = (notaNum, date);
                            closestDistance = distance;
                        }
                    }
                }
            }
        }

        return closestNota;
    }

    // Métodos não utilizados - removidos
}
