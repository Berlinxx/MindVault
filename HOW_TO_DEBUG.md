# How to Debug MindVault MSIX App

## Quick Steps to Deploy and Debug

### Method 1: Using Visual Studio (Recommended)

1. **Open Visual Studio**
2. **Set Configuration**: 
   - Set to **Debug** (top toolbar)
   - Set platform to **Windows Machine** or **x64**
3. **Select Target Framework**:
   - Right-click project ? Properties
   - Or use dropdown: Select `net9.0-windows10.0.19041.0`
4. **Deploy the package**:
   - Go to **Build** menu ? **Deploy Solution** (or press Ctrl+Alt+F7)
   - Or right-click project ? **Deploy**
5. **Start Debugging**:
   - Press **F5** or click the green **Start** button
   - Or go to **Debug** menu ? **Start Debugging**

### Method 2: Using Command Line

```powershell
# Clean previous builds
dotnet clean

# Build and deploy in Debug mode
dotnet build -c Debug -f net9.0-windows10.0.19041.0

# Deploy the MSIX package
msbuild mindvault.csproj /t:Deploy /p:Configuration=Debug /p:TargetFramework=net9.0-windows10.0.19041.0

# Or publish to deploy
dotnet publish -c Debug -f net9.0-windows10.0.19041.0
```

Then launch the app from Start Menu or run from:
```
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\
```

### Method 3: Manual Deployment

If Visual Studio deployment doesn't work:

1. **Build the package**:
```powershell
dotnet publish -c Debug -f net9.0-windows10.0.19041.0
```

2. **Find the MSIX**:
```
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\AppPackages\mindvault_*\
```

3. **Install the package**:
   - Right-click the `.msix` file
   - Select **Install**
   - Or use PowerShell:
   ```powershell
   Add-AppxPackage -Path "path\to\mindvault.msix"
   ```

4. **Attach debugger**:
   - Launch the app from Start Menu
   - In Visual Studio: **Debug** ? **Attach to Process**
   - Find `mindvault.exe` and click **Attach**

---

## Troubleshooting

### Error: "The project needs to be deployed before we can debug"

**Solution**: Deploy first using one of these methods:

1. **In Visual Studio**:
   - **Build** ? **Deploy Solution** (Ctrl+Alt+F7)

2. **Or enable auto-deploy**:
   - Right-click project ? **Properties**
   - Go to **Debug** tab
   - Check **Deploy**

### Error: "Certificate not trusted"

**Solution**:
```powershell
# Install the certificate
certutil -addstore Root "MindVault_Certificate.cer"
certutil -addstore TrustedPeople "MindVault_Certificate.cer"
```

### Error: "Package installation failed"

**Solutions**:

1. **Remove old version**:
```powershell
Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage
```

2. **Enable Developer Mode**:
   - Settings ? Update & Security ? For developers
   - Turn ON **Developer mode**

3. **Check certificate**:
```powershell
certutil -verifystore Root "MindVault"
```

---

## Configuration Files Updated

### mindvault.csproj
```xml
<PropertyGroup Condition="$(TargetFramework.Contains('windows'))">
  <WindowsPackageType>MSIX</WindowsPackageType>
  <GenerateAppxPackageOnBuild>true</GenerateAppxPackageOnBuild>
  <AppxPackageSigningEnabled>true</AppxPackageSigningEnabled>
  <PackageCertificateThumbprint>83CE...</PackageCertificateThumbprint>
</PropertyGroup>
```

### Properties/launchSettings.json
```json
{
  "profiles": {
    "Windows Machine": {
      "commandName": "MsixPackage",
      "nativeDebugging": false,
      "alwaysReinstallApp": false,
      "deployMode": "CopyToDevice"
    }
  }
}
```

---

## Debugging Tips

### 1. Check Logs
Python extraction logs are at:
```
%LOCALAPPDATA%\MindVault\run_log.txt
```

### 2. Debug Output
In Visual Studio, check the **Output** window:
- View ? Output
- Select "Debug" from dropdown

### 3. Breakpoints
Set breakpoints in:
- `AddFlashcardsPage.xaml.cs` ? `OnSummarize()` method
- `PythonBootstrapper.cs` ? `EnsurePythonReadyAsync()` method
- `SummarizeContentPage.xaml.cs` ? `OnGenerate()` method

### 4. Test Python Path
Run this to verify Python installation:
```powershell
test_python_paths.bat
```

---

## Quick Deploy Command

For fast deploy and run:
```powershell
# One-line deploy and start
dotnet build -c Debug -f net9.0-windows10.0.19041.0 && start shell:AppsFolder\$(Get-AppxPackage -Name "*mindvault*" | Select -ExpandProperty PackageFamilyName)!App
```

---

## Build Configurations

### Debug Mode (for development)
- MSIX package generated
- Easier to attach debugger
- Symbols included
- Faster builds

### Release Mode (for distribution)
- Optimized code
- Signed MSIX
- Ready for distribution
- No debug symbols

---

**Status**: ? Configuration updated for MSIX debugging!

Now you can press **F5** to deploy and debug!
