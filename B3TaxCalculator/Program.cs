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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Erro: {ex.Message}");
    }
}

Console.WriteLine();
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
    Console.WriteLine($"📅 {result.Month:D2}/{result.Year}");
    Console.WriteLine($"   Compras: R$ {result.TotalBuy:N2}");
    Console.WriteLine($"   Vendas: R$ {result.TotalSell:N2}");

    if (result.Profit > 0)
    {
        Console.WriteLine($"   Lucro: R$ {result.Profit:N2}");
    }
    if (result.Loss > 0)
    {
        Console.WriteLine($"   Prejuízo: R$ {result.Loss:N2}");
    }
    if (result.AccumulatedLoss > 0)
    {
        Console.WriteLine($"   Prejuízo acumulado: R$ {result.AccumulatedLoss:N2}");
    }
    if (result.TaxableProfit > 0)
    {
        Console.WriteLine($"   Lucro tributável: R$ {result.TaxableProfit:N2}");
    }
    if (result.Tax > 0)
    {
        Console.WriteLine($"   💰 IMPOSTO A PAGAR: R$ {result.Tax:N2}");
    }

    Console.WriteLine($"   ℹ️  {result.Description}");
    Console.WriteLine();
}

var totalTax = results.Sum(r => r.Tax);
if (totalTax > 0)
{
    Console.WriteLine($"💵 TOTAL DE IMPOSTO A PAGAR: R$ {totalTax:N2}");
}
else
{
    Console.WriteLine("✓ Nenhum imposto a pagar no período analisado.");
}

Console.WriteLine();
Console.WriteLine("Pressione qualquer tecla para sair...");
Console.ReadKey();