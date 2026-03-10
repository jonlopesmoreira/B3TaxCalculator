using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TaxCalculator
{
    public class MonthlyResult
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalBuy { get; set; }
        public decimal TotalSell { get; set; }
        public decimal Profit { get; set; }
        public decimal Loss { get; set; }
        public decimal AccumulatedLoss { get; set; }
        public decimal TaxableProfit { get; set; }
        public decimal Tax { get; set; }
        public bool IsExempt { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    private const decimal DayTradeTaxRate = 0.20m;
    private const decimal SwingTradeTaxRate = 0.15m;
    private const decimal ExemptionLimit = 20000m;

    private decimal _accumulatedLoss = 0m;

    public List<MonthlyResult> Calculate(List<Trade> trades)
    {
        var results = new List<MonthlyResult>();
        var tradesByMonth = GroupByMonth(trades);

        foreach (var (year, month, monthTrades) in tradesByMonth)
        {
            var result = CalculateMonth(year, month, monthTrades);
            results.Add(result);
        }

        return results;
    }

    private List<(int Year, int Month, List<Trade> Trades)> GroupByMonth(List<Trade> trades)
    {
        var grouped = trades
            .OrderBy(t => t.Date)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => (g.Key.Year, g.Key.Month, g.ToList()))
            .ToList();

        return grouped;
    }

    private MonthlyResult CalculateMonth(int year, int month, List<Trade> trades)
    {
        var result = new MonthlyResult
        {
            Year = year,
            Month = month
        };

        decimal totalBuy = 0m;
        decimal totalSell = 0m;
        decimal profit = 0m;

        var buyPositions = new Dictionary<string, (int Quantity, decimal AvgPrice)>();

        foreach (var trade in trades.OrderBy(t => t.Date))
        {
            if (trade.IsBuy)
            {
                totalBuy += trade.NetTotal;

                if (buyPositions.ContainsKey(trade.Asset))
                {
                    var pos = buyPositions[trade.Asset];
                    var totalQty = pos.Quantity + trade.Quantity;
                    var totalCost = (pos.Quantity * pos.AvgPrice) + trade.NetTotal;
                    var newAvgPrice = totalCost / totalQty;
                    buyPositions[trade.Asset] = (totalQty, newAvgPrice);
                }
                else
                {
                    buyPositions[trade.Asset] = (trade.Quantity, trade.Price);
                }
            }
            else if (trade.IsSell)
            {
                totalSell += trade.Total;

                if (buyPositions.ContainsKey(trade.Asset))
                {
                    var pos = buyPositions[trade.Asset];
                    var costBasis = pos.AvgPrice * trade.Quantity;
                    var saleValue = trade.NetTotal;
                    var tradeProfitLoss = saleValue - costBasis;

                    profit += tradeProfitLoss;

                    var newQty = pos.Quantity - trade.Quantity;
                    if (newQty > 0)
                    {
                        buyPositions[trade.Asset] = (newQty, pos.AvgPrice);
                    }
                    else
                    {
                        buyPositions.Remove(trade.Asset);
                    }
                }
            }
        }

        result.TotalBuy = totalBuy;
        result.TotalSell = totalSell;

        if (profit > 0)
        {
            result.Profit = profit;
            result.TaxableProfit = Math.Max(0, profit - _accumulatedLoss);
        }
        else
        {
            result.Loss = Math.Abs(profit);
            _accumulatedLoss += Math.Abs(profit);
        }

        result.AccumulatedLoss = _accumulatedLoss;

        if (totalSell <= ExemptionLimit)
        {
            result.IsExempt = true;
            result.Description = $"Isento - vendas totais (R$ {totalSell:N2}) abaixo de R$ 20.000,00";
        }
        else if (result.TaxableProfit > 0)
        {
            result.Tax = result.TaxableProfit * SwingTradeTaxRate;
            result.Description = $"DARF: R$ {result.Tax:N2} (15% sobre lucro tributável)";
            _accumulatedLoss = Math.Max(0, _accumulatedLoss - result.Profit);
        }
        else if (result.Loss > 0)
        {
            result.Description = $"Prejuízo de R$ {result.Loss:N2} acumulado para compensação futura";
        }
        else
        {
            result.Description = "Sem operações de venda";
        }

        return result;
    }
}
