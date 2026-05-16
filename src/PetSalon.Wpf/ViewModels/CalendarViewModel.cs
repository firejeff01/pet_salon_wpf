using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public enum CalendarViewMode { Month, Week, Day }

public partial class CalendarViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    public CalendarViewModel(IServiceScopeFactory scope, INavigationService nav) : base(scope)
    {
        _nav = nav;
        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentMonth = new DateOnly(today.Year, today.Month, 1);
        SelectedDate = today;
    }

    public ObservableCollection<CalendarDayCell> Days { get; } = new();
    public ObservableCollection<CalendarDayCell> WeekDays { get; } = new();
    public ObservableCollection<Appointment> DayAppointments { get; } = new();

    [ObservableProperty] private DateOnly _currentMonth;
    [ObservableProperty] private DateOnly _selectedDate;
    [ObservableProperty] private CalendarViewMode _viewMode = CalendarViewMode.Month;

    public string MonthLabel => $"{CurrentMonth.Year} 年 {CurrentMonth.Month} 月";

    public override Task InitializeAsync() => ReloadAsync();

    [RelayCommand] private Task Reload() => ReloadAsync();

    private Task ReloadAsync() => RunAsync(async () =>
    {
        var summary = await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>()
            .CalendarSummaryAsync(CurrentMonth.Year, CurrentMonth.Month));
        var byDate = summary.ToDictionary(s => s.Date);
        var first = CurrentMonth;
        var startOffset = (int)first.DayOfWeek;
        var gridStart = first.AddDays(-startOffset);
        Days.Clear();
        for (var i = 0; i < 42; i++)
        {
            var d = gridStart.AddDays(i);
            byDate.TryGetValue(d, out var entry);
            Days.Add(new CalendarDayCell(d, d.Month == first.Month, entry));
        }
        OnPropertyChanged(nameof(MonthLabel));
    });

    [RelayCommand] private void PrevMonth() { CurrentMonth = CurrentMonth.AddMonths(-1); _ = ReloadAsync(); }
    [RelayCommand] private void NextMonth() { CurrentMonth = CurrentMonth.AddMonths(1); _ = ReloadAsync(); }
    [RelayCommand] private void Today()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentMonth = new DateOnly(today.Year, today.Month, 1);
        SelectedDate = today;
        _ = ReloadAsync();
        _ = RefreshActiveViewAsync();
    }

    [RelayCommand]
    private void SelectDay(CalendarDayCell? cell)
    {
        if (cell is null) return;
        SelectedDate = cell.Date;
        if (ViewMode == CalendarViewMode.Month)
            _nav.NavigateTo<DailyAppointmentsViewModel>(vm => vm.Date = cell.Date);
        else
            _ = RefreshActiveViewAsync();
    }

    [RelayCommand]
    private async Task SwitchToMonth()
    {
        ViewMode = CalendarViewMode.Month;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task SwitchToWeek()
    {
        ViewMode = CalendarViewMode.Week;
        await ReloadWeekAsync();
    }

    [RelayCommand]
    private async Task SwitchToDay()
    {
        ViewMode = CalendarViewMode.Day;
        await ReloadDayAsync();
    }

    private async Task RefreshActiveViewAsync()
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Week: await ReloadWeekAsync(); break;
            case CalendarViewMode.Day: await ReloadDayAsync(); break;
            default: await ReloadAsync(); break;
        }
    }

    private Task ReloadWeekAsync() => RunAsync(async () =>
    {
        var offset = (int)SelectedDate.DayOfWeek;
        var sunday = SelectedDate.AddDays(-offset);
        var saturday = sunday.AddDays(6);

        var appts = await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().ListAsync(new AppointmentListFilter
        {
            DateFrom = sunday,
            DateTo = saturday,
        }));
        var byDate = appts.GroupBy(a => a.Date).ToDictionary(g => g.Key, g => (IReadOnlyList<Appointment>)g.ToList());

        WeekDays.Clear();
        for (var i = 0; i < 7; i++)
        {
            var d = sunday.AddDays(i);
            byDate.TryGetValue(d, out var dayAppts);
            var statusSummary = dayAppts?
                .GroupBy(a => a.Status)
                .ToDictionary(g => g.Key, g => g.Count())
                ?? new Dictionary<string, int>();
            var entry = new CalendarSummaryEntry(d, dayAppts?.Count ?? 0, statusSummary);
            WeekDays.Add(new CalendarDayCell(d, true, entry));
        }
    });

    private Task ReloadDayAsync() => RunAsync(async () =>
    {
        var appts = await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().ListAsync(new AppointmentListFilter
        {
            Date = SelectedDate,
        }));
        DayAppointments.Clear();
        foreach (var a in appts) DayAppointments.Add(a);
    });

    partial void OnSelectedDateChanged(DateOnly value) => _ = RefreshActiveViewAsync();
}

public sealed record CalendarDayCell(DateOnly Date, bool IsCurrentMonth, CalendarSummaryEntry? Summary)
{
    public string DayLabel => Date.Day.ToString();
    public bool IsToday => Date == DateOnly.FromDateTime(DateTime.Today);
    public int? Count => Summary?.Count;
    public bool HasAppointments => (Summary?.Count ?? 0) > 0;
}
