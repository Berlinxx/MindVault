# MindVault - Setup Guide for Team Members

## Quick Setup (3 steps)

### 1. Extract the Project
- Extract the entire compressed file to a **simple path**
- ? Good: `C:\MindVault\`
- ? Avoid: `C:\Users\YourName\Desktop\New Folder (2)\AI DONE\`

### 2. Run Setup Checker
- Open PowerShell in the project folder
- Run: `.\check_setup.ps1`
- Follow any instructions shown

### 3. Build & Run
- Open `mindvault.sln` in Visual Studio 2022
- Set target to **Windows**
- Press F5 to build and run

---

## Troubleshooting

### Python Import Errors

If you get errors about `llama_cpp` not found:

**Option A: Use Bundled Python (Offline)**
1. Verify `Python311\` folder exists in project root
2. Check `Python311\Lib\site-packages\llama_cpp\` exists
3. Run `check_setup.ps1` to verify

**Option B: Install on Your System Python**
1. Open Command Prompt as Administrator
2. Run: `python -m pip install llama-cpp-python`
3. This requires Visual Studio Build Tools or internet

### Missing DLL Errors

Install **Microsoft Visual C++ Redistributables**:
- Download: https://aka.ms/vs/17/release/vc_redist.x64.exe
- Install both x86 and x64 versions
- Restart your PC

### Antivirus Blocking

Add these to your antivirus exclusions:
- The entire project folder
- Specifically: `Python311\python.exe`

### Build Errors

Make sure you have:
- Visual Studio 2022 (latest version)
- .NET 9 SDK installed
- Windows 10 SDK (10.0.19041 or higher)

---

## What's Included

### Fully Offline (No Internet Needed)
- ? Python 3.11 (`Python311\` folder)
- ? llama-cpp-python preinstalled
- ? AI model file (`Models\*.gguf`)
- ? All dependencies

### First Run
The app should work **completely offline** if all files are present. The setup checker will verify everything is ready.

---

## Contact

If you still have issues after running `check_setup.ps1`, contact the project owner with:
1. Screenshot of the setup checker output
2. Error messages from Visual Studio
3. Your Windows version

---

## Notes for Sharing

When sharing this project with others:
1. Compress the **entire project folder** (including Python311)
2. Use 7-Zip or WinRAR (not Windows built-in)
3. Verify compressed file includes:
   - `Python311\` folder (~150 MB)
   - `Models\*.gguf` file (~200-300 MB)
   - `Wheels\` folder (~50 MB)
4. Upload to Google Drive / cloud storage
5. Share link with team

**Total compressed size**: ~400-500 MB
