using System.Text;
using B3TaxCalculator.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace B3TaxCalculator.Core.Services;

public class PdfReader
{
    public static PdfReadResult Read(string path)
    {
        var flatText = new StringBuilder();
        var rowText = new StringBuilder();

        using (var document = PdfDocument.Open(path))
        {
            foreach (var page in document.GetPages())
            {
                flatText.AppendLine(page.Text);
                flatText.AppendLine();
                rowText.AppendLine(ReadPagePreservingRows(page));
                rowText.AppendLine();
            }
        }

        return new PdfReadResult
        {
            FlatText = flatText.ToString(),
            RowText = rowText.ToString()
        };
    }

    private static string ReadPagePreservingRows(Page page)
    {
        var words = page.GetWords()
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        if (words.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<List<Word>>();
        const double lineTolerance = 2.5;

        foreach (var word in words)
        {
            var line = lines.FirstOrDefault(l => Math.Abs(l[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= lineTolerance);
            if (line is null)
            {
                lines.Add(new List<Word> { word });
            }
            else
            {
                line.Add(word);
            }
        }

        var builder = new StringBuilder();

        foreach (var line in lines.OrderByDescending(l => l[0].BoundingBox.Bottom))
        {
            var ordered = line.OrderBy(w => w.BoundingBox.Left).ToList();
            double? previousRight = null;

            foreach (var word in ordered)
            {
                if (previousRight.HasValue)
                {
                    var gap = word.BoundingBox.Left - previousRight.Value;
                    builder.Append(gap > 8 ? "  " : " ");
                }

                builder.Append(word.Text);
                previousRight = word.BoundingBox.Right;
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}

