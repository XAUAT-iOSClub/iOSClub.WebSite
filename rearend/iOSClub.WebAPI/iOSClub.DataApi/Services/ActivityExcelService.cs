using ClosedXML.Excel;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace iOSClub.DataApi.Services;

public interface IActivityExcelService
{
    byte[] Export(IEnumerable<ActivityParticipantDO> participants);
    byte[] BuildTemplate();
    byte[] BuildErrorReport(IEnumerable<ActivityImportErrorVO> errors);
    (List<ActivityParticipantCreateDTO> Rows, List<ActivityImportErrorVO> Errors) Import(Stream stream, string fileName);
}

/// <summary>
/// 活动参与者 Excel 导入导出服务。
/// 导出/模板/错误报告用 ClosedXML（.xlsx）；导入用 NPOI（同时兼容 .xls 与 .xlsx）。
/// </summary>
public class ActivityExcelService : IActivityExcelService
{
    private static readonly string[] HeaderNames = ["姓名", "学号", "学院", "班级"];
    public const string TemplateFileName = "活动参与者导入模板.xlsx";

    // ===== 导出（.xlsx）=====
    public byte[] Export(IEnumerable<ActivityParticipantDO> participants)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("参与者名单");
        WriteHeader(ws, HeaderNames);
        var row = 2;
        foreach (var p in participants)
        {
            ws.Cell(row, 1).Value = p.Name;
            ws.Cell(row, 2).Value = p.StudentId;
            ws.Cell(row, 3).Value = p.Academy;
            ws.Cell(row, 4).Value = p.ClassName;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ===== 导入模板（.xlsx，仅表头 + 一行示例）=====
    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();

        // 第 1 个工作表「参与者」只放表头：导入时只读取它，避免把示例数据当成真实参与者导入
        var ws = workbook.Worksheets.Add("参与者");
        WriteHeader(ws, HeaderNames);
        ws.Columns().AdjustToContents();

        // 第 2 个工作表放填写说明与示例（不参与导入）
        var guide = workbook.Worksheets.Add("填写说明");
        guide.Cell(1, 1).Value = "填写说明";
        guide.Cell(2, 1).Value = "1. 请在「参与者」工作表中，从第 2 行开始逐行填写，不要修改第 1 行表头。";
        guide.Cell(3, 1).Value = "2. 四列分别为：姓名、学号、学院、班级，均必填。";
        guide.Cell(4, 1).Value = "3. 学号必须是 10 位数字；同一活动内学号不能重复。";
        guide.Cell(5, 1).Value = "4. 保存为 .xlsx / .xls 后，在活动详情页点「选择文件 → 预览 → 确认导入」。";
        guide.Cell(7, 1).Value = "示例（请仅在「参与者」表中按此格式填写）：";
        guide.Cell(8, 1).Value = "张三";
        guide.Cell(8, 2).Value = "2023000001";
        guide.Cell(8, 3).Value = "计算机和信息工程学院";
        guide.Cell(8, 4).Value = "计科2301";
        guide.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ===== 错误报告（.xlsx）=====
    public byte[] BuildErrorReport(IEnumerable<ActivityImportErrorVO> errors)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("导入错误");
        WriteHeader(ws, ["行号", "姓名", "学号", "学院", "班级", "失败原因"]);
        var row = 2;
        foreach (var e in errors)
        {
            ws.Cell(row, 1).Value = e.RowNumber;
            ws.Cell(row, 2).Value = e.Name;
            ws.Cell(row, 3).Value = e.StudentId;
            ws.Cell(row, 4).Value = e.Academy;
            ws.Cell(row, 5).Value = e.ClassName;
            ws.Cell(row, 6).Value = e.Reason;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteHeader(IXLWorksheet ws, string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = names[i];
            cell.Style.Font.Bold = true;
        }
    }

    // ===== 导入（.xls / .xlsx）=====
    public (List<ActivityParticipantCreateDTO> Rows, List<ActivityImportErrorVO> Errors) Import(Stream stream, string fileName)
    {
        var rows = new List<ActivityParticipantCreateDTO>();
        var errors = new List<ActivityImportErrorVO>();

        IWorkbook workbook;
        try
        {
            workbook = fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                ? new HSSFWorkbook(stream)
                : new XSSFWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("文件内容无法解析，请确认上传的是有效的 .xlsx 或 .xls 文件", ex);
        }

        using var workbookScope = workbook;

        if (workbook.NumberOfSheets == 0)
        {
            errors.Add(new ActivityImportErrorVO { RowNumber = 0, Reason = "文件中没有工作表" });
            return (rows, errors);
        }

        var sheet = workbook.GetSheetAt(0);
        var headerRow = sheet.GetRow(sheet.FirstRowNum);
        if (headerRow == null)
        {
            errors.Add(new ActivityImportErrorVO { RowNumber = 0, Reason = "文件为空或缺少表头" });
            return (rows, errors);
        }

        // 按表头文字定位列
        var formatter = new DataFormatter();
        var colMap = new Dictionary<string, int>();
        for (var c = 0; c < headerRow.LastCellNum; c++)
        {
            var hc = headerRow.GetCell(c);
            var text = hc == null ? "" : formatter.FormatCellValue(hc).Trim();
            if (HeaderNames.Contains(text) && !colMap.ContainsKey(text))
                colMap[text] = c;
        }
        var missing = HeaderNames.Where(h => !colMap.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            errors.Add(new ActivityImportErrorVO { RowNumber = 0, Reason = $"表头缺少必需列：{string.Join("、", missing)}" });
            return (rows, errors);
        }

        var seenStudentIds = new HashSet<string>();

        for (var r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;

            string Get(string name) { var cc = row.GetCell(colMap[name]); return cc == null ? "" : formatter.FormatCellValue(cc).Trim(); }

            var name = Get("姓名");
            var studentId = Get("学号");
            var academy = Get("学院");
            var className = Get("班级");

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(studentId)
                && string.IsNullOrWhiteSpace(academy) && string.IsNullOrWhiteSpace(className))
                continue;

            var reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(name)) reasons.Add("姓名不能为空");
            else if (name.Length < 2 || name.Length > 20) reasons.Add("姓名长度需在 2-20 个字符");

            if (string.IsNullOrWhiteSpace(studentId)) reasons.Add("学号不能为空");
            else if (studentId.Length != 10 || !studentId.All(char.IsDigit)) reasons.Add("学号必须是 10 位数字");
            else if (!seenStudentIds.Add(studentId)) reasons.Add("文件内学号重复");

            if (string.IsNullOrWhiteSpace(academy)) reasons.Add("学院不能为空");
            if (string.IsNullOrWhiteSpace(className)) reasons.Add("班级不能为空");
            else if (className.Length < 2 || className.Length > 30) reasons.Add("班级长度需在 2-30 个字符");

            if (reasons.Count > 0)
            {
                errors.Add(new ActivityImportErrorVO
                {
                    RowNumber = r + 1, Name = name, StudentId = studentId,
                    Academy = academy, ClassName = className, Reason = string.Join("；", reasons)
                });
                continue;
            }

            rows.Add(new ActivityParticipantCreateDTO
            {
                Name = name, StudentId = studentId, Academy = academy, ClassName = className
            });
        }

        return (rows, errors);
    }
}
