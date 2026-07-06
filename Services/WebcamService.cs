using System.IO;
using OpenCvSharp;

namespace ScreenPulse.Services;

public static class WebcamService
{
    // 从默认摄像头拍一张照片,失败(没有摄像头/被占用/权限问题)时返回 false,调用方静默跳过
    public static bool TryCapturePhoto(string outputPath)
    {
        try
        {
            using var capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                return false;
            }

            // 部分摄像头首帧是黑的,多取几帧让曝光/白平衡稳定下来
            using var frame = new Mat();
            for (int i = 0; i < 5; i++)
            {
                if (!capture.Read(frame) || frame.Empty())
                {
                    return false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            return Cv2.ImWrite(outputPath, frame);
        }
        catch
        {
            return false;
        }
    }
}
