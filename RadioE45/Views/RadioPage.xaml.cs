using RadioE45.Logic;
using System.Timers;
using System;
using Microsoft.Maui.Dispatching;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Alerts; // For MainThread

namespace RadioE45.Views;

public partial class RadioPage : ContentPage
{
    private RadioStation radioStation;

    private System.Timers.Timer timer;
    private Random random = new Random();
    private int barCount = 64;

    // Field to hold the current audio levels for sliding animation
    private float[] currentLevels;


    private bool isPlaying = true;

    private bool isAnimating;

    public RadioPage(RadioStation radioStation)
    {
        InitializeComponent();

        this.radioStation = radioStation;
        
        // MediaPlayer.Source = radioStation.URL;
        RadioName.Text = radioStation.Name;
        RadioImage.Source = radioStation.IconPath;

        // Initialize the currentLevels array (starting with zeros)
        currentLevels = new float[barCount];
        for (int i = 0; i < barCount; i++)
        {
            currentLevels[i] = 0f;
        }

        // Start a timer to update audio levels every 200ms
        timer = new System.Timers.Timer(60);
        timer.Elapsed += Timer_Elapsed;
        timer.Start();

        FirstColor.Color = radioStation.LightColor.Color;
        SecondColor.Color = radioStation.DarkColor.Color;
        MediaPlayer.MediaFailed += async (s,e) =>
        {
            string errorMessage = $"Faild to load media: {e.ErrorMessage} {radioStation.Name}";

            // Optional: Show a toast notification (for a small popup)
            await Toast.Make(errorMessage, CommunityToolkit.Maui.Core.ToastDuration.Long).Show();
        };
    }

    private void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {

        // Slide the audio data: shift currentLevels left by one index
        Array.Copy(currentLevels, 1, currentLevels, 0, barCount - 1);
        // Generate a new random level for the rightmost bar
        currentLevels[barCount - 1] = (float)random.NextDouble();


        // Update the visualizer on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AudioVisualizer.UpdateAudioLevels(currentLevels);
        });
    }

    private void MediaAction_Clicked(object sender, EventArgs e)
    {
        if (isPlaying)
        {
            MediaPlayer.Pause();
            MediaActionButton.Source = "play_icon.png";
        }
        else
        {
            MediaPlayer.Play();
            MediaActionButton.Source = "pause_icon.png";
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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        MediaPlayer.Source = MediaSource.FromUri(radioStation.URL);
        MediaPlayer.Play();
        isPlaying = true;

        isAnimating = true;
        AnimateGradient();
    }

    protected override void OnDisappearing()
    {
        MediaPlayer.Stop();
        isPlaying = false;
        isAnimating = false;
        base.OnDisappearing();

    }

    private async void AnimateGradient()
    {
        while (isAnimating)
        {
            await AnimateOffset(0, 0.3, 3000, Easing.SinInOut); // Smooth forward animation
            await AnimateOffset(0.3, 0, 3000, Easing.SinInOut); // Smooth backward animation
        }
    }

    private async Task AnimateOffset(double start, double end, int duration, Easing easing)
    {
        double timeElapsed = 0;
        int frameRate = 60; // 60 FPS for smoothness
        double frameTime = 1000.0 / frameRate; // Milliseconds per frame

        while (timeElapsed < duration)
        {
            if (!isAnimating) return;

            double progress = timeElapsed / duration; // Normalize progress (0 to 1)
            double easedProgress = easing.Ease(progress); // Apply easing
            double newOffset = Lerp(start, end, easedProgress); // Interpolate

            FirstColor.Offset = (float)newOffset;
            SecondColor.Offset = (float)(1 - newOffset); // Reverse for smooth effect

            await Task.Delay((int)frameTime);
            timeElapsed += frameTime; // Increment time
        }

        // Ensure it finishes exactly at the end
        FirstColor.Offset = (float)end;
        SecondColor.Offset = (float)(1 - end);
    }

    private double Lerp(double start, double end, double t)
    {
        return start + (end - start) * t;
    }
}

