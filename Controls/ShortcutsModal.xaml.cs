using CommunityToolkit.Maui.Views;

namespace mindvault.Controls;

public partial class ShortcutsModal : Popup
{
    public ShortcutsModal()
    {
        InitializeComponent();

        // Set platform-specific margins (Android only)
#if ANDROID
        if (ModalBorder is not null)
            ModalBorder.Margin = new Thickness(40, 0, 40, 0);
#endif

        // Setup keyboard handler for Enter key
        SetupKeyboardHandler();
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        Close();
    }

#if WINDOWS
    private void SetupKeyboardHandler()
    {
        // Hook keyboard events when popup is shown
        this.Opened += (s, e) =>
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window &&
                window.Content is Microsoft.UI.Xaml.UIElement content)
            {
                content.KeyDown += OnWindowsKeyDown;
            }
        };

        this.Closed += (s, e) =>
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window &&
                window.Content is Microsoft.UI.Xaml.UIElement content)
            {
                content.KeyDown -= OnWindowsKeyDown;
            }
        };
    }

    private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            // Both Enter and Escape close the shortcuts modal
            OnCloseClicked(null, EventArgs.Empty);
        }
    }
#else
    private void SetupKeyboardHandler()
    {
        // Keyboard handling only implemented for Windows
    }
#endif
}

