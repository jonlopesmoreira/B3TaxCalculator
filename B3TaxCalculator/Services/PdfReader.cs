using UglyToad.PdfPig;

namespace B3TaxCalculator.Services;

public class PdfReader
{
    public static string Read(string path)
    {
        var text = "";

        using (var document = PdfDocument.Open(path))
        {
            foreach (var page in document.GetPages())
            {
                text += page.Text;
            }
        }

        return text;
    }
    }

