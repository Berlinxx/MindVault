# Hamburger Menu Import Update

## Summary

Updated the hamburger menu import button to match the ReviewersPage import functionality. Instead of navigating to the ImportPage preview, it now directly imports the deck and navigates to ReviewersPage.

## Changes Made

### File: `Utils/MenuWiring.cs`

#### Updated `menu.ImportTapped` Event Handler

**Previous Behavior**:
1. File picker ? Select JSON
2. Decrypt if encrypted
3. Parse JSON
4. Navigate to **ImportPage** for preview
5. User clicks "Import" on ImportPage
6. Deck is created and user navigates back

**New Behavior** (matching ReviewersPage):
1. File picker ? Select JSON
2. Decrypt if encrypted (with retry loop)
3. Parse JSON
4. **Ask for progress import preference** (Continue/Start Fresh)
5. **Directly create reviewer in database**
6. **Add flashcards to database**
7. **Import progress data** if user chose to continue
8. **Update preloader cache**
9. Show success message
10. **Navigate directly to ReviewersPage**

#### New Helper Methods Added

1. **`EnsureUniqueTitleAsync(DatabaseService db, string title)`**
   - Ensures the reviewer title is unique
   - Adds "(2)", "(3)", etc. if title already exists
   - Matches ImportPage logic

2. **`ImportProgressData(DatabaseService db, int reviewerId, string progressData)`**
   - Imports and remaps progress data
   - Supports both plain JSON and base64-encoded formats
   - Maps progress by card position (order) instead of ID
   - Preserves SRS state, due dates, ease factors, etc.

## Key Features

### ? Direct Import
- No preview page - deck is created immediately
- Faster workflow for users who trust their exports
- Matches ReviewersPage import experience

### ? Password Retry Logic
- Asks for password if file is encrypted
- Shows "Incorrect Password" modal with retry option
- User can choose "Try Again" or "Cancel"
- Consistent with AddFlashcardsPage and ReviewersPage

### ? Progress Import
- Detects progress data in export file
- Asks user: "Continue from where you left off?" or "Start fresh?"
- Maps progress to new card IDs (order-based mapping)
- Preserves all SRS state (Stage, DueAt, Ease, etc.)

### ? Navigation
- After successful import, navigates to `///ReviewersPage`
- Uses absolute navigation to reset stack
- Ensures user sees their newly imported deck immediately

### ? Error Handling
- Comprehensive try-catch blocks
- User-friendly error messages via `AppModal`
- Debug logging for troubleshooting
- Graceful handling of cancelled operations

## User Flow Comparison

### Before (Old Flow):
```
Hamburger Menu ? Import
    ?
File Picker (JSON)
    ?
Password (if encrypted)
    ?
ImportPage (Preview)
    ?
Click "Import" button
    ?
Progress dialog (Continue/Start Fresh)
    ?
Deck created
    ?
Navigate back to previous page
```

### After (New Flow - Same as ReviewersPage):
```
Hamburger Menu ? Import
    ?
File Picker (JSON)
    ?
Password (if encrypted, with retry)
    ?
Progress dialog (Continue/Start Fresh)
    ?
Deck created directly
    ?
Success message
    ?
Navigate to ReviewersPage ?
```

## Technical Details

### Progress Data Mapping
```csharp
// Maps progress by card POSITION instead of ID
for (int i = 0; i < Math.Min(oldProgress.Count, newCards.Count); i++)
{
    var oldCard = oldProgress[i];  // Old progress data
    var newCard = newCards[i];     // New card with new ID
    
    var progressEntry = new
    {
        Id = newCard.Id,  // ? NEW card ID
        Stage = oldCard.GetProperty("Stage").GetString(),  // ? OLD progress data
        DueAt = oldCard.GetProperty("DueAt").GetDateTime(),
        // ... all other SRS fields from old data
    };
}
```

This works because:
- Cards are exported and imported in the same order
- Card order is preserved during import
- Progress is matched by position, not by ID

### Database Operations
```csharp
// 1. Create reviewer
var reviewer = new Data.Reviewer { Title = finalTitle, CreatedUtc = DateTime.UtcNow };
await db.AddReviewerAsync(reviewer);

// 2. Add flashcards
foreach (var c in cards)
{
    var flashcard = new Data.Flashcard
    {
        ReviewerId = reviewer.Id,
        Question = c.Q,
        Answer = c.A,
        Learned = false,
        Order = order++
    };
    await db.AddFlashcardAsync(flashcard);
    addedCards.Add(flashcard);
}

// 3. Update cache
var preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();
preloader.Decks[reviewer.Id] = addedCards;

// 4. Save progress to Preferences
Preferences.Set($"ReviewState_{reviewerId}", progressJson);
```

## Consistency Achieved

All three import entry points now work identically:

| Feature | AddFlashcardsPage | ReviewersPage | Hamburger Menu |
|---------|-------------------|---------------|----------------|
| File Format | JSON only | JSON only | JSON only ? |
| Encryption Support | ? | ? | ? |
| Password Retry | ? | ? | ? |
| Progress Import | ? | ? | ? |
| Direct Import | ? | ? | ? |
| Final Navigation | ReviewerEditorPage | ReviewersPage | **ReviewersPage** ? |

## Testing Checklist

### Basic Import
- [ ] Open hamburger menu
- [ ] Click "Import"
- [ ] Select unencrypted JSON file
- [ ] Verify deck appears in ReviewersPage
- [ ] Check card count is correct

### Encrypted Import
- [ ] Select encrypted JSON file
- [ ] Enter **incorrect** password
- [ ] Verify "Incorrect Password" modal appears
- [ ] Click "Try Again"
- [ ] Enter **correct** password
- [ ] Verify deck is imported successfully

### Progress Import
- [ ] Select JSON file with progress data
- [ ] Verify "Progress Detected" modal appears
- [ ] Choose "Continue" option
- [ ] Open the deck in CourseReviewPage
- [ ] Verify progress is restored (cards are at correct stages)

### Navigation
- [ ] Import deck from hamburger menu
- [ ] Verify navigation goes to **ReviewersPage**
- [ ] Verify deck appears immediately in the list
- [ ] Verify deck title is correct

### Error Cases
- [ ] Try importing TXT file ? Should show "Only JSON files supported"
- [ ] Cancel file picker ? Should return gracefully
- [ ] Cancel password entry ? Should return gracefully
- [ ] Import empty JSON ? Should show "No cards found"

## Debug Logging

All import operations are logged for troubleshooting:

```csharp
System.Diagnostics.Debug.WriteLine($"[MenuWiring] Created reviewer with ID: {reviewer.Id}");
System.Diagnostics.Debug.WriteLine($"[MenuWiring] Added {addedCards.Count} flashcards");
System.Diagnostics.Debug.WriteLine($"[MenuWiring] Import complete, navigated to ReviewersPage");
```

Check Visual Studio's **Output window** (Debug pane) to verify import operations.

## Benefits

1. **Faster Workflow**: No intermediate preview page
2. **Consistent UX**: All import buttons work the same way
3. **Better Navigation**: Always ends at ReviewersPage (expected location)
4. **Progress Preservation**: Full SRS state is maintained
5. **Error Recovery**: Password retry logic prevents import failures

## Migration Notes

### For Users
- **No breaking changes** - existing exports work the same
- **Faster import** - one less screen to navigate
- **Better destination** - always lands on ReviewersPage

### For Developers
- ImportPage is still available but not used by hamburger menu
- Progress mapping logic is duplicated (could be refactored to shared service)
- All database operations happen in MenuWiring now

## Related Files

- `Utils/MenuWiring.cs` - Updated hamburger menu import
- `Pages/ImportPage.xaml.cs` - Original preview page (still used by other entry points)
- `Pages/ReviewersPage.xaml.cs` - Reference implementation for direct import
- `Pages/AddFlashcardsPage.xaml.cs` - Another import entry point

## Future Enhancements

1. **Refactor Progress Mapping**: Extract to shared service (avoid duplication)
2. **Batch Import**: Allow importing multiple files at once
3. **Import History**: Track recently imported decks
4. **Conflict Resolution**: Better handling of duplicate deck names
5. **Progress Visualization**: Show preview of progress before importing

---

**Build Status**: ? **SUCCESSFUL**  
**Navigation**: ? Goes to ReviewersPage after import  
**Consistency**: ? Matches ReviewersPage import behavior  
**Last Updated**: $(Get-Date)
