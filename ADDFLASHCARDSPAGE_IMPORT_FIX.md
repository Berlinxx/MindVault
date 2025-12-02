# AddFlashcardsPage Import Button Update

## Issue
The import button in `AddFlashcardsPage.xaml` was still using the old TXT format and did not support:
- JSON file format (the new standard)
- Password-protected encrypted exports
- Proper decryption workflow with retry logic

This was inconsistent with the working implementations in:
- `ReviewersPage.xaml.cs` (import button)
- `MenuWiring.cs` (hamburger menu import)

## Changes Made

### 1. Updated `Pages/AddFlashcardsPage.xaml.cs`

#### `OnImportPaste` Method
- **Changed file picker to JSON only** (was TXT):
  ```csharp
  var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
  {
      { DevicePlatform.Android, new[] { "application/json" } },
      { DevicePlatform.iOS, new[] { "public.json" } },
      { DevicePlatform.MacCatalyst, new[] { "public.json" } },
      { DevicePlatform.WinUI, new[] { ".json" } },
  });
  ```

- **Added encryption detection and password prompt**:
  ```csharp
  if (mindvault.Services.ExportEncryptionService.IsEncrypted(content))
  {
      // Ask for password using PasswordInputModal
      // Retry loop with incorrect password handling
      // Decrypt using ExportEncryptionService.Decrypt()
  }
  ```

- **Updated to use JSON parser**:
  ```csharp
  var (importedTitle, parsed, progressData) = ParseJsonExport(content);
  ```

- **Enhanced progress data handling**:
  - Supports plain JSON format (new) and base64-encoded format (legacy TXT fallback)
  - Proper error handling for both formats

#### Added `ParseJsonExport` Method
New method to parse JSON export format:
```csharp
static (string Title, List<(string Q, string A)> Cards, string ProgressData) ParseJsonExport(string json)
{
    var export = System.Text.Json.JsonSerializer.Deserialize<mindvault.Models.ReviewerExport>(json);
    // Extract title, cards, and progress data
    // Return tuple with parsed data
}
```

### 2. Updated `Pages/AddFlashcardsPage.xaml`

#### Import Button Text
- **Old**: "Import .txt export file into this deck."
- **New**: "Import .json export file into this deck."

#### Tooltip and Accessibility
- Updated tooltip: "Import flashcards from .json file"
- Updated semantic hint: "Imports flashcards from a JSON export file"

## Implementation Details

### Password Protection Flow
1. User selects JSON file
2. System checks if file is encrypted using `ExportEncryptionService.IsEncrypted()`
3. If encrypted:
   - Shows `PasswordInputModal` with password field
   - User enters password
   - System attempts decryption
   - If incorrect: Shows retry modal with "Try Again" / "Cancel" options
   - If cancelled: Exits import flow
   - Loop continues until correct password or cancellation
4. Parse decrypted JSON content
5. Import cards and optional progress data

### Progress Data Handling
The import now supports both formats:
- **New JSON format**: Progress data stored as plain JSON in the export file
- **Legacy TXT format**: Progress data stored as base64-encoded JSON (fallback)

Auto-detects format and handles accordingly:
```csharp
try
{
    var test = System.Text.Json.JsonSerializer.Deserialize<List<JsonElement>>(progressData);
    progressJson = progressData; // Plain JSON
}
catch
{
    var bytes = Convert.FromBase64String(progressData);
    progressJson = System.Text.Encoding.UTF8.GetString(bytes); // Base64 legacy
}
```

### Error Handling
Comprehensive error handling for:
- Invalid file format
- Empty files
- Decryption failures
- JSON parsing errors
- Database operations
- Progress data import failures

All errors are logged to debug console with context.

## Consistency with Other Pages

The implementation now matches the working patterns in:

### ReviewersPage Import
- Uses same file picker configuration
- Same encryption detection and password flow
- Same JSON parsing logic
- Same progress handling

### MenuWiring Import
- Identical encryption handling
- Same password retry logic
- Same modal usage (PasswordInputModal, InfoModal)

## User Experience Improvements

1. **Security**: Full support for password-protected exports
2. **Consistency**: Same import experience across all pages
3. **Feedback**: Clear error messages and retry options
4. **Accessibility**: Updated semantic descriptions for screen readers
5. **Format Support**: JSON only (modern, standard format)

## Testing Recommendations

1. **Test unencrypted JSON import**:
   - Export a deck without password
   - Import it in AddFlashcardsPage
   - Verify cards are imported correctly

2. **Test encrypted JSON import**:
   - Export a deck with password protection
   - Import it in AddFlashcardsPage
   - Enter correct password ? should import successfully
   - Enter incorrect password ? should show retry option
   - Cancel password entry ? should exit import

3. **Test progress data import**:
   - Export a deck with progress enabled
   - Import it and choose "Continue Progress"
   - Verify progress data is restored correctly

4. **Test error cases**:
   - Try importing TXT file ? should show "Only JSON files supported"
   - Try importing invalid JSON ? should show parsing error
   - Try importing empty file ? should show "File is empty"

## Files Modified

1. `Pages/AddFlashcardsPage.xaml.cs` - Updated import logic
2. `Pages/AddFlashcardsPage.xaml` - Updated UI text

## Migration Notes

This change makes AddFlashcardsPage import button consistent with:
- ReviewersPage import button
- Hamburger menu import option
- ExportPage export format

All import/export functionality now uses JSON format exclusively for new exports, with legacy TXT format support only for backward compatibility in progress data.

## Related Documentation

- `JSON_ONLY_FORMAT_IMPLEMENTATION.md` - Original JSON format implementation
- `OPTIONAL_PASSWORD_PROTECTION_IMPLEMENTATION.md` - Password protection feature
- `PROGRESS_IMPORT_FIX.md` - Progress data import improvements
- `EXPORT_IMPORT_UX_FIX.md` - UX improvements for export/import flow
