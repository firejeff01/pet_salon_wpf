using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class OwnerPageViewModel : ViewModelBase
{
    private readonly IDialogService _dialog;
    private readonly IServiceScopeFactory _scope;

    public OwnerPageViewModel(IServiceScopeFactory scope, IDialogService dialog) : base(scope)
    {
        _scope = scope;
        _dialog = dialog;
        Form = new OwnerFormFields();
    }

    public ObservableCollection<Owner> Owners { get; } = new();
    public ObservableCollection<Pet> OwnerPets { get; } = new();

    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private Owner? _selectedOwner;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string? _statusMessage;

    public OwnerFormFields Form { get; }

    public override Task InitializeAsync() => RefreshAsync();

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    private Task RefreshAsync() => RunAsync(async () =>
    {
        var list = await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().ListAsync(SearchKeyword));
        Owners.Clear();
        foreach (var o in list) Owners.Add(o);
        if (SelectedOwner is not null)
            SelectedOwner = Owners.FirstOrDefault(o => o.OwnerId == SelectedOwner.OwnerId);
    });

    [RelayCommand]
    private void NewOwner()
    {
        SelectedOwner = null;
        IsCreating = true;
        Form.Reset();
        OwnerPets.Clear();
    }

    partial void OnSelectedOwnerChanged(Owner? value)
    {
        if (value is null) return;
        IsCreating = false;
        Form.LoadFrom(value);
        _ = LoadPetsAsync(value.OwnerId);
    }

    private Task LoadPetsAsync(string ownerId) => RunAsync(async () =>
    {
        var pets = await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().ListPetsAsync(ownerId));
        OwnerPets.Clear();
        foreach (var p in pets) OwnerPets.Add(p);
    });

    [RelayCommand]
    private Task Save() => RunAsync(async () =>
    {
        var input = Form.ToInput();
        Owner saved;
        var creating = IsCreating || SelectedOwner is null;
        if (creating)
            saved = await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().CreateAsync(input));
        else
            saved = await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().UpdateAsync(SelectedOwner!.OwnerId, input));
        StatusMessage = "已儲存";
        await RefreshAsync();
        SelectedOwner = Owners.FirstOrDefault(o => o.OwnerId == saved.OwnerId);
        _dialog.Success("儲存成功", creating ? $"飼主「{saved.Name}」已建立" : $"飼主「{saved.Name}」資料已更新");
    });

    [RelayCommand]
    private Task DeleteOwner() => RunAsync(async () =>
    {
        if (SelectedOwner is null) return;
        var ownerId = SelectedOwner.OwnerId;
        var ownerName = SelectedOwner.Name;
        if (!_dialog.Confirm(
                "刪除飼主",
                $"確定刪除飼主「{ownerName}」及其 {OwnerPets.Count} 隻寵物嗎？此動作無法復原。"))
            return;

        try
        {
            await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().DeleteAsync(ownerId));
        }
        catch (PetSalon.Core.Common.AppException ex) when (ex.Code == "OWNER_HAS_HISTORY")
        {
            _dialog.Error("無法刪除", "該飼主已有預約或美容紀錄，無法永久刪除。");
            return;
        }

        SelectedOwner = null;
        IsCreating = false;
        Form.Reset();
        OwnerPets.Clear();
        await RefreshAsync();
        _dialog.Success("已刪除", $"飼主「{ownerName}」及其寵物資料已刪除");
    });

    [RelayCommand]
    private void AddPet()
    {
        if (SelectedOwner is null) { _dialog.Error("尚未選取", "請先選取或建立飼主"); return; }
        var vm = new PetEditViewModel(_scope, _dialog);
        _ = vm.LoadForCreateAsync(SelectedOwner.OwnerId);
        if (_dialog.ShowDialog(vm, "新增寵物", 720, 720) == true)
            _ = LoadPetsAsync(SelectedOwner.OwnerId);
    }

    [RelayCommand]
    private void EditPet(Pet? pet)
    {
        if (pet is null || SelectedOwner is null) return;
        var vm = new PetEditViewModel(_scope, _dialog);
        _ = vm.LoadForEditAsync(pet.PetId);
        if (_dialog.ShowDialog(vm, "編輯寵物", 720, 720) == true)
            _ = LoadPetsAsync(SelectedOwner.OwnerId);
    }

    [RelayCommand]
    private void AddAppointment()
    {
        if (SelectedOwner is null) { _dialog.Error("尚未選取", "請先選取或建立飼主"); return; }
        if (OwnerPets.Count == 0) { _dialog.Error("尚無寵物", "請先為此飼主新增寵物再建立預約"); return; }
        var vm = new AppointmentEditViewModel(_scope, _dialog);
        _ = vm.LoadForCreateAsync(DateOnly.FromDateTime(DateTime.Today), OwnerPets[0].PetId);
        _dialog.ShowDialog(vm, "新增預約", 600, 600);
    }
}

public partial class OwnerFormFields : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _nationalId = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _emergencyContactName = string.Empty;
    [ObservableProperty] private string _emergencyContactPhone = string.Empty;
    [ObservableProperty] private string _emergencyContactRelationship = string.Empty;
    [ObservableProperty] private string _preferredAnimalHospitalName = string.Empty;
    [ObservableProperty] private string _preferredAnimalHospitalPhone = string.Empty;
    [ObservableProperty] private string _preferredAnimalHospitalAddress = string.Empty;
    [ObservableProperty] private bool _isStoredValueCustomer;
    [ObservableProperty] private decimal _storedValueBalance;
    [ObservableProperty] private string _note = string.Empty;

    public void Reset()
    {
        Name = NationalId = Phone = Address = string.Empty;
        EmergencyContactName = EmergencyContactPhone = EmergencyContactRelationship = string.Empty;
        PreferredAnimalHospitalName = PreferredAnimalHospitalPhone = PreferredAnimalHospitalAddress = string.Empty;
        IsStoredValueCustomer = false;
        StoredValueBalance = 0;
        Note = string.Empty;
    }

    public void LoadFrom(Owner o)
    {
        Name = o.Name; NationalId = o.NationalId; Phone = o.Phone; Address = o.Address;
        EmergencyContactName = o.EmergencyContactName; EmergencyContactPhone = o.EmergencyContactPhone;
        EmergencyContactRelationship = o.EmergencyContactRelationship;
        PreferredAnimalHospitalName = o.PreferredAnimalHospitalName;
        PreferredAnimalHospitalPhone = o.PreferredAnimalHospitalPhone;
        PreferredAnimalHospitalAddress = o.PreferredAnimalHospitalAddress;
        IsStoredValueCustomer = o.IsStoredValueCustomer;
        StoredValueBalance = o.StoredValueBalance;
        Note = o.Note;
    }

    public OwnerInput ToInput() => new()
    {
        Name = Name, NationalId = NationalId, Phone = Phone, Address = Address,
        EmergencyContactName = EmergencyContactName,
        EmergencyContactPhone = EmergencyContactPhone,
        EmergencyContactRelationship = EmergencyContactRelationship,
        PreferredAnimalHospitalName = PreferredAnimalHospitalName,
        PreferredAnimalHospitalPhone = PreferredAnimalHospitalPhone,
        PreferredAnimalHospitalAddress = PreferredAnimalHospitalAddress,
        IsStoredValueCustomer = IsStoredValueCustomer,
        StoredValueBalance = StoredValueBalance,
        Note = Note,
    };
}
