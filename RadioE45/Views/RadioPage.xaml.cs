using RadioE45.Logic;
using System.Timers;
using System;
using Microsoft.Maui.Dispatching;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace RadioE45.Views;

public partial class RadioPage : ContentPage
{
    private RadioStation radioStation;

    private System.Timers.Timer timer;
    private System.Timers.Timer watchdogTimer;
    private Random random = new Random();
    private int barCount = 64;

    private float[] currentLevels;

    private bool isPlaying = true;
    private bool isAnimating;
    private bool isReconnecting = false;

    public RadioPage(RadioStation radioStation)
    {
        InitializeComponent();

        this.radioStation = radioStation;

        RadioName.Text = radioStation.Name;
        RadioImage.Source = radioStation.IconPath;

        currentLevels = new float[barCount];

        timer = new System.Timers.Timer(60);
        timer.Elapsed += Timer_Elapsed;
        timer.Start();

        // Watchdog: if we should be playing but MediaElement has stalled, reconnect
        watchdogTimer = new System.Timers.Timer(10_000);
        watchdogTimer.Elapsed += Watchdog_Elapsed;
        watchdogTimer.AutoReset = true;

        FirstColor.Color = radioStation.LightColor.Color;
        SecondColor.Color = radioStation.DarkColor.Color;

        MediaPlayer.MediaFailed += OnMediaFailed;
        MediaPlayer.MediaEnded += OnMediaEnded;

        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    // --- Reconnection logic ---

    private async void OnMediaFailed(object s, MediaFailedEventArgs e)
    {
        if (!isPlaying) return;
        await Task.Delay(3000);
        TryReconnect();
    }

    private void OnMediaEnded(object s, EventArgs e)
    {
        // A live stream should never end; treat it as a dropped connection
        if (isPlaying)
            TryReconnect();
    }

    private void OnConnectivityChanged(object s, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet && isPlaying)
            TryReconnect();
    }

    private void Watchdog_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (!isPlaying) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var state = MediaPlayer.CurrentState;
            if (state != MediaElementState.Playing && state != MediaElementState.Buffering)
                TryReconnect();
        });
    }

    private void TryReconnect()
    {
        if (isReconnecting) return;
        isReconnecting = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            MediaPlayer.Source = null;
            MediaPlayer.Source = MediaSource.FromUri(radioStation.URL);
            MediaPlayer.Play();
            isReconnecting = false;
        });
    }

    // --- Visualizer timer ---

    private void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        Array.Copy(currentLevels, 1, currentLevels, 0, barCount - 1);
        currentLevels[barCount - 1] = (float)random.NextDouble();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AudioVisualizer.UpdateAudioLevels(currentLevels);
        });
    }

    // --- Controls ---

    private void MediaAction_Clicked(object sender, EventArgs e)
    {
        if (isPlaying)
        {
            MediaPlayer.Pause();
            MediaActionButton.Source = "play_icon.png";
            watchdogTimer.Stop();
        }
        else
        {
            MediaPlayer.Play();
            MediaActionButton.Source = "pause_icon.png";
            watchdogTimer.Start();
        }

        isPlaying = !isPlaying;
    }

    private void SkipToEnd_Clicked(object sender, EventArgs e)
    {
        MediaPlayer.Source = null;
        MediaPlayer.Source = MediaSource.FromUri(radioStation.URL);

        MediaPlayer.Play();
        MediaActionButton.Source = "pause_icon.png";
        isPlaying = true;
    }

    // --- Page lifecycle ---

    protected override void OnAppearing()
    {
        base.OnAppearing();

        MediaPlayer.Source = MediaSource.FromUri(radioStation.URL);
        MediaPlayer.Play();
        isPlaying = true;
        watchdogTimer.Start();

        isAnimating = true;
        AnimateGradient();
    }

    protected override void OnDisappearing()
    {
        watchdogTimer.Stop();
        MediaPlayer.Stop();
        isPlaying = false;
        isAnimating = false;

        MediaPlayer.MediaFailed -= OnMediaFailed;
        MediaPlayer.MediaEnded -= OnMediaEnded;
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;

        base.OnDisappearing();
    }

    // --- Gradient animation ---

    private async void AnimateGradient()
    {
        while (isAnimating)
        {
            await AnimateOffset(0, 0.3, 3000, Easing.SinInOut);
            await AnimateOffset(0.3, 0, 3000, Easing.SinInOut);
        }
    }

    private async Task AnimateOffset(double start, double end, int duration, Easing easing)
    {
        double timeElapsed = 0;
        int frameRate = 60;
        double frameTime = 1000.0 / frameRate;

        while (timeElapsed < duration)
        {
            if (!isAnimating) return;

            double progress = timeElapsed / duration;
            double easedProgress = easing.Ease(progress);
            double newOffset = Lerp(start, end, easedProgress);

            FirstColor.Offset = (float)newOffset;
            SecondColor.Offset = (float)(1 - newOffset);

            await Task.Delay((int)frameTime);
            timeElapsed += frameTime;
        }

        FirstColor.Offset = (float)end;
        SecondColor.Offset = (float)(1 - end);
    }

    private double Lerp(double start, double end, double t)
    {
        return start + (end - start) * t;
    }
}

