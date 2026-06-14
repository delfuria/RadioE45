using Foundation;
using Microsoft.Maui;

namespace RadioE45;

// Subclassing MauiUISceneDelegate lets MAUI own the entire scene→window wiring:
// WillConnect creates UIWindow(windowScene) and attaches the MAUI rendering pipeline.
// The [Register] name must match UISceneDelegateClassName in Info.plist exactly.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
