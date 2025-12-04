# How to Find and Clear MindVault Database

## ?? Database Location

Your MindVault database is stored at:

**Windows:**
```
%LocalAppData%\Packages\[YourAppPackage]\LocalState\mindvault.db3
```

**Android:**
```
/data/data/com.companyname.mindvault/files/.local/share/mindvault.db3
```

**iOS:**
```
~/Library/Application Support/mindvault.db3
```

## ??? Using Developer Tools (NEW!)

I've added a **Developer Tools** page to help you manage the database and see the onboarding screens.

### How to Access Developer Tools:

**Method 1: Hamburger Menu (EASIEST)** ?
1. **Open the app**
2. **Tap the hamburger menu** (? button in the top-left)
3. Scroll down and tap **"Developer Tools"** (shown in red/italic text)
4. The Developer Tools page will open!

**Method 2: Triple-Tap (Hidden)**
1. **Open the app**
2. **Go to the Reviewers page** (the main page with your decks)
3. **Triple-tap the "REVIEWERS" title** at the top of the page
4. The Developer Tools page will open!

> **Note:** If triple-tap doesn't work, you need to do a full rebuild:
> - Stop debugging (Shift+F5)
> - Clean Solution (Build ? Clean Solution)
> - Rebuild Solution (Build ? Rebuild Solution)
> - Run again (F5)

### What You Can Do:

? **View Database Location**
- See the exact path where your database is stored
- Copy the path to clipboard
- Open the folder in Windows Explorer (Windows only)

? **Check Data Usage**
- See how much space your database is using
- See how much space Python/AI models are using
- Refresh usage info anytime

? **Clear Data Options**
- **Reset Database Only** - Deletes all flashcards/reviewers, keeps settings and Python
- **Reset Python Environment** - Removes Python/AI models, keeps database
- **Reset Settings Only** - Resets all settings to defaults
- **RESET ALL DATA** - Deletes EVERYTHING and shows onboarding screens again! ??

## ?? To See Onboarding Screens:

1. **Open hamburger menu (?)** or **triple-tap "REVIEWERS"**
2. Tap **"Developer Tools"**
3. Scroll down to "Quick Actions"
4. Tap **"??? RESET ALL DATA"** (the red button at the bottom)
5. Confirm twice (it's a destructive action)
6. The app will reset and show onboarding screens!

## ?? Alternative Methods:

### Method 1: Windows File Explorer

1. Press `Win + R` to open Run dialog
2. Type: `%LocalAppData%\Packages`
3. Find the folder starting with `com.companyname.mindvault`
4. Go to `LocalState` folder
5. Delete `mindvault.db3` file
6. Restart the app

### Method 2: Android Settings

1. Go to **Settings ? Apps ? MindVault**
2. Tap **Storage**
3. Tap **Clear Data**
4. Restart the app

### Method 3: Code (for developers)

Use the `AppDataResetService` in your code:

```csharp
var resetService = ServiceHelper.GetRequiredService<AppDataResetService>();

// Option 1: Reset everything (shows onboarding)
var (success, message) = await resetService.ResetAllDataAsync();

// Option 2: Database only
var (success, message) = await resetService.ResetDatabaseOnlyAsync();

// Option 3: Settings only
var (success, message) = resetService.ResetSettingsOnly();

// Option 4: Python/AI environment only
var (success, message) = await resetService.ResetPythonEnvironmentAsync();
```

## ?? Important Notes:

- **RESET ALL DATA** cannot be undone - all your flashcards will be deleted!
- Always export your important decks before resetting
- The database is currently **unencrypted** (stored in plain text)
- After reset, you'll see the onboarding screens and need to set up your profile again

## ?? For Debugging:

Check the Visual Studio Output window for debug messages:
- `[AppDataReset]` - Reset operations
- `[DatabaseService]` - Database operations
- `[DeveloperTools]` - Developer tools actions

## ?? Data Location Summary:

| Item | Windows Location |
|------|------------------|
| Database | `%LocalAppData%\Packages\[App]\LocalState\mindvault.db3` |
| Python | `%LocalAppData%\MindVault\Runtime\` |
| AI Model | `%LocalAppData%\MindVault\Runtime\Models\` |
| Settings | Windows Registry (Preferences API) |
| Exported Files | `%UserProfile%\Downloads\` |

---

**Need Help?** If you can't find the Developer Tools or have issues:
1. Make sure you're on the Reviewers page (not the home page)
2. Triple-tap directly on the text "REVIEWERS" (not the hamburger menu)
3. Check the Visual Studio Output window for any error messages
