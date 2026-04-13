using Velopack;

namespace DiktaMe.App;

/// <summary>
/// Custom entry point required by Velopack. Replaces the XAML-generated Main()
/// (suppressed via DISABLE_XAML_GENERATED_MAIN in csproj).
/// VelopackApp.Build().Run() MUST be the first call — it handles install,
/// uninstall, and update lifecycle hooks via command-line arguments
/// (--veloapp-install, --veloapp-updated, --veloapp-uninstall).
/// For normal app launches, it returns immediately.
/// </summary>
public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Velopack lifecycle hooks — must run before ANY other initialization.
        VelopackApp.Build().Run();

        // Standard WinUI 3 startup (from auto-generated App.g.i.cs)
        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
