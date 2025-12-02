# Due Cards Count Fix

## Problem
The "due" count on the ReviewersPage was always showing "0 due" even when cards should have been due for review. This happened because:

1. **Hardcoded Zero**: The `Due` property was hardcoded to `0` in `ReviewersPage.xaml.cs`
2. **Missing CooldownUntil**: The `CooldownUntil` field was not being saved/restored in the SRS progress, so cards couldn't be properly checked for readiness
3. **No Due Calculation**: There was no logic to actually check which cards had a `DueAt` time in the past

## Solution

### 1. Added Due Count Calculation (`ReviewersPage.xaml.cs`)
Created a new `CalculateDueCount()` method that:
- Loads the saved SRS progress from Preferences
- Skips cards that are still at `Stage.Avail` (not yet introduced)
- Checks if cards have both `DueAt` and `CooldownUntil` times in the past
- Returns the count of cards that are actually ready for review

### 2. Updated Card Loading Logic
Modified the `OnAppearing()` method to call `CalculateDueCount()` instead of hardcoding `Due = 0`:

```csharp
// Calculate how many cards are currently due
var dueCount = CalculateDueCount(r.Id, cards);

var card = new ReviewerCard
{
    // ...
    Due = dueCount,  // Now calculated properly!
    // ...
};
```

### 3. Fixed SRS Progress Persistence (`SrsEngine.cs`)

**SaveProgress()**: Now saves `CooldownUntil` along with other card properties:
```csharp
var payload = _cards.Select(c => new
{
    c.Id,
    Stage = c.Stage.ToString(),
    c.DueAt,
    c.CooldownUntil,  // ? Added this
    // ...other properties
}).ToList();
```

**RestoreProgress()**: Now restores `CooldownUntil` when loading saved progress:
```csharp
card.CooldownUntil = dto.TryGetProperty("CooldownUntil", out var cd) 
    ? cd.GetDateTime() 
    : card.DueAt;  // Fallback for backward compatibility
```

## How Due Cards Work

A card is considered "due" when:
1. It's not at `Stage.Avail` (i.e., it has been introduced/seen before)
2. Current time >= `DueAt` (the scheduled review time has passed)
3. Current time >= `CooldownUntil` (the short cooldown period has expired)

The cooldown prevents cards from being immediately re-shown after being answered.

## Due Time Behavior

**Important**: Due times are **absolute timestamps** stored as `DateTime.UtcNow`. They continue to count down even when:
- The app is closed
- The device is turned off
- Days or weeks pass

When you reopen the app, the `CalculateDueCount()` method compares the saved `DueAt` times against the current `DateTime.UtcNow`, so cards scheduled for review hours or days ago will correctly show as due.

## Testing
After this fix:
1. Answer some cards correctly ? they'll be scheduled for future review (e.g., +1 day, +3 days, etc.)
2. Close the app and wait (or manually advance your system clock for testing)
3. Reopen the app ? the due count should now reflect cards whose `DueAt` time has passed
4. Cards on cooldown (just answered) won't show as due until both `DueAt` AND `CooldownUntil` are in the past

## Related Files
- `Pages/ReviewersPage.xaml.cs` - Displays the due count
- `Srs/SrsEngine.cs` - Schedules cards and saves/restores progress
- `Srs/SrsCard.cs` - Contains `IsDue` property definition
