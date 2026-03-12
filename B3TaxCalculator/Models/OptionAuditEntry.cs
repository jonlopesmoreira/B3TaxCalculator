namespace B3TaxCalculator.Models;

public class OptionAuditEntry
{
    public DateTime Date { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal GrossValue { get; set; }
    public decimal NetValueImpact { get; set; }
    public decimal GrossToImpactDifference => GrossValue - NetValueImpact;
    public decimal AccumulatedNetValue { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
