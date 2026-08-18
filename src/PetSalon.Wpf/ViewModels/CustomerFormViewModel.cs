using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Dtos;
using PetSalon.Core.Services;
using PetSalon.Wpf.Services;

namespace PetSalon.Wpf.ViewModels;

public partial class CustomerFormViewModel : ViewModelBase
{
    private readonly IDialogService _dialog;

    public CustomerFormViewModel(IServiceScopeFactory scope, IDialogService dialog) : base(scope)
    {
        _dialog = dialog;
        Owner = new OwnerFormFields();
        Pets.Add(new CustomerPetEntry());
    }

    public OwnerFormFields Owner { get; }
    public ObservableCollection<CustomerPetEntry> Pets { get; } = new();
    public IReadOnlyList<string> SpeciesList => PetSalon.Core.Enums.PetSpecies.All;
    public IReadOnlyList<string> GenderList => PetSalon.Core.Enums.PetGender.All;
    public IReadOnlyList<string> ChipStatusList => ChipStatusOptions.All;

    [RelayCommand]
    private void AddPetEntry() => Pets.Add(new CustomerPetEntry());

    [RelayCommand]
    private void RemovePetEntry(CustomerPetEntry? entry)
    {
        if (entry is null || Pets.Count <= 1) return;
        Pets.Remove(entry);
    }

    [RelayCommand]
    private Task Submit() => RunAsync(async () =>
    {
        var input = new CustomerRegistrationInput
        {
            Owner = Owner.ToInput(),
            Pets = Pets.Select(p => p.ToInput()).ToList(),
        };

        CustomerRegistrationResult result;
        try
        {
            result = await WithScopeAsync(sp => sp.GetRequiredService<CustomerRegistrationService>().CreateAsync(input));
        }
        catch (AppException ex) when (ex.Code == "POTENTIAL_DUPLICATE_OWNER")
        {
            var matches = ex.Details as IReadOnlyList<PotentialDuplicateOwner>;
            var names = matches is { Count: > 0 }
                ? string.Join("、", matches.Select(m => $"{m.Name}（{m.Phone}）"))
                : "既有顧客";
            if (!_dialog.Confirm(
                    "可能重複的顧客",
                    $"身分證字號或電話與 {names} 相同。是否仍要建立新資料？"))
                return;

            input.AllowDuplicate = true;
            result = await WithScopeAsync(sp => sp.GetRequiredService<CustomerRegistrationService>().CreateAsync(input));
        }

        _dialog.Success("已送出", $"飼主「{result.Owner.Name}」資料已建立");
        Reset();
    });

    [RelayCommand]
    private void Reset()
    {
        Owner.Reset();
        Pets.Clear();
        Pets.Add(new CustomerPetEntry());
    }
}

public partial class CustomerPetEntry : ObservableObject
{
    private bool _suppressMedicalMutex;

    public CustomerPetEntry()
    {
        MedicalHistory = new ObservableCollection<OptionSelection>(
            MedicalHistoryOptions.All.Select(x => new OptionSelection(x)));
        foreach (var item in MedicalHistory)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(OptionSelection.IsSelected) || _suppressMedicalMutex) return;
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
                        var none = MedicalHistory.First(x => x.Name == "以上皆無");
                        none.IsSelected = false;
                    }
                }
                finally
                {
                    _suppressMedicalMutex = false;
                    OnPropertyChanged(nameof(ShowMedicalHistoryOther));
                }
            };
        }
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _species = "犬";
    [ObservableProperty] private string _breed = string.Empty;
    [ObservableProperty] private string _gender = "公";
    [ObservableProperty] private string _age = string.Empty;
    [ObservableProperty] private bool _isNeutered;
    [ObservableProperty] private string _chipStatus = ChipStatusOptions.Unspecified;
    [ObservableProperty] private string? _chipData;
    [ObservableProperty] private string _personalityRaw = string.Empty;
    [ObservableProperty] private string _medicalHistoryOther = string.Empty;
    [ObservableProperty] private string _note = string.Empty;

    public ObservableCollection<OptionSelection> MedicalHistory { get; }
    public bool ShowChipData => ChipStatus != ChipStatusOptions.Unspecified;
    public bool ChipDataRequired => ChipStatus == ChipStatusOptions.HasChip;
    public bool ShowMedicalHistoryOther => MedicalHistory.Any(x => x.Name == "其它" && x.IsSelected);

    partial void OnChipStatusChanged(string value)
    {
        ChipData = null;
        OnPropertyChanged(nameof(ShowChipData));
        OnPropertyChanged(nameof(ChipDataRequired));
    }

    public CustomerPetInput ToInput()
    {
        var personality = string.IsNullOrWhiteSpace(PersonalityRaw)
            ? new List<string> { "親人" }
            : PersonalityRaw.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(8)
                .ToList();
        return new CustomerPetInput
        {
            Name = Name,
            Species = Species,
            Breed = Breed,
            Gender = Gender,
            Age = Age,
            IsNeutered = IsNeutered,
            ChipStatus = ChipStatus,
            ChipData = ChipData,
            Personality = personality,
            MedicalHistory = MedicalHistory.Where(x => x.IsSelected).Select(x => x.Name).ToList(),
            MedicalHistoryOther = MedicalHistoryOther,
            Note = Note,
        };
    }
}
