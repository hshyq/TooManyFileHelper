using ClosedXML.Excel;

namespace 文件整理助手;

/// <summary>
/// 将本次查重中被跳过的重复组导出为 XLSX。
/// </summary>
public sealed class DuplicateSkippedExporter
{
    /// <summary>
    /// 导出被跳过的重复组文件明细。
    /// </summary>
    /// <param name="groups">跳过顺序及组内文件。</param>
    /// <param name="filePath">目标 XLSX 文件路径。</param>
    public Task ExportAsync(IReadOnlyList<DuplicateSkippedGroup> groups, string filePath)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("已跳过重复组");
            string[] headers = ["跳过序号", "文件名", "所在文件夹完整路径", "文件大小（字节）"];
            for (int column = 0; column < headers.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = headers[column];
            }

            int row = 2;
            foreach (var group in groups.OrderBy(item => item.Order))
            {
                foreach (var file in group.Files.OrderBy(
                    item => item.FullPath,
                    StringComparer.OrdinalIgnoreCase))
                {
                    sheet.Cell(row, 1).Value = group.Order;
                    sheet.Cell(row, 2).Value = file.Name;
                    sheet.Cell(row, 3).Value = Path.GetDirectoryName(file.FullPath) ?? "";
                    sheet.Cell(row, 4).Value = file.Size;
                    row++;
                }
            }

            var header = sheet.Range(1, 1, 1, headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
            header.Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
            sheet.SheetView.FreezeRows(1);
            sheet.Column(1).Width = 12;
            sheet.Column(2).Width = 36;
            sheet.Column(3).Width = 72;
            sheet.Column(4).Width = 18;
            sheet.Column(4).Style.NumberFormat.Format = "#,##0";
            sheet.RangeUsed()?.SetAutoFilter();
            workbook.SaveAs(filePath);
        });
    }
}
