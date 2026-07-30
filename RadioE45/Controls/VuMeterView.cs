using Microsoft.Maui.Graphics;

namespace RadioE45.Controls;

// Simulated VU meter: no platform exposes raw PCM levels for a MediaElement-backed stream
// without native audio taps (Visualizer on Android, MTAudioProcessingTap on iOS/Mac,
// AudioGraph on Windows), so bars follow a smoothed random walk instead of the real signal.
// Same look-and-feel everywhere, driven purely by IsActive.
public class VuMeterView : GraphicsView
{
    private const int BarCount = 2;
    private const double TickIntervalMs = 90;
    private const double NewTargetChance = 0.35;
    private const double AttackFactor = 0.6;
    private const double DecayFactor = 0.25;

    public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
        nameof(IsActive), typeof(bool), typeof(VuMeterView), false, propertyChanged: OnIsActiveChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private readonly Random _random = new();
    private readonly double[] _levels = new double[BarCount];
    private readonly double[] _targets = new double[BarCount];
    private readonly VuMeterDrawable _drawable;
    private IDispatcherTimer? _timer;

    public VuMeterView()
    {
        _drawable = new VuMeterDrawable(_levels);
        Drawable = _drawable;
    }

    private static void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (VuMeterView)bindable;
        if ((bool)newValue)
            view.Start();
        else
            view.Stop();
    }

    private void Start()
    {
        if (_timer is not null)
            return;

        IDispatcher? dispatcher = Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        for (int i = 0; i < BarCount; i++)
            _targets[i] = _random.NextDouble();

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(TickIntervalMs);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void Stop()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;

        Array.Clear(_levels);
        Invalidate();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        for (int i = 0; i < BarCount; i++)
        {
            if (_random.NextDouble() < NewTargetChance)
                _targets[i] = _random.NextDouble();

            double target = _targets[i];
            double current = _levels[i];
            _levels[i] = current < target
                ? current + (target - current) * AttackFactor
                : current + (target - current) * DecayFactor;
        }

        Invalidate();
    }

    private sealed class VuMeterDrawable(double[] levels) : IDrawable
    {
        private const int Rows = 4;
        private const int GreenRows = 2;
        private const int YellowRows = 1;
        private const float HSpacing = 4f;
        private const float VSpacing = 3f;
        private const double UnlitAlpha = 0.15;

        private static readonly Color GreenColor = Color.FromArgb("#30A46C");
        private static readonly Color YellowColor = Color.FromArgb("#F5D90A");
        private static readonly Color RedColor = Color.FromArgb("#E5484D");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            int columns = levels.Length;
            float cellWidth = (dirtyRect.Width - HSpacing * (columns - 1)) / columns;
            float cellHeight = (dirtyRect.Height - VSpacing * (Rows - 1)) / Rows;
            if (cellWidth <= 0 || cellHeight <= 0)
                return;

            for (int col = 0; col < columns; col++)
            {
                double level = Math.Clamp(levels[col], 0, 1);
                int lit = (int)Math.Round(level * Rows);
                float x = dirtyRect.X + col * (cellWidth + HSpacing);

                for (int row = 0; row < Rows; row++)
                {
                    float y = dirtyRect.Bottom - (row + 1) * cellHeight - row * VSpacing;
                    Color color = ColorForRow(row);
                    canvas.FillColor = row < lit ? color : color.WithAlpha((float)UnlitAlpha);
                    canvas.FillRoundedRectangle(x, y, cellWidth, cellHeight, 2f);
                }
            }
        }

        private static Color ColorForRow(int row) =>
            row < GreenRows ? GreenColor : row < GreenRows + YellowRows ? YellowColor : RedColor;
    }
}
