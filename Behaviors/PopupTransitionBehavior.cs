using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace mindvault.Behaviors;

public class PopupTransitionBehavior : Behavior<Popup>
{
    public static readonly BindableProperty AnimationTypeProperty =
        BindableProperty.Create(
            nameof(AnimationType),
            typeof(PopupAnimationType),
            typeof(PopupTransitionBehavior),
            PopupAnimationType.FadeScaleIn);

    public static readonly BindableProperty DurationProperty =
        BindableProperty.Create(
            nameof(Duration),
            typeof(uint),
            typeof(PopupTransitionBehavior),
            (uint)250);

    public PopupAnimationType AnimationType
    {
        get => (PopupAnimationType)GetValue(AnimationTypeProperty);
        set => SetValue(AnimationTypeProperty, value);
    }

    public uint Duration
    {
        get => (uint)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    private Popup? _associatedPopup;
    private View? _popupContent;

    protected override void OnAttachedTo(Popup bindable)
    {
        base.OnAttachedTo(bindable);
        _associatedPopup = bindable;
        _popupContent = bindable.Content;

        if (_popupContent != null)
        {
            // Run animation when popup is loaded
            _popupContent.Loaded += OnPopupLoaded;
        }
    }

    protected override void OnDetachingFrom(Popup bindable)
    {
        base.OnDetachingFrom(bindable);
        
        if (_popupContent != null)
        {
            _popupContent.Loaded -= OnPopupLoaded;
        }

        _associatedPopup = null;
        _popupContent = null;
    }

    private async void OnPopupLoaded(object? sender, EventArgs e)
    {
        if (_popupContent == null)
            return;

        await AnimatePopupAsync(_popupContent);
    }

    private async Task AnimatePopupAsync(View content)
    {
        switch (AnimationType)
        {
            case PopupAnimationType.Fade:
                await AnimateFadeAsync(content);
                break;

            case PopupAnimationType.ScaleIn:
                await AnimateScaleInAsync(content);
                break;

            case PopupAnimationType.FadeScaleIn:
                await AnimateFadeScaleInAsync(content);
                break;

            case PopupAnimationType.None:
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

    private async Task AnimateScaleInAsync(VisualElement element)
    {
        element.Scale = 0.9;
        await element.ScaleTo(1.0, Duration, Easing.CubicOut);
    }

    private async Task AnimateFadeScaleInAsync(VisualElement element)
    {
        element.Opacity = 0;
        element.Scale = 0.9;

        var fadeTask = element.FadeTo(1, Duration, Easing.CubicOut);
        var scaleTask = element.ScaleTo(1.0, Duration, Easing.CubicOut);

        await Task.WhenAll(fadeTask, scaleTask);
    }
}
