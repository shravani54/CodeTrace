using OpenCvSharp;

namespace CodeTrace.Student;   // use the same namespace as your other files

public class CameraService : IDisposable
{
    private VideoCapture? _cap;
    private CancellationTokenSource? _cts;

    public bool Start(Func<byte[], Task> onFrame, int intervalMs = 3000)
    {
        _cap = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
        if (!_cap.IsOpened()) return false;          // no camera or access blocked

        _cap.Set(VideoCaptureProperties.FrameWidth, 640);
        _cap.Set(VideoCaptureProperties.FrameHeight, 480);

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            using var mat = new Mat();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_cap.Read(mat) && !mat.Empty())
                    {
                        Cv2.ImEncode(".jpg", mat, out var bytes,
                            new[] { (int)ImwriteFlags.JpegQuality, 50 });
                        await onFrame(bytes);
                    }
                    await Task.Delay(intervalMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch { /* ignore one failed frame and keep going */ }
            }
        }, ct);

        return true;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cap?.Release();
        _cap?.Dispose();
    }
}