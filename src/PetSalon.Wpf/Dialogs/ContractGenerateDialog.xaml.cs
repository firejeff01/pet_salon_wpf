using System.Windows.Controls;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Dialogs;

public partial class ContractGenerateDialog : UserControl
{
    public ContractGenerateDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ContractGenerateDialogViewModel vm)
                await vm.LoadPreviewAsync();
        };
    }
}
