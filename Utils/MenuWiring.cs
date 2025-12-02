using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using mindvault.Pages;
using mindvault.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace mindvault.Utils;

public static class MenuWiring
{
    public static void Wire(BottomSheetMenu menu, INavigation initialNav, ContentPage ownerPage)
    {
        // Store owner page for reliable popup showing
        ContentPage? storedOwnerPage = ownerPage;
        
        // Helper to get current page + navigation each time (avoid stale nav)
        (ContentPage? page, INavigation? nav) GetCurrent()
        {
            // Prefer the stored owner page (the page that owns this menu)
            if (storedOwnerPage != null)
                return (storedOwnerPage, storedOwnerPage.Navigation);
            
            // Fallback to dynamic resolution
            ContentPage? page = null;
            if (Shell.Current?.CurrentPage is ContentPage shellPage) page = shellPage;
            else if (Application.Current?.Windows?.FirstOrDefault()?.Page is ContentPage mainPage) page = mainPage;
            else if (Application.Current?.Windows?.FirstOrDefault()?.Page is NavigationPage navPage && navPage.CurrentPage is ContentPage currentPage) page = currentPage;
            var nav = page?.Navigation ?? initialNav;
            return (page, nav);
        }

        // Header tap -> Home
        menu.HeaderTapped += async (_, __) =>
        {
            var (_, nav) = GetCurrent();
            if (Shell.Current is not null)
                await Navigator.GoToAsync($"///{nameof(HomePage)}");
            else if (nav != null)
                await Navigator.PopToRootAsync(nav);
        };

        menu.CreateTapped += async (_, __) =>
        {
            var (_, nav) = GetCurrent();
            System.Diagnostics.Debug.WriteLine($"[MenuWiring] Create tapped - navigating to TitleReviewerPage");
            if (Shell.Current is not null)
                await Navigator.GoToAsync($"///{nameof(TitleReviewerPage)}");
            else if (nav != null)
                await Navigator.PushAsync(new TitleReviewerPage(), nav);
        };

        menu.BrowseTapped += async (_, __) =>
        {
            var (_, nav) = GetCurrent();
            System.Diagnostics.Debug.WriteLine($"[MenuWiring] Browse tapped - navigating to ReviewersPage");
            if (Shell.Current is not null)
                await Navigator.GoToAsync($"///{nameof(ReviewersPage)}");
            else if (nav != null)
                await Navigator.PushAsync(new ReviewersPage(), nav);
        };

        menu.MultiplayerTapped += async (_, __) =>
        {
            var (_, nav) = GetCurrent();
            System.Diagnostics.Debug.WriteLine($"[MenuWiring] Multiplayer tapped - navigating to MultiplayerPage");
            if (Shell.Current is not null)
                await Navigator.GoToAsync(nameof(MultiplayerPage));
            else if (nav != null)
                await Navigator.PushAsync(new MultiplayerPage(), nav);
        };

        menu.ImportTapped += async (_, __) =>
        {
            var (currentPage, nav) = GetCurrent();
            if (currentPage == null || nav == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuWiring] Could not get current page/nav");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MenuWiring] Import tapped, calling ReviewersPage.PerformImportAsync");
            
            // Call the proven ReviewersPage import logic directly
            await ReviewersPage.PerformImportAsync(currentPage, nav);
        };

        menu.SettingsTapped += async (_, __) =>
        {
            var (_, nav) = GetCurrent();
            System.Diagnostics.Debug.WriteLine($"[MenuWiring] Settings tapped - navigating to ProfileSettingsPage");
            if (Shell.Current is not null)
                await Navigator.GoToAsync(nameof(ProfileSettingsPage));
            else if (nav != null)
                await Navigator.PushAsync(new ProfileSettingsPage(), nav);
        };
        
        System.Diagnostics.Debug.WriteLine($"[MenuWiring] All menu handlers wired successfully");
    }
}
