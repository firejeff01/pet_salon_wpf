using FluentAssertions;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Entities;
using PetSalon.Infrastructure.Pdf;
using Xunit;

namespace PetSalon.Core.Tests.Pdf;

/// <summary>
/// 整合測試：實際啟 Chromium 產 PDF。
/// 第一次跑會下載 Chromium（~150MB，慢），之後 cache 之後 ~3-5s/test。
/// </summary>
[Trait("category", "integration")]
public class PuppeteerSharpContractGeneratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _templatePath;
    private readonly string _fontPath;
    private readonly string _chromiumCacheDir;

    public PuppeteerSharpContractGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pdf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // 找模板與字型（複製在 Infrastructure 輸出資料夾）
        var infraOutput = Path.GetDirectoryName(typeof(PuppeteerSharpContractGenerator).Assembly.Location)!;
        _templatePath = Path.Combine(infraOutput, "Pdf", "Templates", "contract.hbs");
        _fontPath = Path.Combine(infraOutput, "Pdf", "Assets", "kaiu.ttf");

        // Chromium cache 共用，避免每個測試類別重複下載
        _chromiumCacheDir = Path.Combine(Path.GetTempPath(), "petsalon-test-chromium-cache");
        Directory.CreateDirectory(_chromiumCacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static ContractRenderData SampleData(string ownerName = "張三", string petName = "毛毛")
    {
        var owner = new Owner
        {
            OwnerId = "owner_1", Name = ownerName, NationalId = "A123456789",
            Phone = "0912-345-678", Address = "桃園市桃園區中山路 1 號",
            EmergencyContactName = "李四", EmergencyContactPhone = "0987-654-321",
            EmergencyContactRelationship = "配偶",
            PreferredAnimalHospitalName = "安欣動物醫院",
            PreferredAnimalHospitalPhone = "03-3367775",
            PreferredAnimalHospitalAddress = "桃園市桃園區中福街60號",
            IsStoredValueCustomer = true,
            StoredValueBalance = 1000m,
        };
        var pet = new Pet
        {
            PetId = "pet_1", OwnerId = owner.OwnerId, Name = petName,
            Species = "犬", Breed = "柴犬", Gender = "公", Age = "3 歲",
            IsNeutered = true, ChipNumber = "0123456789",
            Personality = new List<string> { "親人", "親狗" },
            MedicalHistory = new List<string> { "心臟病" },
            PhysicalExamination = new PhysicalExamination(),
        };
        var appt = new Appointment
        {
            AppointmentId = "appt_1", OwnerId = owner.OwnerId, PetId = pet.PetId,
            Date = new DateOnly(2026, 5, 16), Time = new TimeOnly(10, 0),
        };
        var rec = new GroomingRecord
        {
            GroomingRecordId = "rec_1", AppointmentId = appt.AppointmentId,
            ServiceDate = appt.Date, ServiceTime = appt.Time,
            Services = new()
            {
                new GroomingServiceItem { Item = "洗澡", Price = 500 },
                new GroomingServiceItem { Item = "美容", Price = 800 },
            },
            TotalCost = 1300, StoredValueDeduction = 1000, CashPayment = 300,
            Personality = pet.Personality.ToList(),
            MedicalHistory = pet.MedicalHistory.ToList(),
            PhysicalExamination = new PhysicalExamination(),
        };
        var fakeSig = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        return new ContractRenderData(owner, pet, appt, rec, fakeSig,
            owner.PreferredAnimalHospitalName, owner.PreferredAnimalHospitalPhone, owner.PreferredAnimalHospitalAddress);
    }

    [Fact]
    public void Template_and_font_assets_exist_in_output()
    {
        File.Exists(_templatePath).Should().BeTrue($"模板應該複製到 {_templatePath}");
        File.Exists(_fontPath).Should().BeTrue($"字型應該複製到 {_fontPath}");
    }

    [Fact]
    public void Renderer_produces_html_with_owner_and_pet_fields()
    {
        var renderer = new ContractTemplateRenderer(_templatePath, _fontPath);
        var html = renderer.Render(SampleData("王小明", "毛毛球"));

        // Dump to file for inspection on failure (穩定路徑，不會被 Dispose 清掉)
        var stableDir = Path.Combine(Path.GetTempPath(), "petsalon-render-dump");
        Directory.CreateDirectory(stableDir);
        var dumpPath = Path.Combine(stableDir, "render.html");
        File.WriteAllText(dumpPath, html);

        html.Should().Contain("王小明", $"rendered html dumped to {dumpPath}");
        html.Should().Contain("毛毛球");
        html.Should().Contain("柴犬");
        html.Should().Contain("民國");
        html.Should().Contain("data:font/ttf;base64,", "字型應該以 data URI 嵌入");
    }

    [Fact]
    public void Renderer_marks_selected_personality_and_medical_history()
    {
        var renderer = new ContractTemplateRenderer(_templatePath, _fontPath);
        var html = renderer.Render(SampleData());
        // 選中項目會有 "on" class
        html.Should().Contain("cb on");
    }

    [Fact]
    public async Task GenerateContract_writes_valid_pdf_with_pdf_magic_header()
    {
        await using var gen = new PuppeteerSharpContractGenerator(_templatePath, _fontPath, _chromiumCacheDir);
        var output = await gen.GenerateContractAsync(SampleData(), _tempDir, nextVersion: 1);

        File.Exists(output.AbsolutePath).Should().BeTrue();
        var bytes = await File.ReadAllBytesAsync(output.AbsolutePath);
        bytes.Length.Should().BeGreaterThan(5000, "真 PDF 應該不只幾百 byte");
        // PDF magic: %PDF
        bytes[0].Should().Be(0x25);  // %
        bytes[1].Should().Be(0x50);  // P
        bytes[2].Should().Be(0x44);  // D
        bytes[3].Should().Be(0x46);  // F
    }

    /// <summary>產一份範例 PDF 到 %TEMP%\petsalon-pdf-sample\ 給人類目視檢查。</summary>
    [Fact]
    public async Task DumpSamplePdf_for_manual_inspection()
    {
        var stableDir = Path.Combine(Path.GetTempPath(), "petsalon-pdf-sample");
        Directory.CreateDirectory(stableDir);
        foreach (var f in Directory.GetFiles(stableDir, "*.pdf")) File.Delete(f);

        await using var gen = new PuppeteerSharpContractGenerator(_templatePath, _fontPath, _chromiumCacheDir);
        var output = await gen.GenerateContractAsync(SampleData("王小明", "毛毛球"), stableDir, nextVersion: 1);

        File.Exists(output.AbsolutePath).Should().BeTrue();
        File.WriteAllText(Path.Combine(stableDir, "latest.txt"), output.AbsolutePath);
    }

    [Fact]
    public async Task GenerateContract_filename_contains_version_and_names()
    {
        await using var gen = new PuppeteerSharpContractGenerator(_templatePath, _fontPath, _chromiumCacheDir);
        var output = await gen.GenerateContractAsync(SampleData("林大華", "球球"), _tempDir, nextVersion: 3);

        var name = Path.GetFileName(output.AbsolutePath);
        name.Should().Contain("林大華");
        name.Should().Contain("球球");
        name.Should().Contain("v3");
        name.Should().EndWith(".pdf");
    }
}
