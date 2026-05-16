using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class AppointmentEditViewModel : ViewModelBase, IDialogResultProvider
{
    private readonly IDialogService _dialog;

    public AppointmentEditViewModel(IServiceScopeFactory scope, IDialogService dialog) : base(scope)
    {
        _dialog = dialog;
    }

    public event Action<bool?>? RequestClose;

    public ObservableCollection<Owner> AvailableOwners { get; } = new();
    public ObservableCollection<Pet> AvailablePets { get; } = new();

    [ObservableProperty] private string? _appointmentId;
    [ObservableProperty] private string? _selectedOwnerId;
    [ObservableProperty] private string? _petId;
    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private string _timeText = "10:00";
    [ObservableProperty] private string _note = string.Empty;

    public Task LoadForCreateAsync(DateOnly date, string? petId = null) => RunAsync(async () =>
    {
        AppointmentId = null;
        Date = date;
        TimeText = "10:00";
        Note = string.Empty;

        await ReloadOwnersAsync();

        if (!string.IsNullOrEmpty(petId))
        {
            var pet = await WithScopeAsync(sp => sp.GetRequiredService<PetService>().GetByIdAsync(petId));
            SelectedOwnerId = pet.OwnerId;
            await ReloadPetsAsync(pet.OwnerId);
            PetId = petId;
        }
        else
        {
            PetId = null;
            SelectedOwnerId = AvailableOwners.FirstOrDefault()?.OwnerId;
        }
    });

    public Task LoadForEditAsync(string apptId) => RunAsync(async () =>
    {
        await ReloadOwnersAsync();
        var a = await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().GetByIdAsync(apptId));
        AppointmentId = a.AppointmentId;
        SelectedOwnerId = a.OwnerId;
        await ReloadPetsAsync(a.OwnerId);
        PetId = a.PetId;
        Date = a.Date;
        TimeText = a.Time.ToString("HH:mm");
        Note = a.Note;
    });

    private async Task ReloadOwnersAsync()
    {
        var list = await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().ListAsync());
        AvailableOwners.Clear();
        foreach (var o in list) AvailableOwners.Add(o);
    }

    private async Task ReloadPetsAsync(string? ownerId)
    {
        AvailablePets.Clear();
        if (string.IsNullOrEmpty(ownerId)) return;
        var list = await WithScopeAsync(sp => sp.GetRequiredService<PetService>().ListAsync(ownerId));
        foreach (var p in list) AvailablePets.Add(p);
        if (!AvailablePets.Any(p => p.PetId == PetId))
            PetId = AvailablePets.FirstOrDefault()?.PetId;
    }

    partial void OnSelectedOwnerIdChanged(string? value) => _ = ReloadPetsAsync(value);

    [RelayCommand]
    private Task Save() => RunAsync(async () =>
    {
        if (!TimeOnly.TryParseExact(TimeText, "HH:mm", out var time))
            throw PetSalon.Core.Common.AppException.Validation("時間格式須為 HH:mm");
        if (string.IsNullOrEmpty(PetId))
            throw PetSalon.Core.Common.AppException.Validation("請選擇寵物");

        var creating = AppointmentId is null;
        if (creating)
        {
            await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().CreateAsync(new AppointmentCreateInput
            {
                PetId = PetId,
                Date = Date,
                Time = time,
                Note = Note,
            }));
        }
        else
        {
            await WithScopeAsync(sp => sp.GetRequiredService<AppointmentService>().UpdateAsync(AppointmentId!, new AppointmentUpdateInput
            {
                Date = Date,
                Time = time,
                Note = Note,
            }));
        }
        _dialog.Success("儲存成功", creating ? "預約已建立" : "預約已更新");
        RequestClose?.Invoke(true);
    });

    [RelayCommand] private void Cancel() => RequestClose?.Invoke(false);
}
