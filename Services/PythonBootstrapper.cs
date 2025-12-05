using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO.Compression;

namespace mindvault.Services
{
    public class PythonBootstrapper
    {
        const string BundledPythonZip = "python311.zip";

        // Single source of truth for Python extraction
        // For MSIX apps, we MUST use FileSystem.AppDataDirectory which is virtualized
        // For non-MSIX apps, use standard LocalApplicationData
        string RootDir
        {
            get
            {
                // For MAUI apps (especially MSIX), use FileSystem.AppDataDirectory
                // This correctly handles MSIX virtualization
                string basePath;
                try
                {
                    basePath = FileSystem.AppDataDirectory;
                    System.Diagnostics.Debug.WriteLine($"[PythonBootstrapper] Using FileSystem.AppDataDirectory: {basePath}");
                }
                catch
                {
                    // Fallback for non-MAUI contexts
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    System.Diagnostics.Debug.WriteLine($"[PythonBootstrapper] Fallback to LocalApplicationData: {basePath}");
                }
                
                if (string.IsNullOrWhiteSpace(basePath))
                {
                    basePath = Environment.GetEnvironmentVariable("LOCALAPPDATA")
                               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
                }
                
                // For FileSystem.AppDataDirectory, it already points to the app-specific folder
                // We just add "Python" subfolder, not "MindVault" (that's already in the path)
                var mindVaultPath = basePath.Contains("MindVault", StringComparison.OrdinalIgnoreCase) 
                    ? basePath 
                    : Path.Combine(basePath, "MindVault");
                    
                System.Diagnostics.Debug.WriteLine($"[PythonBootstrapper] Final RootDir: {mindVaultPath}");
                
                return mindVaultPath;
            }
        }

        string PythonDir => Path.Combine(RootDir, "Python311");
        string LogFile => Path.Combine(RootDir, "run_log.txt");
        public string LogPath => LogFile;
        public string RootDirPath => RootDir;
        public string PythonWorkDir => Path.Combine(RootDir, "Runtime");
        public string PythonExePath => _pythonExe ?? Path.Combine(PythonDir, "python.exe");

        string? _pythonExe;
        bool _pythonChecked;

        bool IsBundledPython => _pythonExe != null && _pythonExe.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);

        void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(RootDir);
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] {msg}\n");
                Debug.WriteLine($"[PythonBootstrapper] {msg}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PythonBootstrapper] LOG ERROR: {ex.Message}");
            }
        }

        // Find python.exe in PythonDir, validate python311.dll exists next to it
        string? FindEmbeddedPythonExeRecursive()
        {
            try
            {
                var pythonDir = PythonDir;
                Log($"FindEmbeddedPythonExeRecursive: Searching in {pythonDir}");

                if (!Directory.Exists(pythonDir))
                {
                    Log($"FindEmbeddedPythonExeRecursive: Directory does not exist");
                    return null;
                }

                // First, try the direct path (standard embedded Python location)
                var direct = Path.Combine(pythonDir, "python.exe");
                var directDll = Path.Combine(pythonDir, "python311.dll");
                if (File.Exists(direct) && File.Exists(directDll))
                {
                    Log($"FindEmbeddedPythonExeRecursive: Found at root: {direct}");
                    return direct;
                }

                // If not found at root, search subdirectories (prefer shallowest path)
                string? best = null;
                int bestDepth = int.MaxValue;

                foreach (var exe in Directory.EnumerateFiles(pythonDir, "python.exe", SearchOption.AllDirectories))
                {
                    var lower = exe.ToLowerInvariant();

                    // Skip venv shims
                    if (lower.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}venv{Path.DirectorySeparatorChar}"))
                        continue;

                    var exeDir = Path.GetDirectoryName(exe) ?? pythonDir;
                    var dll = Path.Combine(exeDir, "python311.dll");

                    if (!File.Exists(dll))
                    {
                        Log($"FindEmbeddedPythonExeRecursive: Candidate {exe} missing python311.dll, skipping");
                        continue;
                    }

                    var depth = exe.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
                    if (depth < bestDepth)
                    {
                        best = exe;
                        bestDepth = depth;
                        Log($"FindEmbeddedPythonExeRecursive: New best at depth {depth}: {exe}");
                    }
                }

                if (best != null)
                {
                    Log($"FindEmbeddedPythonExeRecursive: Selected: {best}");
                }
                else
                {
                    Log($"FindEmbeddedPythonExeRecursive: No valid python.exe found");
                }

                return best;
            }
            catch (Exception ex)
            {
                Log($"FindEmbeddedPythonExeRecursive: Exception: {ex.Message}");
                return null;
            }
        }

        public string? ResolveExistingPythonPath() => FindEmbeddedPythonExeRecursive();

        public bool TryGetExistingPython(out string path)
        {
            var p = FindEmbeddedPythonExeRecursive();
            if (!string.IsNullOrEmpty(p) && File.Exists(p))
            {
                // CRITICAL: Also verify python311.dll exists next to python.exe
                var exeDir = Path.GetDirectoryName(p);
                var dllPath = Path.Combine(exeDir ?? "", "python311.dll");
                if (File.Exists(dllPath))
                {
                    _pythonExe = p;
                    _pythonChecked = true;
                    path = p;
                    Log($"TryGetExistingPython: Found valid Python at {p} with DLL at {dllPath}");
                    return true;
                }
                else
                {
                    Log($"TryGetExistingPython: Found python.exe at {p} but python311.dll missing at {dllPath}");
                }
            }
            path = string.Empty;
            Log($"TryGetExistingPython: No valid Python found in {PythonDir}");
            return false;
        }

        public bool IsSetupFlagPresent()
        {
            try { return File.Exists(Path.Combine(RootDir, "setup_complete.txt")); }
            catch { return false; }
        }

        public void WriteSetupFlag()
        {
            try
            {
                Directory.CreateDirectory(RootDir);
                File.WriteAllText(Path.Combine(RootDir, "setup_complete.txt"), $"OK {DateTime.UtcNow:O}");
            }
            catch { }
        }

        async Task<bool> ExtractZipInlineAsync(string zipPath, string destinationPath, IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                Log($"ExtractZipInlineAsync: zipPath={zipPath}");
                Log($"ExtractZipInlineAsync: destinationPath={destinationPath}");

                if (!File.Exists(zipPath))
                {
                    Log($"ERROR: ZIP file not found at {zipPath}");
                    return false;
                }

                // Clean up if directory exists but is invalid
                if (Directory.Exists(destinationPath))
                {
                    var existingExe = ResolveExistingPythonPath();
                    if (!string.IsNullOrEmpty(existingExe) && File.Exists(existingExe))
                    {
                        var exeDir = Path.GetDirectoryName(existingExe);
                        var dll = Path.Combine(exeDir ?? "", "python311.dll");
                        if (File.Exists(dll))
                        {
                            Log($"Python already valid at {existingExe}");
                            return true;
                        }
                    }

                    try
                    {
                        Directory.Delete(destinationPath, true);
                        Log($"Cleaned up invalid directory: {destinationPath}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to delete directory: {ex.Message}");
                    }
                }

                Directory.CreateDirectory(destinationPath);
                Log($"Created destination directory: {destinationPath}");

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipPath);
                    var totalEntries = archive.Entries.Count;
                    Log($"ZIP archive opened, {totalEntries} entries to extract");
                    var extractedCount = 0;
                    var lastReportedPercentage = -1;

                    foreach (var entry in archive.Entries)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            var entryPath = Path.Combine(destinationPath, entry.FullName);
                            var dir = Path.GetDirectoryName(entryPath);

                            if (!string.IsNullOrEmpty(dir))
                                Directory.CreateDirectory(dir);

                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                for (int retry = 0; retry < 3; retry++)
                                {
                                    try
                                    {
                                        entry.ExtractToFile(entryPath, overwrite: true);
                                        break;
                                    }
                                    catch (IOException) when (retry < 2)
                                    {
                                        Thread.Sleep(100);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"Error extracting {entry.Name}: {ex.Message}");
                        }

                        extractedCount++;
                        var percentage = (int)((extractedCount / (double)totalEntries) * 100);
                        if (percentage != lastReportedPercentage && percentage % 5 == 0)
                        {
                            progress?.Report($"Extracting Python ({percentage}%)...");
                            lastReportedPercentage = percentage;
                        }
                    }

                    Log($"Extraction complete: {extractedCount} entries processed");
                }, ct);

                // Normalize nested layout if needed
                NormalizePythonLayout();

                var exe = ResolveExistingPythonPath();
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    Log($"? Python extracted to: {exe}");
                    return true;
                }

                Log($"ERROR: python.exe not found after extraction");
                return false;
            }
            catch (Exception ex)
            {
                Log($"ExtractZipInlineAsync EXCEPTION: {ex.Message}");
                return false;
            }
        }

        async Task<bool> ExtractBundledPythonZipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var zipPath = Path.Combine(AppContext.BaseDirectory, BundledPythonZip);
                Log($"ExtractBundledPythonZipAsync: Looking for ZIP at {zipPath}");
                Log($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");
                Log($"LocalApplicationData: {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");

                if (!File.Exists(zipPath))
                {
                    Log($"ERROR: Bundled ZIP not found at {zipPath}");
                    return false;
                }

                var sizeMb = new FileInfo(zipPath).Length / (1024.0 * 1024.0);
                Log($"Found python311.zip: {sizeMb:F1} MB");
                progress?.Report($"Extracting Python ({sizeMb:F0} MB)...");

                var ok = await ExtractZipInlineAsync(zipPath, PythonDir, progress, ct);

                if (ok)
                {
                    var exe = FindEmbeddedPythonExeRecursive();
                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                    {
                        _pythonExe = exe;
                        _pythonChecked = true;
                        Log($"? Bundled Python extraction complete: {exe}");
                        return true;
                    }
                }

                Log($"ERROR: Extraction failed");
                return false;
            }
            catch (Exception ex)
            {
                Log($"ExtractBundledPythonZipAsync EXCEPTION: {ex.Message}");
                return false;
            }
        }

        // Flatten nested Python311\Python311 layout
        void NormalizePythonLayout()
        {
            var baseDir = PythonDir;
            var nested = Path.Combine(baseDir, "Python311");

            if (!Directory.Exists(nested))
                return;

            var nestedExe = Path.Combine(nested, "python.exe");
            if (!File.Exists(nestedExe))
                return;

            Log($"NormalizePythonLayout: Flattening nested Python311 at {nested}");

            foreach (var entry in Directory.EnumerateFileSystemEntries(nested, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(entry);
                var target = Path.Combine(baseDir, name);

                try
                {
                    if (Directory.Exists(entry))
                        CopyDirectory(entry, target);
                    else
                        File.Copy(entry, target, true);
                }
                catch (Exception ex)
                {
                    Log($"NormalizePythonLayout: Copy '{name}' failed: {ex.Message}");
                }
            }

            try { Directory.Delete(nested, true); } catch { }
            Log($"NormalizePythonLayout: Flattening complete");
        }

        void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(destDir, name));
            }
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destDir, name), true);
            }
        }

        public async Task EnsurePythonReadyAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            var rootDir = RootDir;
            var pythonDir = PythonDir;
            var workDir = PythonWorkDir;

            // CRITICAL: Log all path information for debugging MSIX virtualization
            Log($"========== PYTHON BOOTSTRAP START ==========");
            Log($"Environment.SpecialFolder.LocalApplicationData = {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
            Log($"LOCALAPPDATA env var = {Environment.GetEnvironmentVariable("LOCALAPPDATA")}");
            Log($"AppContext.BaseDirectory = {AppContext.BaseDirectory}");
            try { Log($"FileSystem.AppDataDirectory = {FileSystem.AppDataDirectory}"); } catch (Exception ex) { Log($"FileSystem.AppDataDirectory ERROR: {ex.Message}"); }
            Log($"RootDir (resolved) = {rootDir}");
            Log($"PythonDir (resolved) = {pythonDir}");
            Log($"PythonWorkDir (resolved) = {workDir}");
            Log($"RootDir exists before create = {Directory.Exists(rootDir)}");

            Directory.CreateDirectory(rootDir);
            Directory.CreateDirectory(workDir);
            
            Log($"RootDir exists after create = {Directory.Exists(rootDir)}");
            Log($"RootDir full path = {Path.GetFullPath(rootDir)}");

            var existing = FindEmbeddedPythonExeRecursive();
            if (!string.IsNullOrEmpty(existing))
            {
                _pythonExe = existing;
                _pythonChecked = true;
                Log($"Found existing Python at: {existing}");
                Log($"Python exe full path: {Path.GetFullPath(existing)}");
                progress?.Report("? Using embedded Python");
            }
            else
            {
                Log("No existing Python found, will extract from ZIP");
                var extracted = await ExtractBundledPythonZipAsync(progress, ct);
                if (!extracted)
                {
                    var zipPath = Path.Combine(AppContext.BaseDirectory, BundledPythonZip);
                    Log($"ERROR: Python extraction failed");
                    throw new InvalidOperationException(
                        $"Python runtime not found.\n\n" +
                        $"Expected location: {zipPath}"
                    );
                }
            }

            await EnsureResourceFilesAsync(progress, ct);
            WriteSetupFlag();
            
            // Final verification with full paths
            Log($"========== FINAL VERIFICATION ==========");
            Log($"PythonExePath = {PythonExePath}");
            Log($"PythonExePath exists = {File.Exists(PythonExePath)}");
            var finalDir = Path.GetDirectoryName(PythonExePath) ?? "";
            var finalDll = Path.Combine(finalDir, "python311.dll");
            Log($"python311.dll path = {finalDll}");
            Log($"python311.dll exists = {File.Exists(finalDll)}");
            
            // Check for Lib folder (required for imports)
            var libDir = Path.Combine(finalDir, "Lib");
            var sitePackages = Path.Combine(libDir, "site-packages");
            Log($"Lib folder path = {libDir}");
            Log($"Lib folder exists = {Directory.Exists(libDir)}");
            Log($"site-packages path = {sitePackages}");
            Log($"site-packages exists = {Directory.Exists(sitePackages)}");
            
            // Check for llama_cpp in site-packages
            if (Directory.Exists(sitePackages))
            {
                var llamaCppDir = Path.Combine(sitePackages, "llama_cpp");
                var llamaCppPythonDir = Path.Combine(sitePackages, "llama_cpp_python");
                Log($"llama_cpp folder exists = {Directory.Exists(llamaCppDir)}");
                Log($"llama_cpp_python folder exists = {Directory.Exists(llamaCppPythonDir)}");
                
                // List site-packages contents
                Log($"Contents of site-packages:");
                try
                {
                    foreach (var dir in Directory.GetDirectories(sitePackages).Take(20))
                    {
                        Log($"  [DIR] {Path.GetFileName(dir)}");
                    }
                }
                catch { }
            }
            else
            {
                Log($"WARNING: site-packages folder not found - llama_cpp cannot be imported!");
                Log($"The bundled python311.zip may not be a complete embedded Python distribution.");
            }
            
            // List files in Python directory
            if (Directory.Exists(finalDir))
            {
                Log($"Files in {finalDir}:");
                foreach (var f in Directory.GetFiles(finalDir).Take(20))
                {
                    Log($"  - {Path.GetFileName(f)}");
                }
            }
            Log($"========== PYTHON BOOTSTRAP COMPLETE ==========");
        }

        async Task EnsureResourceFilesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var work = PythonWorkDir;
                Directory.CreateDirectory(work);

                var scriptsDir = Path.Combine(work, "Scripts");
                var modelsDir = Path.Combine(work, "Models");
                Directory.CreateDirectory(scriptsDir);
                Directory.CreateDirectory(modelsDir);

                // Copy flashcard_ai.py
                var bundledScript = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
                var targetScript = Path.Combine(scriptsDir, "flashcard_ai.py");

                if (File.Exists(bundledScript))
                {
                    File.Copy(bundledScript, targetScript, true);
                    Log($"? Copied flashcard_ai.py to {targetScript}");
                }
                else
                {
                    Log($"WARNING: flashcard_ai.py not found at {bundledScript}");
                }

                // Copy AI model
                const string modelName = "mindvault_qwen2_0.5b_q4_k_m.gguf";
                var buildModel = Path.Combine(AppContext.BaseDirectory, "Models", modelName);
                var targetModel = Path.Combine(modelsDir, modelName);

                if (File.Exists(buildModel))
                {
                    var modelSizeMb = new FileInfo(buildModel).Length / (1024.0 * 1024.0);

                    var shouldCopy = !File.Exists(targetModel);
                    if (!shouldCopy && File.Exists(targetModel))
                    {
                        var targetSizeMb = new FileInfo(targetModel).Length / (1024.0 * 1024.0);
                        shouldCopy = Math.Abs(modelSizeMb - targetSizeMb) > 0.1;
                    }

                    if (shouldCopy)
                    {
                        progress?.Report($"Copying AI model ({modelSizeMb:F0} MB)...");
                        await Task.Run(() =>
                        {
                            using var source = new FileStream(buildModel, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);
                            using var dest = new FileStream(targetModel, FileMode.Create, FileAccess.Write, FileShare.None, 81920);
                            source.CopyTo(dest, 81920);
                        }, ct);
                        Log($"? Copied model to {targetModel}");
                    }
                    else
                    {
                        Log($"? Model already present at {targetModel}");
                    }
                }
                else
                {
                    Log($"WARNING: Model not found at {buildModel}");
                }

                progress?.Report("Resources ready");
                Log("? Resource files verification complete");
            }
            catch (Exception ex)
            {
                Log($"EnsureResourceFilesAsync EXCEPTION: {ex.Message}");
            }
        }

        async Task RunHiddenAsync(string exe, string args, IProgress<string>? progress, CancellationToken ct, string workDir)
        {
            Log($"Exec: {exe} {args}");
            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.Environment["PYTHONHOME"] = PythonDir;
            psi.Environment["PYTHONPATH"] = string.Join(";", new[]
            {
                Path.Combine(PythonDir, "Lib"),
                Path.Combine(PythonDir, "Lib", "site-packages"),
                Path.Combine(PythonDir, "Scripts"),
                Path.Combine(PythonDir, "DLLs")
            });

            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException("Failed to start process.");

            var so = await p.StandardOutput.ReadToEndAsync();
            var se = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

            if (!string.IsNullOrWhiteSpace(so))
                foreach (var line in so.Split('\n').Take(200)) Log(line);
            if (!string.IsNullOrWhiteSpace(se))
            {
                Log("ERR BLOCK START");
                foreach (var line in se.Split('\n').Take(400)) Log("ERR: " + line);
                Log("ERR BLOCK END");
            }
        }

        string DetectGpuVendor()
        {
            try
            {
                var psi = new ProcessStartInfo("nvidia-smi", "-L")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    var outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(1500);
                    if (p.ExitCode == 0 && outp.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                        return "nvidia";
                }
            }
            catch { }
            return "none";
        }

        async Task<bool> TryInstallLocalLlamaWheelAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var dirs = new[]
                {
                    baseDir,
                    Path.Combine(baseDir, "Wheels"),
                    Path.Combine(baseDir, "wheels"),
                    Path.Combine(baseDir, "Wheels", "cpu"),
                    Path.Combine(baseDir, "Wheels", "gpu")
                }.Where(Directory.Exists).ToList();

                Log($"TryInstallLocalLlamaWheelAsync: Searching for wheels in {string.Join(", ", dirs)}");

                var wheels = dirs
                    .SelectMany(d => Directory.EnumerateFiles(d, "llama_cpp_python-*.whl", SearchOption.TopDirectoryOnly))
                    .Where(f => f.Contains("cp311"))
                    .ToList();

                Log($"TryInstallLocalLlamaWheelAsync: Found {wheels.Count} wheels");
                foreach (var w in wheels)
                {
                    Log($"  - {Path.GetFileName(w)}");
                }

                if (wheels.Count == 0)
                {
                    Log("No local llama wheels found");
                    return false;
                }

                var gpu = DetectGpuVendor();
                string? sel = null;

                if (gpu == "nvidia")
                {
                    sel = wheels.FirstOrDefault(w => w.Contains("cu122") || w.Contains("cuda122") || w.Contains("cu121") || w.Contains("cuda12"));
                    Log($"TryInstallLocalLlamaWheelAsync: GPU detected, looking for CUDA wheel");
                }

                // Fallback to CPU wheel (prefer cpuavx for better performance)
                sel ??= wheels.FirstOrDefault(w => w.Contains("cpuavx") && !w.Contains("cuda") && !w.Contains("cu1"));
                sel ??= wheels.FirstOrDefault(w => !w.Contains("cuda") && !w.Contains("cu1"));

                if (sel == null)
                {
                    Log("TryInstallLocalLlamaWheelAsync: No suitable wheel found");
                    return false;
                }

                Log($"TryInstallLocalLlamaWheelAsync: Selected wheel: {sel}");
                
                // First try pip install if pip is available
                if (await HasPipAsync())
                {
                    Log("TryInstallLocalLlamaWheelAsync: pip available, using pip install");
                    progress?.Report($"Installing AI component...");
                    await RunHiddenAsync(PythonExePath, $"-m pip install --no-index --no-deps --force-reinstall \"{sel}\"", progress, ct, PythonWorkDir);
                    if (await ImportTestAsync("llama_cpp", ct))
                    {
                        Log("TryInstallLocalLlamaWheelAsync: pip install succeeded");
                        return true;
                    }
                }
                
                // If pip not available or failed, manually extract the wheel
                Log("TryInstallLocalLlamaWheelAsync: pip not available or failed, manually extracting wheel");
                progress?.Report($"Installing AI component (manual extraction)...");
                
                var pyDir = Path.GetDirectoryName(PythonExePath) ?? PythonDir;
                var sitePackages = Path.Combine(pyDir, "Lib", "site-packages");
                
                // Create site-packages if it doesn't exist
                Directory.CreateDirectory(sitePackages);
                Log($"TryInstallLocalLlamaWheelAsync: Extracting to {sitePackages}");
                
                // A .whl file is just a zip file - extract it to site-packages
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(sel);
                    var totalEntries = archive.Entries.Count;
                    var extractedCount = 0;
                    
                    foreach (var entry in archive.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        
                        try
                        {
                            // Skip RECORD, WHEEL, METADATA files in .dist-info
                            // But DO extract the actual package files
                            var entryPath = Path.Combine(sitePackages, entry.FullName);
                            var dir = Path.GetDirectoryName(entryPath);
                            
                            if (!string.IsNullOrEmpty(dir))
                                Directory.CreateDirectory(dir);
                            
                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(entryPath, overwrite: true);
                            }
                            
                            extractedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log($"TryInstallLocalLlamaWheelAsync: Error extracting {entry.Name}: {ex.Message}");
                        }
                    }
                    
                    Log($"TryInstallLocalLlamaWheelAsync: Extracted {extractedCount}/{totalEntries} entries");
                }, ct);
                
                // Verify installation
                var llamaCppDir = Path.Combine(sitePackages, "llama_cpp");
                if (Directory.Exists(llamaCppDir))
                {
                    Log($"TryInstallLocalLlamaWheelAsync: llama_cpp folder created at {llamaCppDir}");
                    
                    // List contents
                    foreach (var f in Directory.GetFiles(llamaCppDir).Take(10))
                    {
                        Log($"  - {Path.GetFileName(f)}");
                    }
                }
                
                // Test import
                var importOk = await ImportTestAsync("llama_cpp", ct);
                Log($"TryInstallLocalLlamaWheelAsync: Import test result: {importOk}");
                return importOk;
            }
            catch (Exception ex)
            {
                Log("TryInstallLocalLlamaWheelAsync error: " + ex.Message);
                return false;
            }
        }

        async Task<bool> ImportTestAsync(string module, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(PythonWorkDir);
                var psi = new ProcessStartInfo(PythonExePath, $"-c \"import {module};print('IMPORT_OK')\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = PythonWorkDir
                };
                
                // Set up Python environment variables
                var pyDir = Path.GetDirectoryName(PythonExePath) ?? PythonDir;
                psi.Environment["PYTHONHOME"] = pyDir;
                psi.Environment["PYTHONPATH"] = string.Join(";", new[]
                {
                    Path.Combine(pyDir, "Lib"),
                    Path.Combine(pyDir, "Lib", "site-packages"),
                    Path.Combine(pyDir, "Scripts"),
                    Path.Combine(pyDir, "DLLs")
                });

                using var p = Process.Start(psi);
                if (p == null) return false;

                var so = await p.StandardOutput.ReadToEndAsync();
                var se = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync(ct);

                Log($"Import {module} stdout: {so.Trim()}");
                if (!string.IsNullOrWhiteSpace(se))
                    Log("Import " + module + " stderr: " + se.Split('\n').FirstOrDefault());

                return p.ExitCode == 0 && so.Contains("IMPORT_OK");
            }
            catch (Exception ex)
            {
                Log("ImportTest exception for " + module + ": " + ex.Message);
                return false;
            }
        }

        async Task EnsurePipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (await HasPipAsync()) return;

            var baseDir = AppContext.BaseDirectory;
            var bundledGetPip = Path.Combine(baseDir, "get-pip.py");

            if (!File.Exists(bundledGetPip))
            {
                throw new InvalidOperationException("pip not found and 'get-pip.py' is not bundled.");
            }

            var targetGetPip = Path.Combine(PythonDir, "get-pip.py");
            File.Copy(bundledGetPip, targetGetPip, true);

            await RunHiddenAsync(PythonExePath, $"\"{targetGetPip}\"", progress, ct, PythonDir);
        }

        async Task<bool> HasPipAsync()
        {
            try
            {
                var psi = new ProcessStartInfo(PythonExePath, "-m pip --version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = PythonWorkDir
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                await p.WaitForExitAsync();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        public async Task EnsureLlamaReadyAsync(IProgress<string>? progress, CancellationToken ct)
        {
            // First check if llama_cpp is already available (pre-installed in the bundled Python)
            Log("EnsureLlamaReadyAsync: Checking if llama_cpp is already available...");
            if (await ImportTestAsync("llama_cpp", ct))
            {
                Log("EnsureLlamaReadyAsync: llama_cpp already available!");
                progress?.Report("? AI components ready");
                return;
            }
            
            Log("EnsureLlamaReadyAsync: llama_cpp not found, attempting installation from wheel...");
            
            // Try to install from local wheel (this now includes manual extraction as fallback)
            if (await TryInstallLocalLlamaWheelAsync(progress, ct)) 
            {
                Log("EnsureLlamaReadyAsync: Successfully installed from local wheel");
                progress?.Report("? AI components installed");
                return;
            }

            // If we get here, wheel installation failed
            Log("EnsureLlamaReadyAsync: Wheel installation failed");
            throw new InvalidOperationException(
                "llama-cpp-python could not be installed.\n\n" +
                "Please ensure:\n" +
                "1. The Wheels folder contains llama_cpp_python-*.whl files\n" +
                "2. The Python311 folder has a Lib\\site-packages directory\n\n" +
                "Check the log file for details: " + LogPath
            );
        }

        public string PrepareScriptToLocal()
        {
            var scriptsDir = Path.Combine(PythonWorkDir, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            var scriptPath = Path.Combine(scriptsDir, "flashcard_ai.py");
            var src = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
            if (File.Exists(src)) File.Copy(src, scriptPath, true);
            return File.Exists(scriptPath) ? scriptPath : string.Empty;
        }

        public string GetFlashcardsOutputPath() => Path.Combine(PythonWorkDir, "flashcards.json");

        public async Task<bool> IsLlamaAvailableAsync(CancellationToken ct = default)
        {
            return await ImportTestAsync("llama_cpp", ct);
        }

        public async Task<bool> IsEnvironmentHealthyAsync(CancellationToken ct = default)
        {
            TryGetExistingPython(out var p);
            var pyOk = !string.IsNullOrEmpty(p) && File.Exists(p);
            var scriptOk = File.Exists(Path.Combine(PythonWorkDir, "Scripts", "flashcard_ai.py"));
            var modelOk = File.Exists(Path.Combine(PythonWorkDir, "Models", "mindvault_qwen2_0.5b_q4_k_m.gguf"));
            var llamaOk = pyOk && await ImportTestAsync("llama_cpp", ct);
            return pyOk && scriptOk && modelOk && llamaOk;
        }

        public async Task<bool> QuickSystemPythonHasLlamaAsync(CancellationToken ct = default)
        {
            if (TryGetExistingPython(out var p) && File.Exists(p))
            {
                return await ImportTestAsync("llama_cpp", ct);
            }
            return false;
        }

        public async Task<bool> BuildLlamaInCmdAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (await TryInstallLocalLlamaWheelAsync(progress, ct)) return true;

            throw new InvalidOperationException(
                "llama-cpp-python wheel not found.\n\n" +
                "Please ensure llama-cpp-python wheel files (.whl) are included in the 'Wheels' directory."
            );
        }
    }
}
