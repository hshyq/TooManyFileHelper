using ClosedXML.Excel;

namespace 文件整理助手;

public class ExcelExporter
{
    public async Task ExportAsync(List<FileEntry> entries, string savePath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("文件清单");

            // 表头（按 PRD F4 顺序：文件名称、格式、所在全路径、文件大小）
            ws.Cell(1, 1).Value = "文件名称";
            ws.Cell(1, 2).Value = "格式";
            ws.Cell(1, 3).Value = "所在全路径";
            ws.Cell(1, 4).Value = "文件大小";

            var headerRange = ws.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // 数据行
            for (int i = 0; i < entries.Count; i++)
            {
                int row = i + 2;
                ws.Cell(row, 1).Value = entries[i].Name;
                ws.Cell(row, 2).Value = entries[i].Extension;
                ws.Cell(row, 3).Value = entries[i].FullPath;
                ws.Cell(row, 4).Value = FormatSize(entries[i].Size);
            }

            // 列宽自适应
            ws.Columns().AdjustToContents();

            workbook.SaveAs(savePath);
        });
    }

    /// <summary>
    /// 格式化文件大小，统一以 KB 为单位显示
    /// 最小显示 1 KB，避免极小文件显示 0 KB
    /// </summary>
    private static string FormatSize(long bytes)
    {
        long kb = Math.Max(1, (long)Math.Round(bytes / 1024.0));
        return $"{kb:N0} KB";
    }
}
