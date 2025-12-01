using CommunityToolkit.Maui.Views;

namespace mindvault.Controls
{
    public partial class AppModal : CommunityToolkit.Maui.Views.Popup
    {
        public string TitleText { get; private set; }
        public string MessageText { get; private set; }
        public string LeftButtonText { get; private set; }
        public string? RightButtonText { get; private set; }

        public bool IsSingleButton { get; private set; }
        public bool IsTwoButtons => !IsSingleButton;
        public bool IsLeftDelete { get; private set; }
        public bool IsRightDelete { get; private set; }
        private bool _primaryIsDelete;
        private bool _primaryReturnsTrue;

        public AppModal(string title, string message, string primaryText, string? secondaryText = null)
        {
            InitializeComponent();

            TitleText = title;
            MessageText = message;

            IsSingleButton = string.IsNullOrWhiteSpace(secondaryText);
            _primaryIsDelete = primaryText?.Contains("Delete", StringComparison.OrdinalIgnoreCase) == true;
            bool primaryIsReset = primaryText?.Equals("Reset", StringComparison.OrdinalIgnoreCase) == true;

            // Check if secondary text is "Cancel" or contains "Cancel"
            bool secondaryIsCancel = secondaryText?.Contains("Cancel", StringComparison.OrdinalIgnoreCase) == true;

            // If primary is delete/reset and we have two buttons, swap positions (delete/reset goes right)
            if (!IsSingleButton && (_primaryIsDelete || primaryIsReset))
            {
                LeftButtonText = secondaryText!;
                RightButtonText = primaryText;
                IsLeftDelete = false;
                IsRightDelete = true;
                _primaryReturnsTrue = true; // Primary (delete/reset) should return true when clicked
            }
            else
            {
                LeftButtonText = primaryText;
                RightButtonText = secondaryText;
                IsLeftDelete = _primaryIsDelete || primaryIsReset;
                IsRightDelete = secondaryText?.Contains("Delete", StringComparison.OrdinalIgnoreCase) == true ||
                               secondaryText?.Equals("Reset", StringComparison.OrdinalIgnoreCase) == true;
                _primaryReturnsTrue = true; // Left button (primary) returns true
            }

            BindingContext = this;

            // Set platform-specific margins (Android only)
#if ANDROID
            ModalBorder.Margin = new Thickness(40, 0, 40, 0);
#endif

            // Set button colors based on action type
            // Cancel = BLUE, Delete/Reset = RED
            
            // Left button color
            if (IsLeftDelete)
            {
                // Left is Delete/Reset - RED
                LeftButton.BackgroundColor = Color.FromArgb("#DC3545");
                LeftButtonTwo.BackgroundColor = Color.FromArgb("#DC3545");
            }
            else
            {
                // Left is Cancel or normal button - BLUE
                LeftButton.BackgroundColor = Color.FromArgb("#0D6EFD");
                LeftButtonTwo.BackgroundColor = Color.FromArgb("#0D6EFD");
            }

            // Right button color
            if (IsRightDelete || primaryIsReset)
            {
                // Right is Delete/Reset - RED
                RightButtonTwo.BackgroundColor = Color.FromArgb("#DC3545");
            }
            else
            {
                // Right is Cancel or normal button - BLUE
                RightButtonTwo.BackgroundColor = Color.FromArgb("#0D6EFD");
            }

            // Setup keyboard handler for Enter key
            SetupKeyboardHandler();
        }

        private void OnLeftClicked(object? sender, EventArgs e)
        {
            // When buttons are swapped (Delete/Reset on right):
            //   - Left button is Cancel (secondary) - returns false
            // When buttons are NOT swapped:
            //   - Left button is primary - returns true
            
            bool primaryIsReset = RightButtonText?.Equals("Reset", StringComparison.OrdinalIgnoreCase) == true;
            
            if (!IsSingleButton && (_primaryIsDelete || primaryIsReset))
            {
                // Buttons are swapped, left is Cancel
                Close(false);
            }
            else
            {
                // Left is primary
                Close(_primaryReturnsTrue);
            }
        }

        private void OnRightClicked(object? sender, EventArgs e)
        {
            // When buttons are swapped (Delete/Reset on right):
            //   - Right button is Delete/Reset (primary) - returns true
            // When buttons are NOT swapped:
            //   - Right button is secondary - returns false
            
            bool primaryIsReset = RightButtonText?.Equals("Reset", StringComparison.OrdinalIgnoreCase) == true;
            
            if (!IsSingleButton && (_primaryIsDelete || primaryIsReset))
            {
                // Buttons are swapped, right is Delete/Reset
                Close(true);
            }
            else
            {
                // Right is secondary
                Close(false);
            }
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
                // Trigger the primary action button (right button for delete/reset, left for others)
                bool primaryIsReset = RightButtonText?.Equals("Reset", StringComparison.OrdinalIgnoreCase) == true;
                
                if (!IsSingleButton && (_primaryIsDelete || primaryIsReset || IsRightDelete))
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
