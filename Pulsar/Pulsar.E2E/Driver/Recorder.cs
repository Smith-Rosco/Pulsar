// [Path]: Pulsar/Pulsar.E2E/Driver/Recorder.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScreenRecorderLib;

namespace Pulsar.E2E.Driver
{
    /// <summary>
    /// Video recording via ScreenRecorderLib (H.264 MP4). Used for `record` steps
    /// and for flake diagnosis: the clip ends up in the diagnostic package.
    /// </summary>
    public sealed class ScreenRecorder : IDisposable
    {
        private ScreenRecorderLib.Recorder? _recorder;
        private string? _outputPath;
        private TaskCompletionSource<bool>? _completion;

        /// <summary>Starts recording the primary screen to the given MP4 path.</summary>
        public void Start(string outputMp4Path)
        {
            if (_recorder != null)
            {
                throw new InvalidOperationException("Recording is already active.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputMp4Path))!);
            _outputPath = outputMp4Path;
            _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    // Small files for CI artifacts; quality-based rate control.
                    Framerate = 15,
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.Quality
                    }
                }
            };

            // Capture the main monitor via Desktop Duplication instead of the default
            // Windows.Graphics.Capture. WGC targets windows/surfaces and can pause or
            // renegotiate when the captured display is interrupted by new topmost
            // layers; Desktop Duplication (Windows 8+) streams the desktop image
            // directly and is the deterministic choice for full-screen diagnostic
            // clips (also avoids the WGC capture-border prompt on Windows 11).
            // ScreenRecorderLib 7.x exposes RecorderApi per DisplayRecordingSource,
            // injected through SourceOptions.
            //
            // NOTE: this is NOT the fix for the historical "0-byte MP4" bug — that was
            // a Stop() race with DWM composition churn (see
            // Docs/lessons/SCREENRECORDERLIB_STOP_RACE_ZERO_BYTE.md). This selection
            // only makes the capture backend deterministic.
            var mainMonitor = DisplayRecordingSource.MainMonitor;
            if (mainMonitor != null)
            {
                options.SourceOptions = new SourceOptions
                {
                    RecordingSources = new List<RecordingSourceBase>
                    {
                        new DisplayRecordingSource(mainMonitor)
                        {
                            RecorderApi = RecorderApi.DesktopDuplication
                        }
                    }
                };
            }

            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += OnComplete;
            _recorder.OnRecordingFailed += OnFailed;
            _recorder.Record(outputMp4Path);
        }

        private void OnComplete(object? sender, RecordingCompleteEventArgs e)
        {
            _completion?.TrySetResult(true);
        }

        private void OnFailed(object? sender, RecordingFailedEventArgs e)
        {
            _completion?.TrySetException(new InvalidOperationException($"Recording failed: {e.Error}"));
        }

        /// <summary>Stops recording and waits for the file to finalize and unlock. Returns the MP4 path.</summary>
        public string Stop()
        {
            if (_recorder == null)
            {
                throw new InvalidOperationException("No recording is active.");
            }

            // Order matters: wait for OnRecordingComplete BEFORE disposing the
            // recorder. Disposing first aborts Media Foundation sink finalization,
            // so OnRecordingComplete never fires and the file handle never releases.
            _recorder.Stop();

            bool completionFired;
            try
            {
                completionFired = _completion!.Task.Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException ex)
            {
                throw new InvalidOperationException("Recording finalization failed.", ex.InnerException ?? ex);
            }

            var currentSize = File.Exists(_outputPath!) ? new FileInfo(_outputPath!).Length : -1;

            _recorder.Dispose();
            _recorder = null;

            var path = _outputPath!;
            if (!WaitForFileRelease(path, TimeSpan.FromSeconds(30)))
            {
                throw new InvalidOperationException(
                    $"Recording file is still locked 30s after stop (completionFired={completionFired}, size={currentSize}): '{path}'");
            }

            return path;
        }

        /// <summary>
        /// Polls until the file can be opened exclusively. ScreenRecorderLib finalizes
        /// the MP4 asynchronously and Windows Defender real-time scanning can hold a
        /// freshly written video for a while — both are transparent to callers.
        /// </summary>
        private static bool WaitForFileRelease(string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (IOException)
                {
                    Thread.Sleep(250);
                }
            }

            return false;
        }

        public void Dispose()
        {
            _recorder?.Dispose();
        }
    }
}
