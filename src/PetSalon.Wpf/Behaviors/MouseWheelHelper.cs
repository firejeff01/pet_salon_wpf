using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PetSalon.Wpf.Behaviors;

/// <summary>
/// 解 WPF 經典問題：ListBox / DataGrid / ComboBox / TreeView 內建之 ScrollViewer
/// 在「自己已捲到頂或底」時還是會吞 PreviewMouseWheel，導致外層 ScrollViewer 拿不到事件。
/// 設 BubbleScroll="True" 後：內部還有 scroll 空間時保持原行為（自己捲）；
/// 一旦碰到邊界，wheel 事件 reroute 給最近的父 ScrollViewer 接手。
/// </summary>
public static class MouseWheelHelper
{
    public static readonly DependencyProperty BubbleScrollProperty = DependencyProperty.RegisterAttached(
        "BubbleScroll", typeof(bool), typeof(MouseWheelHelper),
        new PropertyMetadata(false, OnBubbleScrollChanged));

    public static void SetBubbleScroll(DependencyObject obj, bool value) => obj.SetValue(BubbleScrollProperty, value);
    public static bool GetBubbleScroll(DependencyObject obj) => (bool)obj.GetValue(BubbleScrollProperty);

    private static void OnBubbleScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        if ((bool)e.NewValue) el.PreviewMouseWheel += Handler;
        else el.PreviewMouseWheel -= Handler;
    }

    private static void Handler(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject dep) return;

        // 1) 若內部 ScrollViewer 還有可捲空間，讓它自己處理（不 bubble）
        var inner = FindInnerScrollViewer(dep);
        if (inner is not null)
        {
            if (e.Delta < 0 && inner.VerticalOffset < inner.ScrollableHeight - 0.001) return;
            if (e.Delta > 0 && inner.VerticalOffset > 0.001) return;
        }

        // 2) 自己捲不動了，找父 ScrollViewer
        DependencyObject? parent = VisualTreeHelper.GetParent(dep);
        while (parent is not null and not ScrollViewer)
            parent = VisualTreeHelper.GetParent(parent);
        if (parent is not ScrollViewer outer) return;

        e.Handled = true;
        outer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender,
        });
    }

    private static ScrollViewer? FindInnerScrollViewer(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindInnerScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }
}
