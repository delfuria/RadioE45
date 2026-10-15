using Microsoft.UI.Dispatching;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary><see cref="IUiDispatcher"/> on the window's <see cref="DispatcherQueue"/> (desktop and web).</summary>
public sealed class UnoUiDispatcher(UnoWindowHost host) : IUiDispatcher
{
    public bool IsUiThread => host.DispatcherQueue?.HasThreadAccess ?? false;

    public void Post(Action action)
    {
        if (host.DispatcherQueue is { } queue)
            queue.TryEnqueue(() => action());
    }

    public Task InvokeAsync(Action action) =>
        InvokeAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });

    public Task InvokeAsync(Func<Task> action)
    {
        var queue = host.DispatcherQueue;
        if (queue is null || queue.HasThreadAccess)
            return action();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = queue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        if (!queued)
            completion.SetException(new InvalidOperationException("The UI dispatcher queue is shutting down."));

        return completion.Task;
    }

    public IUiTimer? CreateTimer(TimeSpan interval, Action tick)
    {
        if (host.DispatcherQueue is not { } queue)
            return null;

        var timer = queue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = true;
        return new UnoUiTimer(timer, tick);
    }

    // Same lifecycle as the MAUI timer: Stop() also detaches the tick, the owner creates a new
    // timer to restart (OnAirViewModel does).
    private sealed class UnoUiTimer : IUiTimer
    {
        private readonly DispatcherQueueTimer _timer;
        private readonly Action _tick;

        public UnoUiTimer(DispatcherQueueTimer timer, Action tick)
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

        private void OnTick(DispatcherQueueTimer sender, object args) => _tick();
    }
}
