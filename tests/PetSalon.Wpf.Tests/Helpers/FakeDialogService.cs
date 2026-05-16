using PetSalon.Wpf.Services;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Tests.Helpers;

public sealed class FakeDialogService : IDialogService
{
    public List<(string title, string message)> Infos { get; } = new();
    public List<(string title, string message)> Successes { get; } = new();
    public List<(string title, string message)> Warnings { get; } = new();
    public List<(string title, string message)> Errors { get; } = new();
    public List<(string title, string message)> Confirms { get; } = new();
    public Func<string, string, bool> ConfirmResponse { get; set; } = (_, _) => true;
    public List<(ViewModelBase vm, string title)> Dialogs { get; } = new();
    public Func<ViewModelBase, string, bool?> DialogResponse { get; set; } = (_, _) => true;

    public void Info(string title, string message) => Infos.Add((title, message));
    public void Success(string title, string message) => Successes.Add((title, message));
    public void Warning(string title, string message) => Warnings.Add((title, message));
    public void Error(string title, string message) => Errors.Add((title, message));
    public bool Confirm(string title, string message) { Confirms.Add((title, message)); return ConfirmResponse(title, message); }
    public bool? ShowDialog(ViewModelBase viewModel, string title, double width = 720, double height = 600)
    {
        Dialogs.Add((viewModel, title));
        return DialogResponse(viewModel, title);
    }
}

public sealed class FakeNavigationService : INavigationService
{
    public List<Type> Visited { get; } = new();
    public ViewModelBase? Current { get; private set; }
    public event Action<ViewModelBase>? CurrentViewModelChanged;

    public void NavigateTo<TVm>(Action<TVm>? configure = null) where TVm : ViewModelBase
    {
        Visited.Add(typeof(TVm));
    }

    public void NavigateTo(ViewModelBase vm)
    {
        Visited.Add(vm.GetType());
        Current = vm;
        CurrentViewModelChanged?.Invoke(vm);
    }
}
