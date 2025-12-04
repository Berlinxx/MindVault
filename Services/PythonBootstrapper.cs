using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using mindvault.Services; // for ZipExtractionService
using System.IO.Compression;

namespace mindvault.Services
{
    public class PythonBootstrapper
    {
        const string BundledPythonZip = "python311.zip";
        string RootDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindVault");
        string PythonDir => Path.Combine(RootDir, "Python311");
        string LogFile => Path.Combine(RootDir, "run_log.txt");
        public string LogPath => LogFile;
        public string PythonWorkDir => Path.Combine(RootDir, "Runtime");
        public string PythonExePath => _pythonExe ?? Path.Combine(PythonDir, "python.exe");
        string? _pythonExe;
        bool IsBundledPython => _pythonExe != null && _pythonExe.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);
        bool _pythonChecked;
        void Log(string msg){ try{ Directory.CreateDirectory(RootDir); File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] {msg}\n"); }catch{} }

        // Resolve python.exe under LocalAppData, skipping venv shims
        string? FindEmbeddedPythonExeRecursive()
        {
            try
            {
                if (!Directory.Exists(PythonDir)) return null;
                string? best=null; int bestDepth=-1;
                foreach(var exe in Directory.EnumerateFiles(PythonDir, "python.exe", SearchOption.AllDirectories))
                {
                    var lower = exe.ToLowerInvariant();
                    if (lower.Contains(Path.DirectorySeparatorChar+"lib"+Path.DirectorySeparatorChar+"venv"+Path.DirectorySeparatorChar+"scripts"+Path.DirectorySeparatorChar+"nt"+Path.DirectorySeparatorChar))
                        continue;
                    var depth = exe.Count(c=>c==Path.DirectorySeparatorChar || c==Path.AltDirectorySeparatorChar);
                    if (depth>bestDepth){ best=exe; bestDepth=depth; }
                }
                var direct = Path.Combine(PythonDir, "python.exe");
                if (best==null && File.Exists(direct)) best=direct;
                return best;
            }
            catch{ return null; }
        }
        public string? ResolveExistingPythonPath(){ return FindEmbeddedPythonExeRecursive(); }
        public bool TryGetExistingPython(out string path)
        {
            var p = FindEmbeddedPythonExeRecursive();
            if (!string.IsNullOrEmpty(p) && File.Exists(p)){ _pythonExe=p; _pythonChecked=true; path=p; return true; }
            path=string.Empty; return false;
        }
        public bool IsSetupFlagPresent(){ try{ return File.Exists(Path.Combine(RootDir, "setup_complete.txt")); }catch{ return false; } }
        public void WriteSetupFlag(){ try{ Directory.CreateDirectory(RootDir); File.WriteAllText(Path.Combine(RootDir, "setup_complete.txt"), $"OK {DateTime.UtcNow:O}"); }catch{} }

        private async Task<bool> ExtractZipInlineAsync(string zipPath, string destinationPath, IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                if (!File.Exists(zipPath)) return false;
                if (Directory.Exists(destinationPath))
                {
                    var existingExe = ResolveExistingPythonPath();
                    if (!string.IsNullOrEmpty(existingExe) && File.Exists(existingExe)) return true;
                    try { Directory.Delete(destinationPath, true); } catch { }
                }
                Directory.CreateDirectory(destinationPath);
                
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipPath);
                    var totalEntries = archive.Entries.Count;
                    var extractedCount = 0;
                    
                    foreach (var entry in archive.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        
                        var path = Path.Combine(destinationPath, entry.FullName);
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        if (!string.IsNullOrEmpty(entry.Name)) entry.ExtractToFile(path, overwrite: true);
                        
                        extractedCount++;
                        var percentage = (int)((extractedCount / (double)totalEntries) * 100);
                        progress?.Report($"Extracting Python ({percentage}%)...");
                    }
                }, ct);
                
                var exe = ResolveExistingPythonPath();
                return !string.IsNullOrEmpty(exe) && File.Exists(exe);
            }
            catch { return false; }
        }

        async Task<bool> ExtractBundledPythonZipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var zipPath = Path.Combine(AppContext.BaseDirectory, BundledPythonZip);
                if (!File.Exists(zipPath)) { Log($"Bundled ZIP not found: {zipPath}"); return false; }
                var sizeMb = new FileInfo(zipPath).Length / (1024.0 * 1024.0);
                progress?.Report($"Extracting Python ({sizeMb:F0} MB)...");
                Log($"Extracting {zipPath} to {PythonDir}");
                var ok = await ExtractZipInlineAsync(zipPath, PythonDir, progress, ct);
                var exe = FindEmbeddedPythonExeRecursive();
                if (ok && !string.IsNullOrEmpty(exe) && File.Exists(exe))
                { _pythonExe = exe; _pythonChecked = true; Log("Bundled Python extraction complete"); return true; }
                Log("Bundled Python extraction failed");
                return false;
            }
            catch (Exception ex){ Log("ExtractBundledPythonZipAsync error: "+ex.Message); return false; }
        }

        public async Task EnsurePythonReadyAsync(IProgress<string>? progress=null, CancellationToken ct=default)
        {
            Directory.CreateDirectory(RootDir);
            var existing = FindEmbeddedPythonExeRecursive();
            if (!string.IsNullOrEmpty(existing))
            {
                _pythonExe = existing; _pythonChecked=true; Directory.CreateDirectory(PythonWorkDir);
                progress?.Report("? Using embedded Python");
            }
            else
            {
                // Try bundled zip - this is the ONLY source
                var extracted = await ExtractBundledPythonZipAsync(progress, ct);
                if (!extracted)
                {
                    // No fallback - throw error asking user to extract python311.zip
                    var zipPath = Path.Combine(AppContext.BaseDirectory, BundledPythonZip);
                    throw new InvalidOperationException(
                        $"Python runtime not found.\n\n" +
                        $"Please extract 'python311.zip' from the solution directory to:\n{AppContext.BaseDirectory}\n\n" +
                        $"Expected location: {zipPath}"
                    );
                }
            }
            // Ensure resources
            await EnsureResourceFilesAsync(progress, ct);
            WriteSetupFlag();
        }

        async Task EnsureResourceFilesAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var work = PythonWorkDir; Directory.CreateDirectory(work);
                var scriptsDir = Path.Combine(work, "Scripts"); var modelsDir = Path.Combine(work, "Models"); Directory.CreateDirectory(scriptsDir); Directory.CreateDirectory(modelsDir);
                var bundledScript = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
                var targetScript = Path.Combine(scriptsDir, "flashcard_ai.py"); if (File.Exists(bundledScript)) File.Copy(bundledScript, targetScript, true);
                const string modelName = "mindvault_qwen2_0.5b_q4_k_m.gguf"; var buildModel = Path.Combine(AppContext.BaseDirectory, "Models", modelName); var targetModel = Path.Combine(modelsDir, modelName); if (File.Exists(buildModel)) File.Copy(buildModel, targetModel, true);
                progress?.Report("Resources ready"); Log("? Required files verified.");
            }catch(Exception ex){ Log("Resource verification error: "+ex.Message); }
        }

        async Task RunHiddenAsync(string exe, string args, IProgress<string>? progress, CancellationToken ct, string workDir)
        {
            Log($"Exec: {exe} {args}");
            var psi = new ProcessStartInfo(exe, args){ WorkingDirectory=workDir, UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true };
            // Set env for LocalAppData embedded runtime or bundled
            if (IsBundledPython)
            {
                var bundledDir = Path.Combine(AppContext.BaseDirectory, "Python311");
                psi.Environment["PYTHONHOME"] = bundledDir;
                psi.Environment["PYTHONPATH"] = string.Join(";", new[]{ Path.Combine(bundledDir,"Lib"), Path.Combine(bundledDir,"Lib","site-packages"), Path.Combine(bundledDir,"Scripts"), Path.Combine(bundledDir,"DLLs") });
            }
            else
            {
                psi.Environment["PYTHONHOME"] = PythonDir;
                psi.Environment["PYTHONPATH"] = string.Join(";", new[]{ Path.Combine(PythonDir,"Lib"), Path.Combine(PythonDir,"Lib","site-packages"), Path.Combine(PythonDir,"Scripts"), Path.Combine(PythonDir,"DLLs") });
            }
            using var p = Process.Start(psi); if (p==null) throw new InvalidOperationException("Failed to start process.");
            var so = await p.StandardOutput.ReadToEndAsync(); var se = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(ct);
            if (!string.IsNullOrWhiteSpace(so)) foreach(var line in so.Split('\n').Take(200)) Log(line);
            if (!string.IsNullOrWhiteSpace(se)) { Log("ERR BLOCK START"); foreach(var line in se.Split('\n').Take(400)) Log("ERR: "+line); Log("ERR BLOCK END"); }
        }

        string DetectGpuVendor(){ try{ var psi=new ProcessStartInfo("nvidia-smi","-L"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}; using var p=Process.Start(psi); if(p!=null){ var outp=p.StandardOutput.ReadToEnd(); p.WaitForExit(1500); if(p.ExitCode==0 && outp.Contains("GPU",StringComparison.OrdinalIgnoreCase)) return "nvidia"; } }catch{} return "none"; }

        async Task<bool> TryInstallLocalLlamaWheelAsync(IProgress<string>? progress, CancellationToken ct)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var dirs = new[]{ baseDir, Path.Combine(baseDir, "Wheels"), Path.Combine(baseDir, "wheels"), Path.Combine(baseDir, "Wheels","cpu"), Path.Combine(baseDir, "Wheels","gpu"), Path.Combine(baseDir, "wheels","cpu"), Path.Combine(baseDir, "wheels","gpu") }.Where(Directory.Exists).ToList();
                foreach(var d in dirs) Log("Local wheel search dir: "+d);
                var wheels = dirs.SelectMany(d=>Directory.EnumerateFiles(d, "llama_cpp_python-*.whl", SearchOption.TopDirectoryOnly)).Where(f=>f.Contains("cp311")).ToList();
                if (wheels.Count==0) 
                {
                    Log("No local llama wheels found");
                    return false;
                }
                var gpu = DetectGpuVendor(); string? sel=null;
                if (gpu=="nvidia")
                {
                    sel = wheels.FirstOrDefault(w=>w.ToLowerInvariant().Contains(Path.DirectorySeparatorChar+"gpu"+Path.DirectorySeparatorChar) && (w.Contains("cu122")||w.Contains("cuda122")||w.Contains("cu121")||w.Contains("cuda121")))
                       ?? wheels.FirstOrDefault(w=>w.Contains("cu122")||w.Contains("cuda122")||w.Contains("cu121")||w.Contains("cuda121"));
                }
                if (sel==null){ sel = wheels.FirstOrDefault(w=>w.ToLowerInvariant().Contains(Path.DirectorySeparatorChar+"cpu"+Path.DirectorySeparatorChar) && !w.Contains("cuda") && !w.Contains("cu1")) ?? wheels.FirstOrDefault(w=>!w.Contains("cuda") && !w.Contains("cu1")); }
                if (sel==null) return false;
                progress?.Report($"Installing bundled {(gpu=="nvidia" && (sel.Contains("cu")||sel.Contains("cuda"))?"GPU":"CPU")} wheel...");
                await RunHiddenAsync(PythonExePath, $"-m pip install --no-index --no-deps --force-reinstall \"{sel}\"", progress, ct, PythonWorkDir);
                return await ImportTestAsync("llama_cpp", ct);
            }catch(Exception ex){ Log("TryInstallLocalLlamaWheelAsync error: "+ex.Message); return false; }
        }

        async Task<bool> ImportTestAsync(string module, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(PythonWorkDir);
                var psi = new ProcessStartInfo(PythonExePath, $"-c \"import {module};print('IMPORT_OK')\""){ UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true, WorkingDirectory=PythonWorkDir };
                using var p = Process.Start(psi); if (p==null) return false; var so = await p.StandardOutput.ReadToEndAsync(); var se = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(ct);
                if (!string.IsNullOrWhiteSpace(se)) Log("Import "+module+" stderr: "+se.Split('\n').FirstOrDefault());
                return p.ExitCode==0 && so.Contains("IMPORT_OK");
            }catch(Exception ex){ Log("ImportTest exception for "+module+": "+ex.Message); return false; }
        }

        async Task EnsurePipAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (await HasPipAsync()) return;
            
            // Try to find get-pip.py bundled in solution
            var baseDir = AppContext.BaseDirectory;
            var bundledGetPip = Path.Combine(baseDir, "get-pip.py");
            
            if (!File.Exists(bundledGetPip))
            {
                throw new InvalidOperationException(
                    "pip not found and 'get-pip.py' is not bundled.\n\n" +
                    "Please ensure 'get-pip.py' is included in the solution directory for offline pip installation."
                );
            }
            
            var pythonDir = PythonDir;
            Directory.CreateDirectory(pythonDir);
            var targetGetPip = Path.Combine(pythonDir, "get-pip.py");
            File.Copy(bundledGetPip, targetGetPip, true);
            
            await RunHiddenAsync(PythonExePath, $"\"{targetGetPip}\"", progress, ct, pythonDir);
        }
        
        async Task<bool> HasPipAsync(){ try{ var psi=new ProcessStartInfo(PythonExePath, "-m pip --version"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true,WorkingDirectory=PythonWorkDir}; using var p=Process.Start(psi); if(p==null) return false; await p.WaitForExitAsync(); return p.ExitCode==0; }catch{ return false; } }

        public async Task EnsureLlamaReadyAsync(IProgress<string>? progress, CancellationToken ct)
        {
            await EnsurePipAsync(progress, ct);
            if (await TryInstallLocalLlamaWheelAsync(progress, ct)) return;
            
            // No online fallback - must have local wheel
            throw new InvalidOperationException(
                "llama-cpp-python wheel not found.\n\n" +
                "Please ensure llama-cpp-python wheel files (.whl) are included in the 'Wheels' directory of the solution.\n\n" +
                "Expected locations:\n" +
                "- Wheels/cpu/llama_cpp_python-*-cp311-*.whl (for CPU)\n" +
                "- Wheels/gpu/llama_cpp_python-*-cp311-*.whl (for GPU)"
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
            // Python must exist, script and model present, and llama importable
            TryGetExistingPython(out var p);
            var pyOk = !string.IsNullOrEmpty(p) && File.Exists(p);
            var scriptOk = File.Exists(Path.Combine(PythonWorkDir, "Scripts", "flashcard_ai.py"));
            var modelOk = File.Exists(Path.Combine(PythonWorkDir, "Models", "mindvault_qwen2_0.5b_q4_k_m.gguf"));
            var llamaOk = pyOk && await ImportTestAsync("llama_cpp", ct);
            return pyOk && scriptOk && modelOk && llamaOk;
        }

        public async Task<bool> QuickSystemPythonHasLlamaAsync(CancellationToken ct = default)
        {
            // Only check LocalAppData python, no system Python fallback
            if (TryGetExistingPython(out var p) && File.Exists(p))
            {
                return await ImportTestAsync("llama_cpp", ct);
            }
            return false;
        }

        public async Task<bool> BuildLlamaInCmdAsync(IProgress<string>? progress, CancellationToken ct)
        {
            // Install from local wheel only - no online fallback
            if (await TryInstallLocalLlamaWheelAsync(progress, ct)) return true;
            
            throw new InvalidOperationException(
                "llama-cpp-python wheel not found.\n\n" +
                "Cannot install llama-cpp-python without bundled wheel files.\n\n" +
                "Please ensure llama-cpp-python wheel files (.whl) are included in the 'Wheels' directory."
            );
        }
    }
}
