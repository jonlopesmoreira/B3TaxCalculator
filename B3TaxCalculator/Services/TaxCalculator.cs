using B3TaxCalculator.Models;

namespace B3TaxCalculator.Services;

public class TaxCalculator
{
    public class MonthlyResult
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal PriorMonthTaxCarryover { get; set; }
        public decimal TaxCarryoverToNextMonth { get; set; }
        public decimal TaxToPayThisMonth { get; set; }

        // Ações à vista
        public decimal StockTotalBuy { get; set; }
        public decimal StockTotalSell { get; set; }
        public decimal StockTotalFees { get; set; }
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
        public decimal OptionTotalFees { get; set; }
        public decimal OptionCompensatingBuyTotal { get; set; }
        public decimal OptionGrossSell { get; set; }     // Valor bruto das vendas
        public decimal OptionNetProfit { get; set; }     // 85% do valor bruto (lucro líquido)
        public decimal OptionProfit { get; set; }
        public decimal OptionLoss { get; set; }
        public decimal OptionAccumulatedLoss { get; set; }
        public decimal OptionTaxableProfit { get; set; }
        public decimal OptionTax { get; set; }           // 15% do valor bruto (DARF)
        public string OptionDescription { get; set; } = string.Empty;
        public List<string> OptionCompensatingTrades { get; set; } = new List<string>(); // Operações que reduziram o lucro
        public List<OptionAuditEntry> OptionAuditEntries { get; set; } = new List<OptionAuditEntry>();

        // Total geral
        public decimal TotalTax => StockTax + OptionTax;
    }

    private const decimal DayTradeTaxRate = 0.20m;
    private const decimal SwingTradeTaxRate = 0.15m;
    private const decimal StockExemptionLimit = 20000m; // Apenas para ações
    private const decimal MinimumDarfPayment = 10m;

    private decimal _stockAccumulatedLoss = 0m;
    private decimal _optionAccumulatedLoss = 0m;
    private decimal _optionRunningNetAccumulated = 0m;

    public List<MonthlyResult> Calculate(List<Trade> trades)
    {
        var results = new List<MonthlyResult>();
        var tradesByMonth = GroupByMonth(trades);

        foreach (var (year, month, monthTrades) in tradesByMonth)
        {
            var result = CalculateMonth(year, month, monthTrades);
            results.Add(result);
        }

        ApplyMinimumDarfRule(results);

        return results;
    }

    private static void ApplyMinimumDarfRule(List<MonthlyResult> results)
    {
        decimal carryover = 0m;

        foreach (var result in results.OrderBy(r => r.Year).ThenBy(r => r.Month))
        {
            result.PriorMonthTaxCarryover = carryover;

            var payableAmount = carryover + result.TotalTax;
            if (payableAmount >= MinimumDarfPayment)
            {
                result.TaxToPayThisMonth = payableAmount;
                result.TaxCarryoverToNextMonth = 0m;
                carryover = 0m;
            }
            else
            {
                result.TaxToPayThisMonth = 0m;
                result.TaxCarryoverToNextMonth = payableAmount;
                carryover = payableAmount;
            }
        }
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
        decimal totalFees = 0m;
        decimal compensatingBuyTotal = 0m;
        decimal profit = 0m;
        var compensatingTrades = new List<string>(); // Rastrear operações que compensam
        var optionAuditEntries = new List<OptionAuditEntry>();
        var runningNetAccumulated = _optionRunningNetAccumulated;

        var buyPositions = new Dictionary<string, (int Quantity, decimal AvgPrice)>();
        var shortPositions = new Dictionary<string, (int Quantity, decimal AvgPrice)>(); // Posições vendidas (short)

        foreach (var trade in trades.OrderBy(t => t.Date))
        {
            totalFees += trade.Fees;

            if (trade.IsBuy)
            {
                totalBuy += trade.NetTotal;
                var unitBuyNet = trade.NetTotal / trade.Quantity;

                // Verificar se está zerando uma posição short
                if (shortPositions.ContainsKey(trade.Asset))
                {
                    var shortPos = shortPositions[trade.Asset];
                    var quantityToClose = Math.Min(trade.Quantity, shortPos.Quantity);

                    // Calcular lucro/prejuízo do short
                    var costToClose = unitBuyNet * quantityToClose;
                    var tradeProfitLoss = isStock
                        ? (shortPos.AvgPrice * quantityToClose) - costToClose
                        : -costToClose;
                    profit += tradeProfitLoss;

                    if (!isStock && quantityToClose > 0)
                    {
                        runningNetAccumulated += -costToClose;
                        optionAuditEntries.Add(new OptionAuditEntry
                        {
                            Date = trade.Date,
                            Asset = trade.Asset,
                            Side = trade.Side,
                            Price = trade.Price,
                            Quantity = quantityToClose,
                            NetValueImpact = -costToClose,
                            AccumulatedNetValue = runningNetAccumulated
                        });
                    }

                    if (!isStock && quantityToClose > 0)
                    {
                        compensatingBuyTotal += costToClose;
                        compensatingTrades.Add($"{trade.Date:dd/MM}: COMPRA {trade.Asset} {quantityToClose}x @ {trade.Price:N2} = R$ {costToClose:N2}");
                    }

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
                            var totalCost = (pos.Quantity * pos.AvgPrice) + (unitBuyNet * remainingBuy);
                            var newAvgPrice = totalCost / totalQty;
                            buyPositions[trade.Asset] = (totalQty, newAvgPrice);
                        }
                        else
                        {
                            buyPositions[trade.Asset] = (remainingBuy, unitBuyNet);
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
                    buyPositions[trade.Asset] = (trade.Quantity, unitBuyNet);
                }
            }
            else if (trade.IsSell)
            {
                totalSell += trade.NetTotal;
                var unitSellNet = trade.NetTotal / trade.Quantity;

                // Verificar se está vendendo posição comprada (long)
                if (buyPositions.ContainsKey(trade.Asset))
                {
                    var pos = buyPositions[trade.Asset];
                    var quantityToSell = Math.Min(trade.Quantity, pos.Quantity);

                    var costBasis = pos.AvgPrice * quantityToSell;
                    var saleValue = unitSellNet * quantityToSell;
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
                        if (!isStock)
                        {
                            profit += unitSellNet * remainingSell;
                            runningNetAccumulated += unitSellNet * remainingSell;
                            optionAuditEntries.Add(new OptionAuditEntry
                            {
                                Date = trade.Date,
                                Asset = trade.Asset,
                                Side = trade.Side,
                                Price = trade.Price,
                                Quantity = remainingSell,
                                NetValueImpact = unitSellNet * remainingSell,
                                AccumulatedNetValue = runningNetAccumulated
                            });
                        }

                        if (shortPositions.ContainsKey(trade.Asset))
                        {
                            var shortPos = shortPositions[trade.Asset];
                            var totalQty = shortPos.Quantity + remainingSell;
                            var totalGain = (shortPos.Quantity * shortPos.AvgPrice) + (unitSellNet * remainingSell);
                            var newAvgPrice = totalGain / totalQty;
                            shortPositions[trade.Asset] = (totalQty, newAvgPrice);
                        }
                        else
                        {
                            shortPositions[trade.Asset] = (remainingSell, unitSellNet);
                        }
                    }
                }
                else
                {
                    if (!isStock)
                    {
                        profit += trade.NetTotal;
                        runningNetAccumulated += trade.NetTotal;
                        optionAuditEntries.Add(new OptionAuditEntry
                        {
                            Date = trade.Date,
                            Asset = trade.Asset,
                            Side = trade.Side,
                            Price = trade.Price,
                            Quantity = trade.Quantity,
                            NetValueImpact = trade.NetTotal,
                            AccumulatedNetValue = runningNetAccumulated
                        });
                    }

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
                        shortPositions[trade.Asset] = (trade.Quantity, unitSellNet);
                    }
                }
            }
        }

        if (isStock)
        {
            result.StockTotalBuy = totalBuy;
            result.StockTotalSell = totalSell;
            result.StockTotalFees = totalFees;

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
            result.OptionTotalFees = totalFees;
            result.OptionCompensatingBuyTotal = compensatingBuyTotal;
            result.OptionGrossSell = totalSell;
            result.OptionCompensatingTrades = compensatingTrades;
            result.OptionAuditEntries = optionAuditEntries;
            _optionRunningNetAccumulated = runningNetAccumulated;

            if (profit > 0)
            {
                result.OptionProfit = profit;
                result.OptionTaxableProfit = Math.Max(0, profit - _optionAccumulatedLoss);
            }
            else if (profit < 0)
            {
                result.OptionLoss = Math.Abs(profit);
                _optionAccumulatedLoss += Math.Abs(profit);
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
                result.OptionNetProfit = profit;
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
