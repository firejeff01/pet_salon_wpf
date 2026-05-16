using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class PetServiceTests : ServiceTestBase
{
    private OwnerService OwnerSvc => new(Db, Ids, Clock);
    private PetService PetSvc => new(Db, Ids, Clock);

    private async Task<string> CreateOwnerAsync(string name = "張三")
    {
        var owner = await OwnerSvc.CreateAsync(new OwnerInput
        {
            Name = name,
            NationalId = "A123",
            Phone = "0912",
            Address = "桃園",
            EmergencyContactName = "聯絡人",
            EmergencyContactPhone = "0987",
            EmergencyContactRelationship = "配偶",
        });
        return owner.OwnerId;
    }

    private static PetInput ValidPet(string ownerId, string name = "毛毛") => new()
    {
        OwnerId = ownerId,
        Name = name,
        Species = "犬",
        Breed = "柴犬",
        Gender = "公",
        Age = "3 歲",
        IsNeutered = true,
        Personality = new List<string> { "親人", "親狗" },
        MedicalHistory = new List<string>(),
        PhysicalExamination = new PhysicalExamination(),
    };

    // ===================== Create =====================

    [Fact]
    public async Task Create_happy_path_persists_with_owner_relationship()
    {
        var ownerId = await CreateOwnerAsync();
        var pet = await PetSvc.CreateAsync(ValidPet(ownerId));
        pet.PetId.Should().StartWith("pet_");
        pet.OwnerId.Should().Be(ownerId);
        pet.CreatedAt.Should().Be(Clock.Now);
    }

    [Fact]
    public async Task Create_fails_when_owner_missing()
    {
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(ValidPet("nonexistent")))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "OWNER_NOT_FOUND");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_blank_name(string blank)
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Name = blank;
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*寵物名稱*必填*");
    }

    [Theory]
    [InlineData("鳥")]
    [InlineData("dog")]
    [InlineData("")]
    public async Task Create_rejects_invalid_species(string bad)
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Species = bad;
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*物種*犬*貓*");
    }

    [Fact]
    public async Task Create_rejects_blank_breed()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Breed = "";
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*品種*必填*");
    }

    [Theory]
    [InlineData("中性")]
    [InlineData("male")]
    [InlineData("")]
    public async Task Create_rejects_invalid_gender(string bad)
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Gender = bad;
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*性別*公*母*");
    }

    [Fact]
    public async Task Create_rejects_blank_age()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Age = "";
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*年齡*必填*");
    }

    [Fact]
    public async Task Create_rejects_empty_personality()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Personality = new();
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*個性*至少*");
    }

    [Fact]
    public async Task Create_rejects_invalid_personality_option()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Personality = new() { "暴衝" };  // 不在白名單
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*個性*暴衝*允許*");
    }

    [Fact]
    public async Task Create_rejects_invalid_medical_history_option()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.MedicalHistory = new() { "不存在的病" };
        await FluentActions.Awaiting(() => PetSvc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*病史*不存在的病*允許*");
    }

    [Fact]
    public async Task Create_allows_empty_medical_history()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.MedicalHistory = new();
        var pet = await PetSvc.CreateAsync(input);
        pet.MedicalHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_normalizes_empty_chip_to_null()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.ChipNumber = "";
        var pet = await PetSvc.CreateAsync(input);
        pet.ChipNumber.Should().BeNull();
    }

    [Fact]
    public async Task Create_normalizes_whitespace_chip_to_null()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.ChipNumber = "   ";
        var pet = await PetSvc.CreateAsync(input);
        pet.ChipNumber.Should().BeNull();
    }

    [Fact]
    public async Task Create_persists_chip_number_when_provided()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.ChipNumber = "0123456789";
        var pet = await PetSvc.CreateAsync(input);
        pet.ChipNumber.Should().Be("0123456789");
    }

    [Fact]
    public async Task Create_persists_physical_examination_fields()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.PhysicalExamination = new PhysicalExamination
        {
            Eyes = "異常", Ears = "正常", Teeth = "異常",
            Limbs = "正常", Skin = "正常", Fur = "打結",
        };
        var pet = await PetSvc.CreateAsync(input);
        pet.PhysicalExamination.Eyes.Should().Be("異常");
        pet.PhysicalExamination.Teeth.Should().Be("異常");
        pet.PhysicalExamination.Fur.Should().Be("打結");
    }

    [Fact]
    public async Task Create_trims_breed_and_note_whitespace()
    {
        var ownerId = await CreateOwnerAsync();
        var input = ValidPet(ownerId);
        input.Breed = "  柴犬  ";
        input.Note = "  乖巧  ";
        var pet = await PetSvc.CreateAsync(input);
        pet.Breed.Should().Be("柴犬");
        pet.Note.Should().Be("乖巧");
    }

    // ===================== Update =====================

    [Fact]
    public async Task Update_throws_when_pet_not_found()
    {
        var ownerId = await CreateOwnerAsync();
        await FluentActions.Awaiting(() => PetSvc.UpdateAsync("missing", ValidPet(ownerId)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "PET_NOT_FOUND");
    }

    [Fact]
    public async Task Update_modifies_pet_and_refreshes_UpdatedAt()
    {
        var ownerId = await CreateOwnerAsync();
        var pet = await PetSvc.CreateAsync(ValidPet(ownerId));

        Clock.Now = Clock.Now.AddHours(2);
        var input = ValidPet(ownerId);
        input.Name = "改名";
        var updated = await PetSvc.UpdateAsync(pet.PetId, input);

        updated.Name.Should().Be("改名");
        updated.UpdatedAt.Should().Be(Clock.Now);
    }

    [Fact]
    public async Task Update_validation_still_enforced()
    {
        var ownerId = await CreateOwnerAsync();
        var pet = await PetSvc.CreateAsync(ValidPet(ownerId));

        var bad = ValidPet(ownerId);
        bad.Personality = new();  // 必填，違規
        await FluentActions.Awaiting(() => PetSvc.UpdateAsync(pet.PetId, bad))
            .Should().ThrowAsync<AppException>().WithMessage("*個性*");
    }

    // ===================== GetById + List =====================

    [Fact]
    public async Task GetByIdAsync_includes_owner_navigation()
    {
        var ownerId = await CreateOwnerAsync();
        var pet = await PetSvc.CreateAsync(ValidPet(ownerId));

        var found = await PetSvc.GetByIdAsync(pet.PetId);
        found.Owner.Should().NotBeNull();
        found.Owner!.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_not_found()
    {
        await FluentActions.Awaiting(() => PetSvc.GetByIdAsync("missing"))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "PET_NOT_FOUND");
    }

    [Fact]
    public async Task ListAsync_filters_by_owner_only()
    {
        var o1 = await CreateOwnerAsync("A");
        var o2 = await CreateOwnerAsync("B");
        await PetSvc.CreateAsync(ValidPet(o1, "毛毛"));
        await PetSvc.CreateAsync(ValidPet(o2, "汪汪"));

        (await PetSvc.ListAsync(o1)).Should().ContainSingle().Which.Name.Should().Be("毛毛");
        (await PetSvc.ListAsync(o2)).Should().ContainSingle().Which.Name.Should().Be("汪汪");
    }

    [Fact]
    public async Task ListAsync_filters_by_keyword_in_name_or_breed()
    {
        var o = await CreateOwnerAsync();
        var p1 = ValidPet(o, "毛毛"); p1.Breed = "柴犬";
        var p2 = ValidPet(o, "汪汪"); p2.Breed = "黃金獵犬";
        await PetSvc.CreateAsync(p1);
        await PetSvc.CreateAsync(p2);

        (await PetSvc.ListAsync(null, "毛毛")).Should().ContainSingle();
        (await PetSvc.ListAsync(null, "獵")).Should().ContainSingle().Which.Name.Should().Be("汪汪");
        (await PetSvc.ListAsync(null, "犬")).Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_returns_empty_on_no_match()
    {
        var o = await CreateOwnerAsync();
        await PetSvc.CreateAsync(ValidPet(o));
        (await PetSvc.ListAsync(o, "no_match_xyz")).Should().BeEmpty();
    }
}
