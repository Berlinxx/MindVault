using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using mindvault.Utils; // for AnimHelpers
using Microsoft.Maui.Controls; // needed for ContentPage

namespace mindvault.Services;

public static class Navigator
{
    static readonly SemaphoreSlim _gate = new(1, 1);
    static bool _isBusy;

    static async Task AnimateOutAsync()
    {
        try
        {
            var current = (Shell.Current?.CurrentPage as ContentPage)?.Content as VisualElement;
            if (current != null)
                await AnimHelpers.SlideFadeOutAsync(current);
        }
        catch { }
    }

    static async Task AnimateInAsync()
    {
        try
        {
            var current = (Shell.Current?.CurrentPage as ContentPage)?.Content as VisualElement;
            if (current != null)
                await AnimHelpers.SlideFadeInAsync(current, 30, 140, 180);
        }
        catch { }
    }

    static async Task WithGate(Func<Task> action)
    {
        if (_isBusy) return;
        await _gate.WaitAsync();
        _isBusy = true;
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigation error: {ex}"); }
        finally { _isBusy = false; _gate.Release(); }
    }

    public static Task GoToAsync(string route) => WithGate(async () =>
    {
        await AnimateOutAsync();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync(route);
        });
        await AnimateInAsync();
    });

    public static Task PushAsync(Page page, INavigation nav) => WithGate(async () =>
    {
        await AnimateOutAsync();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await nav.PushAsync(page);
        });
        await AnimateInAsync();
    });

    public static Task PopAsync(INavigation nav) => WithGate(async () =>
    {
        await AnimateOutAsync();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await nav.PopAsync();
        });
        await AnimateInAsync();
    });

    public static Task PopToRootAsync(INavigation nav) => WithGate(async () =>
    {
        await AnimateOutAsync();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await nav.PopToRootAsync();
        });
        await AnimateInAsync();
    });
}


