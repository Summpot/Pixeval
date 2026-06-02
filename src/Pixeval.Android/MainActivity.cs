using Android.App;
using Android.Content;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Pixeval.AppManagement;

namespace Pixeval.Android;

[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = PixivLoginActivationHub.CallbackScheme,
    DataHost = PixivLoginActivationHub.CallbackHost,
    DataPath = PixivLoginActivationHub.CallbackPath)]
[Activity(
    Label = $"{nameof(Pixeval)}.{nameof(Android)}",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
