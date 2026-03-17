using System.Diagnostics;
using System.Drawing;
using System.Text;
using B3TaxCalculator.Services;
using System.Windows.Forms;

internal sealed class MainForm : Form
{
    private const string LinkedInUrl = "https://www.linkedin.com/in/jonlopesmoreira/";
    private const string GitHubUrl = "https://github.com/jonlopesmoreira";

    private readonly Label _instructionLabel;
    private readonly Button _authorButton;
    private readonly ContextMenuStrip _authorMenu;
    private readonly Button _selectFilesButton;
    private readonly Button _exitButton;
    private readonly RichTextBox _outputTextBox;

    public MainForm()
    {
        Text = "Calculador de Imposto de Renda para opções - B3";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1200, 800);

        _instructionLabel = new Label
        {
            AutoSize = true,
            Text = "Selecione as notas de corretagem em PDF para processar e visualizar o resultado abaixo."
        };

        _authorButton = new Button
        {
            AutoSize = true,
            Text = "Autor",
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _authorButton.Click += AuthorButton_Click;

        _authorMenu = new ContextMenuStrip();
        _authorMenu.Items.Add(new ToolStripMenuItem("Jonathas Lopes Moreira")
        {
            Enabled = false
        });
        _authorMenu.Items.Add(new ToolStripSeparator());

        var linkedInItem = new ToolStripMenuItem("LinkedIn");
        linkedInItem.Click += (_, _) => OpenUrl(LinkedInUrl);
        _authorMenu.Items.Add(linkedInItem);

        var gitHubItem = new ToolStripMenuItem("GitHub");
        gitHubItem.Click += (_, _) => OpenUrl(GitHubUrl);
        _authorMenu.Items.Add(gitHubItem);

        _selectFilesButton = new Button
        {
            AutoSize = true,
            Text = "Selecionar PDF(s)"
        };
        _selectFilesButton.Click += SelectFilesButton_Click;

        _exitButton = new Button
        {
            AutoSize = true,
            Text = "Sair"
        };
        _exitButton.Click += (_, _) => Close();

        _outputTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 10),
            Text = "Clique em 'Selecionar PDF(s)' para inserir as notas de corretagem e processar os cálculos."
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttonsPanel.Controls.Add(_selectFilesButton);
        buttonsPanel.Controls.Add(_exitButton);

        var topPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.Controls.Add(_instructionLabel, 0, 0);
        topPanel.Controls.Add(_authorButton, 1, 0);

        var headerPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        headerPanel.Controls.Add(topPanel);
        headerPanel.Controls.Add(buttonsPanel);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.Controls.Add(headerPanel, 0, 0);
        mainPanel.Controls.Add(_outputTextBox, 0, 1);

        Controls.Add(mainPanel);
    }

    private async void SelectFilesButton_Click(object? sender, EventArgs e)
    {
        using var fileDialog = new OpenFileDialog
        {
            Title = "Selecione suas notas de corretagem em PDF",
            Filter = "Arquivos PDF (*.pdf)|*.pdf",
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var files = fileDialog.FileNames;
        if (files.Length == 0)
        {
            return;
        }

        try
        {
            ToggleUi(false);
            _outputTextBox.Text = "Processando arquivos...";

            var resultText = await Task.Run(() => BuildResultText(files));
            _outputTextBox.Text = resultText;
            _outputTextBox.SelectionStart = 0;
            _outputTextBox.SelectionLength = 0;
            _outputTextBox.ScrollToCaret();
        }
        catch (Exception ex)
        {
            var message = $"Erro ao processar os arquivos: {ex.Message}";
            _outputTextBox.Text = message;
            MessageBox.Show(this, message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private void ToggleUi(bool enabled)
    {
        _selectFilesButton.Enabled = enabled;
        _exitButton.Enabled = enabled;
        UseWaitCursor = !enabled;
    }

    private void AuthorButton_Click(object? sender, EventArgs e)
    {
        _authorMenu.Show(_authorButton, new Point(0, _authorButton.Height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _authorMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Não foi possível abrir o link: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void InitializeComponent()
    {

    }

    private static string BuildResultText(string[] files)
    {
        var output = new StringBuilder();

        output.AppendLine("=== Calculador de Imposto de Renda - B3 ===");
        output.AppendLine();
        output.AppendLine($"Processando {files.Length} nota(s) de corretagem...");
        output.AppendLine();

        var allTrades = new List<B3TaxCalculator.Models.Trade>();

        foreach (var file in files)
        {
            try
            {
                output.AppendLine($"Lendo: {Path.GetFileName(file)}");
                var pdf = PdfReader.Read(file);
#if DEBUG
                var debugFolder = Path.Combine(AppContext.BaseDirectory, "DebugPdf");
                Directory.CreateDirectory(debugFolder);
                var baseName = Path.GetFileNameWithoutExtension(file);
                File.WriteAllText(Path.Combine(debugFolder, $"{baseName}.flat.txt"), pdf.FlatText);
                File.WriteAllText(Path.Combine(debugFolder, $"{baseName}.rows.txt"), pdf.RowText);
                output.AppendLine($"  Debug salvo em: {debugFolder}");
#endif
                var trades = TradeParser.ParseFromText(pdf);
                allTrades.AddRange(trades);
                var validCount = trades.Count(t => !t.IsExercise);
                output.AppendLine($"  ✓ {trades.Count} operação(ões) encontrada(s) ({validCount} válida(s) para cálculo)");

                var tradesByDate = trades.GroupBy(t => t.Date).OrderBy(g => g.Key);

                foreach (var group in tradesByDate)
                {
                    output.AppendLine($"\n>> {group.Key:dd/MM/yyyy}:");
                    foreach (var trade in group)
                    {
                        var tipo = trade.IsBuy ? "COMPRA" : "VENDA";
                        var market = trade.Market == "VISTA" ? "Acao" : "Opcao";
                        var note = trade.IsExercise ? " (EXERC)" : string.Empty;
                        output.AppendLine($"   {tipo,-6} | {market,-5} | {trade.Asset,-16} | {trade.Quantity,4} * {trade.Price,-7:N2} = R$ {trade.Total,-8:N2}{note}");
                    }
                }
                output.AppendLine();
            }
            catch (Exception ex)
            {
                output.AppendLine($"  ✗ Erro: {ex.Message}");
            }
        }

        output.AppendLine($"Total de operações: {allTrades.Count(t => !t.IsExercise)}");
        output.AppendLine();

        if (allTrades.Count == 0)
        {
            output.AppendLine("Nenhuma operação encontrada nos PDFs.");
            return output.ToString();
        }

        var calculator = new TaxCalculator();
        var results = calculator.Calculate(allTrades);

        output.AppendLine("=== Resumo Mensal ===");
        output.AppendLine();

        foreach (var result in results)
        {
            output.AppendLine(new string('=', 50));
            output.AppendLine($"MES {result.Month:D2}/{result.Year}");
            output.AppendLine(new string('=', 50));
            output.AppendLine();

            if (result.StockTotalBuy > 0 || result.StockTotalSell > 0)
            {
                output.AppendLine("   [ACOES A VISTA]");
                output.AppendLine($"      Compras.............: R$ {result.StockTotalBuy,10:N2}");
                output.AppendLine($"      Vendas..............: R$ {result.StockTotalSell,10:N2}");
                output.AppendLine($"      Custos abatidos.....: R$ {result.StockTotalFees,10:N2}");

                if (result.StockProfit > 0)
                {
                    output.AppendLine($"      Lucro...............: R$ {result.StockProfit,10:N2}");
                }
                if (result.StockLoss > 0)
                {
                    output.AppendLine($"      Prejuizo............: R$ {result.StockLoss,10:N2}");
                }
                if (result.StockAccumulatedLoss > 0)
                {
                    output.AppendLine($"      Prejuizo acumulado..: R$ {result.StockAccumulatedLoss,10:N2}");
                }
                if (result.StockTaxableProfit > 0)
                {
                    output.AppendLine($"      Lucro tributavel....: R$ {result.StockTaxableProfit,10:N2}");
                }
                if (result.StockTax > 0)
                {
                    output.AppendLine($"      IMPOSTO DEVIDO......: R$ {result.StockTax,10:N2}");
                }

                output.AppendLine($"      Observacao..........: {result.StockDescription}");
                output.AppendLine();
            }

            if (result.OptionTotalBuy > 0 || result.OptionTotalSell > 0)
            {
                output.AppendLine("   [OPCOES]");
                output.AppendLine($"      Vendas liquidas.....: R$ {result.OptionGrossSell,10:N2}");
                output.AppendLine($"      Recompras zeragem...: R$ {result.OptionCompensatingBuyTotal,10:N2}");
                output.AppendLine($"      Compras totais......: R$ {result.OptionTotalBuy,10:N2}");
                output.AppendLine($"      Custos abatidos.....: R$ {result.OptionTotalFees,10:N2}");

                if (result.OptionProfit > 0)
                {
                    output.AppendLine($"      Lucro...............: R$ {result.OptionProfit,10:N2}");
                }
                if (result.OptionLoss > 0)
                {
                    output.AppendLine($"      Prejuizo............: R$ {result.OptionLoss,10:N2}");
                }
                if (result.OptionAccumulatedLoss > 0)
                {
                    output.AppendLine($"      Prejuizo acumulado..: R$ {result.OptionAccumulatedLoss,10:N2}");
                }
                if (result.OptionTaxableProfit > 0)
                {
                    output.AppendLine($"      Lucro tributavel....: R$ {result.OptionTaxableProfit,10:N2}");
                    output.AppendLine($"      DARF (15%)..........: R$ {result.OptionTax,10:N2}");
                }

                if (result.OptionCompensatingTrades.Count > 0 && result.OptionTotalBuy > 0)
                {
                    output.AppendLine();
                    output.AppendLine("      Recompras que reduziram o lucro:");
                    foreach (var trade in result.OptionCompensatingTrades)
                    {
                        output.AppendLine($"         {trade}");
                    }
                    output.AppendLine($"      Total compensado....: R$ {result.OptionCompensatingBuyTotal,10:N2}");
                }

                if (result.OptionAuditEntries.Count > 0)
                {
                    output.AppendLine();
                    output.AppendLine("      Auditoria do liquido acumulado:");
                    foreach (var entry in result.OptionAuditEntries)
                    {
                        var tipo = entry.Side == "C" ? "COMPRA" : "VENDA";
                        output.AppendLine($"         {entry.Date:dd/MM} {tipo,-6} {entry.Asset,-12} Bruto: R$ {entry.GrossValue,8:N2} | Dif: R$ {entry.GrossToImpactDifference,8:N2} | Impacto: R$ {entry.NetValueImpact,8:N2} | Acumulado: R$ {entry.AccumulatedNetValue,8:N2}");
                    }
                }

                output.AppendLine($"      Observacao..........: {result.OptionDescription}");
                output.AppendLine();
            }

            output.AppendLine($"   IMPOSTO APURADO........: R$ {result.TotalTax,10:N2}");
            output.AppendLine($"   SALDO ANTERIOR.........: R$ {result.PriorMonthTaxCarryover,10:N2}");
            output.AppendLine($"   DARF A PAGAR NO MES....: R$ {result.TaxToPayThisMonth,10:N2}");
            output.AppendLine($"   SALDO P/ PROXIMO MES...: R$ {result.TaxCarryoverToNextMonth,10:N2}");

            output.AppendLine();
        }

        var totalTax = results.Sum(r => r.TaxToPayThisMonth);
        var carryoverTax = results.LastOrDefault()?.TaxCarryoverToNextMonth ?? 0m;
        output.AppendLine(new string('=', 50));
        output.AppendLine($"TOTAL DE IMPOSTO A PAGAR..: R$ {totalTax,10:N2}");
        output.AppendLine($"SALDO ACUMULADO FINAL.....: R$ {carryoverTax,10:N2}");
        output.AppendLine(new string('=', 50));

        return output.ToString();
    }
}
