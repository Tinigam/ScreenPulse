using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Screen = System.Windows.Forms.Screen;

namespace ScreenPulse.Services;

public class ScreenCapture
{
    public required int ScreenIndex { get; init; }
    public required Bitmap Image { get; init; }
    public required byte[] Thumbprint { get; init; }
}

public static class ScreenshotService
{
    // 每个屏幕单独截图,便于分别去重与检索
    public static List<ScreenCapture> CaptureAllScreens()
    {
        var results = new List<ScreenCapture>();
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var bounds = screens[i].Bounds;
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            results.Add(new ScreenCapture
            {
                ScreenIndex = i,
                Image = bitmap,
                Thumbprint = ComputeThumbprint(bitmap)
            });
        }
        return results;
    }

    // 缩小成 32x32 灰度像素作为“指纹”,用于快速比较两张图是否基本相同
    public static byte[] ComputeThumbprint(Bitmap source)
    {
        const int size = 32;
        using var small = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(source, 0, 0, size, size);
        }

        var buffer = new byte[size * size];
        int idx = 0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var pixel = small.GetPixel(x, y);
                buffer[idx++] = (byte)((pixel.R + pixel.G + pixel.B) / 3);
            }
        }
        return buffer;
    }

    // 返回 0~1 的相似度,1 表示完全一致
    public static double CompareThumbprints(byte[] a, byte[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        long diffSum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diffSum += Math.Abs(a[i] - b[i]);
        }
        double avgDiff = diffSum / (double)a.Length; // 0~255
        return 1.0 - (avgDiff / 255.0);
    }

    public static void SaveAsJpeg(Bitmap bitmap, string path, long quality = 75L)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        bitmap.Save(path, encoder, encoderParams);
    }
}
