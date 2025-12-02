using CommunityToolkit.Maui.Views;
using System.Threading.Tasks;

namespace mindvault.Controls
{
    public partial class InfoModal : CommunityToolkit.Maui.Views.Popup
    {
        public string TitleText { get; private set; }
        public string MessageText { get; private set; }
        public string LeftButtonText { get; private set; }
        public string? RightButtonText { get; private set; }

        public bool IsSingleButton { get; private set; }
        public bool IsTwoButtons => !IsSingleButton;

        private readonly TaskCompletionSource<bool> _tcs = new();
        public Task<bool> CompletionTask => _tcs.Task;

        public InfoModal(string title, string message, string primaryText, string? secondaryText = null)
        {
            InitializeComponent();

            TitleText = title;
            MessageText = message;
            IsSingleButton = string.IsNullOrWhiteSpace(secondaryText);

            if (IsSingleButton)
            {
                LeftButtonText = primaryText;
                RightButtonText = null;
            }
            else
            {
                // For two buttons: left is secondary (Cancel), right is primary (Install/OK)
                LeftButtonText = secondaryText!;
                RightButtonText = primaryText;
            }

            BindingContext = this;

            // Set platform-specific margins (Android only)
#if ANDROID
            ModalBorder.Margin = new Thickness(40, 0, 40, 0);
#endif

            // Button color logic:
            // - Single button mode: always blue
            // - Two button mode: check button text for color
            LeftButton.BackgroundColor = Color.FromArgb("#0D6EFD"); // Blue for single button
            
            if (!IsSingleButton)
            {
                // Check if secondary button is "Cancel" for red color, otherwise blue
                bool isCancel = secondaryText?.Equals("Cancel", StringComparison.OrdinalIgnoreCase) == true;
                LeftButtonTwo.BackgroundColor = isCancel 
                    ? Color.FromArgb("#DC3545")  // Red for Cancel
                    : Color.FromArgb("#0D6EFD"); // Blue for other secondary actions
                
                // Check if primary button contains "Password" for red color
                bool isPasswordAction = primaryText?.Contains("Password", StringComparison.OrdinalIgnoreCase) == true;
                RightButtonTwo.BackgroundColor = isPasswordAction
                    ? Color.FromArgb("#DC3545")  // Red for "Add Password"
                    : Color.FromArgb("#0D6EFD"); // Blue for other actions
            }

            // Setup keyboard handler for Enter key
            SetupKeyboardHandler();
        }

        private void OnLeftClicked(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[InfoModal] Left button clicked, IsSingleButton={IsSingleButton}");
            // In single button mode: return true (OK clicked)
            // In two-button mode: left is Cancel, return false
            if (IsSingleButton)
            {
                try { _tcs.TrySetResult(true); } catch { }
                System.Diagnostics.Debug.WriteLine($"[InfoModal] Closing with result: true");
                Close(true); // Single "OK" button
            }
            else
            {
                try { _tcs.TrySetResult(false); } catch { }
                System.Diagnostics.Debug.WriteLine($"[InfoModal] Closing with result: false (Cancel)");
                Close(false); // "Cancel" button in two-button mode
            }
        }

        private void OnRightClicked(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[InfoModal] Right button clicked (primary action)");
            // Right button is always the primary action (Install/OK) in two-button mode
            try { _tcs.TrySetResult(true); } catch { }
            System.Diagnostics.Debug.WriteLine($"[InfoModal] Closing with result: true");
            Close(true);
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
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                // Trigger the primary action button (right button in two-button mode, left in single-button mode)
                if (!IsSingleButton)
                {
                    OnRightClicked(null, EventArgs.Empty);
                }
                else
                {
                    OnLeftClicked(null, EventArgs.Empty);
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                // Escape always cancels (returns false)
                try { _tcs.TrySetResult(false); } catch { }
                Close(false);
            }
        }
#else
        private void SetupKeyboardHandler()
        {
            // Keyboard handling only implemented for Windows
        }
#endif
    }
}
