using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace iOSClub.WebAPI.Controllers;

/// <summary>
/// 财务模块接口。统一前缀 /Finance，鉴权直接复用网站 JWT 中的角色声明。
/// 财务模块没有登录页、不新建账号，也不修改现有业务表。
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize(Roles = FinanceRoles.Allowed)]
public class FinanceController(
    IFinanceRepository repository,
    FinanceService service,
    IFinanceDocumentService documentService,
    IFinanceStorage storage,
    FinanceAuditService audit,
    IOptions<FinanceOptions> options) : ControllerBase
{
    private const long HardMaxUploadBytes = 10L * 1024 * 1024;
    // 10MB 文件的 multipart 开销会超过 10MB，请求上限单独放宽，文件大小仍按 10MB 校验。
    private const long UploadRequestBytes = 12L * 1024 * 1024;

    private FinanceOptions Options => options.Value;

    private string? CurrentUserId() =>
        HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
        HttpContext.User.FindFirst("sub")?.Value;

    private Task<FinanceActor> ActorAsync(CancellationToken ct) => audit.GetActorAsync(ct);

    // ===== 元信息 =====

    [HttpGet("meta")]
    public ActionResult<ApiResponse<FinanceMetaVO>> Meta()
        => Ok(ApiResponse<FinanceMetaVO>.Success(service.Meta()));

    // ===== 发票 =====

    /// <summary>上传发票。单文件 ≤10MB；为避免超过全局 10MB 请求上限，前端逐张上传。</summary>
    [HttpPost("invoices/upload")]
    [RequestSizeLimit(UploadRequestBytes)]
    public async Task<ActionResult<ApiResponse<List<FinanceInvoiceVO>>>> Upload(
        [FromForm] List<IFormFile>? files, CancellationToken ct)
    {
        if (files == null || files.Count == 0)
            return Ok(ApiResponse<List<FinanceInvoiceVO>>.Fail(ErrorCode.ParameterEmpty, "请选择要上传的发票"));

        if (files.Count > Options.MaxFilesPerUpload)
            return Ok(ApiResponse<List<FinanceInvoiceVO>>.Fail(ErrorCode.ParameterOutOfRange,
                $"单次最多上传 {Options.MaxFilesPerUpload} 个文件"));

        var errors = new List<string>();
        foreach (var file in files)
        {
            if (file.Length <= 0)
                errors.Add($"{file.FileName}：文件为空");
            else if (file.Length > HardMaxUploadBytes)
                errors.Add($"{file.FileName}：超过 10MB");
            else if (!storage.IsAllowedExtension(file.FileName))
                errors.Add($"{file.FileName}：格式不在白名单（{string.Join("/", Options.SupportedExtensions)}）");
        }

        if (errors.Count > 0)
            return Ok(ApiResponse<List<FinanceInvoiceVO>>.Fail(ErrorCode.ParameterValidationFailed,
                "上传文件不符合要求", string.Join("；", errors)));

        var actor = await ActorAsync(ct);
        var now = DateTime.UtcNow;
        var invoices = new List<FinanceInvoiceDO>();
        var savedPaths = new List<string>();

        try
        {
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                var path = await storage.SaveAsync(stream, file.FileName, "invoices", ct);
                savedPaths.Add(path);

                var extension = Path.GetExtension(file.FileName);
                invoices.Add(new FinanceInvoiceDO
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = path,
                    OriginalFileName = Truncate(Path.GetFileName(file.FileName), 255),
                    ContentType = IFinanceStorage.GetContentType(extension),
                    FileSize = file.Length,
                    Status = FinanceInvoiceStatus.Pending,
                    CreatedBy = actor.UserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await repository.CreateInvoicesAsync(invoices, ct);
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                try { storage.Delete(path); }
                catch (Exception) { /* 清理失败不掩盖原始异常 */ }
            }

            throw;
        }

        await audit.WriteAsync(FinanceActions.UploadInvoice, FinanceTargetType.Invoice,
            invoices.Count == 1 ? invoices[0].Id : null, null,
            new { count = invoices.Count, files = invoices.Select(i => i.OriginalFileName).ToList() },
            "success", $"上传 {invoices.Count} 张发票", actor, ct);

        var result = new List<FinanceInvoiceVO>();
        foreach (var invoice in invoices)
        {
            var vo = await repository.GetInvoiceVOAsync(invoice.Id, ct);
            if (vo != null) result.Add(vo);
        }

        return Ok(ApiResponse<List<FinanceInvoiceVO>>.Success(result, $"已上传 {result.Count} 张发票"));
    }

    [HttpPost("invoices/{id}/recognize")]
    public async Task<ActionResult<ApiResponse<FinanceInvoiceSaveResultVO>>> Recognize(string id,
        CancellationToken ct)
    {
        var before = await repository.GetInvoiceDOAsync(id, ct);
        try
        {
            var result = await service.RecognizeInvoiceAsync(id, ct);
            var failed = result.Invoice?.Status == FinanceInvoiceStatus.Failed;
            await audit.WriteAsync(FinanceActions.RecognizeInvoice, FinanceTargetType.Invoice, id, before,
                result.Invoice, failed ? "failure" : "success", result.Invoice?.RecognitionError, await ActorAsync(ct), ct);

            return Ok(ApiResponse<FinanceInvoiceSaveResultVO>.Success(result,
                failed ? "识别失败，请手工录入" : "识别完成，请核对字段"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.RecognizeInvoice, FinanceTargetType.Invoice, id, before, null,
                "failure", ex.Message, await ActorAsync(ct), ct);
            throw;
        }
    }

    [HttpPut("invoices/{id}")]
    public async Task<ActionResult<ApiResponse<FinanceInvoiceSaveResultVO>>> UpdateInvoice(string id,
        [FromBody] FinanceInvoiceUpdateDTO dto, CancellationToken ct)
    {
        var before = await repository.GetInvoiceDOAsync(id, ct);
        try
        {
            var result = await service.SaveInvoiceAsync(id, dto, ct);
            if (!result.Saved)
            {
                await audit.WriteAsync(dto.Confirm ? FinanceActions.ConfirmInvoice : FinanceActions.UpdateInvoice,
                    FinanceTargetType.Invoice, id, before, null, "failure",
                    string.Join("；", result.Errors), await ActorAsync(ct), ct);
                return Ok(ApiResponse<FinanceInvoiceSaveResultVO>.FailWithData(
                    ErrorCode.ParameterValidationFailed, result, "发票校验未通过",
                    string.Join("；", result.Errors)));
            }

            await audit.WriteAsync(dto.Confirm ? FinanceActions.ConfirmInvoice : FinanceActions.UpdateInvoice,
                FinanceTargetType.Invoice, id, before, result.Invoice, "success", null, await ActorAsync(ct), ct);

            var message = dto.Confirm ? "发票已人工确认，进入待报销状态" : "发票已保存";
            if (result.Warnings.Count > 0) message += "；" + string.Join("；", result.Warnings);
            return Ok(ApiResponse<FinanceInvoiceSaveResultVO>.Success(result, message));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(dto.Confirm ? FinanceActions.ConfirmInvoice : FinanceActions.UpdateInvoice,
                FinanceTargetType.Invoice, id, before, null, "failure", ex.Message, await ActorAsync(ct), ct);
            throw;
        }
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<PagedResult<FinanceInvoiceVO>>>> GetInvoices(
        string? status = null, string? materialStatus = null, bool? excludeReimbursedMaterial = null,
        string? materialId = null, bool? unassigned = null,
        string? keyword = null,
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await repository.GetInvoicesAsync(new FinanceInvoiceQuery
        {
            Status = status,
            MaterialStatus = materialStatus,
            ExcludeReimbursedMaterial = excludeReimbursedMaterial,
            MaterialId = materialId,
            Unassigned = unassigned,
            Keyword = keyword,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(ApiResponse<PagedResult<FinanceInvoiceVO>>.Success(result));
    }

    [HttpGet("invoices/assignable")]
    public async Task<ActionResult<ApiResponse<List<FinanceInvoiceVO>>>> GetAssignable(
        string? keyword = null, int take = 100, CancellationToken ct = default)
        => Ok(ApiResponse<List<FinanceInvoiceVO>>.Success(
            await repository.GetAssignableInvoicesAsync(keyword, take, ct)));

    [HttpGet("invoices/{id}")]
    public async Task<ActionResult<ApiResponse<FinanceInvoiceVO>>> GetInvoice(string id, CancellationToken ct)
    {
        var invoice = await repository.GetInvoiceVOAsync(id, ct);
        return invoice == null
            ? Ok(ApiResponse<FinanceInvoiceVO>.Fail(ErrorCode.ResourceNotFound, "发票不存在"))
            : Ok(ApiResponse<FinanceInvoiceVO>.Success(invoice));
    }

    /// <summary>读取发票原件。必须带 token，浏览器不能直接访问裸路径。</summary>
    [HttpGet("invoices/{id}/file")]
    public async Task<IActionResult> GetInvoiceFile(string id, CancellationToken ct)
    {
        var invoice = await repository.GetInvoiceDOAsync(id, ct);
        if (invoice == null || string.IsNullOrWhiteSpace(invoice.FilePath) || !storage.Exists(invoice.FilePath))
            return Ok(ApiResponse.Fail(ErrorCode.FileNotFound, "发票原件不存在"));

        var stream = storage.OpenRead(invoice.FilePath);
        return File(stream, invoice.ContentType ?? "application/octet-stream");
    }

    [HttpDelete("invoices/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteInvoice(string id, CancellationToken ct)
    {
        var before = await repository.GetInvoiceDOAsync(id, ct);
        var actor = await ActorAsync(ct);
        try
        {
            await service.DeleteInvoiceAsync(id, ct);
            await audit.WriteAsync(FinanceActions.DeleteInvoice, FinanceTargetType.Invoice, id, before, null,
                "success", "软删除发票", actor, ct);
            return Ok(ApiResponse.Success("发票已删除"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.DeleteInvoice, FinanceTargetType.Invoice, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    // ===== 报销材料 =====

    [HttpGet("materials")]
    public async Task<ActionResult<ApiResponse<PagedResult<FinanceMaterialVO>>>> GetMaterials(
        string? status = null, string? keyword = null, string? academicYear = null, bool includeDeleted = false,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await repository.GetMaterialsAsync(new FinanceMaterialQuery
        {
            Status = status,
            Keyword = keyword,
            AcademicYear = academicYear,
            IncludeDeleted = includeDeleted,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(ApiResponse<PagedResult<FinanceMaterialVO>>.Success(result));
    }

    [HttpGet("materials/{id}")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> GetMaterial(string id,
        CancellationToken ct)
    {
        var material = await repository.GetMaterialDetailAsync(id, ct);
        return material == null
            ? Ok(ApiResponse<FinanceMaterialDetailVO>.Fail(ErrorCode.ResourceNotFound, "材料不存在"))
            : Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material));
    }

    [HttpPost("materials")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> CreateMaterial(
        [FromBody] FinanceMaterialUpsertDTO dto, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.CreateMaterialAsync(dto, actor, ct);
            await audit.WriteAsync(FinanceActions.CreateMaterial, FinanceTargetType.Material, material.Id, null,
                material, "success", material.Id, actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material, "材料已创建"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.CreateMaterial, FinanceTargetType.Material, null, null, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpPut("materials/{id}")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> UpdateMaterial(string id,
        [FromBody] FinanceMaterialUpsertDTO dto, CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.UpdateMaterialAsync(id, dto, ct);
            await audit.WriteAsync(FinanceActions.UpdateMaterial, FinanceTargetType.Material, id, before, material,
                "success", null, actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material, "材料已保存"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.UpdateMaterial, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpPost("materials/{id}/invoices")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> AssignInvoices(string id,
        [FromBody] FinanceAssignInvoicesDTO dto, CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.AssignInvoicesAsync(id, dto, ct);
            var action = string.Equals(dto.Action, "remove", StringComparison.OrdinalIgnoreCase)
                ? FinanceActions.RemoveInvoice
                : FinanceActions.AssignInvoice;
            await audit.WriteAsync(action, FinanceTargetType.Material, id, before, material, "success",
                $"发票 {dto.InvoiceIds.Count} 张", actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material,
                action == FinanceActions.RemoveInvoice ? "已移出材料" : "已加入材料"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.AssignInvoice, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpPost("materials/{id}/generate")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> Generate(string id,
        [FromBody] FinanceGenerateFormDTO dto, CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.GenerateAsync(id, dto.Kind, actor.Name, ct);
            await audit.WriteAsync(FinanceActions.GenerateForm, FinanceTargetType.Material, id, before, material,
                "success", FinanceFormKind.Label(dto.Kind), actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material,
                $"{FinanceFormKind.Label(dto.Kind)} 已生成"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.GenerateForm, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpPost("materials/{id}/confirm")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> Confirm(string id, CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.ConfirmAsync(id, actor, ct);
            await audit.WriteAsync(FinanceActions.ConfirmMaterial, FinanceTargetType.Material, id, before, material,
                "success", $"扣减 ¥{material.EstimatedDeduction:0.00}", actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material,
                $"已确认报销，本次扣减 ¥{material.EstimatedDeduction:0.00}"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.ConfirmMaterial, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpPost("materials/{id}/revoke")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> Revoke(string id,
        [FromBody] FinanceRevokeDTO dto, CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.RevokeAsync(id, dto.Reason, actor, ct);
            await audit.WriteAsync(FinanceActions.RevokeMaterial, FinanceTargetType.Material, id, before, material,
                "success", dto.Reason, actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material, "已撤销报销，材料回到待报销"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.RevokeMaterial, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpDelete("materials/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteMaterial(string id, CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.DeleteAsync(id, ct);
            await audit.WriteAsync(FinanceActions.DeleteMaterial, FinanceTargetType.Material, id, before, material,
                "success", "软删除，30 天内可恢复", actor, ct);
            return Ok(ApiResponse.Success("材料已删除，30 天内可恢复"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.DeleteMaterial, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpPost("materials/{id}/restore")]
    public async Task<ActionResult<ApiResponse<FinanceMaterialDetailVO>>> RestoreMaterial(string id,
        CancellationToken ct)
    {
        var before = await repository.GetMaterialDOAsync(id, false, ct);
        var actor = await ActorAsync(ct);
        try
        {
            var material = await service.RestoreAsync(id, ct);
            await audit.WriteAsync(FinanceActions.RestoreMaterial, FinanceTargetType.Material, id, before, material,
                "success", null, actor, ct);
            return Ok(ApiResponse<FinanceMaterialDetailVO>.Success(material, "材料已恢复"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.RestoreMaterial, FinanceTargetType.Material, id, before, null,
                "failure", ex.Message, actor, ct);
            throw;
        }
    }

    [HttpGet("materials/{id}/logs")]
    public async Task<ActionResult<ApiResponse<List<FinanceLogVO>>>> MaterialLogs(string id, int take = 50,
        CancellationToken ct = default)
        => Ok(ApiResponse<List<FinanceLogVO>>.Success(
            await repository.GetLogsByTargetAsync(FinanceTargetType.Material, id, take, ct)));

    // ===== 导出 =====

    [HttpGet("materials/{id}/export")]
    public async Task<IActionResult> Export(string id, string kind = "reimburse", string format = "docx",
        CancellationToken ct = default)
    {
        if (!FinanceFormKind.IsValid(kind))
            return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, "表单类型只能是 apply 或 reimburse"));

        var normalizedFormat = (format ?? "docx").Trim().ToLowerInvariant();
        if (normalizedFormat is not ("docx" or "pdf"))
            return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, "导出格式只能是 docx 或 pdf"));

        var material = await repository.GetMaterialDOAsync(id, false, ct);
        if (material == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "材料不存在"));
        if (material.Status == FinanceMaterialStatus.Deleted)
            return Ok(ApiResponse.Fail(ErrorCode.InvalidStatusForOperation, "已删除材料不能导出"));

        var actor = await ActorAsync(ct);
        var docx = await EnsureDocumentAsync(material, kind, actor, ct);
        var fileName = documentService.BuildFileName(material, kind, normalizedFormat);

        byte[] bytes;
        string contentType;
        if (normalizedFormat == "pdf")
        {
            var pdf = await documentService.TryConvertToPdfAsync(docx, Path.GetFileNameWithoutExtension(fileName), ct);
            if (pdf == null)
                return Ok(ApiResponse.Fail(ErrorCode.ExternalServiceNotConfigured,
                    "未配置 PDF 转换器，无法导出 PDF；请先在服务器安装 LibreOffice 并设置 Finance__PdfConverterPath"));
            bytes = pdf;
            contentType = "application/pdf";
        }
        else
        {
            bytes = docx;
            contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }

        await audit.WriteAsync(FinanceActions.ExportMaterial, FinanceTargetType.Material, id, null,
            new { kind, format = normalizedFormat }, "success", fileName, actor, ct);

        return File(bytes, contentType, fileName);
    }

    [HttpPost("materials/export")]
    public async Task<IActionResult> BatchExport([FromBody] FinanceBatchExportDTO dto, CancellationToken ct)
    {
        var kind = string.IsNullOrWhiteSpace(dto.Kind) ? FinanceFormKind.Reimburse : dto.Kind;
        if (!FinanceFormKind.IsValid(kind))
            return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, "表单类型只能是 apply 或 reimburse"));

        var materials = await repository.GetMaterialsForExportAsync(new FinanceBatchExportFilter
        {
            Ids = dto.Ids,
            MaterialNo = dto.MaterialNo,
            ActivityName = dto.ActivityName,
            From = dto.From,
            To = dto.To,
            Status = dto.Status
        }, ct);

        if (materials.Count == 0)
            return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "没有符合条件的材料"));
        if (materials.Count > 200)
            return Ok(ApiResponse.Fail(ErrorCode.ParameterOutOfRange, "单次批量导出不能超过 200 份材料"));

        var actor = await ActorAsync(ct);
        using var zipStream = new MemoryStream();
        var failures = new List<string>();
        var success = 0;

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            foreach (var material in materials)
            {
                try
                {
                    var entity = await repository.GetMaterialDOAsync(material.Id, false, ct);
                    if (entity == null) continue;
                    var docx = await EnsureDocumentAsync(entity, kind, actor, ct);
                    var entryName = documentService.BuildFileName(entity, kind, "docx");
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(docx, ct);
                    success++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{material.Id}：{ex.Message}");
                }
            }

            if (failures.Count > 0)
            {
                var entry = archive.CreateEntry("导出失败清单.txt", CompressionLevel.NoCompression);
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                await writer.WriteLineAsync("以下材料导出失败，请检查后重试：");
                foreach (var failure in failures) await writer.WriteLineAsync(failure);
            }
        }

        if (success == 0)
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "没有可导出的表单",
                string.Join("；", failures)));

        await audit.WriteAsync(FinanceActions.BatchExport, null, null, null,
            new { count = success, kind, failed = failures.Count }, "success",
            $"批量导出 {success} 份，失败 {failures.Count} 份", actor, ct);

        zipStream.Position = 0;
        return File(zipStream.ToArray(), "application/zip", $"财务表单导出_{DateTime.Now:yyyyMMddHHmm}.zip");
    }

    private async Task<byte[]> EnsureDocumentAsync(FinanceMaterialDO material, string kind, FinanceActor actor,
        CancellationToken ct)
    {
        if (documentService.HasDocument(material, kind))
            return documentService.Read(material, kind);

        await service.GenerateAsync(material.Id, kind, actor.Name, ct);
        var refreshed = await repository.GetMaterialDOAsync(material.Id, false, ct)
                        ?? throw new InvalidOperationException("材料不存在");
        return documentService.Read(refreshed, kind);
    }

    // ===== 模板 =====

    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<List<FinanceTemplateVO>>>> GetTemplates(CancellationToken ct)
        => Ok(ApiResponse<List<FinanceTemplateVO>>.Success(await repository.GetTemplatesAsync(ct)));

    [HttpPost("templates")]
    [RequestSizeLimit(5L * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<FinanceTemplateVO>>> UploadTemplate(
        [FromForm] IFormFile? file, [FromForm] string? kind, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResponse<FinanceTemplateVO>.Fail(ErrorCode.ParameterEmpty, "请选择模板文件"));
        if (!FinanceFormKind.IsValid(kind))
            return Ok(ApiResponse<FinanceTemplateVO>.Fail(ErrorCode.ParameterValidationFailed,
                "模板类型只能是 apply 或 reimburse"));
        if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
            return Ok(ApiResponse<FinanceTemplateVO>.Fail(ErrorCode.ParameterValidationFailed, "模板仅支持 .docx"));
        if (file.Length > 5L * 1024 * 1024)
            return Ok(ApiResponse<FinanceTemplateVO>.Fail(ErrorCode.ParameterOutOfRange, "模板不能超过 5MB"));

        var actor = await ActorAsync(ct);
        await using var stream = file.OpenReadStream();
        var path = await storage.SaveAsync(stream, file.FileName, $"templates/{kind}", ct);
        var version = await repository.NextTemplateVersionAsync(kind!, ct);

        var template = new FinanceTemplateDO
        {
            Kind = kind!,
            Version = version,
            FilePath = path,
            OriginalFileName = Truncate(Path.GetFileName(file.FileName), 255),
            Enabled = true,
            CreatedBy = actor.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddTemplateAsync(template, ct);
        await audit.WriteAsync(FinanceActions.UploadTemplate, FinanceTargetType.Template, template.Id, null,
            new { template.Kind, template.Version, template.OriginalFileName }, "success", null, actor, ct);

        return Ok(ApiResponse<FinanceTemplateVO>.Success(new FinanceTemplateVO
        {
            Id = template.Id,
            Kind = template.Kind,
            KindLabel = FinanceFormKind.Label(template.Kind),
            Version = template.Version,
            OriginalFileName = template.OriginalFileName,
            Enabled = template.Enabled,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy
        }, "模板已上传并启用"));
    }

    [HttpPost("templates/{id}/enable")]
    public async Task<ActionResult<ApiResponse>> EnableTemplate(string id, CancellationToken ct)
    {
        var template = await repository.GetTemplateAsync(id, ct);
        if (template == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "模板不存在"));

        if (!await repository.EnableTemplateAsync(id, ct))
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "启用失败"));

        await audit.WriteAsync(FinanceActions.EnableTemplate, FinanceTargetType.Template, id, null,
            new { template.Kind, template.Version }, "success", null, await ActorAsync(ct), ct);

        return Ok(ApiResponse.Success($"已启用 {FinanceFormKind.Label(template.Kind)} 版本 v{template.Version}"));
    }

    [HttpDelete("templates/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteTemplate(string id, CancellationToken ct)
    {
        var template = await repository.GetTemplateAsync(id, ct);
        if (template == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "模板不存在"));

        if (!await repository.DeleteTemplateAsync(id, ct))
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "删除失败"));

        try { storage.Delete(template.FilePath); }
        catch (Exception) { /* 数据库记录删除成功后，文件清理失败不影响结果 */ }

        await audit.WriteAsync(FinanceActions.DeleteTemplate, FinanceTargetType.Template, id, template, null,
            "success", null, await ActorAsync(ct), ct);

        return Ok(ApiResponse.Success("模板已删除"));
    }

    // ===== 经费 =====

    [HttpGet("funds/summary")]
    public async Task<ActionResult<ApiResponse<FinanceFundSummaryVO>>> Summary(string? academicYear = null,
        CancellationToken ct = default)
        => Ok(ApiResponse<FinanceFundSummaryVO>.Success(await service.SummaryAsync(academicYear, ct)));

    [HttpGet("funds/flows")]
    public async Task<ActionResult<ApiResponse<PagedResult<FinanceFlowVO>>>> Flows(
        string? academicYear = null, string? type = null, string? keyword = null, DateTime? from = null,
        DateTime? to = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await repository.GetFlowsAsync(new FinanceFlowQuery
        {
            AcademicYear = academicYear,
            Type = type,
            Keyword = keyword,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(ApiResponse<PagedResult<FinanceFlowVO>>.Success(result));
    }

    [HttpGet("funds/accounts")]
    public async Task<ActionResult<ApiResponse<List<FinanceAccountVO>>>> Accounts(CancellationToken ct)
        => Ok(ApiResponse<List<FinanceAccountVO>>.Success(await repository.GetAccountsAsync(ct)));

    [HttpPut("funds/account")]
    public async Task<ActionResult<ApiResponse<FinanceAccountVO>>> UpdateAccount(
        [FromBody] FinanceAccountUpdateDTO dto, CancellationToken ct)
    {
        var actor = await ActorAsync(ct);
        var before = await repository.GetAccountAsync(dto.AcademicYear ?? "", ct);

        try
        {
            var account = await service.UpdateAccountAsync(dto, actor, ct);
            await audit.WriteAsync(FinanceActions.UpdateAccount, FinanceTargetType.Account, account.AcademicYear,
                before, account, "success", null, actor, ct);
            return Ok(ApiResponse<FinanceAccountVO>.Success(account, "年度经费已保存"));
        }
        catch (Exception ex)
        {
            await audit.WriteAsync(FinanceActions.UpdateAccount, FinanceTargetType.Account, dto.AcademicYear, before,
                null, "failure", ex.Message, actor, ct);
            throw;
        }
    }

    // ===== 日志 =====

    [HttpGet("logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<FinanceLogVO>>>> Logs(
        string? action = null, string? operatorName = null, string? targetType = null, string? targetId = null,
        string? result = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var data = await repository.GetLogsAsync(new FinanceLogQuery
        {
            Action = action,
            Operator = operatorName,
            TargetType = targetType,
            TargetId = targetId,
            Result = result,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(ApiResponse<PagedResult<FinanceLogVO>>.Success(data));
    }

    [HttpGet("logs/export")]
    public async Task<IActionResult> ExportLogs(
        string? action = null, string? operatorName = null, string? targetType = null, string? targetId = null,
        string? result = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var logs = await repository.GetLogsForExportAsync(new FinanceLogQuery
        {
            Action = action,
            Operator = operatorName,
            TargetType = targetType,
            TargetId = targetId,
            Result = result,
            From = from,
            To = to
        }, 20000, ct);

        var builder = new StringBuilder();
        builder.AppendLine("时间,操作人,账号,角色,IP,动作,对象类型,对象编号,结果,说明");
        foreach (var log in logs)
        {
            builder.AppendLine(string.Join(',',
                Csv(log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(log.OperatorName), Csv(log.OperatorId), Csv(log.RoleLabel), Csv(log.Ip),
                Csv(log.Action), Csv(log.TargetType), Csv(log.TargetId), Csv(log.Result), Csv(log.Message)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"财务操作日志_{DateTime.Now:yyyyMMddHHmm}.csv");
    }

    // ===== 工具 =====

    private static string Csv(string? value)
    {
        var text = value ?? "";
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        return text;
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value ?? "" : value.Length <= max ? value : value[..max];
}
