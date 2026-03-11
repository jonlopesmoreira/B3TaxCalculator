namespace B3TaxCalculator.Models;

public class Trade
{
    public DateTime Date { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fees { get; set; }  // Taxas rateadas da nota
    public decimal Total => Quantity * Price;
    public decimal NetTotal => Side == "C" ? Total + Fees : Total - Fees;

    public bool IsBuy => Side == "C";
    public bool IsSell => Side == "V";

    // Informações da nota
    public string NotaNumber { get; set; } = string.Empty;
}