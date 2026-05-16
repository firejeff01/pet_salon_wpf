using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PetSalon.Wpf.Controls;

public partial class SignaturePad : UserControl
{
    public SignaturePad()
    {
        InitializeComponent();
        Ink.StrokeCollected += (_, _) => UpdatePlaceholder();
        Ink.StrokeErased += (_, _) => UpdatePlaceholder();
    }

    public bool HasSignature => Ink.Strokes.Count > 0;

    public byte[]? GetPngBytes()
    {
        if (!HasSignature) return null;

        var w = (int)Math.Max(Ink.ActualWidth, 1);
        var h = (int)Math.Max(Ink.ActualHeight, 1);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            ctx.DrawDrawing(GetInkDrawing());
        }
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public void Clear()
    {
        Ink.Strokes.Clear();
        UpdatePlaceholder();
    }

    public void LoadFromPng(byte[]? png)
    {
        Clear();
        // Signature replay is not supported in this minimal pad; users re-sign each contract.
    }

    private Drawing GetInkDrawing()
    {
        var dg = new DrawingGroup();
        using var ctx = dg.Open();
        foreach (var stroke in Ink.Strokes)
        {
            var geom = stroke.GetGeometry();
            ctx.DrawGeometry(new SolidColorBrush(stroke.DrawingAttributes.Color), null, geom);
        }
        return dg;
    }

    private void OnClear(object sender, RoutedEventArgs e) => Clear();

    private void UpdatePlaceholder()
    {
        Placeholder.Visibility = HasSignature ? Visibility.Collapsed : Visibility.Visible;
    }
}
