using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Common;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Enums;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class DailyAppointmentsViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly IDialogService _dialog;

    public DailyAppointmentsViewModel(IServiceScopeFactory scope, INavigationService nav, IDialogService dialog) : base(scope)
    {
        _nav = nav;
        _dialog = dialog;
        _date = DateOnly.FromDateTime(DateTime.Today);
    }

    public ObservableCollection<Appointment> Appointments { get; } = new();

    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private Appointment? _selected;
    [ObservableProperty] private string _cancelReason = string.Empty;

    partial void OnDateChanged(DateOnly value) => _ = ReloadAsync();
    public override Task InitializeAsync() => ReloadAsync();

    [RelayCommand] private Task Reload() => ReloadAsync();

    private Task ReloadAsync() => RunAsync(async () =>
    {
        var list = await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>()
            .ListAsync(new AppointmentListFilter { Date = Date }));
        Appointments.Clear();
        foreach (var a in list) Appointments.Add(a);
    });

    [RelayCommand] private void Back() => _nav.NavigateTo<CalendarViewModel>();

    [RelayCommand]
    private void OpenGrooming(Appointment? appt)
    {
        if (appt is null) return;
        _nav.NavigateTo<GroomingPageViewModel>(vm => vm.LoadForAppointment(appt.AppointmentId));
    }

    [RelayCommand]
    private Task MarkComplete() => RunAsync(async () =>
    {
        if (Selected is null) { _dialog.Error("尚未選取", "請先選取一筆預約"); return; }
        var id = Selected.AppointmentId;
        await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().UpdateAsync(id, new AppointmentUpdateInput
        {
            Status = AppointmentStatus.Completed,
        }));
        _dialog.Success("狀態已更新", "預約已標記為「已完成」");
        await ReloadAsync();
    });

    [RelayCommand]
    private Task CancelAppointment() => RunAsync(async () =>
    {
        if (Selected is null) { _dialog.Error("尚未選取", "請先選取一筆預約"); return; }
        if (string.IsNullOrWhiteSpace(CancelReason))
        {
            _dialog.Error("缺少資訊", "請填寫取消原因再執行取消");
            return;
        }
        var id = Selected.AppointmentId;
        await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().UpdateAsync(id, new AppointmentUpdateInput
        {
            Status = AppointmentStatus.Cancelled,
            CancelReason = CancelReason.Trim(),
        }));
        _dialog.Success("狀態已更新", "預約已取消");
        CancelReason = string.Empty;
        await ReloadAsync();
    });

    [RelayCommand]
    private Task Delete() => RunAsync(async () =>
    {
        if (Selected is null) { _dialog.Error("尚未選取", "請先選取一筆預約"); return; }
        var id = Selected.AppointmentId;
        if (!_dialog.Confirm("刪除預約", $"確定刪除 {Selected.Time:HH\\:mm} 的預約嗎？此動作無法復原。")) return;
        try
        {
            await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().DeleteAsync(id));
            _dialog.Success("已刪除", "預約已刪除");
            await ReloadAsync();
        }
        catch (AppException ex) when (ex.Code == "APPOINTMENT_HAS_RECORD")
        {
            _dialog.Error("無法刪除", "該預約已有美容紀錄，無法刪除。請先處理對應的紀錄。");
        }
    });
}
