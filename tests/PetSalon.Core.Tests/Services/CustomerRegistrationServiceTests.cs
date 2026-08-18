using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public sealed class CustomerRegistrationServiceTests : ServiceTestBase
{
    private CustomerRegistrationService Svc => new(Db, Ids, Clock);

    private static CustomerRegistrationInput ValidInput() => new()
    {
        Owner = new OwnerInput
        {
            Name = "王小明",
            NationalId = "A123456789",
            Phone = "0912345678",
            Address = "桃園市桃園區",
            Note = "飼主備註",
        },
        Pets =
        [
            new CustomerPetInput
            {
                Name = "毛毛",
                Species = "犬",
                Breed = "柴犬",
                Gender = "公",
                Age = "3 歲",
                ChipStatus = ChipStatusOptions.HasChip,
                ChipData = "900000000001",
                Personality = ["親人"],
                MedicalHistory = ["心臟病"],
                MedicalHistoryOther = "定期回診",
                Note = "寵物備註",
            },
        ],
    };

    [Fact]
    public async Task Create_persists_owner_pet_notes_medical_history_and_chip()
    {
        var result = await Svc.CreateAsync(ValidInput());

        result.Owner.Note.Should().Be("飼主備註");
        result.Pets.Should().ContainSingle();
        var pet = result.Pets.Single();
        pet.Note.Should().Be("寵物備註");
        pet.MedicalHistory.Should().Equal("心臟病");
        pet.MedicalHistoryOther.Should().Be("定期回診");
        pet.ChipNumber.Should().Be("900000000001");
        pet.UnregisteredIdMethod.Should().BeNull();
    }

    [Fact]
    public async Task Create_rejects_has_chip_without_number_before_writing_anything()
    {
        var input = ValidInput();
        input.Pets[0].ChipData = " ";

        await FluentActions.Awaiting(() => Svc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*晶片號碼*必填*");
        (await Db.Owners.CountAsync()).Should().Be(0);
        (await Db.Pets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_warns_for_duplicate_phone_but_allows_explicit_override()
    {
        await Svc.CreateAsync(ValidInput());
        var duplicate = ValidInput();
        duplicate.Owner.Name = "王小華";
        duplicate.Owner.NationalId = "B223456789";

        var exception = await FluentActions.Awaiting(() => Svc.CreateAsync(duplicate))
            .Should().ThrowAsync<AppException>();
        exception.Which.Code.Should().Be("POTENTIAL_DUPLICATE_OWNER");

        duplicate.AllowDuplicate = true;
        await Svc.CreateAsync(duplicate);
        (await Db.Owners.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Create_maps_no_chip_to_optional_alternative_identifier()
    {
        var input = ValidInput();
        input.Pets[0].ChipStatus = ChipStatusOptions.NoChip;
        input.Pets[0].ChipData = "紅色項圈牌 A01";

        var pet = (await Svc.CreateAsync(input)).Pets.Single();
        pet.ChipNumber.Should().BeNull();
        pet.UnregisteredIdMethod.Should().Be("紅色項圈牌 A01");
    }
}
