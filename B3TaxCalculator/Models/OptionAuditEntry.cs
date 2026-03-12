namespace B3TaxCalculator.Models;

public class OptionAuditEntry
{
    public DateTime Date { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal NetValueImpact { get; set; }
    public decimal AccumulatedNetValue { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
