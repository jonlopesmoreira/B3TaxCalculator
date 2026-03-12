using System.Globalization;
using B3TaxCalculator.Models;
using B3TaxCalculator.Services;
using Xunit;

namespace B3TaxCalculator.Tests.Services;

public class TaxCalculatorTests
{
    [Fact]
    public void Calculate_WhenThereAreNoTrades_ReturnsEmptyResult()
    {
        var calculator = new TaxCalculator();

        var results = calculator.Calculate([]);

        Assert.Empty(results);
    }

    [Fact]
    public void Calculate_WhenStockSalesStayBelowExemptionLimit_DoesNotChargeTax()
    {
        using var _ = new CultureScope("pt-BR");
        var calculator = new TaxCalculator();

        var results = calculator.Calculate([
            CreateTrade(new DateTime(2026, 2, 1), "PETR4", "VISTA", "C", 100, 100m),
            CreateTrade(new DateTime(2026, 2, 10), "PETR4", "VISTA", "V", 100, 150m)
        ]);

        var result = Assert.Single(results);

        Assert.True(result.StockIsExempt);
        Assert.Equal(5_000m, result.StockProfit);
        Assert.Equal(0m, result.StockTax);
        Assert.Equal("Isento - vendas (R$ 15.000,00) abaixo de R$ 20.000,00", result.StockDescription);
        Assert.Equal(0m, result.TaxToPayThisMonth);
    }

    [Fact]
    public void Calculate_WhenOptionShortIsClosed_TracksCompensatingBuyAndAudit()
    {
        var calculator = new TaxCalculator();

        var results = calculator.Calculate([
            CreateTrade(new DateTime(2026, 2, 1), "OPTABC", "OPCAO_VENDA", "V", 100, 10m),
            CreateTrade(new DateTime(2026, 2, 5), "OPTABC", "OPCAO_VENDA", "C", 100, 4m)
        ]);

        var result = Assert.Single(results);

        Assert.Equal(1_000m, result.OptionGrossSell);
        Assert.Equal(400m, result.OptionCompensatingBuyTotal);
        Assert.Equal(600m, result.OptionProfit);
        Assert.Equal(90m, result.OptionTax);
        Assert.Single(result.OptionCompensatingTrades);
        Assert.Contains("COMPRA OPTABC", result.OptionCompensatingTrades[0]);
        Assert.Equal(2, result.OptionAuditEntries.Count);
        Assert.Equal(1_000m, result.OptionAuditEntries[0].AccumulatedNetValue);
        Assert.Equal(600m, result.OptionAuditEntries[1].AccumulatedNetValue);
    }

    [Fact]
    public void Calculate_WhenOptionsHavePriorMonthCarryover_UsesExpandedDarfDescription()
    {
        using var _ = new CultureScope("pt-BR");
        var calculator = new TaxCalculator();

        var results = calculator.Calculate([
            CreateTrade(new DateTime(2026, 2, 20), "CMINO540", "OPCAO_VENDA", "V", 1, 38.86m),
            CreateTrade(new DateTime(2026, 3, 10), "CMINC502", "OPCAO_VENDA", "V", 1, 268.40m)
        ]);

        Assert.Equal(2, results.Count);

        var february = results[0];
        var march = results[1];

        Assert.Equal(5.829m, february.OptionTax);
        Assert.Equal(0m, february.TaxToPayThisMonth);
        Assert.Equal(5.829m, february.TaxCarryoverToNextMonth);

        Assert.Equal(5.829m, march.PriorMonthTaxCarryover);
        Assert.Equal(40.26m, march.OptionTax);
        Assert.Equal(46.089m, march.TaxToPayThisMonth);
        Assert.Equal("DARF: R$ 40,26 (15% sobre lucro de R$ 307,26 - R$ 38,86 = R$ 268,40)", march.OptionDescription);
    }

    private static Trade CreateTrade(DateTime date, string asset, string market, string side, int quantity, decimal price, decimal fees = 0m)
    {
        return new Trade
        {
            Date = date,
            Asset = asset,
            Market = market,
            Side = side,
            Quantity = quantity,
            Price = price,
            Fees = fees,
            NotaNumber = $"N{date:yyyyMMdd}-{asset}-{side}"
        };
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
