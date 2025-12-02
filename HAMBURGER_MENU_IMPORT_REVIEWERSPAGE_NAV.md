# Hamburger Menu Import - Navigate to ImportPage with ReviewersPage Destination

## Summary

Updated the hamburger menu import to show the ImportPage preview first, then navigate to ReviewersPage after the user confirms the import (instead of going back to the previous page).

## User Flow

### Before:
```
Hamburger Menu ? Import
    ?
File Picker (JSON)
    ?
Password (if encrypted)
    ?
Direct import to database
    ?
Success message
    ?
Navigate to ReviewersPage
```

### After (Current Implementation):
```
Hamburger Menu ? Import
    ?
File Picker (JSON)
    ?
Password (if encrypted, with retry)
    ?
ImportPage (Preview cards)
    ?
User clicks "Import" button
    ?
Progress dialog (Continue/Start Fresh)
    ?
Deck created
    ?
Success message
    ?
Navigate to ReviewersPage ? (not back to previous page)
```

## Changes Made

### 1. File: `Utils/MenuWiring.cs`

#### Updated `menu.ImportTapped` Event Handler

**Key Changes**:
1. **Navigate to ImportPage** for preview (like before)
2. **Set navigation flag** to go to ReviewersPage instead of back
3. **Password retry logic** with "Try Again"/"Cancel" options
4. **JSON parsing** with encryption support

```csharp
// Create ImportPage with preview
var importPage = new ImportPage(title, cards);
if (!string.IsNullOrEmpty(progressData))
{
    importPage.SetProgressData(progressData);
}

// Set flag to navigate to ReviewersPage after import
importPage.SetNavigateToReviewersPage(true);

await Navigator.PushAsync(importPage, nav);
```

### 2. File: `Pages/ImportPage.xaml.cs`

#### Added Navigation Control

**New Fields**:
```csharp
private bool _navigateToReviewersPage = false; // Flag to control navigation destination
```

**New Methods**:
```csharp
public void SetNavigateToReviewersPage(bool navigate)
{
    _navigateToReviewersPage = navigate;
}
```

#### Updated Navigation Logic in `OnImportTapped`

```csharp
if (_navigateToReviewersPage)
{
    // Navigate to ReviewersPage (from hamburger menu)
    Debug.WriteLine($"[ImportPage] Navigating to ReviewersPage");
    await Shell.Current.GoToAsync("///ReviewersPage");
}
else
{
    // Navigate back to previous page (from ReviewersPage)
    Debug.WriteLine($"[ImportPage] Navigating back to previous page");
    await Navigation.PopAsync();
}
```

## Navigation Behavior by Entry Point

| Entry Point | Destination After Import |
|-------------|-------------------------|
| **Hamburger Menu ? Import** | ReviewersPage ? |
| **ReviewersPage ? Import button** | Back to ReviewersPage ? |
| **AddFlashcardsPage ? Import button** | ReviewerEditorPage ? |

## Features

### ? Preview Before Import
- User sees all cards before importing
- Can review deck title and card count
- Visual confirmation of what's being imported

### ? Progress Detection
- Shows "Progress Detected" modal if file contains saved progress
- Options: "Continue" (restore progress) or "Start Fresh" (reset progress)
- Only shown if progress data exists in the export file

### ? Password Protection
- Detects encrypted files automatically
- Shows password input modal
- **Retry loop**: If password is incorrect, asks "Try Again" or "Cancel"
- Decrypts before showing preview

### ? Unique Title Handling
- Automatically appends "(2)", "(3)", etc. if title already exists
- Prevents duplicate deck names
- Shown in success message

### ? Consistent Navigation
- From **hamburger menu**: Always ends at ReviewersPage
- From **ReviewersPage**: Goes back to ReviewersPage
- User always knows where they'll end up

## Technical Implementation

### ImportPage Navigation Flag

The `ImportPage` now accepts a flag that controls where it navigates after a successful import:

```csharp
// In MenuWiring.cs
importPage.SetNavigateToReviewersPage(true);  // Go to ReviewersPage

// In ReviewersPage.cs
// Default behavior - no flag set
// Goes back to previous page (which is ReviewersPage anyway)
```

### Navigation Decision Tree

```
OnImportTapped() called
    ?
Check _navigateToReviewersPage flag
    ?
?????????????????????????????????????
?                                   ?
YES (from hamburger)               NO (from ReviewersPage)
?                                   ?
await Shell.Current.GoToAsync       await Navigation.PopAsync()
    ("///ReviewersPage")                (back to ReviewersPage)
```

### Error Handling

If primary navigation fails, fallback to ReviewersPage:

```csharp
catch (Exception navEx)
{
    // Try alternative navigation - always go to ReviewersPage
    await Shell.Current.GoToAsync("///ReviewersPage");
}
```

## User Experience

### From Hamburger Menu:

1. **Open hamburger menu** (from any page)
2. **Click "Import"**
3. **Select JSON file**
4. **Enter password** if encrypted (with retry)
5. **Review cards** in ImportPage
6. **Click "Import" button**
7. **Choose progress option** if detected
8. **See success message**: "Imported 'Deck Name' with 25 cards"
9. **Land on ReviewersPage** with new deck visible

### From ReviewersPage:

1. **Click import button** on ReviewersPage
2. **Select JSON file**
3. **Enter password** if encrypted
4. **Review cards** in ImportPage
5. **Click "Import" button**
6. **Choose progress option** if detected
7. **See success message**
8. **Return to ReviewersPage** with new deck visible

## Comparison with Other Import Buttons

| Feature | AddFlashcardsPage | ReviewersPage | Hamburger Menu |
|---------|-------------------|---------------|----------------|
| Shows Preview | ? Direct import | ? ImportPage | ? ImportPage |
| Password Retry | ? | ? | ? |
| Progress Import | ? | ? | ? |
| Final Destination | ReviewerEditorPage | ReviewersPage | **ReviewersPage** ? |

## Debug Logging

All navigation decisions are logged:

```
[ImportPage] Navigating to ReviewersPage
[ImportPage] ? Navigation successful
```

Check Visual Studio's **Output window** (Debug pane) to verify navigation flow.

## Testing Checklist

### From Hamburger Menu:

- [ ] Open hamburger menu from **HomePage**
- [ ] Click "Import"
- [ ] Select unencrypted JSON file
- [ ] Verify ImportPage preview appears
- [ ] Click "Import" button
- [ ] Verify navigation goes to **ReviewersPage**
- [ ] Verify deck appears in list

### With Encrypted File:

- [ ] Open hamburger menu
- [ ] Click "Import"
- [ ] Select encrypted JSON file
- [ ] Enter **incorrect** password
- [ ] Verify "Incorrect Password" modal appears with "Try Again"
- [ ] Click "Try Again"
- [ ] Enter **correct** password
- [ ] Verify ImportPage preview appears
- [ ] Import and verify navigation to ReviewersPage

### With Progress Data:

- [ ] Import file with progress data
- [ ] Verify "Progress Detected" modal appears
- [ ] Choose "Continue" option
- [ ] Verify import completes
- [ ] Open deck in CourseReviewPage
- [ ] Verify progress is restored

### From Different Pages:

Test hamburger menu import from:
- [ ] HomePage
- [ ] TitleReviewerPage
- [ ] ReviewerEditorPage
- [ ] CourseReviewPage
- [ ] All should end up on ReviewersPage

## Known Behavior

### Navigation Stack

When importing from hamburger menu:
- The ImportPage is pushed onto the current navigation stack
- After import, Shell navigation is used to go to ReviewersPage
- This resets the navigation stack to HomePage ? ReviewersPage

### Import from ReviewersPage

When importing from ReviewersPage's import button:
- ImportPage is pushed onto stack
- After import, goes back (pops)
- Result: User is on ReviewersPage (where they started)
- Same end result, different navigation method

## Benefits

1. **Preview Before Import**: Users can see what they're importing
2. **Clear Destination**: Always know you'll end up on ReviewersPage
3. **Consistent UX**: Same behavior as ReviewersPage import button
4. **Error Recovery**: Password retry prevents import failures
5. **Progress Preservation**: Can restore SRS progress from exports

## Future Enhancements

1. **Batch Import**: Select multiple files at once
2. **Import History**: Show recently imported decks
3. **Duplicate Detection**: Warn if importing an existing deck
4. **Preview Customization**: Sort/filter cards before importing
5. **Import Options**: Choose specific cards to import

---

**Build Status**: ? **SUCCESSFUL**  
**Navigation**: ? Goes to ReviewersPage after import  
**Preview**: ? Shows ImportPage before confirming  
**Progress**: ? Asks Continue/Start Fresh  
**Last Updated**: $(Get-Date)
