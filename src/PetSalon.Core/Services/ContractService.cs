using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;

namespace PetSalon.Core.Services;

public sealed class ContractService
{
    private readonly IPetSalonDbContext _db;
    private readonly IPdfGenerator _pdf;
    private readonly IFileOpener _opener;
    private readonly IClock _clock;
    private readonly ContractOutputOptions _options;

    public ContractService(
        IPetSalonDbContext db,
        IPdfGenerator pdf,
        IFileOpener opener,
        IClock clock,
        ContractOutputOptions options)
    {
        _db = db;
        _pdf = pdf;
        _opener = opener;
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// 產生一份預覽用 PDF 到 temp 目錄（不寫 DB、不開檔）。
    /// 用於對話框中顯示預覽，供使用者確認後再呼叫 CommitPreviewAsync 正式寫入。
    /// </summary>
    public async Task<ContractGenerateOutput> PreviewAsync(string groomingRecordId, CancellationToken ct = default)
        => await PreviewAsync(groomingRecordId, ContractShopSignatures.None, ct);

    public async Task<ContractGenerateOutput> PreviewAsync(
        string groomingRecordId,
        ContractShopSignatures? shopSignatures,
        CancellationToken ct = default)
    {
        var rec = await LoadRecordAsync(groomingRecordId, ct);
        var data = BuildRenderData(rec, ownerSignature: null, shopSignatures);
        var previewDir = Path.Combine(
            Path.GetTempPath(),
            "petsalon-contract-preview",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(previewDir);
        // 預覽用固定 version 0（CommitPreview 時會被覆寫為正式 version）
        return await _pdf.GenerateContractAsync(data, previewDir, nextVersion: 0, ct);
    }

    /// <summary>
    /// 確認預覽後正式產 PDF 到 contracts 目錄並寫 DB；同時刪除 preview 檔。
    /// </summary>
    public async Task<ContractGenerateResult> CommitPreviewAsync(string groomingRecordId, string? previewPath, CancellationToken ct = default)
        => await CommitPreviewAsync(groomingRecordId, previewPath, ContractShopSignatures.None, ct);

    public async Task<ContractGenerateResult> CommitPreviewAsync(
        string groomingRecordId,
        string? previewPath,
        ContractShopSignatures? shopSignatures,
        CancellationToken ct = default)
    {
        var rec = await LoadRecordAsync(groomingRecordId, ct);
        var data = BuildRenderData(rec, ownerSignature: null, shopSignatures);
        var nextVersion = (rec.ContractPaths.Count == 0 ? 0 : rec.ContractPaths.Max(c => c.Version)) + 1;
        var dayFolder = Path.Combine(_options.OutputDir, rec.ServiceDate.ToString("yyyyMMdd"));
        var output = await _pdf.GenerateContractAsync(data, dayFolder, nextVersion, ct);

        rec.ContractPaths.Add(new ContractVersion { Version = output.Version, Path = output.AbsolutePath, GeneratedAt = _clock.Now });
        rec.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);

        if (previewPath is not null) { try { if (File.Exists(previewPath)) File.Delete(previewPath); } catch { } }

        var opened = false;
        try { await _opener.OpenAsync(output.AbsolutePath, ct); opened = true; } catch { }
        return new ContractGenerateResult(Path.GetFileName(output.AbsolutePath), output.AbsolutePath, output.Version, opened);
    }

    private async Task<GroomingRecord> LoadRecordAsync(string groomingRecordId, CancellationToken ct)
    {
        return await _db.GroomingRecords
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Owner)
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Pet)
            .FirstOrDefaultAsync(g => g.GroomingRecordId == groomingRecordId, ct)
            ?? throw AppException.NotFound("RECORD_NOT_FOUND", $"美容紀錄 {groomingRecordId} 不存在");
    }

    private ContractRenderData BuildRenderData(
        GroomingRecord rec,
        byte[]? ownerSignature,
        ContractShopSignatures? shopSignatures)
    {
        var appt = rec.Appointment ?? throw AppException.Conflict("MISSING_DATA", "找不到對應的預約");
        var owner = appt.Owner ?? throw AppException.Conflict("MISSING_DATA", "找不到對應的飼主");
        var pet = appt.Pet ?? throw AppException.Conflict("MISSING_DATA", "找不到對應的寵物");

        var hospName = string.IsNullOrWhiteSpace(owner.PreferredAnimalHospitalName) ? DefaultHospital.Name : owner.PreferredAnimalHospitalName;
        var hospPhone = string.IsNullOrWhiteSpace(owner.PreferredAnimalHospitalPhone) ? DefaultHospital.Phone : owner.PreferredAnimalHospitalPhone;
        var hospAddress = string.IsNullOrWhiteSpace(owner.PreferredAnimalHospitalAddress) ? DefaultHospital.Address : owner.PreferredAnimalHospitalAddress;

        var signatures = shopSignatures ?? ContractShopSignatures.None;
        return new ContractRenderData(
            owner, pet, appt, rec, ownerSignature, hospName, hospPhone, hospAddress,
            signatures.GroomerPng, signatures.ManagerPng);
    }

    public async Task<ContractGenerateResult> GenerateAsync(ContractGenerateInput input, CancellationToken ct = default)
    {
        // R2: null 代表「系統不收集簽名」（簽名板已移除），直接產生 PDF。
        //     仍保留舊路徑：傳入 Array.Empty<byte>() 視為「未完成簽名」而報錯（相容 Phase 1 測試）。
        if (input.OwnerSignaturePng is not null && input.OwnerSignaturePng.Length == 0)
            throw AppException.Unprocessable("MISSING_SIGNATURE", "請完成飼主簽名");

        var rec = await _db.GroomingRecords
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Owner)
            .Include(g => g.Appointment)!.ThenInclude(a => a!.Pet)
            .FirstOrDefaultAsync(g => g.GroomingRecordId == input.GroomingRecordId, ct)
            ?? throw AppException.NotFound("RECORD_NOT_FOUND", $"美容紀錄 {input.GroomingRecordId} 不存在");

        var appt = rec.Appointment ?? throw AppException.Conflict("MISSING_DATA", "找不到對應的預約");
        var owner = appt.Owner ?? throw AppException.Conflict("MISSING_DATA", "找不到對應的飼主");
        var pet = appt.Pet ?? throw AppException.Conflict("MISSING_DATA", "找不到對應的寵物");

        var hospName = string.IsNullOrWhiteSpace(owner.PreferredAnimalHospitalName) ? DefaultHospital.Name : owner.PreferredAnimalHospitalName;
        var hospPhone = string.IsNullOrWhiteSpace(owner.PreferredAnimalHospitalPhone) ? DefaultHospital.Phone : owner.PreferredAnimalHospitalPhone;
        var hospAddress = string.IsNullOrWhiteSpace(owner.PreferredAnimalHospitalAddress) ? DefaultHospital.Address : owner.PreferredAnimalHospitalAddress;

        var data = new ContractRenderData(
            owner,
            pet,
            appt,
            rec,
            input.OwnerSignaturePng,
            hospName,
            hospPhone,
            hospAddress,
            input.GroomerSignaturePng,
            input.ManagerSignaturePng);

        var nextVersion = (rec.ContractPaths.Count == 0 ? 0 : rec.ContractPaths.Max(c => c.Version)) + 1;
        var dayFolder = Path.Combine(_options.OutputDir, rec.ServiceDate.ToString("yyyyMMdd"));
        var output = await _pdf.GenerateContractAsync(data, dayFolder, nextVersion, ct);

        rec.ContractPaths.Add(new ContractVersion
        {
            Version = output.Version,
            Path = output.AbsolutePath,
            GeneratedAt = _clock.Now,
        });
        rec.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);

        var opened = false;
        try { await _opener.OpenAsync(output.AbsolutePath, ct); opened = true; } catch { }

        return new ContractGenerateResult(
            Path.GetFileName(output.AbsolutePath),
            output.AbsolutePath,
            output.Version,
            opened);
    }

    public async Task OpenAsync(string contractPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(contractPath) || !File.Exists(contractPath))
            throw AppException.NotFound("NOT_FOUND", "找不到契約 PDF");
        await _opener.OpenAsync(contractPath, ct);
    }
}

public sealed class ContractOutputOptions
{
    public string OutputDir { get; init; } = "contracts";
}
