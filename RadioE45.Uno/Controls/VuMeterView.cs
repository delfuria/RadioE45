using Microsoft.UI.Dispatching;
using RadioE45.Visuals;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace RadioE45.Controls;

/// <summary>
/// Simulated VU meter, the Uno counterpart of RadioE45/Controls/VuMeterView.cs (GraphicsView):
/// same levels (RadioE45.Core/Visuals/VuMeterLevels) drawn on the app's Skia canvas, desktop
/// and web.
/// </summary>
public sealed partial class VuMeterView : SKCanvasElement
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(VuMeterView), new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private readonly VuMeterLevels _levels = new();
    private readonly SKPaint _paint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private DispatcherQueueTimer? _timer;

    public VuMeterView()
    {
        Unloaded += (_, _) => Stop();
        Loaded += (_, _) =>
        {
            if (IsActive)
                Start();
        };
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VuMeterView)d;
        if ((bool)e.NewValue)
            view.Start();
        else
            view.Stop();
    }

    private void Start()
    {
        if (_timer is not null || !IsLoaded)
            return;

        _levels.Start();

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = VuMeterLevels.TickInterval;
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

        _levels.Reset();
        Invalidate();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        _levels.Tick();
        Invalidate();
    }

    protected override void RenderOverride(SKCanvas canvas, Windows.Foundation.Size area)
    {
        const int columns = VuMeterLevels.BarCount;
        const int rows = VuMeterLevels.Rows;
        float width = (float)area.Width;
        float height = (float)area.Height;
        float cellWidth = (width - VuMeterLevels.HorizontalSpacing * (columns - 1)) / columns;
        float cellHeight = (height - VuMeterLevels.VerticalSpacing * (rows - 1)) / rows;
        if (cellWidth <= 0 || cellHeight <= 0)
            return;

        for (int col = 0; col < columns; col++)
        {
            int lit = _levels.LitCells(col);
            float x = col * (cellWidth + VuMeterLevels.HorizontalSpacing);

            for (int row = 0; row < rows; row++)
            {
                float y = height - (row + 1) * cellHeight - row * VuMeterLevels.VerticalSpacing;
                var color = new SKColor(VuMeterLevels.RowArgb(row));
                _paint.Color = row < lit ? color : color.WithAlpha((byte)(VuMeterLevels.UnlitAlpha * 255));
                canvas.DrawRoundRect(x, y, cellWidth, cellHeight,
                    VuMeterLevels.CellCornerRadius, VuMeterLevels.CellCornerRadius, _paint);
            }
        }
    }
}
