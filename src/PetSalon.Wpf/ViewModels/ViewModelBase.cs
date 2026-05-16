using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace PetSalon.Wpf.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    private readonly IServiceScopeFactory? _scopeFactory;

    protected ViewModelBase() { }
    protected ViewModelBase(IServiceScopeFactory scopeFactory) { _scopeFactory = scopeFactory; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public virtual Task InitializeAsync() => Task.CompletedTask;

    protected async Task RunAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action();
        }
        catch (PetSalon.Core.Common.AppException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        if (_scopeFactory is null) throw new InvalidOperationException("Scope factory not provided to this ViewModel");
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    protected async Task WithScopeAsync(Func<IServiceProvider, Task> work)
    {
        if (_scopeFactory is null) throw new InvalidOperationException("Scope factory not provided to this ViewModel");
        await using var scope = _scopeFactory.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }
}
