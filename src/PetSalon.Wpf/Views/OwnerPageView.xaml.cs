using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PetSalon.Core.Entities;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Views;

public partial class OwnerPageView : UserControl
{
    public OwnerPageView() { InitializeComponent(); }

    private void OnPetCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OwnerPageViewModel vm && sender is FrameworkElement fe && fe.DataContext is Pet pet)
            vm.EditPetCommand.Execute(pet);
    }
}
