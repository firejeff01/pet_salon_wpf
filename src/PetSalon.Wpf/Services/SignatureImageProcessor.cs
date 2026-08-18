using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PetSalon.Core.Common;
using PetSalon.Core.Services;

namespace PetSalon.Wpf.Services;

public sealed class SignatureImageProcessor
{
    private readonly ShopSignatureOptions _options;

    public SignatureImageProcessor(ShopSignatureOptions options) => _options = options;

    public byte[] NormalizeToPng(byte[] input)
    {
        if (input.Length == 0 || input.Length > _options.MaxFileBytes * 4)
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片無效或超出大小限制");

        BitmapFrame frame;
        try
        {
            using var source = new MemoryStream(input, writable: false);
            var decoder = BitmapDecoder.Create(
                source,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            frame = decoder.Frames[0];
        }
        catch (Exception)
        {
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "只能匯入有效的 PNG 或 JPEG 圖片");
        }

        if (frame.Decoder.CodecInfo.FriendlyName is not string codec ||
            (!codec.Contains("PNG", StringComparison.OrdinalIgnoreCase) &&
             !codec.Contains("JPEG", StringComparison.OrdinalIgnoreCase) &&
             !codec.Contains("JPG", StringComparison.OrdinalIgnoreCase)))
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "只支援 PNG 或 JPEG 圖片");
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 ||
            frame.PixelWidth > _options.MaxPixelWidth || frame.PixelHeight > _options.MaxPixelHeight)
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片尺寸超出限制");

        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = y * stride + x * 4;
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            var a = pixels[i + 3];
            if (a == 0 || (r >= 248 && g >= 248 && b >= 248))
            {
                pixels[i + 3] = 0;
                continue;
            }
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        if (maxX < minX || maxY < minY)
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "簽名圖片中沒有可辨識的筆跡");

        const int padding = 8;
        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(width - 1, maxX + padding);
        maxY = Math.Min(height - 1, maxY + padding);

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var crop = new CroppedBitmap(bitmap, new System.Windows.Int32Rect(
            minX, minY, maxX - minX + 1, maxY - minY + 1));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(crop));
        using var output = new MemoryStream();
        encoder.Save(output);
        var bytes = output.ToArray();
        if (bytes.Length > _options.MaxFileBytes)
            throw AppException.Unprocessable("INVALID_SIGNATURE_IMAGE", "正規化後的簽名圖片超出大小限制");
        return bytes;
    }

    public static ImageSource ToImageSource(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
