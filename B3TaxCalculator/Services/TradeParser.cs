using System.Text.RegularExpressions;
using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TradeParser
{
    public static List<Trade> ParseFromText(string pdfText)
    {
        var trades = new List<Trade>();
        var lines = pdfText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        DateTime? tradeDate = null;

        foreach (var line in lines)
        {
            if (TryParseDate(line, out var date))
            {
                tradeDate = date;
            }

            if (tradeDate.HasValue && TryParseTrade(line, tradeDate.Value, out var trade))
            {
                trades.Add(trade);
            }
        }

        return trades;
    }

    private static bool TryParseDate(string line, out DateTime date)
    {
        date = DateTime.MinValue;

        var datePattern = @"Data pregão:\s*(\d{2}/\d{2}/\d{4})";
        var match = Regex.Match(line, datePattern);

        if (match.Success)
        {
            return DateTime.TryParse(match.Groups[1].Value, out date);
        }

        return false;
    }

    private static bool TryParseTrade(string line, DateTime date, out Trade trade)
    {
        trade = null!;

        var pattern = @"([CV])\s+VISTA\s+([A-Z0-9]+)\s+(\d+)\s+([\d,]+)\s+([\d,]+)";
        var match = Regex.Match(line, pattern);

        if (match.Success)
        {
            trade = new Trade
            {
                Date = date,
                Side = match.Groups[1].Value,
                Market = "VISTA",
                Asset = match.Groups[2].Value,
                Quantity = int.Parse(match.Groups[3].Value),
                Price = decimal.Parse(match.Groups[4].Value.Replace(",", ".")),
                Fees = 0
            };
            return true;
        }

        return false;
    }
}
