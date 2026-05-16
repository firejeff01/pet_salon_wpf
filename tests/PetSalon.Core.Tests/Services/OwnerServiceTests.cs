using FluentAssertions;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Core.Tests.Helpers;
using Xunit;

namespace PetSalon.Core.Tests.Services;

public class OwnerServiceTests : ServiceTestBase
{
    private OwnerService Svc => new(Db, Ids, Clock);

    private static OwnerInput ValidInput(string name = "張三") => new()
    {
        Name = name,
        NationalId = "A123456789",
        Phone = "0912-345-678",
        Address = "桃園市桃園區中山路 1 號",
        EmergencyContactName = "李四",
        EmergencyContactPhone = "0987-654-321",
        EmergencyContactRelationship = "配偶",
    };

    // ===================== Create =====================

    [Fact]
    public async Task Create_happy_path_persists_owner_with_generated_id_and_timestamps()
    {
        var owner = await Svc.CreateAsync(ValidInput());

        owner.OwnerId.Should().StartWith("owner_");
        owner.Name.Should().Be("張三");
        owner.CreatedAt.Should().Be(Clock.Now);
        owner.UpdatedAt.Should().Be(Clock.Now);
        (await Svc.ListAsync()).Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_blank_name(string blank)
    {
        var input = ValidInput();
        input.Name = blank;
        await FluentActions.Awaiting(() => Svc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*姓名*必填*");
    }

    [Theory]
    [InlineData("NationalId")]
    [InlineData("Phone")]
    [InlineData("Address")]
    // R1 業主決議：EmergencyContactName / EmergencyContactPhone / EmergencyContactRelationship 已改為非必填，移除對應 InlineData
    public async Task Create_rejects_blank_required(string field)
    {
        var input = ValidInput();
        typeof(OwnerInput).GetProperty(field)!.SetValue(input, "");
        await FluentActions.Awaiting(() => Svc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().Where(e => e.Kind == AppErrorKind.Validation);
    }

    [Fact]
    public async Task Create_rejects_negative_stored_value_balance()
    {
        var input = ValidInput();
        input.StoredValueBalance = -1m;
        await FluentActions.Awaiting(() => Svc.CreateAsync(input))
            .Should().ThrowAsync<AppException>().WithMessage("*儲值餘額*負數*");
    }

    [Fact]
    public async Task Create_fills_default_hospital_when_blank()
    {
        var owner = await Svc.CreateAsync(ValidInput());
        owner.PreferredAnimalHospitalName.Should().Be("安欣動物醫院");
        owner.PreferredAnimalHospitalPhone.Should().Be("03-3367775");
        owner.PreferredAnimalHospitalAddress.Should().Be("桃園市桃園區中福街60號");
    }

    [Fact]
    public async Task Create_keeps_custom_hospital_when_provided()
    {
        var input = ValidInput();
        input.PreferredAnimalHospitalName = "其他獸醫院";
        input.PreferredAnimalHospitalPhone = "02-1234";
        input.PreferredAnimalHospitalAddress = "台北市";

        var owner = await Svc.CreateAsync(input);
        owner.PreferredAnimalHospitalName.Should().Be("其他獸醫院");
        owner.PreferredAnimalHospitalPhone.Should().Be("02-1234");
        owner.PreferredAnimalHospitalAddress.Should().Be("台北市");
    }

    [Fact]
    public async Task Create_trims_whitespace_on_fields()
    {
        var input = ValidInput("   王五   ");
        var owner = await Svc.CreateAsync(input);
        owner.Name.Should().Be("王五");
    }

    [Fact]
    public async Task Create_accepts_stored_value_customer_with_positive_balance()
    {
        var input = ValidInput();
        input.IsStoredValueCustomer = true;
        input.StoredValueBalance = 5000m;
        var owner = await Svc.CreateAsync(input);
        owner.IsStoredValueCustomer.Should().BeTrue();
        owner.StoredValueBalance.Should().Be(5000m);
    }

    // ===================== Update =====================

    [Fact]
    public async Task Update_throws_when_owner_not_found()
    {
        await FluentActions.Awaiting(() => Svc.UpdateAsync("not_exists", ValidInput()))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "OWNER_NOT_FOUND" && e.Kind == AppErrorKind.NotFound);
    }

    [Fact]
    public async Task Update_modifies_owner_and_refreshes_UpdatedAt()
    {
        var created = await Svc.CreateAsync(ValidInput());
        Clock.Now = Clock.Now.AddDays(1);

        var input = ValidInput("新名字");
        var updated = await Svc.UpdateAsync(created.OwnerId, input);

        updated.Name.Should().Be("新名字");
        updated.UpdatedAt.Should().Be(Clock.Now);
        updated.CreatedAt.Should().NotBe(Clock.Now);
    }

    // ===================== GetById =====================

    [Fact]
    public async Task GetByIdAsync_throws_when_not_found()
    {
        await FluentActions.Awaiting(() => Svc.GetByIdAsync("nope"))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "OWNER_NOT_FOUND");
    }

    [Fact]
    public async Task GetByIdAsync_returns_existing_owner()
    {
        var created = await Svc.CreateAsync(ValidInput());
        var found = await Svc.GetByIdAsync(created.OwnerId);
        found.Name.Should().Be("張三");
    }

    // ===================== List + Search =====================

    [Fact]
    public async Task ListAsync_returns_all_when_no_keyword()
    {
        await Svc.CreateAsync(ValidInput("張三"));
        await Svc.CreateAsync(ValidInput("李四"));

        (await Svc.ListAsync()).Should().HaveCount(2);
    }

    [Theory]
    [InlineData("張", 1)]
    [InlineData("0912", 2)]  // 兩位都用同電話前綴
    [InlineData("A123", 2)]  // 兩位都用同身分證前綴
    [InlineData("XYZ_NOMATCH", 0)]
    public async Task ListAsync_filters_by_keyword_across_name_phone_nationalId(string keyword, int expectedCount)
    {
        var a = ValidInput("張三"); a.Phone = "0912-111"; a.NationalId = "A123_a";
        var b = ValidInput("李四"); b.Phone = "0912-222"; b.NationalId = "A123_b";
        await Svc.CreateAsync(a);
        await Svc.CreateAsync(b);

        (await Svc.ListAsync(keyword)).Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task ListAsync_handles_empty_db()
    {
        (await Svc.ListAsync()).Should().BeEmpty();
        (await Svc.ListAsync("anything")).Should().BeEmpty();
    }

    [Fact]
    public async Task ListPetsAsync_throws_when_owner_not_found()
    {
        await FluentActions.Awaiting(() => Svc.ListPetsAsync("not_exists"))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "OWNER_NOT_FOUND");
    }

    [Fact]
    public async Task ListPetsAsync_returns_empty_for_owner_with_no_pets()
    {
        var owner = await Svc.CreateAsync(ValidInput());
        (await Svc.ListPetsAsync(owner.OwnerId)).Should().BeEmpty();
    }

    // ===================== AdjustStoredValue =====================

    [Fact]
    public async Task AdjustStoredValue_increases_balance()
    {
        var owner = await Svc.CreateAsync(ValidInput());
        var updated = await Svc.AdjustStoredValueAsync(owner.OwnerId, 500m);
        updated.StoredValueBalance.Should().Be(500m);
    }

    [Fact]
    public async Task AdjustStoredValue_decreases_balance()
    {
        var input = ValidInput();
        input.IsStoredValueCustomer = true;
        input.StoredValueBalance = 500m;
        var owner = await Svc.CreateAsync(input);

        var updated = await Svc.AdjustStoredValueAsync(owner.OwnerId, -200m);
        updated.StoredValueBalance.Should().Be(300m);
        updated.IsStoredValueCustomer.Should().BeTrue();
    }

    [Fact]
    public async Task AdjustStoredValue_to_zero_clears_isStoredValueCustomer()
    {
        var input = ValidInput();
        input.IsStoredValueCustomer = true;
        input.StoredValueBalance = 100m;
        var owner = await Svc.CreateAsync(input);

        var updated = await Svc.AdjustStoredValueAsync(owner.OwnerId, -100m);
        updated.StoredValueBalance.Should().Be(0);
        updated.IsStoredValueCustomer.Should().BeFalse();
    }

    [Fact]
    public async Task AdjustStoredValue_below_zero_throws()
    {
        var owner = await Svc.CreateAsync(ValidInput());
        await FluentActions.Awaiting(() => Svc.AdjustStoredValueAsync(owner.OwnerId, -50m))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "STORED_VALUE_NEGATIVE");
    }

    [Fact]
    public async Task AdjustStoredValue_on_missing_owner_throws()
    {
        await FluentActions.Awaiting(() => Svc.AdjustStoredValueAsync("missing", 100m))
            .Should().ThrowAsync<AppException>().Where(e => e.Code == "OWNER_NOT_FOUND");
    }
}
