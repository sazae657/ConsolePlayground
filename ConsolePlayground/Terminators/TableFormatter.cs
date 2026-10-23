using System.Text;

namespace ConsolePlayground.Cmd.Terminators;

/// <summary>
/// 表っぽく整形
/// </summary>
public static class TableFormatter {
    public static string FormatAsTable(List<List<string>> table, string separator = "  ") {
        if (table == null || table.Count == 0) {
            return string.Empty;
        }

        int colCount = table.Max(row => row?.Count ?? 0);

        // 各列の最大幅(nullは空文字扱い)
        int[] colWidths = new int[colCount];
        for (int col = 0; col < colCount; col++) {
            colWidths[col] = table.Max(row =>
                (row != null && col < row.Count && row[col] != null)
                    ? row[col].Length
                    : 0);
        }

        var sb = new StringBuilder();
        foreach (var row in table) {
            var paddedCells = new List<string>();
            for (int col = 0; col < colCount; col++) {
                string cell = (row != null && col < row.Count && row[col] != null)
                    ? row[col]
                    : string.Empty;

                // 左寄せ（右側に空白埋め）
                paddedCells.Add(cell.PadRight(colWidths[col]));
            }
            sb.AppendLine(string.Join(separator, paddedCells).TrimEnd());
        }

        return sb.ToString();
    }
}
