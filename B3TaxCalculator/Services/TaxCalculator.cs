using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TaxCalculator
{
    public class MonthlyResult
    {
        public int Year { get; set; }
        public int Month { get; set; }

        // Ações à vista
        public decimal StockTotalBuy { get; set; }
        public decimal StockTotalSell { get; set; }
        public decimal StockProfit { get; set; }
        public decimal StockLoss { get; set; }
        public decimal StockAccumulatedLoss { get; set; }
        public decimal StockTaxableProfit { get; set; }
        public decimal StockTax { get; set; }
        public bool StockIsExempt { get; set; }
        public string StockDescription { get; set; } = string.Empty;

        // Opções
        public decimal OptionTotalBuy { get; set; }
        public decimal OptionTotalSell { get; set; }
        public decimal OptionGrossSell { get; set; }     // Valor bruto das vendas
        public decimal OptionNetProfit { get; set; }     // 85% do valor bruto (lucro líquido)
        public decimal OptionProfit { get; set; }
        public decimal OptionLoss { get; set; }
        public decimal OptionAccumulatedLoss { get; set; }
        public decimal OptionTaxableProfit { get; set; }
        public decimal OptionTax { get; set; }           // 15% do valor bruto (DARF)
        public string OptionDescription { get; set; } = string.Empty;

        // Total geral
        public decimal TotalTax => StockTax + OptionTax;
    }

    private const decimal DayTradeTaxRate = 0.20m;
    private const decimal SwingTradeTaxRate = 0.15m;
    private const decimal StockExemptionLimit = 20000m; // Apenas para ações

    private decimal _stockAccumulatedLoss = 0m;
    private decimal _optionAccumulatedLoss = 0m;

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

        // Separar ações e opções
        var stockTrades = trades.Where(t => t.Market == "VISTA").ToList();
        var optionTrades = trades.Where(t => t.Market.StartsWith("OPCAO")).ToList();

        // Calcular ações
        CalculateMarket(stockTrades, result, true);

        // Calcular opções
        CalculateMarket(optionTrades, result, false);

        return result;
    }

    private void CalculateMarket(List<Trade> trades, MonthlyResult result, bool isStock)
    {
        if (trades.Count == 0)
        {
            if (isStock)
            {
                result.StockDescription = "Sem operações de ações";
            }
            else
            {
                result.OptionDescription = "Sem operações de opções";
            }
            return;
        }

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

        if (isStock)
        {
            result.StockTotalBuy = totalBuy;
            result.StockTotalSell = totalSell;

            if (profit > 0)
            {
                result.StockProfit = profit;
                result.StockTaxableProfit = Math.Max(0, profit - _stockAccumulatedLoss);
            }
            else
            {
                result.StockLoss = Math.Abs(profit);
                _stockAccumulatedLoss += Math.Abs(profit);
            }

            result.StockAccumulatedLoss = _stockAccumulatedLoss;

            // Ações: isenção de R$ 20.000
            if (totalSell <= StockExemptionLimit)
            {
                result.StockIsExempt = true;
                result.StockDescription = $"Isento - vendas (R$ {totalSell:N2}) abaixo de R$ 20.000,00";
            }
            else if (result.StockTaxableProfit > 0)
            {
                result.StockTax = result.StockTaxableProfit * SwingTradeTaxRate;
                result.StockDescription = $"DARF: R$ {result.StockTax:N2} (15% sobre lucro tributável)";
                _stockAccumulatedLoss = Math.Max(0, _stockAccumulatedLoss - result.StockProfit);
            }
            else if (result.StockLoss > 0)
            {
                result.StockDescription = $"Prejuízo de R$ {result.StockLoss:N2} acumulado";
            }
        }
        else
        {
            result.OptionTotalBuy = totalBuy;
            result.OptionTotalSell = totalSell;

            // Para opções: tributar sobre TODO o valor bruto das vendas
            result.OptionGrossSell = totalSell;
            result.OptionNetProfit = totalSell * 0.85m;  // 85% fica para o investidor
            result.OptionTax = totalSell * 0.15m;        // 15% é DARF

            if (profit > 0)
            {
                result.OptionProfit = profit;
                result.OptionTaxableProfit = Math.Max(0, profit - _optionAccumulatedLoss);
            }
            else
            {
                result.OptionLoss = Math.Abs(profit);
                _optionAccumulatedLoss += Math.Abs(profit);
            }

            result.OptionAccumulatedLoss = _optionAccumulatedLoss;

            // Descrição
            if (totalSell > 0)
            {
                result.OptionDescription = $"DARF: R$ {result.OptionTax:N2} (15% de R$ {result.OptionGrossSell:N2})";
            }
            else if (result.OptionLoss > 0)
            {
                result.OptionDescription = $"Prejuizo de R$ {result.OptionLoss:N2} acumulado";
            }
            else
            {
                result.OptionDescription = "Sem operacoes de venda";
            }
        }
    }
}
