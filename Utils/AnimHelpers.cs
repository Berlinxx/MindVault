using Microsoft.Maui.Controls;

namespace mindvault.Utils;

public static class AnimHelpers
{
    public static async Task SlideFadeInAsync(VisualElement? target,
        double fromX = 40,
        uint fadeMs = 160,
        uint slideMs = 200)
    {
        if (target == null) return;
        try
        {
            target.Opacity = 0;
            target.TranslationX = fromX;
            var fadeTask = target.FadeTo(1, fadeMs, Easing.CubicIn);
            var slideTask = target.TranslateTo(0, 0, slideMs, Easing.CubicOut);
            await Task.WhenAll(fadeTask, slideTask);
        }
        catch { }
    }

    public static async Task SlideFadeOutAsync(VisualElement? target,
        double toX = -40,
        uint fadeMs = 140,
        uint slideMs = 180)
    {
        if (target == null) return;
        try
        {
            var fadeTask = target.FadeTo(0, fadeMs, Easing.CubicOut);
            var slideTask = target.TranslateTo(toX, 0, slideMs, Easing.CubicIn);
            await Task.WhenAll(fadeTask, slideTask);
        }
        catch { }
    }
}
