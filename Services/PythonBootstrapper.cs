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

        string RootDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindVault");
        string PythonDir => Path.Combine(RootDir, "Python");
        string EmbeddedPythonExe => Path.Combine(PythonDir, "python.exe");
        string SetupFlag => Path.Combine(RootDir, "setup_complete.txt");
        string LogFile => Path.Combine(RootDir, "run_log.txt");

        public string LogPath => LogFile;
        public string PythonWorkDir => UsingSystemPython ? Path.GetDirectoryName(_pythonExe!)! : PythonDir;
        public string RootPath => RootDir;

        string? _pythonExe;
        bool UsingSystemPython => _pythonExe != null && _pythonExe != EmbeddedPythonExe;

        void Log(string msg)
        {
            try { Directory.CreateDirectory(RootDir); File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] {msg}\n"); } catch { }
        }

        async Task<string?> DetectSystemPythonAsync(CancellationToken ct)
        {
            string[] cmds = { "python", "py" };
            foreach (var cmd in cmds)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) continue;
                    await proc.WaitForExitAsync(ct);
                    if (proc.ExitCode == 0)
                    {
                        // Resolve executable path
                        var pathProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = cmd,
                            Arguments = "-c \"import sys;print(sys.executable)\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        if (pathProc == null) continue;
                        var path = await pathProc.StandardOutput.ReadToEndAsync();
                        await pathProc.WaitForExitAsync(ct);
                        path = path.Trim();
                        if (File.Exists(path)) return path;
                    }
                }
                catch { }
            }
            return null;
        }

        public async Task EnsurePythonReadyAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(PythonDir);

            if (_pythonExe == null)
            {
                // New: detect any system python first regardless of pip availability
                _pythonExe = await DetectSystemPythonAsync(ct);
                if (_pythonExe != null)
                {
                    Log("? System Python found.");
                    progress?.Report("? System Python found.");
                }
            }

            if (_pythonExe == null)
            {
                // fallback to previous pip-capable detection
                _pythonExe = await LocateSystemPythonWithPipAsync(ct);
                if (_pythonExe != null)
                {
                    Log("? System Python found.");
                    progress?.Report("? System Python found.");
                }
            }

            if (_pythonExe == null)
            {
                _pythonExe = EmbeddedPythonExe;
                bool needsSetup = !File.Exists(EmbeddedPythonExe) || !File.Exists(SetupFlag);
                if (needsSetup)
                {
                    progress?.Report("Downloading Python runtime...");
                    Log("Starting embedded Python download");
                    await DownloadAndExtractAsync(progress, ct);
                    AdjustPthFile();
                    await EnsurePipAsync(progress, ct);
                    await InstallPackagesAsync(progress, ct);
                    File.WriteAllText(SetupFlag, DateTime.UtcNow.ToString("O"));
                    Log("? Portable Python installed in local app folder.");
                    progress?.Report("? Portable Python installed.");
                }
                else
                {
                    AdjustPthFile();
                    await EnsurePipAsync(progress, ct);
                    await VerifyOrInstallPackagesAsync(progress, ct);
                }
            }
            else
            {
                // System python path, ensure pip & packages
                await EnsurePipAsync(progress, ct);
                await VerifyOrInstallPackagesAsync(progress, ct);
            }

            await UpdatePipAsync(progress, ct);

            // Final verification & fallback attempts
            if (!await ImportTestAsync("llama_cpp", ct))
            {
                Log("Initial import llama_cpp failed. Attempting fallback versions.");
                string[] fallbackVersions = { "0.3.0", "0.2.82", "0.2.79" };
                foreach (var ver in fallbackVersions)
                {
                    if (!await HasPipAsync()) break;
                    Log($"Trying llama-cpp-python version {ver}");
                    await RunHiddenAsync(_pythonExe!, $"-m pip install --upgrade --force-reinstall --prefer-binary llama-cpp-python=={ver}", progress, ct, PythonWorkDir);
                    if (await ImportTestAsync("llama_cpp", ct))
                    {
                        Log($"llama_cpp import succeeded with version {ver}");
                        break;
                    }
                }
            }
            // New: verify required directories & files
            await EnsureResourceFilesAsync(progress, ct);

            // Final environment validation (Prompt Block 7)
            try
            {
                var scriptsDir = Path.Combine(PythonWorkDir, "Scripts");
                var modelsDir = Path.Combine(PythonWorkDir, "Models");
                var pyOk = !string.IsNullOrEmpty(_pythonExe) && File.Exists(_pythonExe!);
                var scriptOk = File.Exists(Path.Combine(scriptsDir, "flashcard_ai.py"));
                var modelOk = File.Exists(Path.Combine(modelsDir, "mindvault_qwen2_0.5b_q4_k_m.gguf"));
                var llamaOk = await ImportTestAsync("llama_cpp", ct);
                if (pyOk && scriptOk && modelOk && llamaOk)
                {
                    Log("? MindVault AI environment ready.");
                    progress?.Report("Setup complete! This is a one-time installation.");
                }
                else
                {
                    Log($"Environment validation incomplete: python={pyOk} script={scriptOk} model={modelOk} llama={llamaOk}");
                }
            }
            catch (Exception ex)
            {
                Log("Environment validation error: " + ex.Message);
            }
        }

        void AdjustPthFile()
        {
            try
            {
                var pthFile = Directory.GetFiles(PythonDir, "python*._pth").FirstOrDefault();
                if (pthFile == null) return;
                var lines = File.ReadAllLines(pthFile).ToList();
                bool modified = false;
                if (!lines.Any(l => l.Trim().Equals("Scripts", StringComparison.OrdinalIgnoreCase))) { lines.Add("Scripts"); modified = true; }
                if (!lines.Any(l => l.Contains("import site"))) { lines.Add("import site"); modified = true; }
                if (modified)
                {
                    File.WriteAllLines(pthFile, lines);
                    Log("Updated _pth file for embed runtime");
                }
            }
            catch (Exception ex) { Log("AdjustPthFile error: " + ex.Message); }
        }

        async Task<string?> LocateSystemPythonWithPipAsync(CancellationToken ct)
        {
            string[] candidates = { "python", "py" };
            foreach (var cmd in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "-m pip --version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) continue;
                    await proc.WaitForExitAsync(ct);
                    if (proc.ExitCode == 0)
                    {
                        var which = new ProcessStartInfo
                        {
                            FileName = cmd,
                            Arguments = "-c \"import sys;print(sys.executable)\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var p2 = Process.Start(which);
                        if (p2 == null) continue;
                        var path = await p2.StandardOutput.ReadToEndAsync();
                        await p2.WaitForExitAsync(ct);
                        path = path.Trim();
                        if (File.Exists(path)) return path;
                    }
                }
                catch { }
            }
            return null;
        }

        async Task EnsurePipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (await HasPipAsync()) { Log("pip already available"); return; }
            progress?.Report("Installing pip...");
            Log("pip missing - downloading get-pip.py");
            try
            {
                var getPipPath = Path.Combine(PythonDir, "get-pip.py");
                using var client = new HttpClient();
                using var resp = await client.GetAsync(GetPipUrl, ct);
                resp.EnsureSuccessStatusCode();
                await File.WriteAllTextAsync(getPipPath, await resp.Content.ReadAsStringAsync(ct), ct);
                Log("Downloaded get-pip.py");
                await RunHiddenAsync(_pythonExe!, $"\"{getPipPath}\"", progress, ct, PythonDir);
            }
            catch (Exception ex) { Log("Failed to install pip: " + ex.Message); }
        }

        async Task<bool> HasPipAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _pythonExe!,
                    Arguments = "-m pip --version",
                    WorkingDirectory = PythonWorkDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch { return false; }
        }

        async Task UpdatePipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                progress?.Report("Updating pip...");
                Log("Running ensurepip upgrade");
                await RunHiddenAsync(_pythonExe!, "-m ensurepip --upgrade", progress, ct, PythonWorkDir);
                Log("Upgrading pip itself");
                await RunHiddenAsync(_pythonExe!, "-m pip install --upgrade pip", progress, ct, PythonWorkDir);
            }
            catch (Exception ex)
            {
                Log("UpdatePipAsync error: " + ex.Message);
            }
        }

        async Task<bool> PipShowAsync(string pkg)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _pythonExe!,
                    Arguments = $"-m pip show {pkg}",
                    WorkingDirectory = PythonWorkDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch { return false; }
        }

        async Task<bool> ImportTestAsync(string module, CancellationToken ct)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _pythonExe!,
                    Arguments = $"-c \"import {module};import sys;print('IMPORT_OK')\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = PythonWorkDir
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync(ct);
                if (!string.IsNullOrWhiteSpace(stderr)) Log($"Import {module} stderr: {stderr.Split('\n').FirstOrDefault()}");
                if (!string.IsNullOrWhiteSpace(stdout)) Log($"Import {module} stdout: {stdout.Trim()}");
                return proc.ExitCode == 0 && stdout.Contains("IMPORT_OK");
            }
            catch (Exception ex)
            {
                Log($"ImportTest exception for {module}: {ex.Message}");
                return false;
            }
        }

        // Public helpers used by flashcard service
        public string PrepareScriptToLocal()
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
            if (!File.Exists(source)) { Log("flashcard_ai.py missing in output"); return string.Empty; }
            var dest = Path.Combine(PythonWorkDir, "flashcard_ai.py");
            File.Copy(source, dest, true);
            Log("Copied flashcard_ai.py to work dir");
            return dest;
        }
        public string GetLocalModelPath()
        {
            var shipped = ModelLocator.GetShippedModelPath();
            if (string.IsNullOrEmpty(shipped) || !File.Exists(shipped)) { Log("Model missing in shipped paths"); return string.Empty; }
            var modelsDir = Path.Combine(PythonWorkDir, "Models");
            Directory.CreateDirectory(modelsDir);
            var dest = Path.Combine(modelsDir, Path.GetFileName(shipped));
            if (!File.Exists(dest)) { File.Copy(shipped, dest, true); Log("Copied model to work dir Models"); }
            return dest;
        }
        public string GetFlashcardsOutputPath() => Path.Combine(PythonWorkDir, "flashcards.json");

        async Task EnsureResourceFilesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var work = PythonWorkDir;
                Directory.CreateDirectory(work);
                var scriptsDir = Path.Combine(work, "Scripts");
                var modelsDir = Path.Combine(work, "Models");
                var libDir = Path.Combine(work, "Lib");
                Directory.CreateDirectory(scriptsDir);
                Directory.CreateDirectory(modelsDir);
                Directory.CreateDirectory(libDir);

                // flashcard_ai.py
                var bundledScript = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
                var targetScript = Path.Combine(scriptsDir, "flashcard_ai.py");
                if (File.Exists(bundledScript) && !File.Exists(targetScript))
                {
                    File.Copy(bundledScript, targetScript, true);
                    Log("Copied flashcard_ai.py to Scripts directory");
                }
                // Ensure model copied
                var modelPath = GetLocalModelPath(); // copies into Models
                if (!string.IsNullOrEmpty(modelPath)) Log("Model verified: " + Path.GetFileName(modelPath));

                // Basic presence checks
                if (!File.Exists(targetScript)) Log("WARNING: flashcard_ai.py missing in Scripts");
                var modelFile = Path.Combine(modelsDir, "mindvault_qwen2_0.5b_q4_k_m.gguf");
                if (!File.Exists(modelFile)) Log("WARNING: model file missing in Models");

                Log("? Required files verified.");
                progress?.Report("? Required files verified.");
            }
            catch (Exception ex)
            {
                Log("Resource verification error: " + ex.Message);
            }
        }

        public async Task<bool> IsLlamaAvailableAsync(CancellationToken ct = default)
        {
            if (_pythonExe == null) return false;
            return await ImportTestAsync("llama_cpp", ct);
        }

        public async Task<bool> IsEnvironmentHealthyAsync(CancellationToken ct = default)
        {
            try
            {
                var pyOk = _pythonExe != null && File.Exists(_pythonExe);
                if (!pyOk)
                {
                    _pythonExe = await DetectSystemPythonAsync(ct) ?? (_pythonExe ?? EmbeddedPythonExe);
                    pyOk = _pythonExe != null && File.Exists(_pythonExe);
                }
                var scriptsDir = Path.Combine(PythonWorkDir, "Scripts");
                var modelsDir = Path.Combine(PythonWorkDir, "Models");
                var scriptRoot = Path.Combine(PythonWorkDir, "flashcard_ai.py");
                var scriptInScripts = Path.Combine(scriptsDir, "flashcard_ai.py");
                var scriptOk = File.Exists(scriptRoot) || File.Exists(scriptInScripts);
                var modelName = "mindvault_qwen2_0.5b_q4_k_m.gguf";
                var modelInModels = Path.Combine(modelsDir, modelName);
                var modelRoot = Path.Combine(PythonWorkDir, modelName);
                var modelOk = File.Exists(modelInModels) || File.Exists(modelRoot);
                var llamaOk = pyOk && await ImportTestAsync("llama_cpp", ct);
                return pyOk && scriptOk && modelOk && llamaOk;
            }
            catch { return false; }
        }

        public async Task<bool> QuickSystemPythonHasLlamaAsync(CancellationToken ct = default)
        {
            string[] cmds = { "python", "py" };
            foreach (var cmd in cmds)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "-c \"import llama_cpp,sys;print('OK');print(sys.executable)\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) continue;
                    var stdout = await proc.StandardOutput.ReadToEndAsync();
                    var stderr = await proc.StandardError.ReadToEndAsync();
                    await proc.WaitForExitAsync(ct);
                    if (proc.ExitCode == 0 && stdout.Contains("OK"))
                    {
                        // second line should be executable path
                        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        var exe = lines.Length > 1 ? lines.Last().Trim() : string.Empty;
                        if (File.Exists(exe))
                        {
                            _pythonExe = exe; // cache for later
                            Log($"Quick system python with llama detected: {_pythonExe}");
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        string DetectGpuVendor()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "none";
                // NVIDIA detection via nvidia-smi
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "-L",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(1500);
                        if (proc.ExitCode == 0 && output.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                            return "nvidia";
                    }
                }
                catch { }
                // Simple heuristic fallback (no WMI): read DISPLAY variable if any future logic added.
            }
            catch { }
            return "none"; // AMD / Intel not detected without WMI; fallback CPU
        }

        async Task InstallLlamaCppWithGpuAsync(IProgress<string>? progress, CancellationToken ct)
        {
            var vendor = DetectGpuVendor();
            Log($"GPU vendor detection: {vendor}");
            if (vendor == "nvidia")
            {
                progress?.Report("NVIDIA GPU detected. Installing CUDA-enabled llama-cpp-python...");
                Log("NVIDIA GPU detected. Installing CUDA-enabled llama-cpp-python wheel.");
                var args = "-m pip install llama-cpp-python --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cu121 --quiet --prefer-binary";
                await RunHiddenAsync(_pythonExe!, args, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct)) { Log("CUDA build import succeeded"); return; }
                args = "-m pip install llama-cpp-python --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cu122 --quiet --prefer-binary";
                await RunHiddenAsync(_pythonExe!, args, progress, ct, PythonWorkDir);
                if (await ImportTestAsync("llama_cpp", ct)) { Log("CUDA cu122 build import succeeded"); return; }
                Log("CUDA build failed, falling back to CPU build");
            }
            else
            {
                Log("No supported GPU found, using CPU-only build.");
            }
            progress?.Report("Installing CPU llama-cpp-python build...");
            await RunHiddenAsync(_pythonExe!, "-m pip install llama-cpp-python --quiet --prefer-binary", progress, ct, PythonWorkDir);
        }

        async Task InstallOrVerifyPackageAsync(string pkg, bool required, IProgress<string>? progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (pkg == "llama-cpp-python")
            {
                var installed = await PipShowAsync(pkg);
                if (installed && await ImportTestAsync("llama_cpp", ct))
                {
                    Log("? llama-cpp-python already installed (import OK).");
                    progress?.Report("? llama-cpp-python installed.");
                    return;
                }
                await InstallLlamaCppWithGpuAsync(progress, ct);
                installed = await PipShowAsync(pkg) && await ImportTestAsync("llama_cpp", ct);
                if (installed)
                { Log("? llama-cpp-python installed."); progress?.Report("? llama-cpp-python installed."); }
                else
                { Log("?? llama-cpp-python installation may have failed."); if (required) progress?.Report("?? llama-cpp-python required; install failed."); }
                return;
            }
            var wasInstalled = await PipShowAsync(pkg);
            if (wasInstalled)
            {
                Log($"? {pkg} already installed.");
                progress?.Report($"? {pkg} installed.");
                return;
            }
            string? wheel = null;
            try
            {
                var wheels = Directory.GetFiles(RootDir, "*.whl", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).StartsWith(pkg.Replace('-', '_'), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                wheel = wheels.FirstOrDefault();
            }
            catch { }
            Log($"?? Installing {pkg}...");
            progress?.Report($"?? Installing {pkg}...");
            string installArg = wheel != null ? $"-m pip install \"{wheel}\" --quiet" : $"-m pip install {pkg} --quiet";
            await RunHiddenAsync(_pythonExe!, installArg, progress, ct, PythonWorkDir);
            var nowInstalled = await PipShowAsync(pkg);
            if (nowInstalled)
            { Log($"? {pkg} installed."); progress?.Report($"? {pkg} installed."); }
            else if (required)
            { Log($"?? {pkg} install failed."); progress?.Report($"?? {pkg} required; install failed."); }
        }

        // RESTORED ORIGINAL SUPPORT METHODS
        async Task RunHiddenAsync(string exe, string args, IProgress<string>? progress, CancellationToken ct, string workDir)
        {
            Log($"Exec: {exe} {args}");
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["LLAMA_CPP_FORCE_CPU"] = "1"; // safe default
            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException("Failed to start process.");
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);
            var outText = await stdoutTask; var errText = await stderrTask;
            if (!string.IsNullOrWhiteSpace(outText)) Log(outText.Split('\n').FirstOrDefault() ?? outText);
            if (!string.IsNullOrWhiteSpace(errText)) Log("ERR: " + (errText.Split('\n').FirstOrDefault() ?? errText));
        }

        async Task<bool> PackageInstalledAsync(string pkg)
        {
            return await PipShowAsync(pkg);
        }

        async Task DownloadAndExtractAsync(IProgress<string>? progress, CancellationToken ct)
        {
            var zipPath = Path.Combine(RootDir, PythonEmbedZip);
            if (!File.Exists(zipPath))
            {
                using var client = new HttpClient();
                using var resp = await client.GetAsync(PythonDownloadUrl, ct);
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(zipPath);
                await resp.Content.CopyToAsync(fs, ct);
                Log("Downloaded Python embed zip");
            }
            progress?.Report("Extracting Python...");
            Log("Extracting Python zip");
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                var dest = Path.Combine(PythonDir, entry.FullName);
                if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, true);
            }
            Log("Extraction complete");
        }

        async Task InstallPackagesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            var pkgs = new[] { "llama-cpp-python", "PyMuPDF", "python-docx", "python-pptx", "tqdm" };
            foreach (var p in pkgs)
            {
                await InstallOrVerifyPackageAsync(p, p == "llama-cpp-python", progress, ct);
            }
        }

        async Task VerifyOrInstallPackagesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            var required = new[] { "llama-cpp-python" };
            var optional = new[] { "python-docx", "python-pptx", "pymupdf" };
            foreach (var pkg in required) await InstallOrVerifyPackageAsync(pkg, true, progress, ct);
            foreach (var pkg in optional) await InstallOrVerifyPackageAsync(pkg, false, progress, ct);
        }
    }
}
