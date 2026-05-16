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

public partial class PetEditViewModel : ViewModelBase, IDialogResultProvider
{
    private readonly IDialogService _dialog;

    private bool _suppressMedicalMutex;

    public PetEditViewModel(IServiceScopeFactory scope, IDialogService dialog) : base(scope)
    {
        _dialog = dialog;
        Personality = new ObservableCollection<OptionSelection>(PersonalityOptions.All.Select(p => new OptionSelection(p)));
        MedicalHistory = new ObservableCollection<OptionSelection>(MedicalHistoryOptions.All.Select(p => new OptionSelection(p)));
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
                        // 勾「以上皆無」→ 取消其他所有
                        foreach (var other in MedicalHistory)
                            if (other != item) other.IsSelected = false;
                    }
                    else if (item.Name != "以上皆無" && item.IsSelected)
                    {
                        // 勾任一其他 → 取消「以上皆無」
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

    public event Action<bool?>? RequestClose;

    [ObservableProperty] private string? _petId;
    [ObservableProperty] private string _ownerId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _species = "犬";
    [ObservableProperty] private string _breed = string.Empty;
    [ObservableProperty] private string _gender = "公";
    [ObservableProperty] private string _age = string.Empty;
    [ObservableProperty] private bool _isNeutered;
    [ObservableProperty] private string? _chipNumber;
    [ObservableProperty] private string? _unregisteredIdMethod;

    public ObservableCollection<OptionSelection> Personality { get; }
    public ObservableCollection<OptionSelection> MedicalHistory { get; }
    public IReadOnlyList<string> BodyConditions { get; }
    public IReadOnlyList<string> FurConditions { get; }
    public IReadOnlyList<string> SpeciesList => PetSalon.Core.Enums.PetSpecies.All;
    public IReadOnlyList<string> GenderList => PetSalon.Core.Enums.PetGender.All;

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

    // R6: 是否顯示 Note 框（包含「異常」時 Visible）
    public bool EyesNoteVisible => EyesCondition.Contains("異常");
    public bool EarsNoteVisible => EarsCondition.Contains("異常");
    public bool TeethNoteVisible => TeethCondition.Contains("異常");
    public bool LimbsNoteVisible => LimbsCondition.Contains("異常");
    public bool SkinNoteVisible => SkinCondition.Contains("異常");
    // Q8: 皮毛只在選項含「異常」時顯示
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

    [ObservableProperty] private string _medicalHistoryOther = string.Empty;
    [ObservableProperty] private string _note = string.Empty;

    public bool IsCreating => PetId is null;
    public string ModeTitle => IsCreating ? "新增寵物" : "編輯寵物";

    public Task LoadForCreateAsync(string ownerId)
    {
        PetId = null;
        OwnerId = ownerId;
        Name = Breed = Age = string.Empty;
        Species = "犬"; Gender = "公"; IsNeutered = false;
        ChipNumber = UnregisteredIdMethod = null;
        foreach (var p in Personality) p.IsSelected = false;
        foreach (var m in MedicalHistory) m.IsSelected = false;
        EyesCondition = EarsCondition = TeethCondition = LimbsCondition = SkinCondition = FurCondition = "正常";
        EyesNote = EarsNote = TeethNote = LimbsNote = SkinNote = FurNote = string.Empty;
        MedicalHistoryOther = Note = string.Empty;
        return Task.CompletedTask;
    }

    public Task LoadForEditAsync(string petId) => RunAsync(async () =>
    {
        var p = await WithScopeAsync(sp => sp.GetRequiredService<PetService>().GetByIdAsync(petId));
        PetId = p.PetId;
        OwnerId = p.OwnerId;
        Name = p.Name; Species = p.Species; Breed = p.Breed; Gender = p.Gender;
        Age = p.Age; IsNeutered = p.IsNeutered;
        ChipNumber = p.ChipNumber;
        UnregisteredIdMethod = p.UnregisteredIdMethod;
        EyesCondition = p.PhysicalExamination.Eyes;
        EarsCondition = p.PhysicalExamination.Ears;
        TeethCondition = p.PhysicalExamination.Teeth;
        LimbsCondition = p.PhysicalExamination.Limbs;
        SkinCondition = p.PhysicalExamination.Skin;
        FurCondition = p.PhysicalExamination.Fur;
        EyesNote = p.PhysicalExamination.EyesNote ?? string.Empty;
        EarsNote = p.PhysicalExamination.EarsNote ?? string.Empty;
        TeethNote = p.PhysicalExamination.TeethNote ?? string.Empty;
        LimbsNote = p.PhysicalExamination.LimbsNote ?? string.Empty;
        SkinNote = p.PhysicalExamination.SkinNote ?? string.Empty;
        FurNote = p.PhysicalExamination.FurNote ?? string.Empty;
        var pSet = p.Personality.ToHashSet();
        foreach (var x in Personality) x.IsSelected = pSet.Contains(x.Name);
        var mSet = p.MedicalHistory.ToHashSet();
        foreach (var x in MedicalHistory) x.IsSelected = mSet.Contains(x.Name);
        MedicalHistoryOther = p.MedicalHistoryOther;
        Note = p.Note;
    });

    [RelayCommand]
    private Task Save() => RunAsync(async () =>
    {
        var input = new PetInput
        {
            OwnerId = OwnerId,
            Name = Name, Species = Species, Breed = Breed, Gender = Gender, Age = Age,
            IsNeutered = IsNeutered,
            ChipNumber = ChipNumber, UnregisteredIdMethod = UnregisteredIdMethod,
            Personality = Personality.Where(p => p.IsSelected).Select(p => p.Name).ToList(),
            PhysicalExamination = new PhysicalExamination
            {
                Eyes = EyesCondition, Ears = EarsCondition, Teeth = TeethCondition,
                Limbs = LimbsCondition, Skin = SkinCondition, Fur = FurCondition,
                EyesNote = EyesNote, EarsNote = EarsNote, TeethNote = TeethNote,
                LimbsNote = LimbsNote, SkinNote = SkinNote, FurNote = FurNote,
            },
            MedicalHistory = MedicalHistory.Where(m => m.IsSelected).Select(m => m.Name).ToList(),
            MedicalHistoryOther = MedicalHistoryOther,
            Note = Note,
        };
        var creating = IsCreating;
        Pet saved;
        if (creating)
            saved = await WithScopeAsync(sp => sp.GetRequiredService<PetService>().CreateAsync(input));
        else
            saved = await WithScopeAsync(sp => sp.GetRequiredService<PetService>().UpdateAsync(PetId!, input));
        _dialog.Success("儲存成功", creating ? $"寵物「{saved.Name}」已建立" : $"寵物「{saved.Name}」資料已更新");
        RequestClose?.Invoke(true);
    });

    [RelayCommand] private void Cancel() => RequestClose?.Invoke(false);
}
