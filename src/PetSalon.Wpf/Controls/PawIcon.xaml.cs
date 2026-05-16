using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PetSalon.Wpf.Controls;

public partial class PawIcon : UserControl
{
    public static readonly DependencyProperty PawFillProperty = DependencyProperty.Register(
        nameof(PawFill), typeof(Brush), typeof(PawIcon),
        new PropertyMetadata(Brushes.White));

    public Brush PawFill
    {
        get => (Brush)GetValue(PawFillProperty);
        set => SetValue(PawFillProperty, value);
    }

    public PawIcon() { InitializeComponent(); }
}
