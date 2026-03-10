using System.Text.RegularExpressions;
using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TradeParser
{
    public static List<Trade> ParseFromText(string pdfText)
    {
        var trades = new List<Trade>();

        // Buscar TODAS as datas no texto
        var datePattern = @"Data preg[ãa]o\s*(\d{2}/\d{2}/\d{4})";
        var dateMatches = Regex.Matches(pdfText, datePattern);

        // Buscar ações à vista (com ON)
        var vistaPattern = @"([CV])VISTA([A-Z]+[0-9]*)\s*ON\s+[A-Z0-9]*@(\d{3})(\d+),(\d{2})(\d+)[.,]?(\d{2,3}),(\d{2})[DC]";
        var vistaMatches = Regex.Matches(pdfText, vistaPattern);

        // Buscar Tesouro Direto (formato colado sem espaços do PdfPig)
        // Formato real extraído: 7-BOVESPACVISTAINVESTO LFTB          F11@#2118,44236,88D
        // Padrão: [NUM]-BOVESPACVISTA[NOME]ESPAÇO[LFTB F11]@[#]?[QTD1DIG][PRECO3DIG],[CENTS][TOTAL3DIG],[CENTS]D
        var tesouroDiretoPattern = @"\d+-BOVESPACVISTA([A-Z]+)\s+(LFTB\s+[A-Z0-9]+)\s*@[#]?(\d{1})(\d{3}),(\d{2})(\d{3}),(\d{2})[DC]";
        var tesouroDiretoMatches = Regex.Matches(pdfText, tesouroDiretoPattern);

        // Buscar opções - capturar código COMPLETO da opção
        // Formato: VOPCAO DE VENDA03/26PETRO412 PN 40,40 PETRE FM#1000,4949,00C
        // Captura: Código completo (PETRO412) antes de ON/PN, não apenas a série (PETRE)
        var optionPattern = @"([CV])OPCAO DE (COMPRA|VENDA)\d{2}/\d{2}([A-Z0-9W]+)\s+[A-Z]+\s+[\d,]+\s+[A-Z]+[A-Z0-9/#\s]*?(\d{3})(\d),(\d{2})(\d+),(\d{2})[DC]";
        var optionMatches = Regex.Matches(pdfText, optionPattern);

        Console.WriteLine($"  📅 {dateMatches.Count} data(s) | 📊 {vistaMatches.Count} ações | 📜 {tesouroDiretoMatches.Count} RF | 📈 {optionMatches.Count} opções");

        // Criar lista de todas as operações com suas posições
        var allOperations = new List<(int Position, string Type, Match Match, string OpType)>();

        foreach (Match match in vistaMatches)
        {
            allOperations.Add((match.Index, "VISTA_ON", match, "VISTA"));
        }

        foreach (Match match in tesouroDiretoMatches)
        {
            allOperations.Add((match.Index, "TESOURO", match, "VISTA"));
        }

        foreach (Match match in optionMatches)
        {
            allOperations.Add((match.Index, "OPCAO", match, "OPCAO"));
        }

        // Ordenar por posição
        allOperations = allOperations.OrderBy(o => o.Position).ToList();

        Console.WriteLine($"\n  🔍 DEBUG: Mapeamento Data -> Operações:");

        // Processar cada operação
        foreach (var op in allOperations)
        {
            var date = FindClosestDateBefore(dateMatches, op.Position);

            if (!date.HasValue)
            {
                continue;
            }

            if (op.Type == "VISTA_ON")
            {
                var match = op.Match;
                var asset = match.Groups[2].Value.Trim();
                var side = match.Groups[1].Value;
                Console.WriteLine($"     {date.Value:dd/MM/yyyy} -> {side}VISTA {asset} (pos: {op.Position})");

                var quantity = int.Parse(match.Groups[3].Value);
                var priceInt = int.Parse(match.Groups[4].Value);
                var priceCents = int.Parse(match.Groups[5].Value);
                var totalIntStr = match.Groups[6].Value + match.Groups[7].Value.TrimStart('.');
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = date.Value,
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
                var assetBase = match.Groups[1].Value.Trim(); // INVESTO
                var assetDetail = match.Groups[2].Value.Trim(); // LFTB F11
                var asset = $"{assetBase} {assetDetail}".Trim();

                // C ou V está implícito como COMPRA no padrão sem espaços
                var side = "C";
                Console.WriteLine($"     {date.Value:dd/MM/yyyy} -> {side}VISTA {asset} [Tesouro] (pos: {op.Position})");

                var quantity = int.Parse(match.Groups[3].Value);
                var priceInt = int.Parse(match.Groups[4].Value);
                var priceCents = int.Parse(match.Groups[5].Value);
                var totalInt = int.Parse(match.Groups[6].Value);
                var totalCents = int.Parse(match.Groups[7].Value);
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = date.Value,
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
                Console.WriteLine($"     {date.Value:dd/MM/yyyy} -> {side}OPCAO {type} {asset} (pos: {op.Position})");

                var quantity = int.Parse(match.Groups[4].Value);
                var priceInt = int.Parse(match.Groups[5].Value);
                var priceCents = int.Parse(match.Groups[6].Value);
                var price = priceInt + (priceCents / 100m);

                trades.Add(new Trade
                {
                    Date = date.Value,
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

    private static DateTime? FindClosestDateBefore(MatchCollection dateMatches, int position)
    {
        DateTime? closestDate = null;
        int closestDistance = int.MaxValue;

        // No PDF, as operações VÊM ANTES da data do pregão
        // Então devemos pegar a data mais próxima DEPOIS da posição da operação
        foreach (Match dateMatch in dateMatches)
        {
            if (dateMatch.Index > position) // Data DEPOIS da operação
            {
                int distance = dateMatch.Index - position;
                if (distance < closestDistance)
                {
                    if (DateTime.TryParse(dateMatch.Groups[1].Value, out var date))
                    {
                        closestDate = date;
                        closestDistance = distance;
                    }
                }
            }
        }

        // Se não encontrou data depois, pegar a mais próxima antes (fallback)
        if (!closestDate.HasValue)
        {
            int prevDistance = int.MaxValue;
            foreach (Match dateMatch in dateMatches)
            {
                if (dateMatch.Index < position)
                {
                    int distance = position - dateMatch.Index;
                    if (distance < prevDistance)
                    {
                        if (DateTime.TryParse(dateMatch.Groups[1].Value, out var date))
                        {
                            closestDate = date;
                            prevDistance = distance;
                        }
                    }
                }
            }
        }

        return closestDate;
    }

    // Métodos não utilizados - manter para compatibilidade futura
    private static List<Trade> ParseAllTradesFromLine(string line, DateTime date)
    {
        return new List<Trade>();
    }

    private static bool TryParseDate(string line, out DateTime date)
    {
        date = DateTime.MinValue;
        return false;
    }
}
