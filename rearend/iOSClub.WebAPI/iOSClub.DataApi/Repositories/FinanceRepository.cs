using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

public class FinanceMaterialQuery
{
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public string? AcademicYear { get; set; }
    public bool IncludeDeleted { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class FinanceInvoiceQuery
{
    public string? Status { get; set; }
    public string? MaterialStatus { get; set; }
    public bool? ExcludeReimbursedMaterial { get; set; }
    public string? MaterialId { get; set; }
    public bool? Unassigned { get; set; }
    public string? Keyword { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class FinanceFlowQuery
{
    public string? AcademicYear { get; set; }
    public string? Type { get; set; }
    public string? Keyword { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class FinanceLogQuery
{
    public string? Action { get; set; }
    public string? Operator { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Result { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public interface IFinanceRepository
{
    // 材料
    Task<PagedResult<FinanceMaterialVO>> GetMaterialsAsync(FinanceMaterialQuery query, CancellationToken ct = default);
    Task<List<FinanceMaterialVO>> GetMaterialsForExportAsync(FinanceBatchExportFilter filter, CancellationToken ct = default);
    Task<FinanceMaterialDO?> GetMaterialDOAsync(string id, bool includeInvoices = false, CancellationToken ct = default);
    Task<FinanceMaterialDetailVO?> GetMaterialDetailAsync(string id, CancellationToken ct = default);
    Task<FinanceMaterialDO> CreateMaterialAsync(FinanceMaterialDO model, CancellationToken ct = default);
    Task<bool> UpdateMaterialAsync(FinanceMaterialDO model, CancellationToken ct = default);
    Task<bool> SetMaterialStatusAsync(string id, string status, DateTime? confirmedAt, string? confirmedBy,
        string? revokeReason, DateTime? deletedAt, CancellationToken ct = default);
    Task<bool> SetMaterialFormAsync(string id, string kind, string path, int version, DateTime generatedAt,
        CancellationToken ct = default);
    Task<long> RecomputeMaterialTotalAsync(string id, CancellationToken ct = default);
    Task<FinanceFlowDO?> ConfirmMaterialAsync(string id, long deductionCents, string? operatorId,
        string? operatorName, DateTime now, CancellationToken ct = default);
    Task<FinanceFlowDO?> RevokeMaterialAsync(string id, string reason, string? operatorId, string? operatorName,
        DateTime now, CancellationToken ct = default);

    // 发票
    Task<PagedResult<FinanceInvoiceVO>> GetInvoicesAsync(FinanceInvoiceQuery query, CancellationToken ct = default);
    Task<FinanceInvoiceDO?> GetInvoiceDOAsync(string id, CancellationToken ct = default);
    Task<FinanceInvoiceVO?> GetInvoiceVOAsync(string id, CancellationToken ct = default);
    Task<List<FinanceInvoiceDO>> GetInvoiceDOsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task<List<FinanceInvoiceDO>> GetMaterialInvoiceDOsAsync(string materialId, bool confirmedOnly,
        CancellationToken ct = default);
    Task<List<FinanceInvoiceVO>> GetInvoicesByMaterialAsync(string materialId, CancellationToken ct = default);
    Task<List<FinanceInvoiceVO>> GetAssignableInvoicesAsync(string? keyword, int take, CancellationToken ct = default);
    Task<List<FinanceInvoiceDO>> FindInvoicesByNumberAsync(string number, string? excludeId,
        CancellationToken ct = default);
    Task CreateInvoicesAsync(IEnumerable<FinanceInvoiceDO> invoices, CancellationToken ct = default);
    Task<bool> UpdateInvoiceAsync(FinanceInvoiceDO invoice, CancellationToken ct = default);
    Task<bool> SoftDeleteInvoiceAsync(string id, DateTime now, CancellationToken ct = default);
    Task<int> AssignInvoicesAsync(string materialId, List<string> invoiceIds, CancellationToken ct = default);
    Task<int> RemoveInvoicesAsync(string materialId, List<string> invoiceIds, CancellationToken ct = default);
    Task<int> CountInvoicesAsync(string materialId, bool confirmedOnly, CancellationToken ct = default);
    Task<long> SumInvoicesAsync(string materialId, bool confirmedOnly, CancellationToken ct = default);

    // 学年经费
    Task<FinanceAccountDO?> GetAccountAsync(string academicYear, CancellationToken ct = default);
    Task<List<FinanceAccountVO>> GetAccountsAsync(CancellationToken ct = default);
    Task<FinanceAccountDO> UpsertAccountAsync(FinanceAccountDO account, CancellationToken ct = default);

    // 经费流水
    Task<FinanceFlowDO> AddFlowAsync(FinanceFlowDO flow, CancellationToken ct = default);
    Task<bool> VoidFlowAsync(string flowId, CancellationToken ct = default);
    Task<FinanceFlowDO?> GetActiveDeductFlowAsync(string materialId, CancellationToken ct = default);
    Task<PagedResult<FinanceFlowVO>> GetFlowsAsync(FinanceFlowQuery query, CancellationToken ct = default);
    Task<List<FinanceFlowVO>> GetRecentFlowsAsync(string academicYear, int take, CancellationToken ct = default);
    Task<long> SumUsedAsync(string academicYear, CancellationToken ct = default);

    // 日志
    Task AddLogAsync(FinanceLogDO log, CancellationToken ct = default);
    Task<PagedResult<FinanceLogVO>> GetLogsAsync(FinanceLogQuery query, CancellationToken ct = default);
    Task<List<FinanceLogVO>> GetLogsByTargetAsync(string targetType, string targetId, int take,
        CancellationToken ct = default);
    Task<List<FinanceLogVO>> GetLogsForExportAsync(FinanceLogQuery query, int max, CancellationToken ct = default);

    // 模板
    Task<List<FinanceTemplateVO>> GetTemplatesAsync(CancellationToken ct = default);
    Task<FinanceTemplateDO?> GetTemplateAsync(string id, CancellationToken ct = default);
    Task<FinanceTemplateDO?> GetEnabledTemplateAsync(string kind, CancellationToken ct = default);
    Task<int> NextTemplateVersionAsync(string kind, CancellationToken ct = default);
    Task<FinanceTemplateDO> AddTemplateAsync(FinanceTemplateDO template, CancellationToken ct = default);
    Task<bool> EnableTemplateAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteTemplateAsync(string id, CancellationToken ct = default);

    // 看板
    Task<(int Reimbursed, int Pending)> GetMaterialCountsAsync(string academicYear, CancellationToken ct = default);
    Task<long> SumPendingAmountAsync(string academicYear, CancellationToken ct = default);
}

/// <summary>批量导出用的筛选条件（与 DTO 解耦，方便仓库层复用）。</summary>
public class FinanceBatchExportFilter
{
    public List<string>? Ids { get; set; }
    public string? MaterialNo { get; set; }
    public string? ActivityName { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Status { get; set; }
    public string? AcademicYear { get; set; }
}

public class FinanceRepository(IDbContextFactory<ClubContext> factory) : IFinanceRepository
{
    private static int NormalizePage(int page) => page < 1 ? 1 : page;
    private static int NormalizePageSize(int pageSize) => pageSize is < 1 or > 100 ? 20 : pageSize;

    public async Task<PagedResult<FinanceMaterialVO>> GetMaterialsAsync(FinanceMaterialQuery query,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceMaterials.AsNoTracking()
            .Include(m => m.Invoices.Where(i => !i.IsDeleted))
            .AsQueryable();

        if (!query.IncludeDeleted && !string.Equals(query.Status, FinanceMaterialStatus.Deleted,
                StringComparison.OrdinalIgnoreCase))
            q = q.Where(m => m.Status != FinanceMaterialStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(m => m.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.AcademicYear)) q = q.Where(m => m.AcademicYear == query.AcademicYear);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(m => (m.ActivityName != null && m.ActivityName.Contains(keyword)) || m.Id.Contains(keyword) ||
                             m.Invoices.Any(i => !i.IsDeleted &&
                                 ((i.Number != null && i.Number.Contains(keyword)) ||
                                  (i.Code != null && i.Code.Contains(keyword)) ||
                                  (i.Seller != null && i.Seller.Contains(keyword)) ||
                                  (i.Item != null && i.Item.Contains(keyword)))));
        }

        var total = await q.CountAsync(ct);
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var items = await q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<FinanceMaterialVO>
        {
            Items = items.Select(m => MapMaterial(m)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<FinanceMaterialVO>> GetMaterialsForExportAsync(FinanceBatchExportFilter filter,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceMaterials.AsNoTracking()
            .Include(m => m.Invoices.Where(i => !i.IsDeleted))
            .Where(m => m.Status != FinanceMaterialStatus.Deleted);

        if (filter.Ids is { Count: > 0 }) q = q.Where(m => filter.Ids.Contains(m.Id));
        if (!string.IsNullOrWhiteSpace(filter.MaterialNo)) q = q.Where(m => m.Id.Contains(filter.MaterialNo.Trim()));
        if (!string.IsNullOrWhiteSpace(filter.ActivityName))
        {
            var name = filter.ActivityName.Trim();
            q = q.Where(m => m.ActivityName != null && m.ActivityName.Contains(name));
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.ToUniversalTime();
            q = q.Where(m => m.CreatedAt >= from);
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value.ToUniversalTime();
            q = q.Where(m => m.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(m => m.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.AcademicYear)) q = q.Where(m => m.AcademicYear == filter.AcademicYear);

        var items = await q.OrderByDescending(m => m.CreatedAt).Take(201).ToListAsync(ct);
        return items.Select(m => MapMaterial(m)).ToList();
    }

    public async Task<FinanceMaterialDO?> GetMaterialDOAsync(string id, bool includeInvoices = false,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceMaterials.AsNoTracking().AsQueryable();
        if (includeInvoices) q = q.Include(m => m.Invoices.Where(i => !i.IsDeleted));
        return await q.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<FinanceMaterialDetailVO?> GetMaterialDetailAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var material = await ctx.FinanceMaterials.AsNoTracking()
            .Include(m => m.Invoices.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (material == null) return null;

        var vo = MapMaterial(material);
        var detail = new FinanceMaterialDetailVO
        {
            Id = vo.Id,
            ActivityName = vo.ActivityName,
            ActivityDate = vo.ActivityDate,
            ActivityType = vo.ActivityType,
            Location = vo.Location,
            Headcount = vo.Headcount,
            Owner = vo.Owner,
            Phone = vo.Phone,
            Summary = vo.Summary,
            ActivityBudget = vo.ActivityBudget,
            TotalAmount = vo.TotalAmount,
            Status = vo.Status,
            StatusLabel = vo.StatusLabel,
            Remark = vo.Remark,
            AcademicYear = vo.AcademicYear,
            FormVersion = vo.FormVersion,
            HasApplyForm = vo.HasApplyForm,
            HasReimburseForm = vo.HasReimburseForm,
            GeneratedAt = vo.GeneratedAt,
            InvoiceCount = vo.InvoiceCount,
            ConfirmedInvoiceCount = vo.ConfirmedInvoiceCount,
            UnconfirmedAmount = vo.UnconfirmedAmount,
            ConfirmedAt = vo.ConfirmedAt,
            ConfirmedBy = vo.ConfirmedBy,
            DeletedAt = vo.DeletedAt,
            CanRestore = vo.CanRestore,
            RevokeReason = vo.RevokeReason,
            CreatedBy = vo.CreatedBy,
            CreatedAt = vo.CreatedAt,
            UpdatedAt = vo.UpdatedAt,
            Invoices = material.Invoices.OrderBy(i => i.CreatedAt).Select(MapInvoice).ToList(),
            EstimatedDeduction = FinanceMoney.ToYuan(
                FinanceFundRules.ComputeDeduction(material.TotalAmountCents, material.ActivityBudgetCents))
        };

        return detail;
    }

    public async Task<FinanceMaterialDO> CreateMaterialAsync(FinanceMaterialDO model, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var year = DateTime.Now.Year;
        var prefix = $"CL-{year}-";

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var last = await ctx.FinanceMaterials.AsNoTracking()
                .Where(m => m.Id.StartsWith(prefix))
                .OrderByDescending(m => m.Id)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            var next = 1;
            if (last != null && int.TryParse(last[prefix.Length..], out var parsed)) next = parsed + 1;
            model.Id = $"{prefix}{next:D4}";

            ctx.FinanceMaterials.Add(model);
            try
            {
                await ctx.SaveChangesAsync(ct);
                return model;
            }
            catch (DbUpdateException) when (attempt < 7)
            {
                // 并发下编号可能撞车：脱离跟踪后重新取号
                ctx.Entry(model).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("生成材料编号失败，请稍后重试");
    }

    public async Task<bool> UpdateMaterialAsync(FinanceMaterialDO model, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceMaterials.FirstOrDefaultAsync(m => m.Id == model.Id, ct);
        if (entity == null) return false;

        entity.ActivityName = model.ActivityName;
        entity.ActivityDate = model.ActivityDate;
        entity.ActivityType = model.ActivityType;
        entity.Location = model.Location;
        entity.Headcount = model.Headcount;
        entity.Owner = model.Owner;
        entity.Phone = model.Phone;
        entity.Summary = model.Summary;
        entity.ActivityBudgetCents = model.ActivityBudgetCents;
        entity.Remark = model.Remark;
        entity.AcademicYear = model.AcademicYear;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> SetMaterialStatusAsync(string id, string status, DateTime? confirmedAt,
        string? confirmedBy, string? revokeReason, DateTime? deletedAt, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceMaterials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (entity == null) return false;

        entity.Status = status;
        entity.ConfirmedAt = confirmedAt;
        entity.ConfirmedBy = confirmedBy;
        entity.RevokeReason = revokeReason;
        entity.DeletedAt = deletedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> SetMaterialFormAsync(string id, string kind, string path, int version,
        DateTime generatedAt, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceMaterials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (entity == null) return false;

        if (kind == FinanceFormKind.Apply) entity.ApplyFormPath = path;
        else entity.ReimburseFormPath = path;

        entity.FormVersion = version;
        entity.GeneratedAt = generatedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<long> RecomputeMaterialTotalAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var material = await ctx.FinanceMaterials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null) return 0;

        var total = await ctx.FinanceInvoices
            .Where(i => i.MaterialId == id && !i.IsDeleted && i.Status == FinanceInvoiceStatus.Confirmed)
            .SumAsync(i => (long?)i.TotalCents, ct) ?? 0;

        material.TotalAmountCents = total;
        material.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
        return total;
    }

    public async Task<FinanceFlowDO?> ConfirmMaterialAsync(string id, long deductionCents, string? operatorId,
        string? operatorName, DateTime now, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var material = await ctx.FinanceMaterials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null) return null;

        material.Status = FinanceMaterialStatus.Reimbursed;
        material.ConfirmedAt = now;
        material.ConfirmedBy = operatorId;
        material.RevokeReason = null;
        material.UpdatedAt = now;

        var flow = new FinanceFlowDO
        {
            MaterialId = material.Id,
            MaterialNo = material.Id,
            ActivityName = material.ActivityName,
            AcademicYear = material.AcademicYear,
            AmountCents = deductionCents,
            Type = FinanceFlowType.Deduct,
            Status = FinanceFlowStatus.Active,
            OperatorId = operatorId,
            OperatorName = operatorName,
            CreatedAt = now
        };
        ctx.FinanceFlows.Add(flow);
        await ctx.SaveChangesAsync(ct);
        return flow;
    }

    public async Task<FinanceFlowDO?> RevokeMaterialAsync(string id, string reason, string? operatorId,
        string? operatorName, DateTime now, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var material = await ctx.FinanceMaterials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null) return null;

        var deduct = await ctx.FinanceFlows
            .Where(f => f.MaterialId == id && f.Type == FinanceFlowType.Deduct &&
                        f.Status == FinanceFlowStatus.Active)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (deduct == null) return null;

        deduct.Status = FinanceFlowStatus.Voided;

        material.Status = FinanceMaterialStatus.Pending;
        material.ConfirmedAt = null;
        material.ConfirmedBy = null;
        material.RevokeReason = reason;
        material.UpdatedAt = now;

        var rollback = new FinanceFlowDO
        {
            MaterialId = material.Id,
            MaterialNo = material.Id,
            ActivityName = material.ActivityName,
            AcademicYear = deduct.AcademicYear,
            AmountCents = deduct.AmountCents,
            Type = FinanceFlowType.Rollback,
            Status = FinanceFlowStatus.Active,
            Reason = reason,
            OperatorId = operatorId,
            OperatorName = operatorName,
            CreatedAt = now
        };
        ctx.FinanceFlows.Add(rollback);
        await ctx.SaveChangesAsync(ct);
        return rollback;
    }

    public async Task<PagedResult<FinanceInvoiceVO>> GetInvoicesAsync(FinanceInvoiceQuery query,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceInvoices.AsNoTracking()
            .Include(i => i.Material)
            .Where(i => !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(i => i.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.MaterialStatus))
            q = q.Where(i => i.Material != null && i.Material.Status == query.MaterialStatus);
        if (query.ExcludeReimbursedMaterial == true)
            q = q.Where(i => i.Material == null || i.Material.Status != FinanceMaterialStatus.Reimbursed);
        if (!string.IsNullOrWhiteSpace(query.MaterialId)) q = q.Where(i => i.MaterialId == query.MaterialId);
        if (query.Unassigned == true) q = q.Where(i => i.MaterialId == null);
        if (query.From.HasValue)
        {
            var from = query.From.Value.ToUniversalTime();
            q = q.Where(i => i.IssueDate >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value.ToUniversalTime();
            q = q.Where(i => i.IssueDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(i => (i.Number != null && i.Number.Contains(keyword)) ||
                             (i.Code != null && i.Code.Contains(keyword)) ||
                             (i.Seller != null && i.Seller.Contains(keyword)) ||
                             (i.Item != null && i.Item.Contains(keyword)) ||
                             (i.Buyer != null && i.Buyer.Contains(keyword)));
        }

        var total = await q.CountAsync(ct);
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var items = await q.OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<FinanceInvoiceVO>
        {
            Items = items.Select(MapInvoice).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FinanceInvoiceDO?> GetInvoiceDOAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);
    }

    public async Task<FinanceInvoiceVO?> GetInvoiceVOAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var invoice = await ctx.FinanceInvoices.AsNoTracking()
            .Include(i => i.Material)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);
        return invoice == null ? null : MapInvoice(invoice);
    }

    public async Task<List<FinanceInvoiceDO>> GetMaterialInvoiceDOsAsync(string materialId, bool confirmedOnly,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceInvoices.AsNoTracking()
            .Where(i => i.MaterialId == materialId && !i.IsDeleted);
        if (confirmedOnly) q = q.Where(i => i.Status == FinanceInvoiceStatus.Confirmed);
        return await q.OrderBy(i => i.CreatedAt).ToListAsync(ct);
    }

    public async Task<List<FinanceInvoiceDO>> GetInvoiceDOsAsync(IEnumerable<string> ids,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var list = ids.Distinct().ToList();
        return await ctx.FinanceInvoices.AsNoTracking()
            .Where(i => list.Contains(i.Id) && !i.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<List<FinanceInvoiceVO>> GetInvoicesByMaterialAsync(string materialId,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var items = await ctx.FinanceInvoices.AsNoTracking()
            .Include(i => i.Material)
            .Where(i => i.MaterialId == materialId && !i.IsDeleted)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);
        return items.Select(MapInvoice).ToList();
    }

    public async Task<List<FinanceInvoiceVO>> GetAssignableInvoicesAsync(string? keyword, int take,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceInvoices.AsNoTracking()
            .Include(i => i.Material)
            .Where(i => !i.IsDeleted && i.MaterialId == null &&
                        i.Status == FinanceInvoiceStatus.Confirmed);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(i => (i.Number != null && i.Number.Contains(k)) ||
                             (i.Seller != null && i.Seller.Contains(k)) ||
                             (i.Item != null && i.Item.Contains(k)));
        }

        var items = await q.OrderByDescending(i => i.CreatedAt).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return items.Select(MapInvoice).ToList();
    }

    public async Task<List<FinanceInvoiceDO>> FindInvoicesByNumberAsync(string number,
        string? excludeId, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceInvoices.AsNoTracking()
            .Include(i => i.Material)
            .Where(i => !i.IsDeleted && i.Number == number &&
                        (excludeId == null || i.Id != excludeId))
            .ToListAsync(ct);
    }

    public async Task CreateInvoicesAsync(IEnumerable<FinanceInvoiceDO> invoices, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        ctx.FinanceInvoices.AddRange(invoices);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateInvoiceAsync(FinanceInvoiceDO invoice, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceInvoices.FirstOrDefaultAsync(i => i.Id == invoice.Id, ct);
        if (entity == null) return false;

        entity.Type = invoice.Type;
        entity.Code = invoice.Code;
        entity.Number = invoice.Number;
        entity.IssueDate = invoice.IssueDate;
        entity.Buyer = invoice.Buyer;
        entity.Seller = invoice.Seller;
        entity.Item = invoice.Item;
        entity.Qty = invoice.Qty;
        entity.PriceCents = invoice.PriceCents;
        entity.Unit = invoice.Unit;
        entity.AmountCents = invoice.AmountCents;
        entity.TaxCents = invoice.TaxCents;
        entity.TotalCents = invoice.TotalCents;
        entity.ConfidenceJson = invoice.ConfidenceJson;
        entity.Status = invoice.Status;
        entity.RecognitionError = invoice.RecognitionError;
        entity.RecognizedAt = invoice.RecognizedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> SoftDeleteInvoiceAsync(string id, DateTime now, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceInvoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.MaterialId = null;
        entity.UpdatedAt = now;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<int> AssignInvoicesAsync(string materialId, List<string> invoiceIds,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var ids = invoiceIds.Distinct().ToList();
        var invoices = await ctx.FinanceInvoices
            .Where(i => ids.Contains(i.Id) && !i.IsDeleted && i.MaterialId == null)
            .ToListAsync(ct);

        foreach (var invoice in invoices)
        {
            invoice.MaterialId = materialId;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync(ct);
        return invoices.Count;
    }

    public async Task<int> RemoveInvoicesAsync(string materialId, List<string> invoiceIds,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var ids = invoiceIds.Distinct().ToList();
        var invoices = await ctx.FinanceInvoices
            .Where(i => ids.Contains(i.Id) && !i.IsDeleted && i.MaterialId == materialId)
            .ToListAsync(ct);

        foreach (var invoice in invoices)
        {
            invoice.MaterialId = null;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync(ct);
        return invoices.Count;
    }

    public async Task<int> CountInvoicesAsync(string materialId, bool confirmedOnly, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceInvoices.Where(i => i.MaterialId == materialId && !i.IsDeleted);
        if (confirmedOnly) q = q.Where(i => i.Status == FinanceInvoiceStatus.Confirmed);
        return await q.CountAsync(ct);
    }

    public async Task<long> SumInvoicesAsync(string materialId, bool confirmedOnly, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceInvoices.Where(i => i.MaterialId == materialId && !i.IsDeleted);
        if (confirmedOnly) q = q.Where(i => i.Status == FinanceInvoiceStatus.Confirmed);
        return await q.SumAsync(i => (long?)i.TotalCents, ct) ?? 0;
    }

    public async Task<FinanceAccountDO?> GetAccountAsync(string academicYear, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AcademicYear == academicYear, ct);
    }

    public async Task<List<FinanceAccountVO>> GetAccountsAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var current = FinanceAcademicYear.Current;
        var items = await ctx.FinanceAccounts.AsNoTracking()
            .OrderByDescending(a => a.AcademicYear)
            .ToListAsync(ct);

        return items.Select(a => new FinanceAccountVO
        {
            Id = a.Id,
            AcademicYear = a.AcademicYear,
            BudgetAmount = FinanceMoney.ToYuan(a.BudgetAmountCents),
            Remark = a.Remark,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            Editable = a.AcademicYear == current
        }).ToList();
    }

    public async Task<FinanceAccountDO> UpsertAccountAsync(FinanceAccountDO account, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceAccounts
            .FirstOrDefaultAsync(a => a.AcademicYear == account.AcademicYear, ct);

        if (entity == null)
        {
            ctx.FinanceAccounts.Add(account);
            await ctx.SaveChangesAsync(ct);
            return account;
        }

        entity.BudgetAmountCents = account.BudgetAmountCents;
        entity.Remark = account.Remark;
        entity.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<FinanceFlowDO> AddFlowAsync(FinanceFlowDO flow, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        ctx.FinanceFlows.Add(flow);
        await ctx.SaveChangesAsync(ct);
        return flow;
    }

    public async Task<bool> VoidFlowAsync(string flowId, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceFlows.FirstOrDefaultAsync(f => f.Id == flowId, ct);
        if (entity == null) return false;
        entity.Status = FinanceFlowStatus.Voided;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<FinanceFlowDO?> GetActiveDeductFlowAsync(string materialId, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceFlows.AsNoTracking()
            .Where(f => f.MaterialId == materialId && f.Type == FinanceFlowType.Deduct &&
                        f.Status == FinanceFlowStatus.Active)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<FinanceFlowVO>> GetFlowsAsync(FinanceFlowQuery query,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceFlows.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.AcademicYear)) q = q.Where(f => f.AcademicYear == query.AcademicYear);
        if (!string.IsNullOrWhiteSpace(query.Type)) q = q.Where(f => f.Type == query.Type);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(f => (f.ActivityName != null && f.ActivityName.Contains(keyword)) ||
                             f.MaterialId.Contains(keyword));
        }

        if (query.From.HasValue) q = q.Where(f => f.CreatedAt >= query.From.Value.ToUniversalTime());
        if (query.To.HasValue) q = q.Where(f => f.CreatedAt <= query.To.Value.ToUniversalTime());

        var total = await q.CountAsync(ct);
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var items = await q.OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<FinanceFlowVO>
        {
            Items = items.Select(MapFlow).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<FinanceFlowVO>> GetRecentFlowsAsync(string academicYear, int take,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var items = await ctx.FinanceFlows.AsNoTracking()
            .Where(f => f.AcademicYear == academicYear)
            .OrderByDescending(f => f.CreatedAt)
            .Take(Math.Clamp(take, 1, 20))
            .ToListAsync(ct);
        return items.Select(MapFlow).ToList();
    }

    public async Task<long> SumUsedAsync(string academicYear, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceFlows.AsNoTracking()
            .Where(f => f.AcademicYear == academicYear && f.Type == FinanceFlowType.Deduct &&
                        f.Status == FinanceFlowStatus.Active)
            .SumAsync(f => (long?)f.AmountCents, ct) ?? 0;
    }

    public async Task AddLogAsync(FinanceLogDO log, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        ctx.FinanceLogs.Add(log);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<FinanceLogVO>> GetLogsAsync(FinanceLogQuery query, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = BuildLogQuery(ctx, query);
        var total = await q.CountAsync(ct);
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var items = await q.OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<FinanceLogVO>
        {
            Items = items.Select(MapLog).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<FinanceLogVO>> GetLogsByTargetAsync(string targetType, string targetId, int take,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var items = await ctx.FinanceLogs.AsNoTracking()
            .Where(l => l.TargetType == targetType && l.TargetId == targetId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        return items.Select(MapLog).ToList();
    }

    public async Task<List<FinanceLogVO>> GetLogsForExportAsync(FinanceLogQuery query, int max,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var items = await BuildLogQuery(ctx, query)
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(max, 1, 50000))
            .ToListAsync(ct);
        return items.Select(MapLog).ToList();
    }

    public async Task<List<FinanceTemplateVO>> GetTemplatesAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var items = await ctx.FinanceTemplates.AsNoTracking()
            .OrderBy(t => t.Kind)
            .ThenByDescending(t => t.Version)
            .ToListAsync(ct);

        return items.Select(t => new FinanceTemplateVO
        {
            Id = t.Id,
            Kind = t.Kind,
            KindLabel = FinanceFormKind.Label(t.Kind),
            Version = t.Version,
            OriginalFileName = t.OriginalFileName,
            Enabled = t.Enabled,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy
        }).ToList();
    }

    public async Task<FinanceTemplateDO?> GetTemplateAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<FinanceTemplateDO?> GetEnabledTemplateAsync(string kind, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceTemplates.AsNoTracking()
            .Where(t => t.Kind == kind && t.Enabled)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> NextTemplateVersionAsync(string kind, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var max = await ctx.FinanceTemplates.Where(t => t.Kind == kind)
            .MaxAsync(t => (int?)t.Version, ct) ?? 0;
        return max + 1;
    }

    public async Task<FinanceTemplateDO> AddTemplateAsync(FinanceTemplateDO template, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        if (template.Enabled)
        {
            var others = await ctx.FinanceTemplates
                .Where(t => t.Kind == template.Kind && t.Enabled)
                .ToListAsync(ct);
            foreach (var other in others) other.Enabled = false;
        }

        ctx.FinanceTemplates.Add(template);
        await ctx.SaveChangesAsync(ct);
        return template;
    }

    public async Task<bool> EnableTemplateAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;

        var others = await ctx.FinanceTemplates
            .Where(t => t.Kind == entity.Kind && t.Id != id && t.Enabled)
            .ToListAsync(ct);
        foreach (var other in others) other.Enabled = false;

        entity.Enabled = true;
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> DeleteTemplateAsync(string id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var entity = await ctx.FinanceTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;
        ctx.FinanceTemplates.Remove(entity);
        return await ctx.SaveChangesAsync(ct) > 0;
    }

    public async Task<(int Reimbursed, int Pending)> GetMaterialCountsAsync(string academicYear,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var q = ctx.FinanceMaterials.Where(m => m.AcademicYear == academicYear &&
                                                m.Status != FinanceMaterialStatus.Deleted);
        var reimbursed = await q.CountAsync(m => m.Status == FinanceMaterialStatus.Reimbursed, ct);
        var pending = await q.CountAsync(m => m.Status == FinanceMaterialStatus.Pending, ct);
        return (reimbursed, pending);
    }

    public async Task<long> SumPendingAmountAsync(string academicYear, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.FinanceMaterials
            .Where(m => m.AcademicYear == academicYear && m.Status == FinanceMaterialStatus.Pending)
            .SumAsync(m => (long?)m.TotalAmountCents, ct) ?? 0;
    }

    private static IQueryable<FinanceLogDO> BuildLogQuery(ClubContext ctx, FinanceLogQuery query)
    {
        var q = ctx.FinanceLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(l => l.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.Result)) q = q.Where(l => l.Result == query.Result);
        if (!string.IsNullOrWhiteSpace(query.TargetType)) q = q.Where(l => l.TargetType == query.TargetType);
        if (!string.IsNullOrWhiteSpace(query.TargetId)) q = q.Where(l => l.TargetId == query.TargetId);
        if (!string.IsNullOrWhiteSpace(query.Operator))
        {
            var op = query.Operator.Trim();
            q = q.Where(l => (l.OperatorName != null && l.OperatorName.Contains(op)) ||
                             (l.OperatorId != null && l.OperatorId.Contains(op)));
        }

        if (query.From.HasValue) q = q.Where(l => l.CreatedAt >= query.From.Value.ToUniversalTime());
        if (query.To.HasValue) q = q.Where(l => l.CreatedAt <= query.To.Value.ToUniversalTime());
        return q;
    }

    internal static FinanceInvoiceVO MapInvoice(FinanceInvoiceDO invoice)
    {
        return new FinanceInvoiceVO
        {
            Id = invoice.Id,
            MaterialId = invoice.MaterialId,
            MaterialNo = invoice.Material?.Id,
            ActivityName = invoice.Material?.ActivityName,
            MaterialStatus = invoice.Material?.Status,
            MaterialStatusLabel = invoice.Material == null
                ? null
                : FinanceMaterialStatus.Label(invoice.Material.Status),
            Type = invoice.Type,
            Code = invoice.Code,
            Number = invoice.Number,
            IssueDate = invoice.IssueDate,
            Buyer = invoice.Buyer,
            Seller = invoice.Seller,
            Item = invoice.Item,
            Qty = invoice.Qty,
            Price = invoice.PriceCents.HasValue ? FinanceMoney.ToYuan(invoice.PriceCents.Value) : null,
            Unit = invoice.Unit,
            Amount = FinanceMoney.ToYuan(invoice.AmountCents),
            Tax = FinanceMoney.ToYuan(invoice.TaxCents),
            Total = FinanceMoney.ToYuan(invoice.TotalCents),
            Confidence = ParseConfidence(invoice.ConfidenceJson),
            Status = invoice.Status,
            StatusLabel = FinanceInvoiceStatus.Label(invoice.Status),
            Confirmed = invoice.Status == FinanceInvoiceStatus.Confirmed,
            HasFile = !string.IsNullOrWhiteSpace(invoice.FilePath),
            FileName = invoice.OriginalFileName,
            ContentType = invoice.ContentType,
            RecognitionError = invoice.RecognitionError,
            RecognizedAt = invoice.RecognizedAt,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }

    internal static FinanceMaterialVO MapMaterial(FinanceMaterialDO material)
    {
        var invoices = material.Invoices.Where(i => !i.IsDeleted).ToList();
        var confirmed = invoices.Where(i => i.Status == FinanceInvoiceStatus.Confirmed).ToList();
        var confirmedTotal = confirmed.Sum(i => i.TotalCents);
        var unconfirmedTotal = invoices.Where(i => i.Status != FinanceInvoiceStatus.Confirmed)
            .Sum(i => i.TotalCents);

        return new FinanceMaterialVO
        {
            Id = material.Id,
            ActivityName = material.ActivityName,
            ActivityDate = material.ActivityDate,
            ActivityType = material.ActivityType,
            Location = material.Location,
            Headcount = material.Headcount,
            Owner = material.Owner,
            Phone = material.Phone,
            Summary = material.Summary,
            ActivityBudget = FinanceMoney.ToYuan(material.ActivityBudgetCents),
            TotalAmount = FinanceMoney.ToYuan(confirmedTotal),
            Status = material.Status,
            StatusLabel = FinanceMaterialStatus.Label(material.Status),
            Remark = material.Remark,
            AcademicYear = material.AcademicYear,
            FormVersion = material.FormVersion,
            HasApplyForm = !string.IsNullOrWhiteSpace(material.ApplyFormPath),
            HasReimburseForm = !string.IsNullOrWhiteSpace(material.ReimburseFormPath),
            GeneratedAt = material.GeneratedAt,
            InvoiceCount = invoices.Count,
            ConfirmedInvoiceCount = confirmed.Count,
            UnconfirmedAmount = FinanceMoney.ToYuan(unconfirmedTotal),
            ConfirmedAt = material.ConfirmedAt,
            ConfirmedBy = material.ConfirmedBy,
            DeletedAt = material.DeletedAt,
            CanRestore = material.Status == FinanceMaterialStatus.Deleted &&
                         material.DeletedAt.HasValue &&
                         material.DeletedAt.Value >= DateTime.UtcNow.AddDays(-30),
            RevokeReason = material.RevokeReason,
            CreatedBy = material.CreatedBy,
            CreatedAt = material.CreatedAt,
            UpdatedAt = material.UpdatedAt
        };
    }

    internal static FinanceFlowVO MapFlow(FinanceFlowDO flow) => new()
    {
        Id = flow.Id,
        MaterialId = flow.MaterialId,
        MaterialNo = string.IsNullOrWhiteSpace(flow.MaterialNo) ? flow.MaterialId : flow.MaterialNo,
        ActivityName = flow.ActivityName,
        AcademicYear = flow.AcademicYear,
        Amount = FinanceMoney.ToYuan(flow.AmountCents),
        Type = flow.Type,
        TypeLabel = FinanceFlowType.Label(flow.Type),
        Status = flow.Status,
        Reason = flow.Reason,
        OperatorId = flow.OperatorId,
        OperatorName = flow.OperatorName,
        CreatedAt = flow.CreatedAt
    };

    internal static FinanceLogVO MapLog(FinanceLogDO log) => new()
    {
        Id = log.Id,
        OperatorId = log.OperatorId,
        OperatorName = log.OperatorName,
        Role = log.Role,
        RoleLabel = FinanceRoles.Label(log.Role),
        Ip = log.Ip,
        UserAgent = log.UserAgent,
        Action = log.Action,
        TargetType = log.TargetType,
        TargetId = log.TargetId,
        Before = log.BeforeJson,
        After = log.AfterJson,
        Result = log.Result,
        Message = log.Message,
        CreatedAt = log.CreatedAt
    };

    private static Dictionary<string, double> ParseConfidence(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, double>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ??
                   new Dictionary<string, double>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, double>();
        }
    }
}
