using System.Diagnostics;
using AudioToolbox;
using AVFoundation;
using Foundation;
using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Audio;

/// <summary>
/// iOS player for progressive live streams (Icecast/SHOUTcast MP3 or AAC): downloads with
/// NSUrlSession, splits packets with AudioFileStream and plays them with an AudioQueue, so the
/// audio is rendered inside the app. AVPlayer hands such streams to buffered AirPlay, which on
/// CarPlay waits about 20 s before the first sound (docs/carplay-ios/carplay-mp3-start-delay.md).
/// HLS stays on <see cref="AvStreamPlayer"/>.
/// Members are called on the UI thread; events may be raised on any thread.
/// </summary>
public sealed class AudioQueueStreamPlayer : IStreamPlayer
{
    private readonly ILogger? _logger;
    private readonly object _stateLock = new();
    private readonly NSObject _interruptionObserver;
    private StreamConnection? _connection;
    private StreamPlayerState _state = StreamPlayerState.None;
    private float _volume = 1f;

    public AudioQueueStreamPlayer(ILogger? logger = null)
    {
        _logger = logger;
        _interruptionObserver = AVAudioSession.Notifications.ObserveInterruption((_, e) =>
        {
            // The system stops our queue during an interruption (phone call, Siri). Report Paused,
            // like AVPlayer does: the live engine's watchdog reopens the stream afterwards.
            if (e.InterruptionType == AVAudioSessionInterruptionType.Began && _connection is { } connection)
            {
                connection.Cancel();
                SetState(connection, StreamPlayerState.Paused);
            }
        });
    }

    public bool HasSource => _connection is not null;

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _connection?.Cancel();

        NSUrl? nsUrl = NSUrl.FromString(url);
        if (nsUrl is null)
        {
            Failed?.Invoke(this, $"Invalid stream URL: {url}");
            return;
        }

        StreamConnection connection = new(this, nsUrl, _volume, _logger);
        _connection = connection;
        SetState(connection, StreamPlayerState.Opening);
        AudioSessionDiagnostics.LogState("open");
        connection.Start();
    }

    public void Close()
    {
        StreamConnection? connection = _connection;
        _connection = null;
        connection?.Cancel();
        SetState(null, StreamPlayerState.Stopped);
    }

    public void SetVolume(double volume)
    {
        _volume = (float)volume;
        _connection?.SetVolume(_volume);
    }

    public void SetMetadata(string artist, string title, string? artworkUrl)
    {
    }

    public void ClearMetadata()
    {
    }

    private void SetState(StreamConnection? source, StreamPlayerState next)
    {
        StreamPlayerState previous;
        lock (_stateLock)
        {
            // Ignore late reports from a connection that has already been replaced or closed.
            if (source is not null && !ReferenceEquals(source, _connection))
                return;

            previous = _state;
            if (previous == next)
                return;

            _state = next;
        }

        StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, next));
    }

    private void OnConnectionFailed(StreamConnection source, string message)
    {
        if (!ReferenceEquals(source, _connection))
            return;

        SetState(source, StreamPlayerState.Failed);
        Failed?.Invoke(this, message);
    }

    private void OnConnectionEnded(StreamConnection source)
    {
        if (!ReferenceEquals(source, _connection))
            return;

        SetState(source, StreamPlayerState.Stopped);
        Ended?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// One opening of the stream. Network data, parsing and buffer filling run on a private serial
    /// queue; the AudioQueue returns played buffers on its own thread. The parser and the queue are
    /// released on the serial queue once the download has completed, never while it may still use them.
    /// </summary>
    private sealed class StreamConnection : NSUrlSessionDataDelegate
    {
        private const int BufferSize = 8 * 1024;
        private const int BufferCount = 16;
        private const int MaxPacketsPerBuffer = 512;
        private const int BuffersBeforeStart = 2;
        private static readonly TimeSpan UnderrunGrace = TimeSpan.FromSeconds(2);

        private readonly AudioQueueStreamPlayer _owner;
        private readonly NSUrl _url;
        private readonly ILogger? _logger;
        private readonly object _lock = new();
        private readonly Queue<nint> _freeBuffers = new();
        private readonly List<AudioStreamPacketDescription> _fillPackets = [];
        private readonly Stopwatch _sinceStart = new();
        private readonly NSOperationQueue _ioQueue = new() { MaxConcurrentOperationCount = 1, Name = "RadioE45.AudioQueueStream" };
        private NSUrlSession? _session;
        private AudioFileStream? _parser;
        private OutputAudioQueue? _queue;
        private AudioFileType _fileType = AudioFileType.MP3;
        private nint _fillBuffer;
        private int _fillOffset;
        private int _enqueued;
        private bool _queueStarted;
        private long _lastEnqueueTicks;
        private bool _underrunCheckPending;
        private bool _cancelled;
        private float _volume;

        public StreamConnection(AudioQueueStreamPlayer owner, NSUrl url, float volume, ILogger? logger)
        {
            _owner = owner;
            _url = url;
            _volume = volume;
            _logger = logger;
        }

        public void Start()
        {
            _sinceStart.Start();
            NSUrlSessionConfiguration configuration = NSUrlSessionConfiguration.DefaultSessionConfiguration;
            configuration.TimeoutIntervalForRequest = 15;
            _session = NSUrlSession.FromConfiguration(configuration, (INSUrlSessionDelegate)this, _ioQueue);
            _session.CreateDataTask(_url).Resume();
        }

        public void Cancel()
        {
            OutputAudioQueue? queue;
            lock (_lock)
            {
                if (_cancelled)
                    return;

                _cancelled = true;
                queue = _queue;
                Monitor.PulseAll(_lock);
            }

            // Silence at once; the parser and the queue are released in DidCompleteWithError.
            queue?.Stop(true);
            _session?.InvalidateAndCancel();
        }

        public void SetVolume(float volume)
        {
            lock (_lock)
            {
                _volume = volume;
                if (_queue is not null)
                    _queue.Volume = volume;
            }
        }

        public override void DidReceiveResponse(NSUrlSession session, NSUrlSessionDataTask dataTask, NSUrlResponse response, Action<NSUrlSessionResponseDisposition> completionHandler)
        {
            if (response is NSHttpUrlResponse http && http.StatusCode >= 400)
            {
                completionHandler(NSUrlSessionResponseDisposition.Cancel);
                _owner.OnConnectionFailed(this, $"HTTP {http.StatusCode}");
                return;
            }

            string mime = response.MimeType ?? string.Empty;
            _fileType = mime.Contains("aac", StringComparison.OrdinalIgnoreCase) ? AudioFileType.AAC_ADTS : AudioFileType.MP3;
            _logger?.LogInformation("AudioQueueStreamPlayer: response {Mime} → {FileType} after {Ms} ms", mime, _fileType, _sinceStart.ElapsedMilliseconds);
            completionHandler(NSUrlSessionResponseDisposition.Allow);
        }

        public override void DidReceiveData(NSUrlSession session, NSUrlSessionDataTask dataTask, NSData data)
        {
            if (_cancelled)
                return;

            if (_parser is null)
            {
                _parser = new AudioFileStream(_fileType);
                _parser.PropertyFound += OnPropertyFound;
                _parser.PacketDecoded += OnPacketDecoded;
            }

            AudioFileStreamStatus status = _parser.ParseBytes((int)data.Length, data.Bytes, false);
            if (status != AudioFileStreamStatus.Ok && !_cancelled)
            {
                Cancel();
                _owner.OnConnectionFailed(this, $"Stream parse error {status}");
            }
        }

        public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
        {
            bool wasCancelled;
            lock (_lock)
            {
                wasCancelled = _cancelled;
                _cancelled = true;
                Monitor.PulseAll(_lock);
            }

            if (!wasCancelled)
            {
                if (error is not null)
                    _owner.OnConnectionFailed(this, error.LocalizedDescription);
                else
                    _owner.OnConnectionEnded(this);
            }

            Release();
            session.FinishTasksAndInvalidate();
        }

        private void OnPropertyFound(object? sender, PropertyFoundEventArgs e)
        {
            if (e.Property != AudioFileStreamProperty.ReadyToProducePackets || _parser is null || _queue is not null)
                return;

            try
            {
                OutputAudioQueue queue = new(_parser.DataFormat);
                byte[] cookie = _parser.MagicCookie;
                if (cookie.Length > 0)
                    queue.MagicCookie = cookie;

                for (int i = 0; i < BufferCount; i++)
                {
                    nint buffer = AllocateBuffer(queue);
                    if (buffer == 0)
                        throw new InvalidOperationException("AudioQueue buffer allocation failed");

                    _freeBuffers.Enqueue(buffer);
                }

                queue.BufferCompleted += OnBufferCompleted;
                lock (_lock)
                {
                    queue.Volume = _volume;
                    _queue = queue;
                }

                _logger?.LogInformation("AudioQueueStreamPlayer: format {Rate} Hz, {Channels} ch after {Ms} ms",
                    _parser.DataFormat.SampleRate, _parser.DataFormat.ChannelsPerFrame, _sinceStart.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Cancel();
                _owner.OnConnectionFailed(this, $"Audio queue error: {ex.Message}");
            }
        }

        private void OnPacketDecoded(object? sender, PacketReceivedEventArgs e)
        {
            if (_queue is null || e.PacketDescriptions is not { Length: > 0 } packets)
                return;

            foreach (AudioStreamPacketDescription packet in packets)
            {
                int size = packet.DataByteSize;
                if (size <= 0 || size > BufferSize)
                    continue;

                if (_fillBuffer != 0 && (_fillOffset + size > BufferSize || _fillPackets.Count >= MaxPacketsPerBuffer))
                    EnqueueFillBuffer();

                if (_fillBuffer == 0 && !TakeFreeBuffer())
                    return;

                AudioQueue.FillAudioData(_fillBuffer, _fillOffset, e.InputData, (int)packet.StartOffset, size);
                _fillPackets.Add(new AudioStreamPacketDescription
                {
                    StartOffset = _fillOffset,
                    DataByteSize = size,
                    VariableFramesInPacket = packet.VariableFramesInPacket,
                });
                _fillOffset += size;
            }
        }

        private static unsafe nint AllocateBuffer(OutputAudioQueue queue) =>
            queue.AllocateBuffer(BufferSize, MaxPacketsPerBuffer, out AudioQueueBuffer* buffer) == AudioQueueStatus.Ok
                ? (nint)buffer
                : 0;

        /// <summary>Waits (on the network queue: back-pressure) until the AudioQueue returns a buffer.</summary>
        private bool TakeFreeBuffer()
        {
            lock (_lock)
            {
                while (_freeBuffers.Count == 0 && !_cancelled)
                    Monitor.Wait(_lock, 200);

                if (_cancelled)
                    return false;

                _fillBuffer = _freeBuffers.Dequeue();
                _fillOffset = 0;
                _fillPackets.Clear();
                return true;
            }
        }

        private void EnqueueFillBuffer()
        {
            OutputAudioQueue? queue = _queue;
            if (queue is null || _fillBuffer == 0 || _cancelled)
                return;

            AudioQueueStatus status = queue.EnqueueBuffer(_fillBuffer, _fillOffset, [.. _fillPackets]);
            _fillBuffer = 0;
            if (status != AudioQueueStatus.Ok)
                return;

            int enqueued;
            lock (_lock)
            {
                enqueued = ++_enqueued;
                _lastEnqueueTicks = Environment.TickCount64;
            }

            if (!_queueStarted && enqueued >= BuffersBeforeStart)
            {
                AudioQueueStatus started = queue.Start();
                if (started != AudioQueueStatus.Ok)
                {
                    Cancel();
                    _owner.OnConnectionFailed(this, $"Audio queue start error {started}");
                    return;
                }

                _queueStarted = true;
                _logger?.LogInformation("AudioQueueStreamPlayer: playing {Ms} ms after open", _sinceStart.ElapsedMilliseconds);
                _owner.SetState(this, StreamPlayerState.Playing);
            }
            else if (_queueStarted && enqueued == 1)
            {
                // Back from an underrun.
                _owner.SetState(this, StreamPlayerState.Playing);
            }
        }

        private void OnBufferCompleted(object? sender, BufferCompletedEventArgs e)
        {
            int enqueued;
            lock (_lock)
            {
                if (_cancelled)
                    return;

                _freeBuffers.Enqueue(e.IntPtrBuffer);
                enqueued = --_enqueued;
                Monitor.PulseAll(_lock);
            }

            // With compressed formats the queue returns a buffer once it has decoded it, not once it
            // has played it: an empty queue is not yet silence. Report Buffering only if no new data
            // arrives for UnderrunGrace (a real network stall), which the live engine's watchdog handles.
            bool check;
            lock (_lock)
            {
                check = enqueued == 0 && !_underrunCheckPending;
                if (check)
                    _underrunCheckPending = true;
            }

            if (check)
                _ = ReportUnderrunIfStillEmptyAsync();
        }

        private async Task ReportUnderrunIfStillEmptyAsync()
        {
            await Task.Delay(UnderrunGrace);
            lock (_lock)
            {
                _underrunCheckPending = false;
                // A live stream keeps the queue near empty while data flows: only a pause in the
                // incoming data (nothing enqueued for UnderrunGrace) is a real stall.
                if (_cancelled || _enqueued > 0 || Environment.TickCount64 - _lastEnqueueTicks < UnderrunGrace.TotalMilliseconds)
                    return;
            }

            _logger?.LogInformation("AudioQueueStreamPlayer: no data for {Ms} ms, buffering", UnderrunGrace.TotalMilliseconds);
            _owner.SetState(this, StreamPlayerState.Buffering);
        }

        private void Release()
        {
            OutputAudioQueue? queue;
            lock (_lock)
            {
                queue = _queue;
                _queue = null;
                _freeBuffers.Clear();
            }

            if (queue is not null)
            {
                queue.BufferCompleted -= OnBufferCompleted;
                queue.Stop(true);
                queue.Dispose();
            }

            if (_parser is not null)
            {
                _parser.PropertyFound -= OnPropertyFound;
                _parser.PacketDecoded -= OnPacketDecoded;
                _parser.Dispose();
                _parser = null;
            }
        }
    }
}
