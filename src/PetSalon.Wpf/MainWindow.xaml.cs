using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PetSalon.Wpf.Dialogs;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding { Key = Key.F11, Command = new RelayDelegate(ToggleFullScreen) });
    }

    private void OnOpenAppDataFolder(object sender, RoutedEventArgs e)
    {
        var appData = Environment.GetEnvironmentVariable("PETSALON_APP_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PetSalon");
        if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
        Process.Start(new ProcessStartInfo { FileName = appData, UseShellExecute = true });
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnReload(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ReloadCommand.Execute(null);
    }

    private void OnToggleFullScreen(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void ToggleFullScreen()
    {
        if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        MessageDialog.Show(this, MessageDialogKind.Info, "關於",
            "貳寶寵物美容工坊 — 犬貓美容定型化契約系統\n" +
            ".NET 10 WPF 重寫版\n" +
            "© 貳寶寵物美容工坊");
    }
}

internal sealed class RelayDelegate : ICommand
{
    private readonly Action _action;
    public RelayDelegate(Action action) { _action = action; }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
