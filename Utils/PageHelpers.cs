using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using mindvault.Utils;
using System.Collections.Generic;

namespace mindvault.Utils;

public static class PageHelpers
{
    // Track which menus have been wired to prevent duplicate handlers
    private static HashSet<BottomSheetMenu> _wiredMenus = new HashSet<BottomSheetMenu>();
    
    /// <summary>
    /// Setup hamburger menu functionality for any page
    /// </summary>
    public static void SetupHamburgerMenu(ContentPage page, string hamburgerName = "HamburgerButton", string menuName = "MainMenu")
    {
        System.Diagnostics.Debug.WriteLine($"[PageHelpers] SetupHamburgerMenu called for page: {page.GetType().Name}");
        
        // Find hamburger button and main menu
        var hamburgerButton = page.FindByName<HamburgerButton>(hamburgerName) ?? 
                             page.FindByName<HamburgerButton>("Burger");
        
        var mainMenu = page.FindByName<BottomSheetMenu>(menuName);

        if (hamburgerButton != null && mainMenu != null)
        {
            System.Diagnostics.Debug.WriteLine($"[PageHelpers] Found HamburgerButton and MainMenu");
            
            // Remove previous handlers to avoid multiple subscriptions when pages re-appear
            hamburgerButton.Clicked -= async (_, __) => await mainMenu.ShowAsync();
            hamburgerButton.Clicked += async (_, __) =>
            {
                System.Diagnostics.Debug.WriteLine($"[PageHelpers] Hamburger button clicked on {page.GetType().Name}");
                await mainMenu.ShowAsync();
            };

            // Wire menu actions only if not already wired
            if (!_wiredMenus.Contains(mainMenu))
            {
                System.Diagnostics.Debug.WriteLine($"[PageHelpers] Wiring menu actions for first time...");
                MenuWiring.Wire(mainMenu, page.Navigation, page);  // Pass the page instance!
                _wiredMenus.Add(mainMenu);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PageHelpers] Menu already wired, skipping...");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PageHelpers] WARNING: HamburgerButton={hamburgerButton != null}, MainMenu={mainMenu != null}");
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
