# MindVault MSIX Packaging - Quick Reference

## ?? For Developers: How to Build

### Option 1: Quick Build (Easiest)
```cmd
BUILD_MSIX.bat
```
Run this from the project root. Done!

### Option 2: Manual Build
```cmd
cd Packaging
build_msix.bat
```

## ?? What to Distribute

Share these 3 files with users:

1. **MindVault.msix** (~150-200 MB)
   - The application installer

2. **MindVault_Certificate.cer** (1 KB)
   - Security certificate for installation

3. **run_me_first.bat** (3 KB)
   - Certificate installer script

**Optional:** Include `INSTALLATION_GUIDE.md` for detailed instructions

## ?? IMPORTANT: Keep Private

**DO NOT SHARE:**
- ? `MindVault_Certificate.pfx` - Your private signing key!

This file should be kept secure and backed up for signing future updates.

## ?? For Users: How to Install

### Quick Steps:
1. Extract ZIP
2. Run `run_me_first.bat` (as admin)
3. Double-click `MindVault.msix`
4. Done! ?

See `INSTALLATION_GUIDE.md` for detailed user instructions.

## ?? Updating the App

### To Create an Update:

1. Edit version in `build_msix.bat`:
   ```batch
   set "VERSION=1.1.0.0"
   ```

2. Run build script again:
   ```cmd
   BUILD_MSIX.bat
   ```

3. Distribute only the new `MindVault.msix`
   - Users don't need certificate again
   - Update installs over old version
   - User data is preserved

## ?? Output Files

After building, you'll find:

```
Project Root/
??? MindVault.msix                     ? Distribute this
??? MindVault_Certificate.cer          ? Distribute this
??? MindVault_Certificate.pfx          ? KEEP PRIVATE!
??? Packaging/
    ??? run_me_first.bat               ? Distribute this
    ??? build_msix.bat                 ? Build script
    ??? README.md                      ? Full documentation
    ??? INSTALLATION_GUIDE.md          ? User guide
```

## ? Pre-Build Checklist

Before building:

- [ ] Project builds successfully in Release mode
- [ ] All AI files present:
  - [ ] `python311.zip` exists
  - [ ] `Models/mindvault_qwen2_0.5b_q4_k_m.gguf` exists
  - [ ] `Scripts/flashcard_ai.py` exists
  - [ ] `Wheels/*.whl` files exist
- [ ] Version number updated (if this is an update)
- [ ] Windows SDK installed (for makeappx.exe and signtool.exe)

## ?? Common Issues

### Build Issues

**"makeappx.exe not found"**
- Install Windows SDK
- Script will auto-detect common SDK locations

**"Build failed"**
- Run from project root directory
- Ensure .NET 9 SDK installed: `dotnet --version`

### Installation Issues

**"Certificate could not be verified"**
- User needs to run `run_me_first.bat` as administrator

**"App can't run on your PC"**
- User needs Windows 10 version 19041 or later

## ?? Package Details

| Component | Size | Required |
|-----------|------|----------|
| App binaries | ~10-20 MB | ? Yes |
| Python311 | ~100 MB | ? Yes (for AI) |
| AI Model (.gguf) | ~200-300 MB | ? Yes (for AI) |
| Dependencies | ~10-20 MB | ? Yes |
| **Total** | **~150-200 MB** | |

All components are necessary for offline AI flashcard generation.

## ?? Distribution Methods

### Method 1: Direct Download
- Upload to Google Drive / OneDrive / Dropbox
- Share link with users
- Users download ZIP with 3 files

### Method 2: Microsoft Store (Future)
- No certificate installation needed
- Automatic updates
- More trusted by Windows
- $19 one-time registration fee

### Method 3: Website
- Host files on your website
- Provide download page with instructions
- Track downloads (optional)

## ?? Security Notes

### Self-Signed Certificate

Pros:
- ? Free
- ? Easy to create
- ? Works fine for personal/small-scale distribution

Cons:
- ?? Requires certificate installation
- ?? May trigger SmartScreen warnings
- ?? Not trusted by default

### Commercial Certificate (Optional)

For wider distribution, consider buying from:
- DigiCert (~$300/year)
- Sectigo (~$200/year)
- Advantages:
  - ? No certificate installation needed
  - ? Trusted by Windows
  - ? No SmartScreen warnings

## ?? Version History Tracking

Keep track of versions:

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0.0 | 2024-12-04 | Initial release |
| 1.1.0.0 | TBD | [Your updates here] |

## ?? Support Resources

- **Full Documentation:** `Packaging/README.md`
- **User Guide:** `Packaging/INSTALLATION_GUIDE.md`
- **Build Issues:** Check Windows SDK installation
- **Install Issues:** See INSTALLATION_GUIDE.md troubleshooting

## ?? Summary

### Developer Workflow:
```
1. BUILD_MSIX.bat
2. Share: .msix + .cer + run_me_first.bat
3. Done!
```

### User Workflow:
```
1. Run run_me_first.bat (admin)
2. Double-click MindVault.msix
3. Enjoy!
```

---

**Ready to build?** Run `BUILD_MSIX.bat` from the project root!
