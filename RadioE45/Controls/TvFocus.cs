namespace RadioE45.Controls;

/// <summary>
/// Gives the remote's focus to a control. Right after a cold start the Android window is still in
/// touch mode and a plain <c>Focus()</c> is refused: the view is focusable but not
/// <c>focusableInTouchMode</c> (measured on the emulator, docs/ANDROID-TV-ROADMAP.md §3).
/// <c>RequestFocusFromTouch</c> leaves touch mode first, which is what a remote user expects.
/// </summary>
public static class TvFocus
{
    public static bool Request(VisualElement element)
    {
#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View view)
            return view.RequestFocusFromTouch();
#endif
        return element.Focus();
    }
}
