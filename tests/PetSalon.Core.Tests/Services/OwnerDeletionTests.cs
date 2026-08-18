using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public sealed class OwnerDeletionTests : ServiceTestBase
{
    private OwnerService OwnerSvc => new(Db, Ids, Clock);
    private PetService PetSvc => new(Db, Ids, Clock);

    private async Task<(Owner Owner, Pet Pet)> SeedAsync()
    {
        var owner = await OwnerSvc.CreateAsync(new OwnerInput
        {
            Name = "待刪顧客",
            NationalId = "A123456789",
            Phone = "0912345678",
            Address = "桃園市",
        });
        var pet = await PetSvc.CreateAsync(new PetInput
        {
            OwnerId = owner.OwnerId,
            Name = "毛毛",
            Species = "犬",
            Breed = "米克斯",
            Gender = "公",
            Age = "2 歲",
            Personality = ["親人"],
            PhysicalExamination = new PhysicalExamination(),
        });
        return (owner, pet);
    }

    [Fact]
    public async Task Delete_removes_owner_and_pets_when_no_history_exists()
    {
        var (owner, _) = await SeedAsync();

        await OwnerSvc.DeleteAsync(owner.OwnerId);

        (await Db.Owners.CountAsync()).Should().Be(0);
        (await Db.Pets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_is_blocked_when_appointment_history_exists()
    {
        var (owner, pet) = await SeedAsync();
        Db.Appointments.Add(new Appointment
        {
            AppointmentId = "appt_history",
            OwnerId = owner.OwnerId,
            PetId = pet.PetId,
            Date = new DateOnly(2026, 8, 18),
            Time = new TimeOnly(10, 0),
            CreatedAt = Clock.Now,
            UpdatedAt = Clock.Now,
        });
        await Db.SaveChangesAsync();

        var exception = await FluentActions.Awaiting(() => OwnerSvc.DeleteAsync(owner.OwnerId))
            .Should().ThrowAsync<AppException>();

        exception.Which.Code.Should().Be("OWNER_HAS_HISTORY");
        (await Db.Owners.CountAsync()).Should().Be(1);
        (await Db.Pets.CountAsync()).Should().Be(1);
    }
}
