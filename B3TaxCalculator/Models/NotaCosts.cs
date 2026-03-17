namespace B3TaxCalculator.Models;

public class NotaCosts
{
    public DateTime Date { get; set; }
    public string NotaNumber { get; set; } = string.Empty;

    public decimal TaxaLiquidacao { get; set; }
    public decimal TaxaRegistro { get; set; }
    public decimal Emolumentos { get; set; }
    public decimal TaxaANA { get; set; }
    public decimal TaxaTransferenciaAtivos { get; set; }
    public decimal Corretagem { get; set; }
    public decimal ISS { get; set; }
    public decimal TaxaCustodia { get; set; }
    public decimal TaxaTermoOpcoes { get; set; }
    public decimal TaxaOperacional { get; set; }  
    public decimal Execucao { get; set; }
    public decimal Impostos { get; set; }       
    public decimal IRRF { get; set; }
    public decimal Outros { get; set; }

    public decimal TotalCustosExplicit { get; set; }

    public decimal TotalCustos =>
        TotalCustosExplicit > 0
            ? TotalCustosExplicit
            : TaxaLiquidacao + TaxaRegistro + Emolumentos +
              TaxaANA + TaxaTransferenciaAtivos + Corretagem + ISS + TaxaCustodia +
              TaxaTermoOpcoes + TaxaOperacional + Execucao + Impostos +
              IRRF + Outros;

    public decimal TotalOperacoes { get; set; }
}
