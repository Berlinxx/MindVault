using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace mindvault.Controls
{
    public class PasswordInputModal : Popup
    {
        public string TitleText { get; private set; }
        public string MessageText { get; private set; }
        public string PlaceholderText { get; private set; }

        private Entry _passwordEntry;
        private Border _modalBorder;

        public PasswordInputModal(string title, string message, string placeholder = "Password")
        {
            TitleText = title;
            MessageText = message;
            PlaceholderText = placeholder;

            CanBeDismissedByTappingOutsideOfPopup = false;
            Color = Color.FromArgb("#91FFFFFF");

            BuildUI();
            SetupKeyboardHandler();

            // Focus the password entry when opened
            this.Opened += (s, e) => _passwordEntry?.Focus();
        }

        private void BuildUI()
        {
            // Create password entry with completely transparent background
            _passwordEntry = new Entry
            {
                Placeholder = PlaceholderText,
                IsPassword = true,
                TextColor = Colors.Black,
                PlaceholderColor = Color.FromArgb("#999999"),
                FontSize = 16,
                MaxLength = 50,
                BackgroundColor = Colors.Transparent,
                Margin = new Thickness(0)
            };
            _passwordEntry.SetValue(SemanticProperties.DescriptionProperty, "Password input field");
            _passwordEntry.SetValue(SemanticProperties.HintProperty, MessageText);

#if WINDOWS
            // Remove the default border on Windows
            _passwordEntry.HandlerChanged += (s, e) =>
            {
                if (_passwordEntry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
                {
                    textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    textBox.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            };
#endif

            // Password entry container - just Entry + underline, no border
            var passwordContainer = new VerticalStackLayout
            {
                Spacing = 0,
                Margin = new Thickness(0, 0, 0, 20),
                Children =
                {
                    _passwordEntry,
                    // Blue underline
                    new BoxView 
                    { 
                        HeightRequest = 2, 
                        BackgroundColor = Color.FromArgb("#0D6EFD"),
                        HorizontalOptions = LayoutOptions.Fill,
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            };

            // Title label
            var titleLabel = new Label
            {
                Text = TitleText,
                TextColor = Colors.Black,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold,
                FontSize = 20,
                Margin = new Thickness(0, 0, 0, 8)
            };
            titleLabel.SetValue(SemanticProperties.HeadingLevelProperty, SemanticHeadingLevel.Level2);
            titleLabel.SetValue(SemanticProperties.DescriptionProperty, TitleText);

            // Message label
            var messageLabel = new Label
            {
                Text = MessageText,
                TextColor = Color.FromArgb("#666666"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            messageLabel.SetValue(SemanticProperties.DescriptionProperty, MessageText);

            // Cancel button
            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#6C757D"),
                TextColor = Colors.White,
                CornerRadius = 12,
                WidthRequest = 120,
                HeightRequest = 45,
                HorizontalOptions = LayoutOptions.Center
            };
            cancelButton.SetValue(SemanticProperties.DescriptionProperty, "Cancel button");
            cancelButton.SetValue(SemanticProperties.HintProperty, "Cancel password entry and close dialog");
            cancelButton.Clicked += OnCancelClicked;

            // OK button
            var okButton = new Button
            {
                Text = "OK",
                BackgroundColor = Color.FromArgb("#0D6EFD"),
                TextColor = Colors.White,
                CornerRadius = 12,
                WidthRequest = 120,
                HeightRequest = 45,
                HorizontalOptions = LayoutOptions.Center
            };
            okButton.SetValue(SemanticProperties.DescriptionProperty, "OK button");
            okButton.SetValue(SemanticProperties.HintProperty, "Confirm password entry");
            okButton.Clicked += OnOkClicked;

            // Buttons grid
            var buttonsGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 12,
                HorizontalOptions = LayoutOptions.Center
            };
            buttonsGrid.Add(cancelButton, 0, 0);
            buttonsGrid.Add(okButton, 1, 0);

            // Main grid
            var mainGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };
            mainGrid.Add(titleLabel, 0, 0);
            mainGrid.Add(messageLabel, 0, 1);
            mainGrid.Add(passwordContainer, 0, 2);
            mainGrid.Add(buttonsGrid, 0, 3);

            // Modal border - NO border radius to avoid white corners
            _modalBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#F5FFFFFF"),
                Stroke = Color.FromArgb("#2C71F0"),
                StrokeThickness = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Padding = new Thickness(24),
                MinimumWidthRequest = 420,
                MaximumWidthRequest = 520,
                StrokeShape = new RoundRectangle { CornerRadius = 0 }, // Changed from 16 to 0
                Content = mainGrid
            };

            // Set platform-specific margins (Android only)
#if ANDROID
            _modalBorder.Margin = new Thickness(40, 0, 40, 0);
#endif

            Content = _modalBorder;
        }

        private void OnOkClicked(object? sender, EventArgs e)
        {
            Close(_passwordEntry?.Text);
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            Close(null);
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
                // Only trigger OK if user explicitly clicks the button
                // Don't auto-submit on Enter to prevent premature password mismatch
                OnOkClicked(null, EventArgs.Empty);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                OnCancelClicked(null, EventArgs.Empty);
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
