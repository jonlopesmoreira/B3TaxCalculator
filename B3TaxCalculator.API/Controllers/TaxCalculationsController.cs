using Microsoft.AspNetCore.Mvc;
using B3TaxCalculator.Services;
using B3TaxCalculator.Models;

namespace B3TaxCalculator.API.Controllers;

/// <summary>
/// Resource para cálculos de impostos sobre operações B3
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TaxCalculationsController : ControllerBase
{
    private readonly ILogger<TaxCalculationsController> _logger;

    public TaxCalculationsController(ILogger<TaxCalculationsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calcula impostos a partir de um ou múltiplos PDFs de notas de operação da B3
    /// </summary>
    /// <remarks>
    /// Aceita multipart/form-data com um ou múltiplos PDFs
    /// 
    /// Exemplo com curl (PDF único):
    /// ```
    /// curl -X POST http://localhost:5187/api/tax-calculations/upload-pdf \
    ///   -F "files=@nota.pdf"
    /// ```
    /// 
    /// Exemplo com curl (múltiplos PDFs):
    /// ```
    /// curl -X POST http://localhost:5187/api/tax-calculations/upload-pdf \
    ///   -F "files=@nota1.pdf" -F "files=@nota2.pdf"
    /// ```
    /// </remarks>
    /// <param name="files">Um ou múltiplos arquivos PDF para processamento</param>
    /// <response code="200">Cálculo realizado com sucesso</response>
    /// <response code="400">Nenhum arquivo PDF fornecido</response>
    /// <response code="500">Erro ao processar PDFs</response>
    [HttpPost("upload-pdf")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadPdfFiles([FromForm] List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Nenhum arquivo PDF fornecido"
                });
            }

            return await ProcessPdfs(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar PDFs");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro ao processar PDFs",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Calcula impostos a partir de uma lista de operações
    /// </summary>
    /// <remarks>
    /// Aceita application/json com array de operações
    /// 
    /// Exemplo com curl:
    /// ```
    /// curl -X POST http://localhost:5187/api/tax-calculations/calculate \
    ///   -H "Content-Type: application/json" \
    ///   -d '[{"date":"2026-01-15","asset":"PETR4","isBuy":true,"quantity":100,"price":25.50}]'
    /// ```
    /// </remarks>
    /// <response code="200">Cálculo realizado com sucesso</response>
    /// <response code="400">Lista de operações inválida ou vazia</response>
    /// <response code="500">Erro ao processar operações</response>
    [HttpPost("calculate")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult CalculateFromTrades([FromBody] List<Trade> trades)
    {
        try
        {
            if (trades == null || trades.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Lista de operações vazia"
                });
            }

            return ProcessTrades(trades);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular impostos");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro ao processar operações",
                error = ex.Message
            });
        }
    }

    private async Task<IActionResult> ProcessPdfs(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { success = false, message = "Nenhum arquivo PDF fornecido" });
        }

        var invalidFiles = files.Where(f => !f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (invalidFiles.Any())
        {
            return BadRequest(new { success = false, message = $"Arquivos inválidos: {string.Join(", ", invalidFiles.Select(f => f.FileName))}" });
        }

        try
        {
            var allTrades = new List<Trade>();
            var processedFiles = new List<string>();
            var allExercises = new List<Trade>();
            var calculationId = Guid.NewGuid();

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
                id = calculationId,
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
            _logger.LogError(ex, "Erro ao processar PDFs");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro ao processar PDFs",
                error = ex.Message
            });
        }
    }

    private IActionResult ProcessTrades(List<Trade> trades)
    {
        if (trades == null || trades.Count == 0)
        {
            return BadRequest(new { success = false, message = "Lista de operações vazia" });
        }

        try
        {
            var exercises = trades.Where(t => t.IsExercise).ToList();
            var calculationId = Guid.NewGuid();

            var calculator = new TaxCalculator();
            var results = calculator.Calculate(trades);
            var totalTax = results.Sum(r => r.TotalTax);
            var validTradesCount = trades.Count(t => !t.IsExercise);

            return Ok(new
            {
                id = calculationId,
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
                message = "Erro ao processar operações",
                error = ex.Message
            });
        }
    }
}
