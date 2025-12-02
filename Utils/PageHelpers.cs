using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using mindvault.Controls;
using mindvault.Utils;

namespace mindvault.Utils;

public static class PageHelpers
{
    /// <summary>
    /// Setup hamburger menu functionality for any page
    /// </summary>
    public static void SetupHamburgerMenu(ContentPage page, string hamburgerName = "HamburgerButton", string menuName = "MainMenu")
    {
        // Find hamburger button and main menu
        var hamburgerButton = page.FindByName<HamburgerButton>(hamburgerName) ?? 
                             page.FindByName<HamburgerButton>("Burger");
        
        var mainMenu = page.FindByName<BottomSheetMenu>(menuName);

        if (hamburgerButton != null && mainMenu != null)
        {
            // Remove previous handlers to avoid multiple subscriptions when pages re-appear
            hamburgerButton.Clicked -= async (_, __) => await mainMenu.ShowAsync();
            hamburgerButton.Clicked += async (_, __) => await mainMenu.ShowAsync();

            // Wire menu actions → navigation
            MenuWiring.Wire(mainMenu, page.Navigation);
        }
    }

    /// <summary>
    /// Safe navigation method that prevents crashes
    /// </summary>
    public static async Task SafeNavigateAsync(ContentPage page, Func<Task> navigationAction, string fallbackMessage = "Navigation failed")
    {
        try
        {
            await navigationAction();
        }
        catch (Exception ex)
        {
            page.ShowPopup(new AppModal("Navigation Error", fallbackMessage, "OK"));
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Safe display alert that prevents crashes
    /// </summary>
    public static async Task SafeDisplayAlertAsync(ContentPage page, string title, string message, string cancel = "OK")
    {
        try
        {
            await page.ShowPopupAsync(new AppModal(title, message, cancel));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Display alert error: {ex.Message}");
        }
    }

    /// <summary>
    /// Safe display alert with confirmation (accept/cancel)
    /// </summary>
    public static async Task<bool> SafeDisplayAlertAsync(ContentPage page, string title, string message, string accept, string cancel)
    {
        try
        {
            var result = await page.ShowPopupAsync(new AppModal(title, message, accept, cancel));
            return result is bool b && b;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Display alert error: {ex.Message}");
            return false;
        }
    }
}
