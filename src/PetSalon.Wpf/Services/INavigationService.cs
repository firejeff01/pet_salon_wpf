using Microsoft.Extensions.DependencyInjection;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Services;

public interface INavigationService
{
    event Action<ViewModelBase>? CurrentViewModelChanged;
    ViewModelBase? Current { get; }
    void NavigateTo<TVm>(Action<TVm>? configure = null) where TVm : ViewModelBase;
    void NavigateTo(ViewModelBase vm);
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _provider;
    private ViewModelBase? _current;

    public NavigationService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public event Action<ViewModelBase>? CurrentViewModelChanged;
    public ViewModelBase? Current => _current;

    public void NavigateTo<TVm>(Action<TVm>? configure = null) where TVm : ViewModelBase
    {
        var vm = (TVm)ActivatorUtilities.GetServiceOrCreateInstance(_provider, typeof(TVm));
        configure?.Invoke(vm);
        SetCurrent(vm);
    }

    public void NavigateTo(ViewModelBase vm) => SetCurrent(vm);

    private void SetCurrent(ViewModelBase vm)
    {
        _current = vm;
        CurrentViewModelChanged?.Invoke(vm);
        _ = vm.InitializeAsync();
    }
}
