using System.Windows.Controls;
using System.Windows.Input;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Views;

public partial class HomeView : UserControl
{
    public HomeView() { InitializeComponent(); }

    private HomeViewModel? Vm => DataContext as HomeViewModel;
    private void OnGoOwners(object sender, MouseButtonEventArgs e) { if (Vm?.GoOwnersCommand.CanExecute(null) == true) Vm.GoOwnersCommand.Execute(null); }
    private void OnGoCalendar(object sender, MouseButtonEventArgs e) { if (Vm?.GoCalendarCommand.CanExecute(null) == true) Vm.GoCalendarCommand.Execute(null); }
    private void OnGoCustomer(object sender, MouseButtonEventArgs e) { if (Vm?.GoCustomerCommand.CanExecute(null) == true) Vm.GoCustomerCommand.Execute(null); }
    private void OnGoBackup(object sender, MouseButtonEventArgs e) { if (Vm?.GoBackupCommand.CanExecute(null) == true) Vm.GoBackupCommand.Execute(null); }
}
