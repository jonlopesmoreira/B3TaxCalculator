namespace B3TaxCalculator.Models;

public class PdfReadResult
{
    public string FlatText { get; set; } = string.Empty;
    public string RowText { get; set; } = string.Empty;

    public string CombinedText => string.Concat(FlatText, "\n", RowText);
}
