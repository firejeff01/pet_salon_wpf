using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class ContractServiceTests : ServiceTestBase
{
    private readonly FakePdfGenerator _pdf = new();
    private readonly NoopFileOpener _opener = new();
    private readonly string _tempDir;
    private OwnerService OwnerSvc => new(Db, Ids, Clock);
    private PetService PetSvc => new(Db, Ids, Clock);
    private AppointmentService ApptSvc => new(Db, Ids, Clock);
    private GroomingRecordService GroomingSvc => new(Db, Ids, Clock, new StoredValueService());
    private ContractService ContractSvc =>
        new(Db, _pdf, _opener, Clock, new ContractOutputOptions { OutputDir = _tempDir });

    public ContractServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "petsalon-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public new void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<string> SetupRecordAsync(string? hospitalName = null)
    {
        var owner = await OwnerSvc.CreateAsync(new OwnerInput
        {
            Name = "張三", NationalId = "A1", Phone = "0912",
            Address = "桃園", EmergencyContactName = "聯絡人",
            EmergencyContactPhone = "0987", EmergencyContactRelationship = "配偶",
            PreferredAnimalHospitalName = hospitalName ?? string.Empty,
        });
        var pet = await PetSvc.CreateAsync(new PetInput
        {
            OwnerId = owner.OwnerId, Name = "毛毛", Species = "犬", Breed = "柴犬",
            Gender = "公", Age = "3", IsNeutered = true,
            Personality = new() { "親人" }, MedicalHistory = new(),
            PhysicalExamination = new PhysicalExamination(),
        });
        var appt = await ApptSvc.CreateAsync(new AppointmentCreateInput
        {
            PetId = pet.PetId,
            Date = new DateOnly(2026, 6, 15),
            Time = new TimeOnly(10, 0),
        });
        var rec = await GroomingSvc.SaveAsync(new GroomingRecordInput
        {
            AppointmentId = appt.AppointmentId,
            Services = new() { new() { Item = "洗澡", Price = 500 } },
            Personality = new() { "親人" },
            MedicalHistory = new(),
            PhysicalExamination = new PhysicalExamination(),
        });
        return rec.GroomingRecordId;
    }

    private static byte[] FakeSignature() => new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header

    // ===================== Generate =====================

    [Fact]
    public async Task Generate_fails_when_signature_missing()
    {
        var recId = await SetupRecordAsync();
        await FluentActions.Awaiting(() => ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId,
            OwnerSignaturePng = Array.Empty<byte>(),
        }))
        .Should().ThrowAsync<AppException>().Where(e => e.Code == "MISSING_SIGNATURE");
    }

    [Fact]
    public async Task Generate_fails_when_record_not_found()
    {
        await FluentActions.Awaiting(() => ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = "missing",
            OwnerSignaturePng = FakeSignature(),
        }))
        .Should().ThrowAsync<AppException>().Where(e => e.Code == "RECORD_NOT_FOUND");
    }

    [Fact]
    public async Task Generate_produces_v1_for_first_call()
    {
        var recId = await SetupRecordAsync();
        var result = await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId,
            OwnerSignaturePng = FakeSignature(),
        });
        result.Version.Should().Be(1);
        File.Exists(result.AbsolutePath).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_increments_version_for_subsequent_calls()
    {
        var recId = await SetupRecordAsync();
        var first = await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });
        var second = await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });
        first.Version.Should().Be(1);
        second.Version.Should().Be(2);
    }

    [Fact]
    public async Task Generate_writes_to_dated_subfolder()
    {
        var recId = await SetupRecordAsync();
        var result = await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });
        result.AbsolutePath.Should().Contain("20260615");
    }

    [Fact]
    public async Task Generate_uses_default_hospital_when_owner_blank()
    {
        var recId = await SetupRecordAsync(hospitalName: null);
        await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });

        _pdf.CapturedCalls.Should().ContainSingle();
        _pdf.CapturedCalls[0].HospitalName.Should().Be("安欣動物醫院");
    }

    [Fact]
    public async Task Generate_uses_owner_hospital_when_provided()
    {
        var recId = await SetupRecordAsync(hospitalName: "其他獸醫院");
        await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });
        _pdf.CapturedCalls[0].HospitalName.Should().Be("其他獸醫院");
    }

    [Fact]
    public async Task Generate_appends_contract_version_to_record()
    {
        var recId = await SetupRecordAsync();
        await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });

        var rec = await GroomingSvc.GetByIdAsync(recId);
        rec.ContractPaths.Should().ContainSingle();
        rec.ContractPaths[0].Version.Should().Be(1);
    }

    [Fact]
    public async Task Generate_opens_file_via_FileOpener_after_writing()
    {
        var recId = await SetupRecordAsync();
        var result = await ContractSvc.GenerateAsync(new ContractGenerateInput
        {
            GroomingRecordId = recId, OwnerSignaturePng = FakeSignature(),
        });
        _opener.Opened.Should().ContainSingle().Which.Should().Be(result.AbsolutePath);
        result.OpenedInViewer.Should().BeTrue();
    }

    // ===================== Open existing =====================

    [Fact]
    public async Task OpenAsync_fails_when_file_missing()
    {
        await FluentActions.Awaiting(() => ContractSvc.OpenAsync("C:\\does\\not\\exist.pdf"))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "NOT_FOUND");
    }

    [Fact]
    public async Task OpenAsync_invokes_FileOpener_for_existing()
    {
        var path = Path.Combine(_tempDir, "test.pdf");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        await ContractSvc.OpenAsync(path);
        _opener.Opened.Should().Contain(path);
    }
}
