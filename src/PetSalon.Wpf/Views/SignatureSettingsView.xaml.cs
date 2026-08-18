using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Views;

public partial class SignatureSettingsView : UserControl
{
    public SignatureSettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindSignaturePad();
        Loaded += (_, _) => BindSignaturePad();
    }

    private void BindSignaturePad()
    {
        if (DataContext is not SignatureSettingsViewModel vm) return;
        vm.CaptureHandwrittenSignature = SignaturePad.GetPngBytes;
        vm.ClearHandwrittenSignature = SignaturePad.Clear;
    }

    private async void OnImportSignature(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SignatureSettingsViewModel vm) return;
        var dialog = new OpenFileDialog
        {
            Title = "選擇店家簽名圖片",
            Filter = "PNG 或 JPEG 圖片|*.png;*.jpg;*.jpeg",
            Multiselect = false,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() == true)
            await vm.ImportFileAsync(dialog.FileName);
    }
}
