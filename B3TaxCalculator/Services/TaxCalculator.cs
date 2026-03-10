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
        public List<string> OptionCompensatingTrades { get; set; } = new List<string>(); // Operações que reduziram o lucro

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
        var compensatingTrades = new List<string>(); // Rastrear operações que compensam

        var buyPositions = new Dictionary<string, (int Quantity, decimal AvgPrice)>();
        var shortPositions = new Dictionary<string, (int Quantity, decimal AvgPrice)>(); // Posições vendidas (short)

        foreach (var trade in trades.OrderBy(t => t.Date))
        {
            if (trade.IsBuy)
            {
                totalBuy += trade.NetTotal;

                // Registrar TODAS as compras (para opções)
                if (!isStock)
                {
                    compensatingTrades.Add($"{trade.Date:dd/MM}: COMPRA {trade.Asset} {trade.Quantity}x @ {trade.Price:N2} = R$ {trade.NetTotal:N2}");
                }

                // Verificar se está zerando uma posição short
                if (shortPositions.ContainsKey(trade.Asset))
                {
                    var shortPos = shortPositions[trade.Asset];
                    var quantityToClose = Math.Min(trade.Quantity, shortPos.Quantity);

                    // Calcular lucro/prejuízo do short
                    var costToClose = trade.Price * quantityToClose;
                    var gainedFromShort = shortPos.AvgPrice * quantityToClose;
                    var tradeProfitLoss = gainedFromShort - costToClose;
                    profit += tradeProfitLoss;

                    // Atualizar posição short
                    var remainingShort = shortPos.Quantity - quantityToClose;
                    if (remainingShort > 0)
                    {
                        shortPositions[trade.Asset] = (remainingShort, shortPos.AvgPrice);
                    }
                    else
                    {
                        shortPositions.Remove(trade.Asset);
                    }

                    // Se sobrou quantidade comprada, adicionar à posição long
                    var remainingBuy = trade.Quantity - quantityToClose;
                    if (remainingBuy > 0)
                    {
                        if (buyPositions.ContainsKey(trade.Asset))
                        {
                            var pos = buyPositions[trade.Asset];
                            var totalQty = pos.Quantity + remainingBuy;
                            var totalCost = (pos.Quantity * pos.AvgPrice) + (trade.Price * remainingBuy);
                            var newAvgPrice = totalCost / totalQty;
                            buyPositions[trade.Asset] = (totalQty, newAvgPrice);
                        }
                        else
                        {
                            buyPositions[trade.Asset] = (remainingBuy, trade.Price);
                        }
                    }
                }
                else if (buyPositions.ContainsKey(trade.Asset))
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

                // Verificar se está vendendo posição comprada (long)
                if (buyPositions.ContainsKey(trade.Asset))
                {
                    var pos = buyPositions[trade.Asset];
                    var quantityToSell = Math.Min(trade.Quantity, pos.Quantity);

                    var costBasis = pos.AvgPrice * quantityToSell;
                    var saleValue = trade.Price * quantityToSell;
                    var tradeProfitLoss = saleValue - costBasis;
                    profit += tradeProfitLoss;

                    var newQty = pos.Quantity - quantityToSell;
                    if (newQty > 0)
                    {
                        buyPositions[trade.Asset] = (newQty, pos.AvgPrice);
                    }
                    else
                    {
                        buyPositions.Remove(trade.Asset);
                    }

                    // Se sobrou quantidade vendida, criar posição short
                    var remainingSell = trade.Quantity - quantityToSell;
                    if (remainingSell > 0)
                    {
                        if (shortPositions.ContainsKey(trade.Asset))
                        {
                            var shortPos = shortPositions[trade.Asset];
                            var totalQty = shortPos.Quantity + remainingSell;
                            var totalGain = (shortPos.Quantity * shortPos.AvgPrice) + (trade.Price * remainingSell);
                            var newAvgPrice = totalGain / totalQty;
                            shortPositions[trade.Asset] = (totalQty, newAvgPrice);
                        }
                        else
                        {
                            shortPositions[trade.Asset] = (remainingSell, trade.Price);
                        }
                    }
                }
                else
                {
                    // Venda descoberta (short) - criar posição short
                    if (shortPositions.ContainsKey(trade.Asset))
                    {
                        var shortPos = shortPositions[trade.Asset];
                        var totalQty = shortPos.Quantity + trade.Quantity;
                        var totalGain = (shortPos.Quantity * shortPos.AvgPrice) + trade.NetTotal;
                        var newAvgPrice = totalGain / totalQty;
                        shortPositions[trade.Asset] = (totalQty, newAvgPrice);
                    }
                    else
                    {
                        shortPositions[trade.Asset] = (trade.Quantity, trade.Price);
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
            result.OptionGrossSell = totalSell;
            result.OptionCompensatingTrades = compensatingTrades;

            // IMPORTANTE: Para opções, o lucro mensal = Vendas - Compras
            // Não importa se é venda coberta ou descoberta, tributa no mês
            var monthlyProfit = totalSell - totalBuy;

            if (monthlyProfit > 0)
            {
                result.OptionProfit = monthlyProfit;
                result.OptionTaxableProfit = Math.Max(0, monthlyProfit - _optionAccumulatedLoss);
            }
            else if (monthlyProfit < 0)
            {
                result.OptionLoss = Math.Abs(monthlyProfit);
                _optionAccumulatedLoss += Math.Abs(monthlyProfit);
            }

            result.OptionAccumulatedLoss = _optionAccumulatedLoss;

            // Calcular imposto sobre lucro tributável
            if (result.OptionTaxableProfit > 0)
            {
                result.OptionTax = result.OptionTaxableProfit * SwingTradeTaxRate;
                result.OptionNetProfit = result.OptionTaxableProfit - result.OptionTax;
                result.OptionDescription = $"DARF: R$ {result.OptionTax:N2} (15% sobre lucro de R$ {result.OptionTaxableProfit:N2})";
                _optionAccumulatedLoss = Math.Max(0, _optionAccumulatedLoss - result.OptionProfit);
            }
            else if (result.OptionLoss > 0)
            {
                result.OptionNetProfit = -result.OptionLoss;
                result.OptionDescription = $"Prejuizo de R$ {result.OptionLoss:N2} acumulado para compensacao futura";
            }
            else if (result.OptionProfit > 0 && result.OptionTaxableProfit == 0)
            {
                result.OptionNetProfit = 0;
                result.OptionDescription = $"Lucro de R$ {result.OptionProfit:N2} compensado com prejuizos anteriores de R$ {_optionAccumulatedLoss:N2}";
            }
            else if (totalSell > 0)
            {
                result.OptionNetProfit = monthlyProfit;
                result.OptionDescription = $"Sem lucro tributavel neste mes";
            }
            else
            {
                result.OptionNetProfit = 0;
                result.OptionDescription = "Sem operacoes";
            }
        }
    }
}
