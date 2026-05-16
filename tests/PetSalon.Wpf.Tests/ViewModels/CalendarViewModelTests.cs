using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Wpf.Tests.Helpers;
using PetSalon.Wpf.ViewModels;
using Xunit;

namespace PetSalon.Wpf.Tests.ViewModels;

public class CalendarViewModelTests : VmTestBase
{
    private readonly FakeNavigationService _nav = new();
    private CalendarViewModel CreateVm() => new(ScopeFactory, _nav);

    [Fact]
    public void Constructor_sets_CurrentMonth_to_first_of_current_month()
    {
        var vm = CreateVm();
        vm.CurrentMonth.Day.Should().Be(1);
        vm.CurrentMonth.Year.Should().Be(DateTime.Today.Year);
        vm.CurrentMonth.Month.Should().Be(DateTime.Today.Month);
    }

    [Fact]
    public async Task InitializeAsync_fills_42_day_cells_for_grid()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.Days.Should().HaveCount(42);
    }

    [Fact]
    public async Task InitializeAsync_marks_IsCurrentMonth_correctly()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        // 應該有 28~31 天屬於本月，其他屬於前/後月
        var thisMonthCells = vm.Days.Count(d => d.IsCurrentMonth);
        thisMonthCells.Should().BeInRange(28, 31);
        vm.Days.Count(d => !d.IsCurrentMonth).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InitializeAsync_marks_today_correctly()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        vm.Days.Single(d => d.Date == today).IsToday.Should().BeTrue();
    }

    [Fact]
    public async Task PrevMonth_decreases_month()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        var original = vm.CurrentMonth;
        vm.PrevMonthCommand.Execute(null);
        vm.CurrentMonth.Should().Be(original.AddMonths(-1));
    }

    [Fact]
    public async Task NextMonth_increases_month()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        var original = vm.CurrentMonth;
        vm.NextMonthCommand.Execute(null);
        vm.CurrentMonth.Should().Be(original.AddMonths(1));
    }

    [Fact]
    public async Task Today_resets_to_current_month()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.PrevMonthCommand.Execute(null);
        vm.PrevMonthCommand.Execute(null);

        vm.TodayCommand.Execute(null);
        vm.CurrentMonth.Year.Should().Be(DateTime.Today.Year);
        vm.CurrentMonth.Month.Should().Be(DateTime.Today.Month);
    }

    [Fact]
    public async Task SelectDay_navigates_to_DailyAppointmentsViewModel()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        var cell = vm.Days[15];

        vm.SelectDayCommand.Execute(cell);
        _nav.Visited.Should().Contain(typeof(DailyAppointmentsViewModel));
    }

    [Fact]
    public void MonthLabel_uses_chinese_year_month()
    {
        var vm = CreateVm();
        vm.MonthLabel.Should().Contain("年").And.Contain("月");
    }

    // ============ TDD #4: 行事曆週/日視圖 ============

    [Fact]
    public void Default_view_mode_is_Month()
    {
        var vm = CreateVm();
        vm.ViewMode.Should().Be(CalendarViewMode.Month);
    }

    [Fact]
    public async Task SwitchToWeek_changes_mode_and_keeps_42_cells_renders_7()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();

        vm.SwitchToWeekCommand.Execute(null);
        vm.ViewMode.Should().Be(CalendarViewMode.Week);

        // 週視圖只回 7 天
        vm.WeekDays.Should().HaveCount(7);
    }

    [Fact]
    public async Task SwitchToDay_changes_mode_and_loads_day_appointments()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SelectedDate = DateOnly.FromDateTime(DateTime.Today);

        vm.SwitchToDayCommand.Execute(null);
        vm.ViewMode.Should().Be(CalendarViewMode.Day);
        vm.DayAppointments.Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToMonth_returns_to_default_view()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        vm.SwitchToWeekCommand.Execute(null);
        vm.SwitchToMonthCommand.Execute(null);
        vm.ViewMode.Should().Be(CalendarViewMode.Month);
    }

    [Fact]
    public async Task WeekDays_starts_on_Sunday_containing_selected_date()
    {
        var vm = CreateVm();
        await vm.InitializeAsync();
        // 2026/05/15 是週五
        vm.SelectedDate = new DateOnly(2026, 5, 15);
        vm.SwitchToWeekCommand.Execute(null);

        vm.WeekDays[0].Date.Should().Be(new DateOnly(2026, 5, 10));  // Sunday
        vm.WeekDays[6].Date.Should().Be(new DateOnly(2026, 5, 16));  // Saturday
        vm.WeekDays.Should().Contain(d => d.Date == new DateOnly(2026, 5, 15));
    }

    [Fact]
    public async Task DayAppointments_returns_appointments_for_selected_date()
    {
        await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1), new TimeOnly(10, 0));
        await CreateAppointmentOnAsync(new DateOnly(2026, 6, 1), new TimeOnly(14, 0));
        await CreateAppointmentOnAsync(new DateOnly(2026, 6, 2), new TimeOnly(11, 0));

        var vm = CreateVm();
        vm.SelectedDate = new DateOnly(2026, 6, 1);
        vm.SwitchToDayCommand.Execute(null);

        for (int i = 0; i < 20 && vm.DayAppointments.Count == 0; i++) await Task.Delay(50);
        vm.DayAppointments.Should().HaveCount(2);
    }

    private async Task CreateAppointmentOnAsync(DateOnly date, TimeOnly time)
    {
        await InScopeAsync<int>(async sp =>
        {
            var owner = await sp.GetRequiredService<PetSalon.Core.Services.OwnerService>().CreateAsync(new PetSalon.Core.Dtos.OwnerInput
            {
                Name = "x" + Guid.NewGuid().ToString("N").Substring(0, 6),
                NationalId = "A", Phone = "B", Address = "C",
                EmergencyContactName = "D", EmergencyContactPhone = "E", EmergencyContactRelationship = "F",
            });
            var pet = await sp.GetRequiredService<PetSalon.Core.Services.PetService>().CreateAsync(new PetSalon.Core.Dtos.PetInput
            {
                OwnerId = owner.OwnerId, Name = "p", Species = "犬", Breed = "x",
                Gender = "公", Age = "1", IsNeutered = false,
                Personality = new() { "親人" }, MedicalHistory = new(),
                PhysicalExamination = new PetSalon.Core.Entities.PhysicalExamination(),
            });
            await sp.GetRequiredService<PetSalon.Core.Services.AppointmentService>().CreateAsync(new PetSalon.Core.Dtos.AppointmentCreateInput
            {
                PetId = pet.PetId, Date = date, Time = time,
            });
            return 0;
        });
    }
}
