using System.Diagnostics;
using System.Text;
using iOSClub.DataApi.Services;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace iOSClub.Tests.ServiceTests;

public class ActivityExcelServiceTests
{
    private static readonly string[] Headers = ["姓名", "学号", "学院", "班级"];

    [Fact]
    public void Import_SupportsXls()
    {
        using var workbook = new HSSFWorkbook();
        Fill(workbook, 2);

        using var stream = ToStream(workbook);
        var service = new ActivityExcelService();

        var (rows, errors) = service.Import(stream, "participants.xls");

        Assert.Empty(errors);
        Assert.Equal(2, rows.Count);
        Assert.Equal("测试同学1", rows[0].Name);
        Assert.Equal("2024000001", rows[0].StudentId);
    }

    [Fact]
    public void Import_Supports1000Rows()
    {
        using var workbook = new XSSFWorkbook();
        Fill(workbook, 1000);

        using var stream = ToStream(workbook);
        var service = new ActivityExcelService();
        var stopwatch = Stopwatch.StartNew();

        var (rows, errors) = service.Import(stream, "participants.xlsx");
        stopwatch.Stop();

        Assert.Empty(errors);
        Assert.Equal(1000, rows.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"1000 行解析耗时 {stopwatch.Elapsed.TotalSeconds:F2} 秒，超过 10 秒");
    }

    [Fact]
    public void Import_RejectsCorruptXlsxWithFriendlyError()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not an xlsx file"));
        var service = new ActivityExcelService();

        var exception = Assert.Throws<InvalidDataException>(() => service.Import(stream, "fake.xlsx"));

        Assert.Contains("有效的 .xlsx 或 .xls 文件", exception.Message);
    }

    private static void Fill(IWorkbook workbook, int rowCount)
    {
        var sheet = workbook.CreateSheet("参与者");
        var header = sheet.CreateRow(0);
        for (var i = 0; i < Headers.Length; i++)
            header.CreateCell(i).SetCellValue(Headers[i]);

        for (var i = 0; i < rowCount; i++)
        {
            var row = sheet.CreateRow(i + 1);
            row.CreateCell(0).SetCellValue($"测试同学{i + 1}");
            row.CreateCell(1).SetCellValue($"{(2024000001L + i):0000000000}");
            row.CreateCell(2).SetCellValue("计算机和信息工程学院");
            row.CreateCell(3).SetCellValue($"计科{2300 + i % 100}");
        }
    }

    private static MemoryStream ToStream(IWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.Write(stream, true);
        stream.Position = 0;
        return stream;
    }
}