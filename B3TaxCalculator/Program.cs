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
        var pdf = PdfReader.Read(file);
        var debugFolder = Path.Combine(AppContext.BaseDirectory, "DebugPdf");
        Directory.CreateDirectory(debugFolder);
        var baseName = Path.GetFileNameWithoutExtension(file);
        File.WriteAllText(Path.Combine(debugFolder, $"{baseName}.flat.txt"), pdf.FlatText);
        File.WriteAllText(Path.Combine(debugFolder, $"{baseName}.rows.txt"), pdf.RowText);
        Console.WriteLine($"  Debug salvo em: {debugFolder}");
        var trades = TradeParser.ParseFromText(pdf);
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
    Console.WriteLine(new string('=', 50));
    Console.WriteLine($"MES {result.Month:D2}/{result.Year}");
    Console.WriteLine(new string('=', 50));
    Console.WriteLine();

    // Ações à vista
    if (result.StockTotalBuy > 0 || result.StockTotalSell > 0)
    {
        Console.WriteLine("   [ACOES A VISTA]");
        Console.WriteLine($"      Compras.............: R$ {result.StockTotalBuy,10:N2}");
        Console.WriteLine($"      Vendas..............: R$ {result.StockTotalSell,10:N2}");
        Console.WriteLine($"      Custos abatidos.....: R$ {result.StockTotalFees,10:N2}");

        if (result.StockProfit > 0)
        {
            Console.WriteLine($"      Lucro...............: R$ {result.StockProfit,10:N2}");
        }
        if (result.StockLoss > 0)
        {
            Console.WriteLine($"      Prejuizo............: R$ {result.StockLoss,10:N2}");
        }
        if (result.StockAccumulatedLoss > 0)
        {
            Console.WriteLine($"      Prejuizo acumulado..: R$ {result.StockAccumulatedLoss,10:N2}");
        }
        if (result.StockTaxableProfit > 0)
        {
            Console.WriteLine($"      Lucro tributavel....: R$ {result.StockTaxableProfit,10:N2}");
        }
        if (result.StockTax > 0)
        {
            Console.WriteLine($"      IMPOSTO DEVIDO......: R$ {result.StockTax,10:N2}");
        }

        Console.WriteLine($"      Observacao..........: {result.StockDescription}");
        Console.WriteLine();
    }

    // Opções
    if (result.OptionTotalBuy > 0 || result.OptionTotalSell > 0)
    {
        Console.WriteLine("   [OPCOES]");
        Console.WriteLine($"      Vendas liquidas.....: R$ {result.OptionGrossSell,10:N2}");
        Console.WriteLine($"      Recompras zeragem...: R$ {result.OptionCompensatingBuyTotal,10:N2}");
        Console.WriteLine($"      Compras totais......: R$ {result.OptionTotalBuy,10:N2}");
        Console.WriteLine($"      Custos abatidos.....: R$ {result.OptionTotalFees,10:N2}");

        if (result.OptionProfit > 0)
        {
            Console.WriteLine($"      Lucro...............: R$ {result.OptionProfit,10:N2}");
        }
        if (result.OptionLoss > 0)
        {
            Console.WriteLine($"      Prejuizo............: R$ {result.OptionLoss,10:N2}");
        }
        if (result.OptionAccumulatedLoss > 0)
        {
            Console.WriteLine($"      Prejuizo acumulado..: R$ {result.OptionAccumulatedLoss,10:N2}");
        }
        if (result.OptionTaxableProfit > 0)
        {
            Console.WriteLine($"      Lucro tributavel....: R$ {result.OptionTaxableProfit,10:N2}");
            Console.WriteLine($"      DARF (15%)..........: R$ {result.OptionTax,10:N2}");
        }

        // Mostrar operações que compensaram (reduziram o lucro bruto)
        if (result.OptionCompensatingTrades.Count > 0 && result.OptionTotalBuy > 0)
        {
            Console.WriteLine();
            Console.WriteLine("      Recompras que reduziram o lucro:");
            foreach (var trade in result.OptionCompensatingTrades)
            {
                Console.WriteLine($"         {trade}");
            }
            Console.WriteLine($"      Total compensado....: R$ {result.OptionCompensatingBuyTotal,10:N2}");
        }

        Console.WriteLine($"      Observacao..........: {result.OptionDescription}");
        Console.WriteLine();
    }

    Console.WriteLine($"   IMPOSTO APURADO........: R$ {result.TotalTax,10:N2}");
    Console.WriteLine($"   SALDO ANTERIOR.........: R$ {result.PriorMonthTaxCarryover,10:N2}");
    Console.WriteLine($"   DARF A PAGAR NO MES....: R$ {result.TaxToPayThisMonth,10:N2}");
    Console.WriteLine($"   SALDO P/ PROXIMO MES...: R$ {result.TaxCarryoverToNextMonth,10:N2}");

    Console.WriteLine();
}

var totalTax = results.Sum(r => r.TaxToPayThisMonth);
var carryoverTax = results.LastOrDefault()?.TaxCarryoverToNextMonth ?? 0m;
Console.WriteLine(new string('=', 50));
Console.WriteLine($"TOTAL DE IMPOSTO A PAGAR..: R$ {totalTax,10:N2}");
Console.WriteLine($"SALDO ACUMULADO FINAL.....: R$ {carryoverTax,10:N2}");
Console.WriteLine(new string('=', 50));

Console.WriteLine();
Console.WriteLine("Pressione qualquer tecla para sair...");
Console.ReadKey();