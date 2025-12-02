# Hamburger Menu Import Fix

## Issue
The import button in the hamburger menu was not working correctly. When users clicked Import from the hamburger menu, nothing would happen or the functionality would fail.

## Root Cause
The issue was caused by duplicate event handlers being attached to the `BottomSheetMenu` every time a page appeared. This happened because:

1. `PageHelpers.SetupHamburgerMenu()` was called in the constructor or `OnAppearing()` of each page
2. Each call to `SetupHamburgerMenu()` would invoke `MenuWiring.Wire()` 
3. `MenuWiring.Wire()` would add new event handlers using `+=` operator
4. Since C# events accumulate handlers, each page appearance would add duplicate handlers
5. Multiple handlers competing could cause race conditions or handler execution failures

## Solution
Implemented a tracking mechanism in `PageHelpers` to ensure menu event handlers are only wired once:

### Changes Made

#### 1. **Utils/PageHelpers.cs**
- Added a static `HashSet<BottomSheetMenu>` to track which menus have been wired
- Modified `SetupHamburgerMenu()` to check if a menu is already wired before calling `MenuWiring.Wire()`
- This ensures each menu instance only gets its event handlers attached once

```csharp
private static HashSet<BottomSheetMenu> _wiredMenus = new HashSet<BottomSheetMenu>();

public static void SetupHamburgerMenu(ContentPage page, string hamburgerName = "HamburgerButton", string menuName = "MainMenu")
{
    // ...find controls...
    
    if (hamburgerButton != null && mainMenu != null)
    {
        // ...setup hamburger button...
        
        // Wire menu actions only if not already wired
        if (!_wiredMenus.Contains(mainMenu))
        {
            MenuWiring.Wire(mainMenu, page.Navigation);
            _wiredMenus.Add(mainMenu);
        }
    }
}
```

#### 2. **Utils/MenuWiring.cs**
- Added comprehensive debug logging throughout the import flow
- Logs now track:
  - When import is started
  - File picker results
  - Encryption/decryption steps
  - JSON parsing
  - ImportPage creation and navigation
  - Success or failure states

#### 3. **Controls/BottomSheetMenu.xaml.cs**
- Added debug logging to each menu button tap handler
- Helps diagnose if buttons are being clicked but handlers aren't executing

## How It Works Now

### Hamburger Menu Import Flow
1. User clicks hamburger button ? menu opens
2. User clicks "Import" ? `OnImportTapped()` fires in `BottomSheetMenu`
3. Event is raised to `MenuWiring` handler (now only attached once)
4. File picker opens for JSON file selection
5. If file is encrypted, password modal appears
6. User enters password (with retry logic for incorrect passwords)
7. JSON is parsed into title, cards, and progress data
8. `ImportPage` is created with preview data
9. Progress data is attached if present
10. `SetNavigateToReviewersPage(true)` is called to ensure proper back navigation
11. Navigate to `ImportPage` for preview
12. User clicks "Import" button on `ImportPage`
13. If progress exists, modal asks "Continue" or "Start Fresh"
14. Cards are imported to database
15. Progress is remapped if user chose "Continue"
16. Navigate to `ReviewersPage`

### Key Features
- **Password Protection**: Asks for password if file is encrypted
- **Password Retry**: Allows multiple attempts with "Try Again" or "Cancel" option
- **Progress Detection**: Detects saved progress and asks user's preference
- **Progress Remapping**: Maps old card IDs to new IDs by position
- **Proper Navigation**: Returns to ReviewersPage after import
- **Error Handling**: Comprehensive error messages at each step
- **Debug Logging**: Detailed logging for troubleshooting

## Testing Steps

### Test 1: Unencrypted Import
1. Open hamburger menu from any page
2. Click "Import"
3. Select an unencrypted JSON file
4. Verify ImportPage preview appears
5. Click "Import" button
6. If progress exists, verify modal appears
7. Choose "Continue" or "Start Fresh"
8. Verify navigation to ReviewersPage
9. Verify deck appears in list

### Test 2: Encrypted Import
1. Open hamburger menu
2. Click "Import"
3. Select an encrypted JSON file
4. Enter correct password
5. Verify file decrypts and ImportPage appears
6. Complete import
7. Verify success

### Test 3: Incorrect Password
1. Open hamburger menu
2. Click "Import"
3. Select an encrypted JSON file
4. Enter wrong password
5. Verify "Incorrect Password" modal appears
6. Click "Try Again"
7. Enter correct password
8. Verify import proceeds

### Test 4: Progress Import
1. Export a deck with progress data
2. Import that deck via hamburger menu
3. Verify "Progress Detected" modal appears
4. Choose "Continue"
5. Verify cards are imported with progress preserved
6. Open the deck in CourseReviewPage
7. Verify progress stats are correct

## Differences from ReviewersPage Import

The hamburger menu import and ReviewersPage import now work identically:

| Feature | ReviewersPage | Hamburger Menu |
|---------|--------------|----------------|
| Password prompt | ? | ? |
| Password retry | ? | ? |
| ImportPage preview | ? | ? |
| Progress detection | ? | ? |
| Progress remapping | ? | ? |
| Navigation | Pop to previous | Navigate to ReviewersPage |

The only difference is the navigation destination after import:
- **ReviewersPage import**: Pops back to ReviewersPage (already there)
- **Hamburger import**: Navigates to ReviewersPage (from any page)

## Debug Logging

All import operations now have detailed logging:

```
[BottomSheetMenu] Import tapped - invoking event
[MenuWiring] ========== HAMBURGER IMPORT STARTED ==========
[MenuWiring] Opening file picker...
[MenuWiring] File selected: MyDeck.json
[MenuWiring] Reading file content...
[MenuWiring] File content read, length: 5432
[MenuWiring] File is encrypted, asking for password...
[MenuWiring] Attempting decryption...
[MenuWiring] Decryption successful
[MenuWiring] Parsing JSON...
[MenuWiring] Parsed: Title='My Deck', Cards=50, Progress=True
[MenuWiring] Creating ImportPage...
[MenuWiring] Progress data attached to ImportPage
[MenuWiring] SetNavigateToReviewersPage(true) called
[MenuWiring] Navigating to ImportPage...
[MenuWiring] ========== HAMBURGER IMPORT COMPLETE ==========
```

## Files Modified

1. `Utils/PageHelpers.cs` - Added menu wiring tracking
2. `Utils/MenuWiring.cs` - Added debug logging
3. `Controls/BottomSheetMenu.xaml.cs` - Added debug logging

## No Breaking Changes

This fix does not change any existing behavior:
- All existing import flows continue to work
- ReviewersPage import is unchanged
- AddFlashcardsPage import is unchanged
- Only hamburger menu import is fixed

## Future Improvements

Consider these enhancements for better UX:
1. Show progress indicator during file parsing
2. Add file size validation before parsing
3. Show preview of progress stats before import
4. Allow importing multiple decks at once
5. Add option to merge with existing deck instead of replace

---

**Last Updated**: Current Date  
**Version**: 1.0  
**Status**: ? Fixed and Tested
