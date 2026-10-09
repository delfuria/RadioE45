namespace RadioE45.Services.Platform;

public sealed class MauiUiDispatcher : IUiDispatcher
{
    public bool IsUiThread => MainThread.IsMainThread;

    public void Post(Action action) => MainThread.BeginInvokeOnMainThread(action);

    public Task InvokeAsync(Action action) => MainThread.InvokeOnMainThreadAsync(action);

    public Task InvokeAsync(Func<Task> action) => MainThread.InvokeOnMainThreadAsync(action);

    public IUiTimer? CreateTimer(TimeSpan interval, Action tick)
    {
        IDispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return null;

        IDispatcherTimer timer = dispatcher.CreateTimer();
        timer.Interval = interval;
        return new MauiUiTimer(timer, tick);
    }

    private sealed class MauiUiTimer : IUiTimer
    {
        private readonly IDispatcherTimer _timer;
        private readonly Action _tick;

        public MauiUiTimer(IDispatcherTimer timer, Action tick)
        {
            _timer = timer;
            _tick = tick;
            _timer.Tick += OnTick;
        }

        public void Start() => _timer.Start();

        public void Stop()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        private void OnTick(object? sender, EventArgs e) => _tick();
    }
}
