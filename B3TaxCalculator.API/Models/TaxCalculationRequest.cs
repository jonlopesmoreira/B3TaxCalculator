using B3TaxCalculator.Services;

namespace B3TaxCalculator.API.Models;

public class TaxCalculationRequest
{
    public List<TradeDto> Trades { get; set; } = new();
}

public class TradeDto
{
    public DateTime Date { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fees { get; set; }
    public string NotaNumber { get; set; } = string.Empty;
    public bool IsExercise { get; set; }
}

public class TaxCalculationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? FileName { get; set; }
    public List<string>? FilesProcessed { get; set; }
    public int TradesFound { get; set; }
    public int ValidTrades { get; set; }
    public int TotalFilesRequested { get; set; }
    public List<TaxCalculator.MonthlyResult> MonthlyResults { get; set; } = new();
}
