using System.Windows.Controls;
using System.Windows.Input;
using PetSalon.Core.Entities;
using PetSalon.Wpf.ViewModels;

namespace PetSalon.Wpf.Views;

public partial class DailyAppointmentsView : UserControl
{
    public DailyAppointmentsView() { InitializeComponent(); }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DailyAppointmentsViewModel vm && sender is DataGridRow row && row.Item is Appointment a)
            vm.OpenGroomingCommand.Execute(a);
    }
}
