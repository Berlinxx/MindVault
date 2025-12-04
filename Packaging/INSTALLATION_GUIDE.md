# MindVault Installation Guide

## Quick Install (3 Easy Steps)

### Step 1: Extract All Files
- Extract the downloaded ZIP file to a folder
- You should see:
  - `MindVault.msix` (the app installer)
  - `MindVault_Certificate.cer` (security certificate)
  - `run_me_first.bat` (installer script)

### Step 2: Install Certificate
1. **Right-click** on `run_me_first.bat`
2. Select **"Run as administrator"**
3. Click **"Yes"** when Windows asks for permission
4. Wait for the success message
5. Press any key to close

### Step 3: Install MindVault
1. **Double-click** `MindVault.msix`
2. Click **"Install"**
3. Wait 10-30 seconds
4. Click **"Launch"** or find MindVault in your Start Menu

## That's it! ??

MindVault is now installed and ready to use.

---

## Troubleshooting

### "Windows protected your PC" message
- This appears because we're using a self-signed certificate
- It's safe to proceed - the app is not harmful
- Solution: Make sure you ran `run_me_first.bat` as administrator first

### "This app package's publisher certificate could not be verified"
- The certificate wasn't installed correctly
- Solution:
  1. Run `run_me_first.bat` as administrator again
  2. Try double-clicking `MindVault.msix` again

### Installation fails or shows error
- Previous version might be installed
- Solution:
  1. Go to Settings ? Apps
  2. Find "MindVault" and click Uninstall
  3. Install again using `MindVault.msix`

### "This app can't run on your PC"
- Your Windows version is too old
- Requirement: Windows 10 version 2004 (May 2020 Update) or newer
- Solution: Update Windows through Settings ? Windows Update

---

## Manual Certificate Installation

If `run_me_first.bat` doesn't work, install the certificate manually:

1. **Right-click** `MindVault_Certificate.cer`
2. Select **"Install Certificate"**
3. Choose **"Local Machine"** (requires administrator)
4. Click **"Next"**
5. Select **"Place all certificates in the following store"**
6. Click **"Browse"**
7. Select **"Trusted Root Certification Authorities"**
8. Click **"OK"** ? **"Next"** ? **"Finish"**
9. Click **"Yes"** on the security warning
10. You should see "The import was successful"

Now double-click `MindVault.msix` to install the app.

---

## System Requirements

- **Operating System:** Windows 10 version 2004 or later (May 2020 Update)
- **Processor:** 64-bit processor
- **RAM:** 4 GB minimum, 8 GB recommended
- **Storage:** 500 MB free space
- **Internet:** Not required (app works fully offline)

---

## Features

? **AI-Powered Flashcards** - Generate flashcards from your notes automatically  
?? **Offline Learning** - Study anytime, anywhere without internet  
?? **Spaced Repetition** - Smart review scheduling for better retention  
?? **Export & Import** - Share flashcard decks with friends  
?? **Multiplayer Mode** - Compete with classmates  
?? **Customizable** - Personalize your study experience  

---

## Uninstalling

To remove MindVault:

1. Open **Settings** (Windows + I)
2. Go to **Apps** ? **Installed apps**
3. Find **MindVault**
4. Click **"..."** ? **"Uninstall"**
5. Confirm

All your data will be removed cleanly with no leftover files.

---

## Privacy & Security

- ? All your data stays on your computer
- ? No tracking, no analytics, no data collection
- ? Fully offline - internet not required
- ? Self-signed certificate for installation only
- ? Sandboxed app - runs securely in Windows container

---

## Support

Having issues? Try these:

1. **Restart your computer** - Solves most installation issues
2. **Run Windows Update** - Ensure Windows is up to date
3. **Disable antivirus temporarily** - Some antivirus may block installation
4. **Check Windows version** - Type `winver` in Start Menu
5. **Clear Microsoft Store cache** - Run `wsreset.exe` as administrator

---

## Updates

When a new version is released:

1. Download the new `MindVault.msix` file
2. **You don't need to install the certificate again**
3. Simply double-click the new MSIX file
4. The update will install over the old version
5. Your data is preserved

---

Enjoy using MindVault! ???
