using B3TaxCalculator.Services;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
    }
}