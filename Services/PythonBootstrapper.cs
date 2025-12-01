using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace mindvault.Services
{
    public class PythonBootstrapper
    {
        const string PythonVersion = "3.11.9";
        const string PythonEmbedZip = "python-3.11.9-embed-amd64.zip";
        const string PythonDownloadUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip";
        const string GetPipUrl = "https://bootstrap.pypa.io/get-pip.py";
        const string PythonInstallerExe = "python-3.11.9-amd64.exe";
        const string PythonInstallerUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-amd64.exe";

        string RootDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindVault");
        string PythonDir => Path.Combine(RootDir, "Python");
        string EmbeddedPythonExe => Path.Combine(PythonDir, "python.exe");
        string SetupFlag => Path.Combine(RootDir, "setup_complete.txt");
        string LogFile => Path.Combine(RootDir, "run_log.txt");

        public string LogPath => LogFile;
        public string PythonWorkDir => UsingSystemPython ? Path.Combine(RootDir, "Runtime") : PythonDir;
        public string RootPath => RootDir;
        public string PythonExePath => _pythonExe ?? EmbeddedPythonExe;

        string? _pythonExe;
        bool UsingSystemPython => _pythonExe != null && _pythonExe != EmbeddedPythonExe && !IsBundledPython;
        bool IsBundledPython => _pythonExe != null && _pythonExe.Contains(Path.Combine(AppContext.BaseDirectory, "Python311"));
        bool _llamaInstallAttempted;
        bool _manualLlamaWindowLaunched;

        void Log(string msg) { try { Directory.CreateDirectory(RootDir); File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] {msg}\n"); } catch { } }

        // ---------------- System Python Detection ----------------
        async Task<string?> DetectSystemPythonAsync(CancellationToken ct)
        {
            string[] cmds = { "python", "py" };
            foreach (var cmd in cmds)
            {
                try
                {
                    var psi = new ProcessStartInfo(cmd, "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                    using var proc = Process.Start(psi); if (proc == null) continue; await proc.WaitForExitAsync(ct); if (proc.ExitCode != 0) continue;
                    var p2 = Process.Start(new ProcessStartInfo(cmd, "-c \"import sys;print(sys.executable)\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
                    if (p2 == null) continue; var path = await p2.StandardOutput.ReadToEndAsync(); await p2.WaitForExitAsync(ct); path = path.Trim(); if (File.Exists(path)) return path;
                }
                catch { }
            }
            return null;
        }
        async Task<string?> LocateSystemPythonWithPipAsync(CancellationToken ct)
        {
            string[] cmds = { "python", "py" };
            foreach (var cmd in cmds)
            {
                try
                {
                    var psi = new ProcessStartInfo(cmd, "-m pip --version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                    using var proc = Process.Start(psi); if (proc == null) continue; await proc.WaitForExitAsync(ct); if (proc.ExitCode != 0) continue;
                    var p2 = Process.Start(new ProcessStartInfo(cmd, "-c \"import sys;print(sys.executable)\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
                    if (p2 == null) continue; var path = await p2.StandardOutput.ReadToEndAsync(); await p2.WaitForExitAsync(ct); path = path.Trim(); if (File.Exists(path)) return path;
                }
                catch { }
            }
            return null;
        }

        // ---------------- Main Ensure ----------------
        public async Task EnsurePythonReadyAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            Directory.CreateDirectory(RootDir);
            if (UsingSystemPython) Directory.CreateDirectory(PythonWorkDir);

            // Priority 1: Check for bundled Python311 in app directory (fully offline)
            var bundledPython = Path.Combine(AppContext.BaseDirectory, "Python311", "python.exe");
            if (File.Exists(bundledPython))
            {
                _pythonExe = bundledPython;
                Log($"? Using bundled Python: {bundledPython}");
                progress?.Report("? Using bundled Python (offline mode)");
                Directory.CreateDirectory(PythonWorkDir);
            }
            // Priority 1b: Check for Python311 in source directory (before build)
            else
            {
                var sourceDirPython = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Python311", "python.exe");
                var resolvedSourcePath = Path.GetFullPath(sourceDirPython);
                if (File.Exists(resolvedSourcePath))
                {
                    _pythonExe = resolvedSourcePath;
                    Log($"? Using source directory Python: {resolvedSourcePath}");
                    progress?.Report("? Using bundled Python from source (offline mode)");
                    Directory.CreateDirectory(PythonWorkDir);
                }
                // Priority 2: Try to detect system Python only if bundled Python not found
                else if (_pythonExe == null || !File.Exists(_pythonExe))
                {
                    Log("? Bundled Python311 not found. Checking system Python...");
                    progress?.Report("? Bundled Python not found, checking system...");
                    _pythonExe = await LocateSystemPythonWithPipAsync(ct) ?? await DetectSystemPythonAsync(ct);
                    if (_pythonExe != null && File.Exists(_pythonExe))
                    { 
                        Log("? System Python detected - may require llama-cpp-python installation"); 
                        progress?.Report("? System Python detected (may need setup)"); 
                        Directory.CreateDirectory(PythonWorkDir); 
                    }
                }
            }
            
            // Priority 3: If still no valid Python executable, launch installer
            if (_pythonExe == null || !File.Exists(_pythonExe))
            {
                Log("No Python detected; launching full installer."); 
                progress?.Report("Installing Python (first-time setup)...");
                var installed = await InstallFullPythonFallbackAsync(progress, ct); 
                if (!installed) 
                { 
                    progress?.Report("Python installation failed."); 
                    throw new InvalidOperationException("Python installation failed. Please install Python 3.11 manually from python.org and ensure 'Add to PATH' is checked.");
                }
                Directory.CreateDirectory(PythonWorkDir);
            }
            
            if (!await HasPipAsync()) 
            { 
                progress?.Report("pip missing – attempting bootstrap..."); 
                await EnsurePipAsync(progress, ct); 
            }

            bool llamaOk = await ImportTestAsync("llama_cpp", ct);
            if (!llamaOk && !_llamaInstallAttempted)
            { 
                _llamaInstallAttempted = true; 
                progress?.Report("llama-cpp-python not found - will install via CMD window..."); 
                Log("Skipping silent llama installation - will use visible CMD window instead");
                // Skip silent installation - let the UI handle CMD window installation
            }

            if (await HasPipAsync()) await VerifyOrInstallPackagesAsync(progress, ct);
            await EnsureResourceFilesAsync(progress, ct);

            try
            {
                var scriptsDir = Path.Combine(PythonWorkDir, "Scripts"); var modelsDir = Path.Combine(PythonWorkDir, "Models");
                var pyOk = !string.IsNullOrEmpty(_pythonExe) && File.Exists(_pythonExe!);
                var scriptOk = File.Exists(Path.Combine(scriptsDir, "flashcard_ai.py"));
                var modelOk = File.Exists(Path.Combine(modelsDir, "mindvault_qwen2_0.5b_q4_k_m.gguf"));
                llamaOk = await ImportTestAsync("llama_cpp", ct);
                
                // Note: llama may not be installed yet if skipping silent install - that's OK
                if (pyOk && scriptOk && modelOk) 
                { 
                    if (llamaOk)
                    {
                        Log("? MindVault AI environment ready."); 
                        progress?.Report("Setup complete."); 
                    }
                    else
                    {
                        Log("Python and resources ready. llama-cpp-python will be installed via CMD.");
                        progress?.Report("Python ready. llama-cpp-python needs CMD installation.");
                    }
                }
                else Log($"Environment validation incomplete: python={pyOk} script={scriptOk} model={modelOk} llama={llamaOk}");
            }
            catch (Exception ex) { Log("Environment validation error: " + ex.Message); }
        }

        // ---------------- Pip / Import helpers ----------------
        async Task EnsurePipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (await HasPipAsync()) { Log("pip already available"); return; }
            progress?.Report("Installing pip..."); var baseDir = UsingSystemPython ? PythonWorkDir : PythonDir; Directory.CreateDirectory(baseDir);
            Log("pip missing - downloading get-pip.py");
            try
            {
                var path = Path.Combine(baseDir, "get-pip.py"); using var client = new HttpClient(); using var resp = await client.GetAsync(GetPipUrl, ct); resp.EnsureSuccessStatusCode();
                await File.WriteAllTextAsync(path, await resp.Content.ReadAsStringAsync(ct), ct); Log("Downloaded get-pip.py");
                await RunHiddenAsync(_pythonExe!, $"\"{path}\"", progress, ct, baseDir);
            }
            catch (Exception ex) { Log("Failed to install pip: " + ex.Message); }
            if (!await HasPipAsync())
            {
                Log("pip still not detected after first attempt; re-adjusting _pth and retrying get-pip"); if (!UsingSystemPython) AdjustPthFile();
                var path = Path.Combine(baseDir, "get-pip.py"); if (File.Exists(path)) await RunHiddenAsync(_pythonExe!, $"\"{path}\" --no-warn-script-location", progress, ct, baseDir);
            }
        }
        async Task<bool> HasPipAsync() { try { var psi = new ProcessStartInfo(_pythonExe!, "-m pip --version") { WorkingDirectory = PythonWorkDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }; using var proc = Process.Start(psi); if (proc == null) return false; await proc.WaitForExitAsync(); return proc.ExitCode == 0; } catch { return false; } }
        async Task<bool> PipShowAsync(string pkg) { try { var psi = new ProcessStartInfo(_pythonExe!, $"-m pip show {pkg}") { WorkingDirectory = PythonWorkDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }; using var proc = Process.Start(psi); if (proc == null) return false; await proc.WaitForExitAsync(); return proc.ExitCode == 0; } catch { return false; } }
        async Task<bool> ImportTestAsync(string module, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(PythonWorkDir);
                var psi = new ProcessStartInfo(_pythonExe!, $"-c \"import {module};import sys;print('IMPORT_OK')\"") { WorkingDirectory = PythonWorkDir, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi); if (proc == null) return false; var stdout = await proc.StandardOutput.ReadToEndAsync(); var stderr = await proc.StandardError.ReadToEndAsync(); await proc.WaitForExitAsync(ct);
                if (!string.IsNullOrWhiteSpace(stderr)) Log($"Import {module} stderr: {stderr.Split('\n').FirstOrDefault()}"); if (!string.IsNullOrWhiteSpace(stdout)) Log($"Import {module} stdout: {stdout.Trim()}");
                return proc.ExitCode == 0 && stdout.Contains("IMPORT_OK");
            }
            catch (Exception ex) { Log($"ImportTest exception for {module}: {ex.Message}"); return false; }
        }

        // ---------------- Resources ----------------
        public string PrepareScriptToLocal()
        {
            var scriptsDir = Path.Combine(PythonWorkDir, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            
            var scriptPath = Path.Combine(scriptsDir, "flashcard_ai.py");
            
            // If already exists, return it
            if (File.Exists(scriptPath))
            {
                Log("flashcard_ai.py already exists in work dir");
                return scriptPath;
            }
            
            // Copy from build output
            var src = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py"); 
            if (!File.Exists(src)) 
            { 
                Log("flashcard_ai.py missing in build output"); 
                return string.Empty; 
            }
            
            File.Copy(src, scriptPath, true); 
            Log("Copied flashcard_ai.py to work dir"); 
            return scriptPath;
        }
        public string GetFlashcardsOutputPath() => Path.Combine(PythonWorkDir, "flashcards.json");
        async Task EnsureResourceFilesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var work = PythonWorkDir; Directory.CreateDirectory(work); var scriptsDir = Path.Combine(work, "Scripts"); var modelsDir = Path.Combine(work, "Models"); var libDir = Path.Combine(work, "Lib");
                Directory.CreateDirectory(scriptsDir); Directory.CreateDirectory(modelsDir); Directory.CreateDirectory(libDir);
                
                // Copy flashcard_ai.py script
                var bundledScript = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py"); 
                var targetScript = Path.Combine(scriptsDir, "flashcard_ai.py"); 
                if (File.Exists(bundledScript)) 
                { 
                    File.Copy(bundledScript, targetScript, true); 
                    Log($"Copied flashcard_ai.py to {targetScript}"); 
                }
                else
                {
                    Log($"WARNING: flashcard_ai.py not found at {bundledScript}");
                }
                
                // Copy model file
                var modelPath = CopyModelToLocalAppData(modelsDir); 
                if (!string.IsNullOrEmpty(modelPath)) 
                {
                    Log($"Model ready at: {modelPath}");
                }
                else
                {
                    Log("WARNING: Model file not found or not copied");
                }
                
                Log("? Required files verified."); 
                progress?.Report("? Required files verified.");
            }
            catch (Exception ex) { Log("Resource verification error: " + ex.Message); }
        }
        
        // New method to copy model to LocalAppData
        string CopyModelToLocalAppData(string targetDir)
        {
            try
            {
                Directory.CreateDirectory(targetDir);
                const string modelName = "mindvault_qwen2_0.5b_q4_k_m.gguf";
                var targetPath = Path.Combine(targetDir, modelName);
                
                // If already exists in LocalAppData, return it
                if (File.Exists(targetPath))
                {
                    Log($"Model already exists at {targetPath}");
                    return targetPath;
                }
                
                // Try to find model in build output
                var buildModel = Path.Combine(AppContext.BaseDirectory, "Models", modelName);
                if (File.Exists(buildModel))
                {
                    Log($"Copying model from build output: {buildModel} -> {targetPath}");
                    File.Copy(buildModel, targetPath, true);
                    return targetPath;
                }
                
                // Try to find in source directory (for development)
                var sourceModel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Models", modelName);
                var resolvedSourceModel = Path.GetFullPath(sourceModel);
                if (File.Exists(resolvedSourceModel))
                {
                    Log($"Copying model from source: {resolvedSourceModel} -> {targetPath}");
                    File.Copy(resolvedSourceModel, targetPath, true);
                    return targetPath;
                }
                
                Log($"Model not found in any location. Searched: {buildModel}, {resolvedSourceModel}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log($"Error copying model to LocalAppData: {ex.Message}");
                return string.Empty;
            }
        }

        // ---------------- Environment checks ----------------
        public async Task<bool> IsLlamaAvailableAsync(CancellationToken ct = default) => _pythonExe != null && await ImportTestAsync("llama_cpp", ct);
        public async Task<bool> IsEnvironmentHealthyAsync(CancellationToken ct = default)
        {
            try
            {
                // Priority 1: Check bundled Python in build output first
                var bundledPython = Path.Combine(AppContext.BaseDirectory, "Python311", "python.exe");
                if (File.Exists(bundledPython))
                {
                    _pythonExe = bundledPython;
                    Log("IsEnvironmentHealthyAsync: Using bundled Python311");
                }
                // Priority 1b: Check source directory (before build)
                else
                {
                    var sourceDirPython = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Python311", "python.exe");
                    var resolvedSourcePath = Path.GetFullPath(sourceDirPython);
                    if (File.Exists(resolvedSourcePath))
                    {
                        _pythonExe = resolvedSourcePath;
                        Log($"IsEnvironmentHealthyAsync: Using source directory Python: {resolvedSourcePath}");
                    }
                }
                
                var pyOk = _pythonExe != null && File.Exists(_pythonExe); 
                if (!pyOk) 
                { 
                    Log("IsEnvironmentHealthyAsync: No bundled Python found, checking system...");
                    _pythonExe = await DetectSystemPythonAsync(ct) ?? (_pythonExe ?? EmbeddedPythonExe); 
                    pyOk = _pythonExe != null && File.Exists(_pythonExe); 
                }
                
                // IMPORTANT: Ensure files are copied to LocalAppData before checking
                var scriptsDir = Path.Combine(PythonWorkDir, "Scripts"); 
                var modelsDir = Path.Combine(PythonWorkDir, "Models");
                Directory.CreateDirectory(scriptsDir);
                Directory.CreateDirectory(modelsDir);
                
                // Copy script if not already there
                var scriptPath = Path.Combine(scriptsDir, "flashcard_ai.py");
                if (!File.Exists(scriptPath))
                {
                    var bundledScript = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
                    if (File.Exists(bundledScript))
                    {
                        File.Copy(bundledScript, scriptPath, true);
                        Log($"Copied flashcard_ai.py to {scriptPath}");
                    }
                }
                
                // Copy model if not already there
                var modelPath = Path.Combine(modelsDir, "mindvault_qwen2_0.5b_q4_k_m.gguf");
                if (!File.Exists(modelPath))
                {
                    modelPath = CopyModelToLocalAppData(modelsDir);
                }
                
                var scriptOk = File.Exists(scriptPath); 
                var modelOk = !string.IsNullOrEmpty(modelPath) && File.Exists(modelPath);
                var llamaOk = pyOk && await ImportTestAsync("llama_cpp", ct); 
                
                Log($"IsEnvironmentHealthyAsync: py={pyOk}, script={scriptOk}, model={modelOk}, llama={llamaOk}");
                return pyOk && scriptOk && modelOk && llamaOk;
            }
            catch (Exception ex) 
            { 
                Log($"IsEnvironmentHealthyAsync exception: {ex.Message}");
                return false; 
            }
        }
        public async Task<bool> QuickSystemPythonHasLlamaAsync(CancellationToken ct = default)
        {
            // Priority 1: Check bundled Python in build output first (skip system check entirely)
            var bundledPython = Path.Combine(AppContext.BaseDirectory, "Python311", "python.exe");
            if (File.Exists(bundledPython))
            {
                _pythonExe = bundledPython;
                Log("QuickSystemPythonHasLlamaAsync: Found bundled Python in build output, checking llama...");
                
                // Quick import test for bundled Python
                if (await ImportTestAsync("llama_cpp", ct))
                {
                    Log("QuickSystemPythonHasLlamaAsync: Bundled Python has llama_cpp!");
                    return true;
                }
                Log("QuickSystemPythonHasLlamaAsync: Bundled Python found but llama_cpp missing");
                return false;
            }
            
            // Priority 1b: Check source directory (before build)
            var sourceDirPython = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Python311", "python.exe");
            var resolvedSourcePath = Path.GetFullPath(sourceDirPython);
            if (File.Exists(resolvedSourcePath))
            {
                _pythonExe = resolvedSourcePath;
                Log($"QuickSystemPythonHasLlamaAsync: Found bundled Python in source directory: {resolvedSourcePath}");
                
                // Quick import test for source Python
                if (await ImportTestAsync("llama_cpp", ct))
                {
                    Log("QuickSystemPythonHasLlamaAsync: Source Python has llama_cpp!");
                    return true;
                }
                Log("QuickSystemPythonHasLlamaAsync: Source Python found but llama_cpp missing");
                return false;
            }
            
            // Priority 2: Check system Python only if no bundled Python
            Log("QuickSystemPythonHasLlamaAsync: No bundled Python found, checking system Python...");
            string[] cmds = { "python", "py" }; 
            foreach (var cmd in cmds) 
            { 
                try 
                { 
                    var psi = new ProcessStartInfo(cmd, "-c \"import llama_cpp,sys;print('OK');print(sys.executable)\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }; 
                    using var proc = Process.Start(psi); 
                    if (proc == null) continue; 
                    var stdout = await proc.StandardOutput.ReadToEndAsync(); 
                    await proc.WaitForExitAsync(ct); 
                    if (proc.ExitCode == 0 && stdout.Contains("OK")) 
                    { 
                        var exe = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last().Trim(); 
                        if (File.Exists(exe)) 
                        { 
                            _pythonExe = exe; 
                            Log($"Quick system python with llama detected: {_pythonExe}"); 
                            return true; 
                        } 
                    } 
                } 
                catch { } 
            } 
            return false;
        }

        // ---------------- GPU / Wheel / Build ----------------
        string DetectGpuVendor()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "none"; try { var psi = new ProcessStartInfo("nvidia-smi", "-L") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }; using var proc = Process.Start(psi); if (proc != null) { var output = proc.StandardOutput.ReadToEnd(); proc.WaitForExit(1500); if (proc.ExitCode == 0 && output.Contains("GPU", StringComparison.OrdinalIgnoreCase)) return "nvidia"; } } catch { }
            }
            catch { }
            return "none";
        }
        async Task<(string Abi, string Plat)> GetPythonWheelTagsAsync(CancellationToken ct)
        {
            try
            {
                var psi = new ProcessStartInfo(PythonExePath, "-c \"import sys,platform;print(f'cp{sys.version_info.major}{sys.version_info.minor}-cp{sys.version_info.major}{sys.version_info.minor}|{platform.machine()}')\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = PythonWorkDir };
                using var p = Process.Start(psi); if (p == null) return ("cp311-cp311", "win_amd64"); var stdout = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync(ct); var parts = stdout.Trim().Split('|'); var abi = parts.Length > 0 ? parts[0] : "cp311-cp311"; var arch = (parts.Length > 1 ? parts[1] : "AMD64").ToLowerInvariant(); var plat = arch.Contains("arm64") ? "win_arm64" : "win_amd64"; return (abi, plat);
            }
            catch { return ("cp311-cp311", "win_amd64"); }
        }
        async Task<bool> TryInstallDirectWheelAsync(IProgress<string>? progress, CancellationToken ct)
        {
            var (abi, plat) = await GetPythonWheelTagsAsync(ct); string[] versions = { "0.3.16", "0.3.12", "0.3.10", "0.3.3", "0.3.2", "0.3.1", "0.3.0", "0.2.86", "0.2.82", "0.2.79" };
            using var http = new HttpClient();
            foreach (var ver in versions)
            {
                ct.ThrowIfCancellationRequested(); var file = $"llama_cpp_python-{ver}-{abi}-{plat}.whl"; var url = $"https://abetlen.github.io/llama-cpp-python/whl/{file}"; Log($"Probing wheel URL: {url}");
                try { using var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), ct); if (!resp.IsSuccessStatusCode) { Log($"Wheel not found: HTTP {(int)resp.StatusCode}"); continue; } }
                catch (Exception ex) { Log("HEAD probe failed: " + ex.Message); continue; }
                progress?.Report($"Installing prebuilt wheel {file}..."); await RunHiddenAsync(PythonExePath, $"-m pip install --no-cache-dir \"{url}\"", progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct)) { Log($"Direct wheel install succeeded: {file}"); return true; }
            }
            return false;
        }
        async Task<bool> TrySourceBuildAsync(bool gpu, IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested(); var buildArgs = "-m pip install --no-binary=:all: --force-reinstall llama-cpp-python"; Log($"Starting source build (gpu={gpu})"); progress?.Report(gpu ? "Compiling CUDA llama-cpp-python (requires VS+CMake)" : "Compiling CPU llama-cpp-python (requires VS+CMake)");
                var psi = new ProcessStartInfo(PythonExePath, buildArgs) { WorkingDirectory = PythonWorkDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                if (gpu) { psi.Environment["CMAKE_ARGS"] = "-DLLAMA_CUBLAS=on"; psi.Environment["LLAMA_CUBLAS"] = "1"; } else { psi.Environment["CMAKE_ARGS"] = "-DLLAMA_CPP_FORCE_CPU=ON"; psi.Environment["LLAMA_CPP_FORCE_CPU"] = "1"; }
                psi.Environment["FORCE_CMAKE"] = "1"; using var proc = Process.Start(psi); if (proc == null) { Log("Source build process failed to start"); return false; }
                var stdoutTask = proc.StandardOutput.ReadToEndAsync(); var stderrTask = proc.StandardError.ReadToEndAsync(); await proc.WaitForExitAsync(ct); var outText = await stdoutTask; var errText = await stderrTask;
                if (!string.IsNullOrWhiteSpace(outText)) Log(string.Join('\n', outText.Split('\n').Take(200))); if (!string.IsNullOrWhiteSpace(errText)) { Log("ERR BLOCK START"); foreach (var line in errText.Split('\n').Take(400)) Log("ERR: " + line); Log("ERR BLOCK END"); }
                if (proc.ExitCode == 0 && await ImportTestAsync("llama_cpp", ct)) { Log("Source build import succeeded"); progress?.Report("llama-cpp-python built from source."); return true; }
                Log("Source build failed exitCode=" + proc.ExitCode); return false;
            }
            catch (Exception ex) { Log("TrySourceBuildAsync error: " + ex.Message); return false; }
        }
        async Task<bool> TryInstallLocalLlamaWheelAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var candidates = new[] { baseDir, Path.Combine(baseDir, "Wheels"), Path.Combine(baseDir, "wheels") }.Where(Directory.Exists).ToList();
                foreach (var dir in candidates) Log("Local wheel search dir: " + dir);
                var allWheels = candidates.SelectMany(c => Directory.EnumerateFiles(c, "llama_cpp_python-*.whl", SearchOption.TopDirectoryOnly)).Where(f => f.Contains("cp311")).ToList();
                
                if (allWheels.Count == 0) { Log("No local llama_cpp_python wheels found."); return false; }
                
                // Auto-detect GPU and choose appropriate wheel
                var gpuVendor = DetectGpuVendor();
                Log($"GPU detection for wheel selection: {gpuVendor}");
                
                string? selectedWheel = null;
                
                if (gpuVendor == "nvidia")
                {
                    // Prefer CUDA wheels
                    selectedWheel = allWheels.FirstOrDefault(w => w.Contains("cuda122") || w.Contains("cu122"));
                    if (selectedWheel != null) Log("Selected CUDA 12.2 wheel for NVIDIA GPU");
                    
                    if (selectedWheel == null)
                    {
                        selectedWheel = allWheels.FirstOrDefault(w => w.Contains("cuda121") || w.Contains("cu121"));
                        if (selectedWheel != null) Log("Selected CUDA 12.1 wheel for NVIDIA GPU");
                    }
                }
                
                // Fallback to CPU wheel
                if (selectedWheel == null)
                {
                    selectedWheel = allWheels.FirstOrDefault(w => !w.Contains("cuda") && !w.Contains("cu1"));
                    if (selectedWheel != null) Log("Selected CPU wheel (no GPU or no GPU wheel found)");
                }
                
                if (selectedWheel == null) { Log("No suitable wheel found."); return false; }
                
                Log("Found local wheel: " + selectedWheel); 
                progress?.Report($"Installing bundled {(gpuVendor == "nvidia" && selectedWheel.Contains("cuda") ? "GPU" : "CPU")} wheel...");
                await RunHiddenAsync(PythonExePath, $"-m pip install --no-index --no-deps --force-reinstall \"{selectedWheel}\"", progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct)) { Log("Local wheel import succeeded; skipping network installs."); progress?.Report("? llama-cpp-python installed from bundled wheel."); return true; }
                Log("Local wheel install attempt failed (import test). Will fall back to network."); return false;
            }
            catch (Exception ex) { Log("TryInstallLocalLlamaWheelAsync error: " + ex.Message); return false; }
        }
        async Task InstallLlamaCppWithGpuAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (!await HasPipAsync()) return;
            if (await TryInstallLocalLlamaWheelAsync(progress, ct)) return; await RunHiddenAsync(PythonExePath, "-m pip install --upgrade pip setuptools wheel --quiet", progress, ct, PythonWorkDir);
            var vendor = DetectGpuVendor(); Log("GPU vendor detection: " + vendor);
            if (vendor == "nvidia") { progress?.Report("Building llama-cpp-python from source with CUDA (first attempt)..."); if (await TrySourceBuildAsync(true, progress, ct)) return; Log("CUDA source build attempt failed; will try prebuilt CUDA wheels."); }
            string baseArgs = "-m pip install llama-cpp-python --prefer-binary --only-binary=:all:";
            if (vendor == "nvidia")
            {
                progress?.Report("Trying prebuilt CUDA wheels (cu121/cu122)..."); var args = baseArgs + " --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cu121"; await RunHiddenAsync(PythonExePath, args, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct)) { Log("CUDA cu121 wheel import succeeded"); return; }
                args = baseArgs + " --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cu122"; await RunHiddenAsync(PythonExePath, args, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct)) { Log("CUDA cu122 wheel import succeeded"); return; }
                Log("CUDA wheel installs failed, falling back to CPU wheels.");
            }
            progress?.Report("Installing CPU llama-cpp-python wheel..."); await RunHiddenAsync(PythonExePath, baseArgs, progress, ct, PythonWorkDir);
            if (await ImportTestAsync("llama_cpp", ct)) { Log("CPU wheel import succeeded"); return; }
            Log("Primary CPU wheel install failed, trying alternate wheel index"); await RunHiddenAsync(PythonExePath, baseArgs + " --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/", progress, ct, PythonWorkDir);
            if (await ImportTestAsync("llama_cpp", ct)) { Log("CPU wheel import succeeded from alternate index"); return; }
            Log("Trying direct wheel URLs"); var ok = await TryInstallDirectWheelAsync(progress, ct); if (ok) return;
            progress?.Report("Building llama-cpp-python from source (CPU fallback)..."); if (await TrySourceBuildAsync(false, progress, ct)) return;
            Log("All automated llama-cpp-python attempts failed; launching manual console."); await LaunchManualLlamaInstallAsync(progress, ct);
        }

        // ---------------- Generic process runner ----------------
        async Task RunHiddenAsync(string exe, string args, IProgress<string>? progress, CancellationToken ct, string workDir, bool waitForExit = true)
        {
            Log($"Exec: {exe} {args}"); 
            var psi = new ProcessStartInfo(exe, args) { WorkingDirectory = workDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            
            // Set Python environment variables for bundled and embedded Python
            if (IsBundledPython)
            {
                // Bundled Python311 - set to bundled location
                var bundledDir = Path.Combine(AppContext.BaseDirectory, "Python311");
                psi.Environment["PYTHONHOME"] = bundledDir;
                psi.Environment["PYTHONPATH"] = string.Join(";", new[] { 
                    Path.Combine(bundledDir, "Lib"), 
                    Path.Combine(bundledDir, "Lib", "site-packages"),
                    Path.Combine(bundledDir, "Scripts"),
                    Path.Combine(bundledDir, "DLLs")
                });
                Log($"Set PYTHONHOME={bundledDir} for bundled Python");
            }
            else if (!UsingSystemPython) 
            { 
                // Embedded Python in LocalAppData
                psi.Environment["PYTHONHOME"] = PythonDir; 
                psi.Environment["PYTHONPATH"] = string.Join(";", new[] { 
                    Path.Combine(PythonDir, "Lib"), 
                    Path.Combine(PythonDir, "Lib", "site-packages"), 
                    Path.Combine(PythonDir, "Scripts") 
                }); 
            }
            
            psi.Environment["LLAMA_CPP_FORCE_CPU"] = "1"; 
            using var proc = Process.Start(psi); 
            if (proc == null) throw new InvalidOperationException("Failed to start process.");
            if (!waitForExit) { Log("Not waiting for process to exit."); return; }
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(); 
            var stderrTask = proc.StandardError.ReadToEndAsync(); 
            await proc.WaitForExitAsync(ct); 
            var outText = await stdoutTask; 
            var errText = await stderrTask;
            if (!string.IsNullOrWhiteSpace(outText)) foreach (var line in outText.Split('\n').Take(200)) Log(line);
            if (!string.IsNullOrWhiteSpace(errText)) { Log("ERR BLOCK START"); foreach (var line in errText.Split('\n').Take(400)) Log("ERR: " + line); Log("ERR BLOCK END"); }
        }

        // ---------------- Full Python Installer Fallback ----------------
        async Task<bool> InstallFullPythonFallbackAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                progress?.Report("Downloading Python installer..."); 
                Log("Starting Python installation.");
                var installerPath = Path.Combine(RootDir, PythonInstallerExe);
                
                // Download installer if not already present
                if (!File.Exists(installerPath)) 
                { 
                    using var client = new HttpClient(); 
                    client.Timeout = TimeSpan.FromMinutes(5);
                    progress?.Report("Downloading Python 3.11.9 (~25MB)...");
                    using var resp = await client.GetAsync(PythonInstallerUrl, ct); 
                    resp.EnsureSuccessStatusCode(); 
                    await using var fs = File.Create(installerPath); 
                    await resp.Content.CopyToAsync(fs, ct); 
                    Log("Downloaded Python installer to: " + installerPath); 
                    progress?.Report("Download complete.");
                }
                else
                {
                    Log("Python installer already downloaded at: " + installerPath);
                }
                
                // Try silent installation first (fully automated, no user interaction)
                progress?.Report("Installing Python silently...");
                Log("Attempting silent Python installation with automatic PATH configuration.");
                
                // Silent install arguments:
                // /quiet = No UI, no prompts
                // InstallAllUsers=0 = Install for current user only (no admin needed)
                // PrependPath=1 = Add Python to PATH automatically
                // Include_pip=1 = Install pip
                // Include_test=0 = Skip tests
                var silentArgs = "/quiet InstallAllUsers=0 PrependPath=1 Include_pip=1 Include_test=0";
                
                var psi = new ProcessStartInfo(installerPath, silentArgs) 
                { 
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }; 
                
                using var proc = Process.Start(psi); 
                if (proc != null) 
                { 
                    progress?.Report("Installing Python... (this may take 1-2 minutes)");
                    await proc.WaitForExitAsync(ct); 
                    Log($"Silent Python installer exited with code: {proc.ExitCode}"); 
                    
                    // Exit code 0 = success
                    if (proc.ExitCode == 0)
                    {
                        Log("Silent installation completed successfully.");
                        
                        // Wait for PATH to refresh and detect Python
                        progress?.Report("Checking Python installation...");
                        for (int i = 0; i < 10; i++)
                        {
                            var newExe = await DetectSystemPythonAsync(ct); 
                            if (!string.IsNullOrEmpty(newExe)) 
                            { 
                                _pythonExe = newExe; 
                                Log("Python detected at: " + _pythonExe); 
                                progress?.Report("? Python installed successfully!"); 
                                return true; 
                            }
                            if (i < 9)
                            {
                                Log($"Python not detected yet (attempt {i + 1}/10), retrying in 1s...");
                                await Task.Delay(1000, ct);
                            }
                        }
                        
                        Log("Silent installation succeeded but Python not detected on PATH.");
                    }
                    else
                    {
                        Log($"Silent installation failed with exit code: {proc.ExitCode}");
                    }
                } 
                
                // If silent install failed, fall back to interactive installer
                Log("Silent installation failed or incomplete. Launching interactive installer as fallback.");
                progress?.Report("Opening Python installer... Please follow the prompts.");
                
                var interactivePsi = new ProcessStartInfo(installerPath) { UseShellExecute = true }; 
                using var interactiveProc = Process.Start(interactivePsi); 
                if (interactiveProc != null) 
                { 
                    progress?.Report("Waiting for Python installation to complete...");
                    await interactiveProc.WaitForExitAsync(ct); 
                    Log("Interactive installer exited with code: " + interactiveProc.ExitCode); 
                } 
                
                // Retry detection after interactive install
                progress?.Report("Checking if Python was installed...");
                for (int i = 0; i < 5; i++)
                {
                    var newExe = await DetectSystemPythonAsync(ct); 
                    if (!string.IsNullOrEmpty(newExe)) 
                    { 
                        _pythonExe = newExe; 
                        Log("Python detected at: " + _pythonExe); 
                        progress?.Report("Python installed successfully!"); 
                        return true; 
                    }
                    if (i < 4)
                    {
                        Log($"Python not detected on PATH yet (attempt {i + 1}/5), retrying in 2s...");
                        progress?.Report($"Waiting for PATH to refresh ({i + 1}/5)...");
                        await Task.Delay(2000, ct);
                    }
                }
                
                Log("Python installation failed: Python not detected on PATH after both installation attempts."); 
                progress?.Report("Python installation may have failed. Please restart the app and try again, OR manually install Python 3.11 from python.org and check 'Add to PATH'.");
                return false;
            }
            catch (Exception ex) 
            { 
                Log("Python installation error: " + ex.Message); 
                progress?.Report("Installation error: " + ex.Message);
                return false; 
            }
        }

        // ---------------- Manual Console ----------------
        async Task LaunchManualLlamaInstallAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (_manualLlamaWindowLaunched) { Log("Manual llama install window already launched; skipping."); return; }
            _manualLlamaWindowLaunched = true;
            try
            {
                progress?.Report("Opening console for manual llama-cpp-python install..."); progress?.Report("Run: python -m pip install llama-cpp-python then close."); Log("Launching interactive cmd.exe for manual llama-cpp-python installation");
                var cmdLine = $"\"{PythonExePath}\" -m pip install llama-cpp-python"; var psi = new ProcessStartInfo("cmd.exe", "/K " + cmdLine) { WorkingDirectory = PythonWorkDir, UseShellExecute = true }; Process.Start(psi);
                progress?.Report("If build errors occur install VS Build Tools (C++), CMake, then retry.");
            }
            catch (Exception ex) { Log("LaunchManualLlamaInstallAsync error: " + ex.Message); }
        }

        // ---------------- Build llama silently (no window) ----------------
        public async Task<bool> BuildLlamaInCmdAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                Log("Installing llama-cpp-python prebuilt wheel silently");
                
                // Detect GPU for optimized installation
                var gpuVendor = DetectGpuVendor();
                Log($"GPU detection result: {gpuVendor}");
                
                // Try installations in order based on GPU detection
                if (gpuVendor == "nvidia")
                {
                    Log("NVIDIA GPU detected - trying CUDA wheels first");
                    progress?.Report("Installing llama-cpp-python (NVIDIA CUDA)...");
                    
                    // Try CUDA 12.2 first
                    var cuda122Args = "-m pip install llama-cpp-python --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cu122 --prefer-binary";
                    await RunHiddenAsync(PythonExePath, cuda122Args, progress, ct, PythonWorkDir);
                    if (await ImportTestAsync("llama_cpp", ct))
                    {
                        Log("CUDA 12.2 wheel installation succeeded");
                        progress?.Report("? llama-cpp-python (CUDA) installed!");
                        return true;
                    }
                    
                    // Try CUDA 12.1
                    progress?.Report("Trying CUDA 12.1 wheels...");
                    var cuda121Args = "-m pip install llama-cpp-python --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cu121 --prefer-binary";
                    await RunHiddenAsync(PythonExePath, cuda121Args, progress, ct, PythonWorkDir);
                    if (await ImportTestAsync("llama_cpp", ct))
                    {
                        Log("CUDA 12.1 wheel installation succeeded");
                        progress?.Report("? llama-cpp-python (CUDA) installed!");
                        return true;
                    }
                    
                    Log("CUDA wheels failed, falling back to CPU wheels");
                }
                
                // Try CPU wheels (either no GPU or CUDA failed)
                progress?.Report("Installing llama-cpp-python (CPU)...");
                
                // Try official wheel repository
                var cpuArgs = "-m pip install llama-cpp-python --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/ --prefer-binary";
                await RunHiddenAsync(PythonExePath, cpuArgs, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct))
                {
                    Log("CPU wheel installation succeeded");
                    progress?.Report("? llama-cpp-python installed!");
                    return true;
                }
                
                // Try standard PyPI
                progress?.Report("Trying alternate source...");
                var pypiArgs = "-m pip install llama-cpp-python --prefer-binary";
                await RunHiddenAsync(PythonExePath, pypiArgs, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct))
                {
                    Log("PyPI wheel installation succeeded");
                    progress?.Report("? llama-cpp-python installed!");
                    return true;
                }
                
                // Try wheel-only (last resort)
                progress?.Report("Trying wheel-only installation...");
                var wheelOnlyArgs = "-m pip install llama-cpp-python --only-binary=:all:";
                await RunHiddenAsync(PythonExePath, wheelOnlyArgs, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct))
                {
                    Log("Wheel-only installation succeeded");
                    progress?.Report("? llama-cpp-python installed!");
                    return true;
                }
                
                Log("All silent llama installation attempts failed");
                progress?.Report("llama-cpp-python installation failed.");
                return false;
            }
            catch (Exception ex)
            {
                Log($"BuildLlamaInCmdAsync error: {ex.Message}");
                progress?.Report($"Installation error: {ex.Message}");
                return false;
            }
        }

        // ---------------- Package Install Verification ----------------
        async Task VerifyOrInstallPackagesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            var optional = new[] { "python-docx", "python-pptx", "pymupdf" }; foreach (var pkg in optional) await InstallOrVerifyPackageAsync(pkg, false, progress, ct);
        }
        async Task InstallOrVerifyPackageAsync(string pkg, bool required, IProgress<string>? progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(pkg, "llama-cpp-python", StringComparison.OrdinalIgnoreCase))
            {
                if (_llamaInstallAttempted) { Log("Skipping duplicate llama-cpp-python install attempt."); return; }
                _llamaInstallAttempted = true; var installed = await PipShowAsync(pkg); if (installed && await ImportTestAsync("llama_cpp", ct)) { Log("? llama-cpp-python already installed (import OK)."); progress?.Report("? llama-cpp-python installed."); return; }
                await InstallLlamaCppWithGpuAsync(progress, ct); installed = await PipShowAsync(pkg) && await ImportTestAsync("llama_cpp", ct);
                if (installed) { Log("? llama-cpp-python installed."); progress?.Report("? llama-cpp-python installed."); }
                else { Log("?? llama-cpp-python installation may have failed."); if (required) progress?.Report("Manual llama-cpp-python install required."); await LaunchManualLlamaInstallAsync(progress, ct); }
                return;
            }
            var wasInstalled = await PipShowAsync(pkg); if (wasInstalled) { Log($"? {pkg} already installed."); progress?.Report($"? {pkg} installed."); return; }
            Log($"?? Installing {pkg}..."); progress?.Report($"?? Installing {pkg}..."); await RunHiddenAsync(PythonExePath, $"-m pip install {pkg} --quiet", progress, ct, PythonWorkDir); var nowInstalled = await PipShowAsync(pkg);
            if (nowInstalled) { Log($"? {pkg} installed."); progress?.Report($"? {pkg} installed."); } else if (required) { Log($"?? {pkg} install failed."); progress?.Report($"?? {pkg} required; install failed."); }
        }
        void AdjustPthFile()
        {
            try
            {
                if (UsingSystemPython) return;
                var pthFile = Directory.GetFiles(PythonDir, "python*._pth").FirstOrDefault();
                if (pthFile == null) return;
                var lines = File.ReadAllLines(pthFile).ToList();
                void AddIfMissing(string val){ if(!lines.Any(l=>l.Trim().Equals(val,StringComparison.OrdinalIgnoreCase))) lines.Add(val); }
                AddIfMissing("Lib"); AddIfMissing("Lib/site-packages"); AddIfMissing("Scripts");
                if(!lines.Any(l=>l.Contains("import site"))) lines.Add("import site");
                File.WriteAllLines(pthFile, lines);
                Log("Updated _pth file for embed runtime");
            }
            catch (Exception ex){ Log("AdjustPthFile error: " + ex.Message); }
        }
    }
}
