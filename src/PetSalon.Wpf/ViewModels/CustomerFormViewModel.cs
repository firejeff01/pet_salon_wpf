using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PetSalon.Core.Dtos;
using PetSalon.Core.Entities;
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

    [RelayCommand]
    private void AddPetEntry() => Pets.Add(new CustomerPetEntry());

    [RelayCommand]
    private void RemovePetEntry(CustomerPetEntry? entry)
    {
        if (entry is null) return;
        if (Pets.Count <= 1) return;
        Pets.Remove(entry);
    }

    [RelayCommand]
    private Task Submit() => RunAsync(async () =>
    {
        var ownerSaved = await WithScopeAsync(sp => sp.GetRequiredService<OwnerService>().CreateAsync(Owner.ToInput()));

        foreach (var pet in Pets)
        {
            if (string.IsNullOrWhiteSpace(pet.Name)) continue;

            var personality = string.IsNullOrWhiteSpace(pet.PersonalityRaw)
                ? new List<string> { "親人" }
                : pet.PersonalityRaw.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(8).ToList();

            await WithScopeAsync(sp => sp.GetRequiredService<PetService>().CreateAsync(new PetInput
            {
                OwnerId = ownerSaved.OwnerId,
                Name = pet.Name, Species = pet.Species, Breed = pet.Breed,
                Gender = pet.Gender, Age = pet.Age, IsNeutered = pet.IsNeutered,
                ChipNumber = pet.ChipNumber,
                Personality = personality,
                MedicalHistory = new List<string>(),
                PhysicalExamination = new PhysicalExamination(),
            }));
        }

        _dialog.Success("已送出", $"飼主「{ownerSaved.Name}」資料已建立");
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
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _species = "犬";
    [ObservableProperty] private string _breed = string.Empty;
    [ObservableProperty] private string _gender = "公";
    [ObservableProperty] private string _age = string.Empty;
    [ObservableProperty] private bool _isNeutered;
    [ObservableProperty] private string? _chipNumber;
    [ObservableProperty] private string _personalityRaw = string.Empty;
}
