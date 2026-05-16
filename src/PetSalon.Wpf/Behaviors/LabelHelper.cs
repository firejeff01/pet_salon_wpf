using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PetSalon.Wpf.Behaviors;

public static class LabelHelper
{
    public static readonly DependencyProperty RequiredTextProperty = DependencyProperty.RegisterAttached(
        "RequiredText", typeof(string), typeof(LabelHelper),
        new PropertyMetadata(null, OnRequiredTextChanged));

    public static string GetRequiredText(TextBlock tb) => (string)tb.GetValue(RequiredTextProperty);
    public static void SetRequiredText(TextBlock tb, string value) => tb.SetValue(RequiredTextProperty, value);

    private static void OnRequiredTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb || e.NewValue is not string text) return;
        tb.Inlines.Clear();
        tb.Inlines.Add(new Run(text + " "));
        var asterisk = new Run("*")
        {
            Foreground = TryFindBrush("DangerBrush") ?? Brushes.Red,
            FontWeight = FontWeights.Bold,
        };
        tb.Inlines.Add(asterisk);
    }

    private static Brush? TryFindBrush(string key)
    {
        try { return Application.Current?.FindResource(key) as Brush; }
        catch { return null; }
    }
}
