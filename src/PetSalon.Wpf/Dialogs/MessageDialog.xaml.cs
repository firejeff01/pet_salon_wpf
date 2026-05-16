using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PetSalon.Wpf.Dialogs;

public enum MessageDialogKind
{
    Info,
    Success,
    Warning,
    Error,
    Confirm,
}

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        };
    }

    public static bool? Show(Window? owner, MessageDialogKind kind, string title, string message)
    {
        var dlg = new MessageDialog { Owner = owner };
        dlg.Configure(kind, title, message);
        return dlg.ShowDialog();
    }

    private void Configure(MessageDialogKind kind, string title, string message)
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        (string icon, string headerKey) = kind switch
        {
            MessageDialogKind.Success => ("✅", "Pink500Brush"),
            MessageDialogKind.Warning => ("⚠️", "WarningBrush"),
            MessageDialogKind.Error => ("❌", "DangerBrush"),
            MessageDialogKind.Confirm => ("❓", "Pink600Brush"),
            _ => ("ℹ️", "InfoBrush"),
        };
        IconText.Text = icon;
        if (Application.Current.TryFindResource(headerKey) is Brush brush)
            HeaderBar.Background = brush;

        ButtonPanel.Children.Clear();
        if (kind == MessageDialogKind.Confirm)
        {
            AddButton("取消", false, isCancel: true);
            AddButton("確定", true, isDefault: true);
        }
        else
        {
            AddButton("確定", true, isDefault: true, isCancel: true);
        }
    }

    private void AddButton(string text, bool result, bool isDefault = false, bool isCancel = false)
    {
        var styleKey = result ? "PrimaryButtonStyle" : "SecondaryButtonStyle";
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource(styleKey),
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        btn.Click += (_, _) => { DialogResult = result; Close(); };
        ButtonPanel.Children.Add(btn);
    }
}
