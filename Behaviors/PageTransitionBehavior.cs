using Microsoft.Maui.Controls;

namespace mindvault.Behaviors;

public class PageTransitionBehavior : Behavior<ContentPage>
{
    public static readonly BindableProperty AnimationTypeProperty =
        BindableProperty.Create(
            nameof(AnimationType),
            typeof(PageAnimationType),
            typeof(PageTransitionBehavior),
            PageAnimationType.FadeSlideUp);

    public static readonly BindableProperty DurationProperty =
        BindableProperty.Create(
            nameof(Duration),
            typeof(uint),
            typeof(PageTransitionBehavior),
            (uint)300);

    public PageAnimationType AnimationType
    {
        get => (PageAnimationType)GetValue(AnimationTypeProperty);
        set => SetValue(AnimationTypeProperty, value);
    }

    public uint Duration
    {
        get => (uint)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    private ContentPage? _associatedPage;

    protected override void OnAttachedTo(ContentPage bindable)
    {
        base.OnAttachedTo(bindable);
        _associatedPage = bindable;
        _associatedPage.Appearing += OnPageAppearing;
    }

    protected override void OnDetachingFrom(ContentPage bindable)
    {
        base.OnDetachingFrom(bindable);
        if (_associatedPage != null)
        {
            _associatedPage.Appearing -= OnPageAppearing;
            _associatedPage = null;
        }
    }

    private async void OnPageAppearing(object? sender, EventArgs e)
    {
        if (_associatedPage == null)
            return;

        await AnimatePageAsync(_associatedPage);
    }

    private async Task AnimatePageAsync(ContentPage page)
    {
        switch (AnimationType)
        {
            case PageAnimationType.Fade:
                await AnimateFadeAsync(page);
                break;

            case PageAnimationType.SlideUp:
                await AnimateSlideUpAsync(page);
                break;

            case PageAnimationType.SlideRight:
                await AnimateSlideRightAsync(page);
                break;

            case PageAnimationType.FadeSlideUp:
                await AnimateFadeSlideUpAsync(page);
                break;

            case PageAnimationType.None:
            default:
                // No animation
                break;
        }
    }

    private async Task AnimateFadeAsync(VisualElement element)
    {
        element.Opacity = 0;
        await element.FadeTo(1, Duration, Easing.CubicOut);
    }

    private async Task AnimateSlideUpAsync(VisualElement element)
    {
        element.TranslationY = 50;
        await element.TranslateTo(0, 0, Duration, Easing.CubicOut);
    }

    private async Task AnimateSlideRightAsync(VisualElement element)
    {
        element.TranslationX = -50;
        await element.TranslateTo(0, 0, Duration, Easing.CubicOut);
    }

    private async Task AnimateFadeSlideUpAsync(VisualElement element)
    {
        element.Opacity = 0;
        element.TranslationY = 30;

        var fadeTask = element.FadeTo(1, Duration, Easing.CubicOut);
        var slideTask = element.TranslateTo(0, 0, Duration, Easing.CubicOut);

        await Task.WhenAll(fadeTask, slideTask);
    }
}
