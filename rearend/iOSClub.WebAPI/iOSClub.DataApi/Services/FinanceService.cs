using System.Text.Json;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Exceptions;
using iOSClub.DataApi.Repositories;
using Microsoft.Extensions.Options;

namespace iOSClub.DataApi.Services;

/// <summary>当前操作人。财务模块没有登录环节，身份全部来自网站 JWT。</summary>
public record FinanceActor(string? UserId, string Name, string Role, string? Ip = null, string? UserAgent = null);

/// <summary>
/// 财务业务规则：发票校验、材料状态流转、经费计算与看板。
/// 数据访问统一走 <see cref="IFinanceRepository"/>，表单生成走 <see cref="IFinanceDocumentService"/>。
/// </summary>
public class FinanceService(
    IFinanceRepository repository,
    IFinanceDocumentService documentService,
    IFinanceStorage storage,
    IFinanceOcrService ocrService,
    IOptions<FinanceOptions> options)
{
    private readonly FinanceOptions _options = options.Value;

    // ===== 发票 =====

    public async Task<FinanceValidationVO> ValidateInvoiceAsync(FinanceInvoiceDO invoice, string? excludeId,
        CancellationToken ct = default)
    {
        var result = new FinanceValidationVO();
        var material = string.IsNullOrWhiteSpace(invoice.MaterialId)
            ? null
            : await repository.GetMaterialDOAsync(invoice.MaterialId, false, ct);

        if (invoice.IssueDate.HasValue &&
            FinanceTime.ToChina(invoice.IssueDate.Value).Date > FinanceTime.Now.Date)
            result.Errors.Add("开票日期不能晚于当天");

        if (invoice.AmountCents < 0 || invoice.TaxCents < 0 || invoice.TotalCents < 0)
            result.Errors.Add("金额、税额与价税合计不能为负数");

        if (invoice.Qty is < 0)
            result.Errors.Add("数量不能为负数");

        if (invoice.PriceCents is < 0)
            result.Errors.Add("单价不能为负数");

        if (invoice.TotalCents <= 0)
            result.Errors.Add("价税合计必须大于 0");

        var number = invoice.Number?.Trim();
        if (!string.IsNullOrWhiteSpace(number))
        {
            var duplicates = await repository.FindInvoicesByNumberAsync(number, excludeId, ct);
            foreach (var duplicate in duplicates)
            {
                result.Errors.Add($"发票号码 {number} 已存在（{duplicate.OriginalFileName ?? "已上传"}），同一张发票只能保留一条记录");
            }
        }

        if (material?.ActivityDate.HasValue == true && invoice.IssueDate.HasValue)
        {
            var days = (FinanceTime.ToChina(invoice.IssueDate.Value).Date -
                        FinanceTime.ToChina(material.ActivityDate.Value).Date).TotalDays;
            if (Math.Abs(days) > 30)
                result.Warnings.Add($"开票日期超出活动时间 ±30 天（相差 {Math.Abs(days):0} 天）");
        }

        if (!string.IsNullOrWhiteSpace(invoice.Buyer) && !IsClubBuyer(invoice.Buyer))
            result.Warnings.Add("购买方名称与社团名称不一致，请自行核对");

        return result;
    }

    public async Task<FinanceInvoiceSaveResultVO> SaveInvoiceAsync(string id, FinanceInvoiceUpdateDTO dto,
        CancellationToken ct = default)
    {
        var invoice = await repository.GetInvoiceDOAsync(id, ct)
                      ?? throw new BusinessException(4000, "发票不存在");

        if (invoice.MaterialId != null)
        {
            var material = await repository.GetMaterialDOAsync(invoice.MaterialId, false, ct);
            if (material is { Status: FinanceMaterialStatus.Reimbursed })
                throw new BusinessException(2003, "材料已报销，需先撤销后才能修改发票");
            if (material is { Status: FinanceMaterialStatus.Deleted })
                throw new BusinessException(2003, "材料已删除，无法修改发票");
        }

        invoice.Type = TrimOrNull(dto.Type, 20);
        invoice.Code = TrimOrNull(dto.Code, 20);
        invoice.Number = TrimOrNull(dto.Number, 20);
        invoice.IssueDate = ToUtc(dto.IssueDate);
        invoice.Buyer = TrimOrNull(dto.Buyer, 100);
        invoice.Seller = TrimOrNull(dto.Seller, 100);
        invoice.Item = TrimOrNull(dto.Item, 100);
        invoice.Qty = dto.Qty;
        invoice.PriceCents = dto.Price.HasValue ? FinanceMoney.FromYuan(dto.Price.Value) : null;
        invoice.Unit = TrimOrNull(dto.Unit, 10);
        invoice.AmountCents = FinanceMoney.FromYuan(dto.Amount);
        invoice.TaxCents = FinanceMoney.FromYuan(dto.Tax);
        invoice.TotalCents = FinanceMoney.FromYuan(dto.Total);

        var validation = await ValidateInvoiceAsync(invoice, id, ct);
        var result = new FinanceInvoiceSaveResultVO { Errors = validation.Errors, Warnings = validation.Warnings };

        if (dto.Confirm)
        {
            if (string.IsNullOrWhiteSpace(invoice.Type)) result.Errors.Add("确认前请填写发票类型");
            if (string.IsNullOrWhiteSpace(invoice.Number)) result.Errors.Add("确认前请填写发票号码");
            if (!invoice.IssueDate.HasValue) result.Errors.Add("确认前请填写开票日期");
            if (string.IsNullOrWhiteSpace(invoice.Buyer)) result.Errors.Add("确认前请填写购买方");
            if (string.IsNullOrWhiteSpace(invoice.Seller)) result.Errors.Add("确认前请填写销售方");
            if (string.IsNullOrWhiteSpace(invoice.Item)) result.Errors.Add("确认前请填写项目名称");
            if (invoice.Qty is not > 0) result.Errors.Add("确认前请填写大于 0 的数量");
            if (invoice.PriceCents is not > 0) result.Errors.Add("确认前请填写大于 0 的单价");
            if (string.IsNullOrWhiteSpace(invoice.Unit)) result.Errors.Add("确认前请填写单位");
            if (invoice.AmountCents <= 0) result.Errors.Add("确认前请填写大于 0 的金额");
            if (invoice.TotalCents <= 0) result.Errors.Add("确认前请填写大于 0 的价税合计");
        }

        if (result.Errors.Count > 0) return result;

        invoice.Status = dto.Confirm ? FinanceInvoiceStatus.Confirmed : FinanceInvoiceStatus.Recognized;
        invoice.RecognitionError = null;
        if (!await repository.UpdateInvoiceAsync(invoice, ct))
            throw new BusinessException(2001, "发票保存失败");

        if (!string.IsNullOrWhiteSpace(invoice.MaterialId))
            await repository.RecomputeMaterialTotalAsync(invoice.MaterialId, ct);

        result.Invoice = await repository.GetInvoiceVOAsync(id, ct);
        return result;
    }

    public async Task<FinanceInvoiceSaveResultVO> RecognizeInvoiceAsync(string id, CancellationToken ct = default)
    {
        var invoice = await repository.GetInvoiceDOAsync(id, ct)
                      ?? throw new BusinessException(4000, "发票不存在");

        if (invoice.Status == FinanceInvoiceStatus.Confirmed)
            throw new BusinessException(2003, "待报销的发票不能重新识别，请先回退到待确认");

        if (!string.IsNullOrWhiteSpace(invoice.MaterialId))
        {
            var parent = await repository.GetMaterialDOAsync(invoice.MaterialId, false, ct);
            if (parent is { Status: not FinanceMaterialStatus.Pending })
                throw new BusinessException(2003, "材料已锁定或删除，不能重新识别发票");
        }

        if (string.IsNullOrWhiteSpace(invoice.FilePath))
            throw new BusinessException(4005, "发票原件不存在，无法识别");

        if (!storage.Exists(invoice.FilePath))
            throw new BusinessException(4005, "发票原件已丢失，请重新上传");

        FinanceOcrResult ocr;
        await using (var stream = storage.OpenRead(invoice.FilePath))
        {
            ocr = await ocrService.RecognizeAsync(stream, invoice.OriginalFileName ?? "invoice",
                invoice.ContentType ?? "application/octet-stream", ct);
        }

        if (ocr.Success)
        {
            if (!string.IsNullOrWhiteSpace(ocr.Type)) invoice.Type = TrimOrNull(ocr.Type, 20);
            if (!string.IsNullOrWhiteSpace(ocr.Code)) invoice.Code = TrimOrNull(ocr.Code, 20);
            if (!string.IsNullOrWhiteSpace(ocr.Number)) invoice.Number = TrimOrNull(ocr.Number, 20);
            if (ocr.IssueDate.HasValue) invoice.IssueDate = ToUtc(ocr.IssueDate);
            if (!string.IsNullOrWhiteSpace(ocr.Buyer)) invoice.Buyer = TrimOrNull(ocr.Buyer, 100);
            if (!string.IsNullOrWhiteSpace(ocr.Seller)) invoice.Seller = TrimOrNull(ocr.Seller, 100);
            if (!string.IsNullOrWhiteSpace(ocr.Item)) invoice.Item = TrimOrNull(ocr.Item, 100);
            if (ocr.Qty.HasValue) invoice.Qty = ocr.Qty;
            if (ocr.Price.HasValue) invoice.PriceCents = FinanceMoney.FromYuan(ocr.Price.Value);
            if (!string.IsNullOrWhiteSpace(ocr.Unit)) invoice.Unit = TrimOrNull(ocr.Unit, 10);
            invoice.AmountCents = ocr.AmountCents;
            invoice.TaxCents = ocr.TaxCents;
            invoice.TotalCents = ocr.TotalCents;

            // 同一张发票可能同时给了图片和 PDF；号码相同即视为同一张，合并为一条记录。
            if (!string.IsNullOrWhiteSpace(invoice.Number))
            {
                var duplicate = (await repository.FindInvoicesByNumberAsync(invoice.Number, invoice.Id, ct))
                    .FirstOrDefault();
                if (duplicate != null)
                {
                    await repository.SoftDeleteInvoiceAsync(invoice.Id, DateTime.UtcNow, ct);
                    return new FinanceInvoiceSaveResultVO
                    {
                        Invoice = await repository.GetInvoiceVOAsync(duplicate.Id, ct),
                        Errors = [],
                        Warnings = [$"发票号码 {invoice.Number} 已存在，已按同一张发票合并"]
                    };
                }
            }

            invoice.Status = FinanceInvoiceStatus.Recognized;
            invoice.RecognitionError = null;
        }
        else
        {
            invoice.Status = FinanceInvoiceStatus.Failed;
            invoice.RecognitionError = ocr.Error;
        }

        invoice.ConfidenceJson = JsonSerializer.Serialize(ocr.Confidence);
        invoice.RecognizedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateInvoiceAsync(invoice, ct))
            throw new BusinessException(2001, "识别结果保存失败");

        var validation = await ValidateInvoiceAsync(invoice, id, ct);
        return new FinanceInvoiceSaveResultVO
        {
            Invoice = await repository.GetInvoiceVOAsync(id, ct),
            Errors = [],
            Warnings = validation.Warnings
        };
    }

    public async Task<FinanceInvoiceVO> DeleteInvoiceAsync(string id, CancellationToken ct = default)
    {
        var invoice = await repository.GetInvoiceDOAsync(id, ct)
                      ?? throw new BusinessException(4000, "发票不存在");

        var materialId = invoice.MaterialId;
        if (materialId != null)
        {
            var material = await repository.GetMaterialDOAsync(materialId, false, ct);
            if (material is { Status: FinanceMaterialStatus.Reimbursed })
                throw new BusinessException(2003, "材料已报销，需先撤销后才能删除发票");
        }

        var vo = await repository.GetInvoiceVOAsync(id, ct) ?? new FinanceInvoiceVO();
        if (!await repository.SoftDeleteInvoiceAsync(id, DateTime.UtcNow, ct))
            throw new BusinessException(2001, "删除失败");

        if (!string.IsNullOrWhiteSpace(materialId))
            await repository.RecomputeMaterialTotalAsync(materialId, ct);

        return vo;
    }

    // ===== 材料 =====

    public async Task<FinanceMaterialDetailVO> CreateMaterialAsync(FinanceMaterialUpsertDTO dto, FinanceActor actor,
        CancellationToken ct = default)
    {
        var error = ValidateMaterial(dto);
        if (error != null) throw new BusinessException(1003, error);

        var now = DateTime.UtcNow;
        var material = new FinanceMaterialDO
        {
            ActivityName = TrimOrNull(dto.ActivityName, 50),
            ActivityDate = ToUtc(dto.ActivityDate),
            ActivityType = TrimOrNull(dto.ActivityType, 20),
            Location = TrimOrNull(dto.Location, 50),
            Headcount = dto.Headcount,
            Owner = TrimOrNull(dto.Owner, 20),
            Phone = TrimOrNull(dto.Phone, 14),
            Summary = TrimOrNull(dto.Summary, 200),
            ActivityBudgetCents = FinanceMoney.FromYuan(dto.ActivityBudget),
            Remark = TrimOrNull(dto.Remark, 200),
            Status = FinanceMaterialStatus.Pending,
            AcademicYear = FinanceAcademicYear.Of(FinanceTime.ToChina(dto.ActivityDate) ?? FinanceTime.Now),
            CreatedBy = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await repository.CreateMaterialAsync(material, ct);
        return await repository.GetMaterialDetailAsync(material.Id, ct)
               ?? throw new BusinessException(2001, "创建材料失败");
    }

    public async Task<FinanceMaterialDetailVO> UpdateMaterialAsync(string id, FinanceMaterialUpsertDTO dto,
        CancellationToken ct = default)
    {
        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status != FinanceMaterialStatus.Pending)
            throw new BusinessException(2003, "只有待报销材料可以编辑；已报销材料请先撤销");

        var error = ValidateMaterial(dto);
        if (error != null) throw new BusinessException(1003, error);

        material.ActivityName = TrimOrNull(dto.ActivityName, 50);
        material.ActivityDate = ToUtc(dto.ActivityDate);
        material.ActivityType = TrimOrNull(dto.ActivityType, 20);
        material.Location = TrimOrNull(dto.Location, 50);
        material.Headcount = dto.Headcount;
        material.Owner = TrimOrNull(dto.Owner, 20);
        material.Phone = TrimOrNull(dto.Phone, 14);
        material.Summary = TrimOrNull(dto.Summary, 200);
        material.ActivityBudgetCents = FinanceMoney.FromYuan(dto.ActivityBudget);
        material.Remark = TrimOrNull(dto.Remark, 200);
        material.AcademicYear = FinanceAcademicYear.Of(
            FinanceTime.ToChina(dto.ActivityDate) ?? FinanceTime.ToChina(material.CreatedAt));

        if (!await repository.UpdateMaterialAsync(material, ct))
            throw new BusinessException(2001, "材料保存失败");

        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    public async Task<FinanceMaterialDetailVO> AssignInvoicesAsync(string id, FinanceAssignInvoicesDTO dto,
        CancellationToken ct = default)
    {
        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status != FinanceMaterialStatus.Pending)
            throw new BusinessException(2003, "只有待报销材料可以加入或移出发票");

        var ids = dto.InvoiceIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (ids.Count == 0) throw new BusinessException(1000, "请选择发票");

        var invoices = await repository.GetInvoiceDOsAsync(ids, ct);
        if (invoices.Count != ids.Count)
            throw new BusinessException(1003, "部分发票不存在或已删除");

        var action = (dto.Action ?? "assign").Trim().ToLowerInvariant();
        if (action == "remove")
        {
            if (invoices.Any(i => i.MaterialId != id))
                throw new BusinessException(1003, "所选发票不属于该材料");
            await repository.RemoveInvoicesAsync(id, ids, ct);
        }
        else
        {
            var occupied = invoices.FirstOrDefault(i => i.MaterialId != null && i.MaterialId != id);
            if (occupied != null)
                throw new BusinessException(2003, $"发票 {occupied.Number} 已加入材料 {occupied.MaterialId}，请先移出");

            var notReady = invoices.FirstOrDefault(i =>
                i.Status != FinanceInvoiceStatus.Confirmed);
            if (notReady != null)
                throw new BusinessException(1003, "只能加入待报销状态的发票，请先完成人工确认");

            // 同一材料内发票号码不能重复（图片/PDF 同一张发票视为同一条记录）。
            var existing = await repository.GetMaterialInvoiceDOsAsync(id, false, ct);
            var seen = existing
                .Where(i => !string.IsNullOrWhiteSpace(i.Number))
                .Select(i => i.Number)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var invoice in invoices)
            {
                if (string.IsNullOrWhiteSpace(invoice.Number)) continue;
                if (!seen.Add(invoice.Number))
                    throw new BusinessException(1003,
                        $"材料内已存在发票号码 {invoice.Number}，不能重复加入材料");
            }

            await repository.AssignInvoicesAsync(id, ids, ct);
        }

        await repository.RecomputeMaterialTotalAsync(id, ct);
        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    public async Task<FinanceMaterialDetailVO> GenerateAsync(string id, string kind, string operatorName,
        CancellationToken ct = default)
    {
        if (!FinanceFormKind.IsValid(kind))
            throw new BusinessException(1003, "表单类型只能是 apply 或 reimburse");

        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status != FinanceMaterialStatus.Pending)
            throw new BusinessException(2003, "只有待报销材料可以生成表单");

        var invoices = await repository.GetMaterialInvoiceDOsAsync(id, true, ct);
        if (invoices.Count == 0)
            throw new BusinessException(1003, "至少需要 1 张待报销状态的发票才能生成表单");

        var total = invoices.Sum(i => i.TotalCents);
        if (total <= 0)
            throw new BusinessException(1003, "发票合计必须大于 0 才能生成表单");

        var bundledTemplate = documentService.GetBundledTemplatePath(kind);
        var template = bundledTemplate == null ? await repository.GetEnabledTemplateAsync(kind, ct) : null;
        var version = material.FormVersion + 1;
        var path = await documentService.GenerateAsync(material, invoices, kind, version,
            bundledTemplate ?? template?.FilePath, operatorName, ct);

        if (!await repository.SetMaterialFormAsync(id, kind, path, version, DateTime.UtcNow, ct))
            throw new BusinessException(2001, "保存表单记录失败");

        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    public async Task<FinanceMaterialDetailVO> ConfirmAsync(string id, FinanceActor actor,
        CancellationToken ct = default)
    {
        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status != FinanceMaterialStatus.Pending)
            throw new BusinessException(2003, "只有待报销材料可以确认报销");

        var activityName = material.ActivityName?.Trim();
        if (string.IsNullOrWhiteSpace(activityName) || activityName.Length is < 2 or > 50)
            throw new BusinessException(1003, "确认报销前必须填写 2-50 字的活动名称");

        var confirmed = await repository.GetMaterialInvoiceDOsAsync(id, true, ct);
        var allCount = await repository.CountInvoicesAsync(id, false, ct);
        if (allCount == 0)
            throw new BusinessException(1003, "材料至少需要 1 张发票");
        if (confirmed.Count != allCount)
            throw new BusinessException(1003, "还有未确认的发票，请先逐张确认");
        if (confirmed.Count == 0)
            throw new BusinessException(1003, "至少需要 1 张待报销状态的发票");

        var total = confirmed.Sum(i => i.TotalCents);
        if (total <= 0)
            throw new BusinessException(1003, "发票合计必须大于 0");

        var deduction = FinanceFundRules.ComputeDeduction(total, material.ActivityBudgetCents);
        var flow = await repository.ConfirmMaterialAsync(id, deduction, actor.UserId, actor.Name, DateTime.UtcNow, ct);
        if (flow == null) throw new BusinessException(2001, "确认报销失败");

        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    public async Task<FinanceMaterialDetailVO> RevokeAsync(string id, string reason, FinanceActor actor,
        CancellationToken ct = default)
    {
        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status != FinanceMaterialStatus.Reimbursed)
            throw new BusinessException(2003, "只有已报销材料可以撤销");

        var trimmed = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length is < 2 or > 200)
            throw new BusinessException(1003, "撤销原因需在 2-200 个字符之间");

        var flow = await repository.RevokeMaterialAsync(id, trimmed, actor.UserId, actor.Name, DateTime.UtcNow, ct);
        if (flow == null)
            throw new BusinessException(2003, "未找到有效扣减流水，无法撤销；请联系管理员核对经费流水");

        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    public async Task<FinanceMaterialDetailVO> DeleteAsync(string id, CancellationToken ct = default)
    {
        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status == FinanceMaterialStatus.Reimbursed)
            throw new BusinessException(2003, "已报销材料需先撤销后才能删除");
        if (material.Status == FinanceMaterialStatus.Deleted)
            throw new BusinessException(2003, "材料已删除");

        var now = DateTime.UtcNow;
        if (!await repository.SetMaterialStatusAsync(id, FinanceMaterialStatus.Deleted, null, null, null, now, ct))
            throw new BusinessException(2001, "删除失败");

        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    public async Task<FinanceMaterialDetailVO> RestoreAsync(string id, CancellationToken ct = default)
    {
        var material = await repository.GetMaterialDOAsync(id, false, ct)
                       ?? throw new BusinessException(4000, "材料不存在");

        if (material.Status != FinanceMaterialStatus.Deleted)
            throw new BusinessException(2003, "材料未被删除，无需恢复");

        if (!material.DeletedAt.HasValue || material.DeletedAt.Value < DateTime.UtcNow.AddDays(-30))
            throw new BusinessException(2003, "删除超过 30 天，无法恢复");

        if (!await repository.SetMaterialStatusAsync(id, FinanceMaterialStatus.Pending, null, null, null, null, ct))
            throw new BusinessException(2001, "恢复失败");

        return await repository.GetMaterialDetailAsync(id, ct)
               ?? throw new BusinessException(4000, "材料不存在");
    }

    // ===== 经费 =====

    public async Task<FinanceFundSummaryVO> SummaryAsync(string? academicYear, CancellationToken ct = default)
    {
        var year = string.IsNullOrWhiteSpace(academicYear) ? FinanceAcademicYear.Current : academicYear.Trim();
        if (!FinanceAcademicYear.IsValid(year))
            throw new BusinessException(1003, "学年格式应为 2026-2027");

        var account = await repository.GetAccountAsync(year, ct);
        var used = await repository.SumUsedAsync(year, ct);
        var inTransit = await repository.SumPendingAmountAsync(year, ct);
        var (reimbursed, pending) = await repository.GetMaterialCountsAsync(year, ct);
        var recent = await repository.GetRecentFlowsAsync(year, 5, ct);

        var budget = account?.BudgetAmountCents ?? 0;
        var remaining = FinanceFundRules.ComputeRemaining(budget, used);

        return new FinanceFundSummaryVO
        {
            AcademicYear = year,
            BudgetAmount = FinanceMoney.ToYuan(budget),
            UsedAmount = FinanceMoney.ToYuan(used),
            InTransitAmount = FinanceMoney.ToYuan(inTransit),
            RemainingAmount = FinanceMoney.ToYuan(remaining),
            UsagePercent = budget > 0
                ? Math.Round(used * 100m / budget, 2)
                : used > 0 ? 100 : 0,
            WarningLevel = FinanceFundRules.WarningLevel(budget, remaining),
            AccountExists = account != null,
            Editable = year == FinanceAcademicYear.Current,
            ReimbursedCount = reimbursed,
            PendingCount = pending,
            RecentFlows = recent
        };
    }

    public async Task<FinanceAccountVO> UpdateAccountAsync(FinanceAccountUpdateDTO dto, FinanceActor actor,
        CancellationToken ct = default)
    {
        var year = dto.AcademicYear?.Trim() ?? "";
        if (!FinanceAcademicYear.IsValid(year))
            throw new BusinessException(1003, "学年格式应为 2026-2027");

        if (year != FinanceAcademicYear.Current)
            throw new BusinessException(2003, "历史学年只读，只能修改当前学年的年度经费");

        if (dto.BudgetAmount < 0)
            throw new BusinessException(1003, "年度经费不能为负数");

        var account = new FinanceAccountDO
        {
            AcademicYear = year,
            BudgetAmountCents = FinanceMoney.FromYuan(dto.BudgetAmount),
            Remark = TrimOrNull(dto.Remark, 200),
            CreatedBy = actor.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await repository.UpsertAccountAsync(account, ct);
        return new FinanceAccountVO
        {
            Id = saved.Id,
            AcademicYear = saved.AcademicYear,
            BudgetAmount = FinanceMoney.ToYuan(saved.BudgetAmountCents),
            Remark = saved.Remark,
            CreatedAt = saved.CreatedAt,
            UpdatedAt = saved.UpdatedAt,
            Editable = true
        };
    }

    public FinanceMetaVO Meta()
    {
        var years = FinanceAcademicYear.RecentYears(6);
        var current = FinanceAcademicYear.Current;
        if (!years.Contains(current)) years.Insert(0, current);

        return new FinanceMetaVO
        {
            ClubName = _options.ClubName,
            CurrentAcademicYear = current,
            Years = years,
            ActivityTypes = _options.ActivityTypes.ToList(),
            LowConfidenceThreshold = _options.LowConfidenceThreshold,
            OcrConfigured = !string.IsNullOrWhiteSpace(_options.OcrEndpoint),
            PdfConfigured = !string.IsNullOrWhiteSpace(_options.PdfConverterPath),
            MaxFileSizeMb = _options.MaxFileSizeMb,
            MaxFilesPerUpload = _options.MaxFilesPerUpload,
            SupportedExtensions = _options.SupportedExtensions.ToList()
        };
    }

    // ===== 工具 =====

    private static string? ValidateMaterial(FinanceMaterialUpsertDTO dto)
    {
        var name = dto.ActivityName?.Trim();
        if (!string.IsNullOrEmpty(name) && name.Length is < 2 or > 50)
            return "活动名称长度需在 2-50 个字符之间";
        if (dto.Summary is { Length: > 200 })
            return "活动简介不能超过 200 个字符";
        if (dto.ActivityBudget < 0)
            return "活动经费不能为负数";
        if (dto.Headcount is < 0)
            return "参加人数不能为负数";
        if (dto.Phone is { Length: > 14 })
            return "联系电话不能超过 14 个字符";
        return null;
    }

    private bool IsClubBuyer(string buyer)
    {
        var normalized = Normalize(buyer);
        var club = Normalize(_options.ClubName);
        return normalized.Contains(club) || normalized.Contains("西安建筑科技大学") ||
               normalized.Contains("西建大") || normalized.Contains("iosclub");
    }

    private static string Normalize(string value) =>
        new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue) return null;
        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
