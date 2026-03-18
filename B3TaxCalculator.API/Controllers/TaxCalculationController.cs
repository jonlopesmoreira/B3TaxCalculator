using Microsoft.AspNetCore.Mvc;
using B3TaxCalculator.Services;
using B3TaxCalculator.Models;

namespace B3TaxCalculator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaxCalculationController : ControllerBase
{
    private readonly ILogger<TaxCalculationController> _logger;

    public TaxCalculationController(ILogger<TaxCalculationController> logger)
    {
        _logger = logger;
    }

    [HttpPost("process-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessPdf(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Arquivo PDF é obrigatório");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Apenas arquivos PDF são aceitos");
        }

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var pdf = PdfReader.Read(tempPath);
            var trades = TradeParser.ParseFromText(pdf);
            var validTrades = trades.Where(t => !t.IsExercise).ToList();
            var exercises = trades.Where(t => t.IsExercise).ToList();

            var calculator = new TaxCalculator();
            var results = calculator.Calculate(trades);
            var totalTax = results.Sum(r => r.TotalTax);

            System.IO.File.Delete(tempPath);

            return Ok(new
            {
                success = true,
                fileName = file.FileName,
                tradesFound = trades.Count,
                validTrades = validTrades.Count,
                exerciseTrades = exercises.Select(t => new
                {
                    date = t.Date,
                    asset = t.Asset,
                    side = t.IsBuy ? "C" : "V",
                    quantity = t.Quantity,
                    price = t.Price,
                    total = t.Total,
                    reduction = t.Fees,
                    note = "Exercício de Opção - reduz imposto"
                }).ToList(),
                totalTaxToPayThisMonth = totalTax,
                monthlyResults = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar PDF");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro ao processar PDF",
                error = ex.Message
            });
        }
    }

    [HttpPost("process-pdfs")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessMultiplePdfs(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest("Pelo menos um arquivo PDF é obrigatório");
        }

        var invalidFiles = files.Where(f => !f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (invalidFiles.Any())
        {
            return BadRequest($"Arquivos inválidos: {string.Join(", ", invalidFiles.Select(f => f.FileName))}");
        }

        try
        {
            var allTrades = new List<Trade>();
            var processedFiles = new List<string>();
            var allExercises = new List<Trade>();

            foreach (var file in files)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                try
                {
                    var pdf = PdfReader.Read(tempPath);
                    var trades = TradeParser.ParseFromText(pdf);
                    var exercises = trades.Where(t => t.IsExercise).ToList();

                    allTrades.AddRange(trades);
                    allExercises.AddRange(exercises);
                    processedFiles.Add(file.FileName);
                }
                finally
                {
                    System.IO.File.Delete(tempPath);
                }
            }

            var calculator = new TaxCalculator();
            var results = calculator.Calculate(allTrades);
            var totalTax = results.Sum(r => r.TotalTax);
            var validTradesCount = allTrades.Count(t => !t.IsExercise);

            return Ok(new
            {
                success = true,
                filesProcessed = processedFiles,
                totalFilesRequested = files.Count,
                totalTradesFound = allTrades.Count,
                totalValidTrades = validTradesCount,
                exerciseTrades = allExercises.Select(t => new
                {
                    date = t.Date,
                    asset = t.Asset,
                    side = t.IsBuy ? "C" : "V",
                    quantity = t.Quantity,
                    price = t.Price,
                    total = t.Total,
                    reduction = t.Fees,
                    note = "Exercício de Opção - reduz imposto"
                }).ToList(),
                totalTaxToPayThisMonth = totalTax,
                monthlyResults = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar múltiplos PDFs");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro ao processar PDFs",
                error = ex.Message
            });
        }
    }

    [HttpPost("calculate-trades")]
    [Consumes("application/json")]
    public IActionResult CalculateTrades([FromBody] List<Trade> trades)
    {
        if (trades == null || trades.Count == 0)
        {
            return BadRequest("Lista de operações é obrigatória");
        }

        try
        {
            var exercises = trades.Where(t => t.IsExercise).ToList();

            var calculator = new TaxCalculator();
            var results = calculator.Calculate(trades);
            var totalTax = results.Sum(r => r.TotalTax);
            var validTradesCount = trades.Count(t => !t.IsExercise);

            return Ok(new
            {
                success = true,
                tradesProcessed = trades.Count,
                validTrades = validTradesCount,
                exerciseTrades = exercises.Select(t => new
                {
                    date = t.Date,
                    asset = t.Asset,
                    side = t.IsBuy ? "C" : "V",
                    quantity = t.Quantity,
                    price = t.Price,
                    total = t.Total,
                    reduction = t.Fees,
                    note = "Exercício de Opção - reduz imposto"
                }).ToList(),
                totalTaxToPayThisMonth = totalTax,
                monthlyResults = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular impostos");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro ao calcular impostos",
                error = ex.Message
            });
        }
    }
}
