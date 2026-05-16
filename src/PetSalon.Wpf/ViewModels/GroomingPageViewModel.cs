using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class GroomingPageViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly IDialogService _dialog;

    private bool _suppressMedicalMutex;

    public GroomingPageViewModel(IServiceScopeFactory scope, INavigationService nav, IDialogService dialog) : base(scope)
    {
        _nav = nav;
        _dialog = dialog;

        Services = new ObservableCollection<ServiceSelection>(
            GroomingServiceOptions.All.Select(s => new ServiceSelection(s)));
        foreach (var s in Services) s.PropertyChanged += (_, _) => RecalculateTotal();

        Personality = new ObservableCollection<OptionSelection>(
            PersonalityOptions.All.Select(p => new OptionSelection(p)));
        MedicalHistory = new ObservableCollection<OptionSelection>(
            MedicalHistoryOptions.All.Select(p => new OptionSelection(p)));

        BodyConditions = BodyConditionOptions.Standard;
        FurConditions = BodyConditionOptions.Fur;
        WireMedicalHistoryMutex();
    }

    // R7: 病史「以上皆無」互斥邏輯
    private void WireMedicalHistoryMutex()
    {
        foreach (var item in MedicalHistory)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(OptionSelection.IsSelected)) return;
                if (_suppressMedicalMutex) return;
                _suppressMedicalMutex = true;
                try
                {
                    if (item.Name == "以上皆無" && item.IsSelected)
                    {
                        foreach (var other in MedicalHistory)
                            if (other != item) other.IsSelected = false;
                    }
                    else if (item.Name != "以上皆無" && item.IsSelected)
                    {
                        var noneItem = MedicalHistory.FirstOrDefault(x => x.Name == "以上皆無");
                        if (noneItem is not null) noneItem.IsSelected = false;
                    }
                }
                finally
                {
                    _suppressMedicalMutex = false;
                }
            };
        }
    }

    // ===== Header =====
    [ObservableProperty] private string? _appointmentId;
    [ObservableProperty] private string? _groomingRecordId;
    [ObservableProperty] private string _ownerName = string.Empty;
    [ObservableProperty] private string _ownerPhone = string.Empty;
    [ObservableProperty] private string _petName = string.Empty;
    [ObservableProperty] private string _petSpecies = string.Empty;
    [ObservableProperty] private string _petBreed = string.Empty;
    [ObservableProperty] private DateOnly _serviceDate;
    [ObservableProperty] private TimeOnly _serviceTime;

    // ===== Services =====
    public ObservableCollection<ServiceSelection> Services { get; }
    [ObservableProperty] private decimal _totalCost;

    // ===== Stored value =====
    [ObservableProperty] private bool _isStoredValueCustomer;
    [ObservableProperty] private decimal _storedValueBalance;
    [ObservableProperty] private decimal _storedValueDeduction;
    [ObservableProperty] private decimal _cashPayment;
    [ObservableProperty] private decimal _storedValueRemaining;

    // ===== Body exam (per-pet defaults loaded from Pet entity) =====
    public IReadOnlyList<string> BodyConditions { get; }
    public IReadOnlyList<string> FurConditions { get; }
    [ObservableProperty] private string _eyesCondition = "正常";
    [ObservableProperty] private string _earsCondition = "正常";
    [ObservableProperty] private string _teethCondition = "正常";
    [ObservableProperty] private string _limbsCondition = "正常";
    [ObservableProperty] private string _skinCondition = "正常";
    [ObservableProperty] private string _furCondition = "正常";

    // R6: 異常備註欄位（MaxLength=30）
    [ObservableProperty] private string _eyesNote = string.Empty;
    [ObservableProperty] private string _earsNote = string.Empty;
    [ObservableProperty] private string _teethNote = string.Empty;
    [ObservableProperty] private string _limbsNote = string.Empty;
    [ObservableProperty] private string _skinNote = string.Empty;
    [ObservableProperty] private string _furNote = string.Empty;

    // R6: 是否顯示 Note 框
    public bool EyesNoteVisible => EyesCondition.Contains("異常");
    public bool EarsNoteVisible => EarsCondition.Contains("異常");
    public bool TeethNoteVisible => TeethCondition.Contains("異常");
    public bool LimbsNoteVisible => LimbsCondition.Contains("異常");
    public bool SkinNoteVisible => SkinCondition.Contains("異常");
    public bool FurNoteVisible => FurCondition.Contains("異常");

    partial void OnEyesConditionChanged(string value)
    {
        OnPropertyChanged(nameof(EyesNoteVisible));
        if (!value.Contains("異常")) EyesNote = string.Empty;
    }
    partial void OnEarsConditionChanged(string value)
    {
        OnPropertyChanged(nameof(EarsNoteVisible));
        if (!value.Contains("異常")) EarsNote = string.Empty;
    }
    partial void OnTeethConditionChanged(string value)
    {
        OnPropertyChanged(nameof(TeethNoteVisible));
        if (!value.Contains("異常")) TeethNote = string.Empty;
    }
    partial void OnLimbsConditionChanged(string value)
    {
        OnPropertyChanged(nameof(LimbsNoteVisible));
        if (!value.Contains("異常")) LimbsNote = string.Empty;
    }
    partial void OnSkinConditionChanged(string value)
    {
        OnPropertyChanged(nameof(SkinNoteVisible));
        if (!value.Contains("異常")) SkinNote = string.Empty;
    }
    partial void OnFurConditionChanged(string value)
    {
        OnPropertyChanged(nameof(FurNoteVisible));
        if (!value.Contains("異常")) FurNote = string.Empty;
    }

    // ===== Personality / Medical history =====
    public ObservableCollection<OptionSelection> Personality { get; }
    public ObservableCollection<OptionSelection> MedicalHistory { get; }
    [ObservableProperty] private string _medicalHistoryOther = string.Empty;

    // ===== Notes =====
    [ObservableProperty] private string _ownerNotes = string.Empty;
    [ObservableProperty] private string _shopNotes = string.Empty;
    [ObservableProperty] private string _otherNotes = string.Empty;

    // ===== Contract versions =====
    public ObservableCollection<ContractVersion> ContractVersions { get; } = new();

    public void LoadForAppointment(string appointmentId)
    {
        AppointmentId = appointmentId;
        _ = RunAsync(async () =>
        {
            await WithScopeAsync(async sp =>
            {
                var appt = await sp.GetRequiredService<AppointmentService>().GetByIdAsync(appointmentId);
                OwnerName = appt.Owner?.Name ?? string.Empty;
                OwnerPhone = appt.Owner?.Phone ?? string.Empty;
                PetName = appt.Pet?.Name ?? string.Empty;
                PetSpecies = appt.Pet?.Species ?? string.Empty;
                PetBreed = appt.Pet?.Breed ?? string.Empty;
                ServiceDate = appt.Date;
                ServiceTime = appt.Time;
                IsStoredValueCustomer = appt.Owner?.IsStoredValueCustomer ?? false;
                StoredValueBalance = appt.Owner?.StoredValueBalance ?? 0;

                var existing = await sp.GetRequiredService<GroomingRecordService>().FindByAppointmentIdAsync(appointmentId);
                if (existing is not null)
                {
                    LoadExisting(existing);
                }
                else
                {
                    LoadDefaultsFromPet(appt.Pet);
                }
                return 0;
            });
            RecalculateTotal();
        });
    }

    private void LoadExisting(GroomingRecord rec)
    {
        GroomingRecordId = rec.GroomingRecordId;
        foreach (var s in Services)
        {
            var item = rec.Services.FirstOrDefault(x => x.Item == s.Name);
            s.IsSelected = item is not null;
            s.Price = item?.Price ?? 0;
        }
        EyesCondition = rec.PhysicalExamination.Eyes;
        EarsCondition = rec.PhysicalExamination.Ears;
        TeethCondition = rec.PhysicalExamination.Teeth;
        LimbsCondition = rec.PhysicalExamination.Limbs;
        SkinCondition = rec.PhysicalExamination.Skin;
        FurCondition = rec.PhysicalExamination.Fur;
        EyesNote = rec.PhysicalExamination.EyesNote ?? string.Empty;
        EarsNote = rec.PhysicalExamination.EarsNote ?? string.Empty;
        TeethNote = rec.PhysicalExamination.TeethNote ?? string.Empty;
        LimbsNote = rec.PhysicalExamination.LimbsNote ?? string.Empty;
        SkinNote = rec.PhysicalExamination.SkinNote ?? string.Empty;
        FurNote = rec.PhysicalExamination.FurNote ?? string.Empty;

        var pSet = rec.Personality.ToHashSet();
        foreach (var p in Personality) p.IsSelected = pSet.Contains(p.Name);
        var mSet = rec.MedicalHistory.ToHashSet();
        foreach (var m in MedicalHistory) m.IsSelected = mSet.Contains(m.Name);
        MedicalHistoryOther = rec.MedicalHistoryOther;
        OwnerNotes = rec.OwnerNotes;
        ShopNotes = rec.ShopNotes;
        OtherNotes = rec.OtherNotes;

        ContractVersions.Clear();
        foreach (var c in rec.ContractPaths.OrderByDescending(c => c.Version)) ContractVersions.Add(c);
    }

    private void LoadDefaultsFromPet(Pet? pet)
    {
        GroomingRecordId = null;
        foreach (var s in Services) { s.IsSelected = false; s.Price = 0; }
        if (pet is null) return;
        EyesCondition = pet.PhysicalExamination.Eyes;
        EarsCondition = pet.PhysicalExamination.Ears;
        TeethCondition = pet.PhysicalExamination.Teeth;
        LimbsCondition = pet.PhysicalExamination.Limbs;
        SkinCondition = pet.PhysicalExamination.Skin;
        FurCondition = pet.PhysicalExamination.Fur;
        EyesNote = pet.PhysicalExamination.EyesNote ?? string.Empty;
        EarsNote = pet.PhysicalExamination.EarsNote ?? string.Empty;
        TeethNote = pet.PhysicalExamination.TeethNote ?? string.Empty;
        LimbsNote = pet.PhysicalExamination.LimbsNote ?? string.Empty;
        SkinNote = pet.PhysicalExamination.SkinNote ?? string.Empty;
        FurNote = pet.PhysicalExamination.FurNote ?? string.Empty;
        var pSet = pet.Personality.ToHashSet();
        foreach (var p in Personality) p.IsSelected = pSet.Contains(p.Name);
        var mSet = pet.MedicalHistory.ToHashSet();
        foreach (var m in MedicalHistory) m.IsSelected = mSet.Contains(m.Name);
        MedicalHistoryOther = pet.MedicalHistoryOther;
        OwnerNotes = ShopNotes = OtherNotes = string.Empty;
        ContractVersions.Clear();
    }

    private void RecalculateTotal()
    {
        TotalCost = Services.Where(s => s.IsSelected).Sum(s => s.Price);
        if (IsStoredValueCustomer && StoredValueBalance > 0)
        {
            StoredValueDeduction = Math.Min(StoredValueBalance, TotalCost);
            CashPayment = TotalCost - StoredValueDeduction;
            StoredValueRemaining = StoredValueBalance - StoredValueDeduction;
        }
        else
        {
            StoredValueDeduction = 0;
            CashPayment = TotalCost;
            StoredValueRemaining = StoredValueBalance;
        }
    }

    partial void OnTotalCostChanged(decimal value) => RecalculateTotal();

    [RelayCommand]
    private Task Save() => RunAsync(async () =>
    {
        if (AppointmentId is null) return;
        var selected = Services.Where(s => s.IsSelected).Select(s => new GroomingServiceInput
        {
            Item = s.Name,
            Price = s.Price,
        }).ToList();

        var input = new GroomingRecordInput
        {
            AppointmentId = AppointmentId,
            Services = selected,
            PhysicalExamination = new PhysicalExamination
            {
                Eyes = EyesCondition, Ears = EarsCondition, Teeth = TeethCondition,
                Limbs = LimbsCondition, Skin = SkinCondition, Fur = FurCondition,
                EyesNote = EyesNote, EarsNote = EarsNote, TeethNote = TeethNote,
                LimbsNote = LimbsNote, SkinNote = SkinNote, FurNote = FurNote,
            },
            Personality = Personality.Where(p => p.IsSelected).Select(p => p.Name).ToList(),
            MedicalHistory = MedicalHistory.Where(m => m.IsSelected).Select(m => m.Name).ToList(),
            MedicalHistoryOther = MedicalHistoryOther,
            OwnerNotes = OwnerNotes,
            ShopNotes = ShopNotes,
            OtherNotes = OtherNotes,
        };

        var saved = await WithScopeAsync(sp => sp.GetRequiredService<GroomingRecordService>().SaveAsync(input));
        GroomingRecordId = saved.GroomingRecordId;
        // 重新讀取以更新儲值餘額
        LoadForAppointment(AppointmentId);
        _dialog.Success("儲存成功", "美容紀錄已儲存");
    });

    [RelayCommand]
    private void GenerateContract()
    {
        if (string.IsNullOrEmpty(GroomingRecordId))
        {
            _dialog.Error("尚未儲存", "請先儲存美容紀錄再產生契約 PDF");
            return;
        }
        var vm = App.Current.Services.GetRequiredService<ContractGenerateDialogViewModel>();
        vm.GroomingRecordId = GroomingRecordId!;
        vm.Caption = $"{OwnerName} / {PetName}";
        if (_dialog.ShowDialog(vm, "契約預覽 — 確認後產生 PDF", 880, 720) == true)
        {
            // 重載以取得新版本號
            if (AppointmentId is not null) LoadForAppointment(AppointmentId);
        }
    }

    [RelayCommand]
    private void OpenContract(ContractVersion? cv)
    {
        if (cv is null) return;
        _ = RunAsync(() => WithScopeAsync(sp =>
        {
            sp.GetRequiredService<ContractService>().OpenAsync(cv.Path);
            return Task.CompletedTask;
        }));
    }

    [RelayCommand]
    private void Back() => _nav.NavigateTo<DailyAppointmentsViewModel>(vm => vm.Date = ServiceDate);
}

public partial class ServiceSelection : ObservableObject
{
    public ServiceSelection(string name) { Name = name; }
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private decimal _price;
}

public partial class OptionSelection : ObservableObject
{
    public OptionSelection(string name) { Name = name; }
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;
}
