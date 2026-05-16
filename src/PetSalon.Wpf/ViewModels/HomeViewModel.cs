using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    public HomeViewModel(IServiceScopeFactory scope, INavigationService nav) : base(scope)
    {
        _nav = nav;
    }

    public ObservableCollection<Appointment> TodayAppointments { get; } = new();

    [ObservableProperty] private int _ownerCount;
    [ObservableProperty] private int _petCount;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayDateLabel = string.Empty;

    public string Greeting => DateTime.Now.Hour switch
    {
        < 11 => "早安",
        < 18 => "午安",
        _ => "晚安",
    };

    public override Task InitializeAsync() => RunAsync(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        TodayDateLabel = $"{today:yyyy/MM/dd}（{ChineseWeekday(today.DayOfWeek)}）";
        await WithScopeAsync(async sp =>
        {
            var owners = await sp.GetRequiredService<OwnerService>().ListAsync();
            OwnerCount = owners.Count;
            var pets = await sp.GetRequiredService<PetService>().ListAsync();
            PetCount = pets.Count;
            var appts = await sp.GetRequiredService<AppointmentService>().ListAsync(new AppointmentListFilter { Date = today });
            TodayAppointments.Clear();
            foreach (var a in appts) TodayAppointments.Add(a);
            TodayCount = appts.Count;
            return 0;
        });
    });

    [RelayCommand] private void GoToday() => _nav.NavigateTo<DailyAppointmentsViewModel>(vm => vm.Date = DateOnly.FromDateTime(DateTime.Today));
    [RelayCommand] private void GoOwners() => _nav.NavigateTo<OwnerPageViewModel>();
    [RelayCommand] private void GoCalendar() => _nav.NavigateTo<CalendarViewModel>();
    [RelayCommand] private void GoCustomer() => _nav.NavigateTo<CustomerFormViewModel>();
    [RelayCommand] private void GoBackup() => _nav.NavigateTo<BackupPageViewModel>();

    private static string ChineseWeekday(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => "週日",
        DayOfWeek.Monday => "週一",
        DayOfWeek.Tuesday => "週二",
        DayOfWeek.Wednesday => "週三",
        DayOfWeek.Thursday => "週四",
        DayOfWeek.Friday => "週五",
        DayOfWeek.Saturday => "週六",
        _ => "",
    };
}
