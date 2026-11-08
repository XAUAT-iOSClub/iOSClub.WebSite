using DocumentFormat.OpenXml.Packaging;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace iOSClub.Tests.FinanceTests;

/// <summary>
/// 财务模块端到端流程：SQLite 建表 → 建材料 → 归集发票 → 生成 docx → 确认报销 → 看板 → 撤销。
/// 使用真实 EF Core / 仓储 / 业务服务，验证跨层契约而不依赖 HTTP 与 Redis。
/// </summary>
public class FinanceWorkflowTests : IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"finance-test-{Guid.NewGuid():N}.db");

    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), $"finance-test-files-{Guid.NewGuid():N}");

    private ClubContext? _context;
    private FinanceRepository? _repository;
    private FinanceStorage? _storage;
    private FinanceDocumentService? _documents;
    private FinanceService? _service;
    private FinanceOptions? _options;
    private DbContextOptions<ClubContext>? _dbOptions;

    public async Task InitializeAsync()
    {
        _options = new FinanceOptions
        {
            StorageRoot = _storageRoot,
            ClubName = "西安建筑科技大学 iOS Club"
        };

        _dbOptions = new DbContextOptionsBuilder<ClubContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ClubContext(_dbOptions);
        await _context.Database.MigrateAsync();

        var factory = new Factory(_dbOptions);
        _repository = new FinanceRepository(factory);
        _storage = new FinanceStorage(Options.Create(_options));
        _documents = new FinanceDocumentService(_storage, Options.Create(_options),
            NullLogger<FinanceDocumentService>.Instance);
        var ocr = new FinanceOcrService(new HttpClient(), Options.Create(_options),
            NullLogger<FinanceOcrService>.Instance);
        _service = new FinanceService(_repository, _documents, _storage, ocr, Options.Create(_options));
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        try { File.Delete(_dbPath); } catch { }
        try { Directory.Delete(_storageRoot, true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FullWorkflow_GeneratesDocx_DeductsAndRollsBack()
    {
        var service = _service!;
        var repository = _repository!;
        var documents = _documents!;
        var actor = new FinanceActor("0000000000", "测试社长", "President");
        var year = FinanceAcademicYear.Current;

        await service.UpdateAccountAsync(new FinanceAccountUpdateDTO
        {
            AcademicYear = year,
            BudgetAmount = 30000m
        }, actor);

        var material = await service.CreateMaterialAsync(new FinanceMaterialUpsertDTO
        {
            ActivityName = "迎新晚会",
            ActivityDate = DateTime.Today,
            ActivityType = "文化艺术",
            Location = "大学生活动中心",
            Headcount = 200,
            Owner = "张三",
            Phone = "13800138000",
            Summary = "迎新晚会活动物料采购",
            ActivityBudget = 800m
        }, actor);

        Assert.StartsWith("CL-", material.Id);
        Assert.Equal(FinanceMaterialStatus.Pending, material.Status);

        await repository.CreateInvoicesAsync([new FinanceInvoiceDO
        {
            MaterialId = material.Id,
            Type = "增值税电子普通发票",
            Code = "044001900111",
            Number = "12345678",
            IssueDate = DateTime.UtcNow.AddDays(-2),
            Buyer = _options!.ClubName,
            Seller = "西安某某科技有限公司",
            Item = "活动物料",
            Qty = 10,
            PriceCents = 10000,
            Unit = "批",
            AmountCents = 100000,
            TaxCents = 3000,
            TotalCents = 103000,
            Status = FinanceInvoiceStatus.Confirmed
        }]);
        await repository.RecomputeMaterialTotalAsync(material.Id);

        var detail = await repository.GetMaterialDetailAsync(material.Id);
        Assert.NotNull(detail);
        Assert.Equal(1, detail!.ConfirmedInvoiceCount);
        Assert.Equal(1030m, detail.TotalAmount);
        Assert.Equal(230m, detail.EstimatedDeduction);

        var generated = await service.GenerateAsync(material.Id, FinanceFormKind.Reimburse, actor.Name);
        Assert.Equal(1, generated.FormVersion);
        Assert.True(generated.HasReimburseForm);

        var entity = await repository.GetMaterialDOAsync(material.Id);
        Assert.NotNull(entity);
        var docx = documents.Read(entity!, FinanceFormKind.Reimburse);
        Assert.True(docx.Length > 0);

        var docxPath = Path.Combine(_storageRoot, "assert.docx");
        await File.WriteAllBytesAsync(docxPath, docx);
        using (var package = WordprocessingDocument.Open(docxPath, false))
        {
            var text = package.MainDocumentPart?.Document?.Body?.InnerText ?? "";
            Assert.Contains("迎新晚会", text);
            Assert.Contains("1030.00", text);
            // 学校固定报销单模板没有“合计大写”单元格，金额以模板的小写合计为准。
        }

        var confirmed = await service.ConfirmAsync(material.Id, actor);
        Assert.Equal(FinanceMaterialStatus.Reimbursed, confirmed.Status);

        var summary = await service.SummaryAsync(year);
        Assert.Equal(230m, summary.UsedAmount);
        Assert.Equal(29770m, summary.RemainingAmount);
        Assert.Equal(1, summary.ReimbursedCount);

        var revoked = await service.RevokeAsync(material.Id, "金额填写有误", actor);
        Assert.Equal(FinanceMaterialStatus.Pending, revoked.Status);

        var afterRevoke = await service.SummaryAsync(year);
        Assert.Equal(0m, afterRevoke.UsedAmount);

        var flows = await repository.GetFlowsAsync(new FinanceFlowQuery { AcademicYear = year });
        Assert.Equal(2, flows.Total);
        Assert.Contains(flows.Items, f => f.Type == FinanceFlowType.Rollback);
        Assert.Contains(flows.Items, f => f.Type == FinanceFlowType.Deduct && f.Status == FinanceFlowStatus.Voided);
    }

    [Fact]
    public async Task DeleteAndRestore_WithinThirtyDays()
    {
        var service = _service!;
        var actor = new FinanceActor("0000000000", "测试社长", "President");

        var material = await service.CreateMaterialAsync(new FinanceMaterialUpsertDTO
        {
            ActivityName = "社团招新",
            ActivityDate = DateTime.Today,
            ActivityBudget = 0m
        }, actor);

        var deleted = await service.DeleteAsync(material.Id);
        Assert.Equal(FinanceMaterialStatus.Deleted, deleted.Status);
        Assert.True(deleted.CanRestore);

        var restored = await service.RestoreAsync(material.Id);
        Assert.Equal(FinanceMaterialStatus.Pending, restored.Status);
    }

    private sealed class Factory(DbContextOptions<ClubContext> options) : IDbContextFactory<ClubContext>
    {
        public ClubContext CreateDbContext() => new(options);
    }
}
