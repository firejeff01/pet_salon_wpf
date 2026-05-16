using System.Windows;
using PetSalon.Wpf.Dialogs;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Services;

public interface IDialogService
{
    void Info(string title, string message);
    void Success(string title, string message);
    void Warning(string title, string message);
    void Error(string title, string message);
    bool Confirm(string title, string message);
    bool? ShowDialog(ViewModelBase viewModel, string title, double width = 720, double height = 600);
}

public sealed class DialogService : IDialogService
{
    public void Info(string title, string message)
        => MessageDialog.Show(GetOwner(), MessageDialogKind.Info, title, message);

    public void Success(string title, string message)
        => MessageDialog.Show(GetOwner(), MessageDialogKind.Success, title, message);

    public void Warning(string title, string message)
        => MessageDialog.Show(GetOwner(), MessageDialogKind.Warning, title, message);

    public void Error(string title, string message)
        => MessageDialog.Show(GetOwner(), MessageDialogKind.Error, title, message);

    public bool Confirm(string title, string message)
        => MessageDialog.Show(GetOwner(), MessageDialogKind.Confirm, title, message) == true;

    public bool? ShowDialog(ViewModelBase viewModel, string title, double width = 720, double height = 600)
    {
        // 限制不超過螢幕工作區（扣掉工具列），避免出現截掉的對話框
        var workArea = System.Windows.SystemParameters.WorkArea;
        var maxW = workArea.Width - 40;
        var maxH = workArea.Height - 40;
        var window = new Window
        {
            Title = title,
            Width = Math.Min(width, maxW),
            Height = Math.Min(height, maxH),
            MaxWidth = workArea.Width,
            MaxHeight = workArea.Height,
            Content = viewModel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = GetOwner(),
            SizeToContent = SizeToContent.Manual,
            FontFamily = new System.Windows.Media.FontFamily("Microsoft JhengHei UI"),
            Background = (System.Windows.Media.Brush)Application.Current.FindResource("BgBrush"),
        };
        if (viewModel is IDialogResultProvider provider)
        {
            Action<bool?>? handler = null;
            handler = r =>
            {
                provider.RequestClose -= handler;
                if (window.IsVisible)
                {
                    window.DialogResult = r;
                    window.Close();
                }
            };
            provider.RequestClose += handler;
        }
        return window.ShowDialog();
    }

    private static Window? GetOwner() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;
}

public interface IDialogResultProvider
{
    event Action<bool?> RequestClose;
}
