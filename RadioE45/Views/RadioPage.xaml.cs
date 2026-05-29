using RadioE45.Logic;
using System.Timers;
using System;
using Microsoft.Maui.Dispatching;
using CommunityToolkit.Maui.Alerts;
#if MACCATALYST
using CommunityToolkit.Maui.Views;
#else
using LibVLCSharp.Shared;
#endif

namespace RadioE45.Views;

public partial class RadioPage : ContentPage
{
    private RadioStation radioStation;

    private System.Timers.Timer timer;
    private Random random = new Random();
    private int barCount = 64;
    private float[] currentLevels;
    private bool isPlaying = true;
    private bool isAnimating;

#if !MACCATALYST
    private LibVLC _libVLC;
    private LibVLCSharp.Shared.MediaPlayer _vlcPlayer;
#endif

    public RadioPage(RadioStation radioStation)
    {
        InitializeComponent();

        this.radioStation = radioStation;

        RadioName.Text = radioStation.Name;
        RadioImage.Source = radioStation.IconPath;

        currentLevels = new float[barCount];
        for (int i = 0; i < barCount; i++)
            currentLevels[i] = 0f;

        timer = new System.Timers.Timer(60);
        timer.Elapsed += Timer_Elapsed;
        timer.Start();

        FirstColor.Color = radioStation.LightColor.Color;
        SecondColor.Color = radioStation.DarkColor.Color;

#if MACCATALYST
        MediaPlayer.MediaFailed += async (s, e) =>
        {
            string errorMessage = $"Faild to load media: {e.ErrorMessage} {radioStation.Name}";
            await Toast.Make(errorMessage, CommunityToolkit.Maui.Core.ToastDuration.Long).Show();
        };
#else
        Core.Initialize();
        _libVLC = new LibVLC();
        _vlcPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

        _vlcPlayer.EncounteredError += async (s, e) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Toast.Make($"Errore riproduzione: {radioStation.Name}", CommunityToolkit.Maui.Core.ToastDuration.Long).Show();
            });
        };
#endif
    }

    private void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        Array.Copy(currentLevels, 1, currentLevels, 0, barCount - 1);
        currentLevels[barCount - 1] = (float)random.NextDouble();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AudioVisualizer.UpdateAudioLevels(currentLevels);
        });
    }

    private void MediaAction_Clicked(object sender, EventArgs e)
    {
        if (isPlaying)
        {
#if MACCATALYST
            MediaPlayer.Pause();
#else
            _vlcPlayer.Pause();
#endif
            MediaActionButton.Source = "play_icon.png";
        }
        else
        {
#if MACCATALYST
            MediaPlayer.Play();
#else
            _vlcPlayer.Play();
#endif
            MediaActionButton.Source = "pause_icon.png";
        }

        isPlaying = !isPlaying;
    }

    private void SkipToEnd_Clicked(object sender, EventArgs e)
    {
#if MACCATALYST
        MediaPlayer.Source = null;
        MediaPlayer.Source = MediaSource.FromUri(radioStation.URL);
        MediaPlayer.Play();
#else
        var media = new Media(_libVLC, new Uri(radioStation.URL));
        _vlcPlayer.Media = media;
        _vlcPlayer.Play();
#endif
        MediaActionButton.Source = "pause_icon.png";
        isPlaying = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

#if MACCATALYST
        MediaPlayer.Source = MediaSource.FromUri(radioStation.URL);
        MediaPlayer.Play();
#else
        var media = new Media(_libVLC, new Uri(radioStation.URL));
        _vlcPlayer.Media = media;
        _vlcPlayer.Play();
#endif
        isPlaying = true;
        isAnimating = true;
        AnimateGradient();
    }

    protected override void OnDisappearing()
    {
#if MACCATALYST
        MediaPlayer.Stop();
#else
        _vlcPlayer.Stop();
#endif
        isPlaying = false;
        isAnimating = false;
        base.OnDisappearing();
    }

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
