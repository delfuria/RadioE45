using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Linq;
using System.Timers;
using Timer = System.Timers.Timer;

namespace RadioE45.Views
{
    class AudioVisualizerView : SKCanvasView
    {
        private float[] AudioLevels = new float[64];  // Stores current levels
        private float[] TargetLevels = new float[64]; // Stores new input levels to transition to
        private Timer shiftTimer;
        private const int UpdateIntervalMs = 30; // Adjust delay here

        public AudioVisualizerView()
        {
            this.PaintSurface += OnPaintSurface;

            // Timer to smoothly shift bars left over time
            shiftTimer = new Timer(UpdateIntervalMs);
            shiftTimer.Elapsed += OnShiftTick;
            shiftTimer.AutoReset = true;
            shiftTimer.Start();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            int barCount = AudioLevels.Length;
            float width = e.Info.Width;
            float height = e.Info.Height;
            float barWidth = width / barCount;
            float centerY = height / 2;

            using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
            {
                for (int i = 0; i < barCount; i++)
                {
                    float level = AudioLevels[i];
                    float halfBarHeight = level * centerY;
                    float gap = 2;
                    float left = i * barWidth;
                    float right = (i + 1) * barWidth - gap;

                    var topRect = new SKRect(left, centerY - halfBarHeight, right, centerY);
                    var bottomRect = new SKRect(left, centerY, right, centerY + halfBarHeight);

                    float cornerRadius = (barWidth * 0.3f);
                    canvas.DrawRoundRect(topRect, cornerRadius, cornerRadius, paint);
                    canvas.DrawRoundRect(bottomRect, cornerRadius, cornerRadius, paint);
                }
            }
        }

        public void UpdateAudioLevels(float[] levels)
        {
            if (levels == null || levels.Length == 0)
                return;

            if (levels.Length != TargetLevels.Length)
                TargetLevels = new float[levels.Length];

            Array.Copy(levels, TargetLevels, levels.Length);
        }

        private void OnShiftTick(object sender, ElapsedEventArgs e)
        {

            // Shift all bars left by one position
            for (int i = 0; i < AudioLevels.Length - 1; i++)
            {
                AudioLevels[i] = AudioLevels[i + 1];
            }

            // Gradually introduce the new target values at the end
            AudioLevels[^1] = TargetLevels[^1];

            InvalidateSurface(); // Redraw the visualizer
        }

    }

}
