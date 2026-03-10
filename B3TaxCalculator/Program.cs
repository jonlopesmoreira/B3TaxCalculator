using B3TaxCalculator.Services;

Console.WriteLine("=== Calculador de Imposto de Renda - B3 ===");
Console.WriteLine();

var folder = Path.Combine(AppContext.BaseDirectory, "Notas");

if (!Directory.Exists(folder))
{
    Console.WriteLine($"Pasta 'Notas' não encontrada em: {folder}");
    Console.WriteLine("Crie a pasta e adicione seus PDFs de notas de corretagem.");
    return;
}

var files = Directory.GetFiles(folder, "*.pdf");

if (files.Length == 0)
{
    Console.WriteLine("Nenhum arquivo PDF encontrado na pasta 'Notas'.");
    return;
}

Console.WriteLine($"Processando {files.Length} nota(s) de corretagem...");
Console.WriteLine();

var allTrades = new List<B3TaxCalculator.Models.Trade>();

foreach (var file in files)
{
    try
    {
        Console.WriteLine($"Lendo: {Path.GetFileName(file)}");
        var text = PdfReader.Read(file);
        var trades = TradeParser.ParseFromText(text);
        allTrades.AddRange(trades);
        Console.WriteLine($"  ✓ {trades.Count} operação(ões) encontrada(s)");

        // Agrupar e mostrar por data/nota
        var tradesByDate = trades.GroupBy(t => t.Date).OrderBy(g => g.Key);

        foreach (var group in tradesByDate)
        {
            Console.WriteLine($"\n>> {group.Key:dd/MM/yyyy}:");
            foreach (var trade in group)
            {
                var tipo = trade.IsBuy ? "COMPRA" : "VENDA";
                var market = trade.Market == "VISTA" ? "Acao" : "Opcao";
                // Formato tabular alinhado: TIPO | Market | Asset(20) | Qtd(3) x R$ Preco(8) = R$ Total(10)
                Console.WriteLine($"   {tipo,-6} | {market,-5} | {trade.Asset,-16} | {trade.Quantity,4} * {trade.Price,-7:N2} = R$ {trade.Total,-8:N2}");
            }
        }
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Erro: {ex.Message}");
    }
}

Console.WriteLine($"Total de operações: {allTrades.Count}");
Console.WriteLine();

if (allTrades.Count == 0)
{
    Console.WriteLine("Nenhuma operação encontrada nos PDFs.");
    return;
}

var calculator = new TaxCalculator();
var results = calculator.Calculate(allTrades);

Console.WriteLine("=== Resumo Mensal ===");
Console.WriteLine();

foreach (var result in results)
{
    Console.WriteLine($">> {result.Month:D2}/{result.Year}");
    Console.WriteLine();

    // Ações à vista
    if (result.StockTotalBuy > 0 || result.StockTotalSell > 0)
    {
        Console.WriteLine("   [ACOES A VISTA]");
        Console.WriteLine($"      Compras: R$ {result.StockTotalBuy:N2}");
        Console.WriteLine($"      Vendas: R$ {result.StockTotalSell:N2}");

        if (result.StockProfit > 0)
        {
            Console.WriteLine($"      Lucro: R$ {result.StockProfit:N2}");
        }
        if (result.StockLoss > 0)
        {
            Console.WriteLine($"      Prejuizo: R$ {result.StockLoss:N2}");
        }
        if (result.StockAccumulatedLoss > 0)
        {
            Console.WriteLine($"      Prejuizo acumulado: R$ {result.StockAccumulatedLoss:N2}");
        }
        if (result.StockTaxableProfit > 0)
        {
            Console.WriteLine($"      Lucro tributavel: R$ {result.StockTaxableProfit:N2}");
        }
        if (result.StockTax > 0)
        {
            Console.WriteLine($"      >> IMPOSTO: R$ {result.StockTax:N2}");
        }

        Console.WriteLine($"      Info: {result.StockDescription}");
        Console.WriteLine();
    }

    // Opções
    if (result.OptionTotalBuy > 0 || result.OptionTotalSell > 0)
    {
        Console.WriteLine("   [OPCOES]");
        Console.WriteLine($"      Compras: R$ {result.OptionTotalBuy:N2}");
        Console.WriteLine($"      Vendas: R$ {result.OptionTotalSell:N2}");

        if (result.OptionProfit > 0)
        {
            Console.WriteLine($"      Lucro: R$ {result.OptionProfit:N2}");
        }
        if (result.OptionLoss > 0)
        {
            Console.WriteLine($"      Prejuizo: R$ {result.OptionLoss:N2}");
        }
        if (result.OptionAccumulatedLoss > 0)
        {
            Console.WriteLine($"      Prejuizo acumulado: R$ {result.OptionAccumulatedLoss:N2}");
        }
        if (result.OptionTaxableProfit > 0)
        {
            Console.WriteLine($"      Lucro tributavel: R$ {result.OptionTaxableProfit:N2}");
        }
        if (result.OptionTax > 0)
        {
            Console.WriteLine($"      >> IMPOSTO: R$ {result.OptionTax:N2}");
        }

        Console.WriteLine($"      Info: {result.OptionDescription}");
        Console.WriteLine();
    }

    if (result.TotalTax > 0)
    {
        Console.WriteLine($"   >> TOTAL DO MES: R$ {result.TotalTax:N2}");
    }

    Console.WriteLine();
}

var totalTax = results.Sum(r => r.TotalTax);
if (totalTax > 0)
{
    Console.WriteLine($">> TOTAL DE IMPOSTO A PAGAR NO PERIODO: R$ {totalTax:N2}");
}
else
{
    Console.WriteLine(">> Nenhum imposto a pagar no periodo analisado.");
}

Console.WriteLine();
Console.WriteLine("Pressione qualquer tecla para sair...");
Console.ReadKey();