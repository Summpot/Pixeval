using Android.App;
using Android.Runtime;
using Avalonia;
using Pixeval.Common;
using Avalonia.Android;
using Pixeval.Utilities;
namespace Pixeval.Android;
[Application]
public class Application : AvaloniaAndroidApplication<App>
{
    protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithPixevalFonts();
}
