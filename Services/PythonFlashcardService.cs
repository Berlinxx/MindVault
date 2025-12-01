using mindvault.Models;
using System.Diagnostics;
using System.Text.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace mindvault.Services;

public class PythonFlashcardService
{
    readonly PythonBootstrapper _bootstrapper;
    private DateTime _lastProgressReport = DateTime.MinValue;
    private const int PROGRESS_REPORT_THROTTLE_MS = 200; // Throttle at source
    
    public PythonFlashcardService(PythonBootstrapper bootstrapper) => _bootstrapper = bootstrapper;

    void Log(string msg)
    { try { File.AppendAllText(_bootstrapper.LogPath, $"[{DateTime.UtcNow:O}] {msg}\n"); } catch { } }

    public async Task<List<FlashcardItem>> GenerateAsync(string lessonText, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report("Preparing environment...");
        Log("Generation started");
        await _bootstrapper.EnsurePythonReadyAsync(progress, ct);

        if (!await CheckModuleAsync("llama_cpp"))
        { Log("llama_cpp still not importable after bootstrap"); progress?.Report("llama-cpp-python import failed. See run_log.txt"); return new List<FlashcardItem>(); }

        var workDir = _bootstrapper.PythonWorkDir;
        Directory.CreateDirectory(workDir);
        
        // Ensure model is in Models directory
        var modelsDir = Path.Combine(workDir, "Models");
        Directory.CreateDirectory(modelsDir);
        
        var lessonPath = Path.Combine(workDir, "lesson_input.txt");
        await File.WriteAllTextAsync(lessonPath, lessonText, ct);
        Log($"Wrote lesson_input.txt length={lessonText.Length}");

        var script = _bootstrapper.PrepareScriptToLocal();
        if (string.IsNullOrEmpty(script)) throw new FileNotFoundException("flashcard_ai.py missing");

        progress?.Report("Running AI model...");
        Log("Launching python process");
        var psi = new ProcessStartInfo
        {
            FileName = _bootstrapper.PythonExePath,
            Arguments = $"\"{script}\" \"{lessonPath}\"",
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (s, e) =>
        {
            if (e.Data is null) return;
            try { stdout.AppendLine(e.Data); } catch { }
            
            // Throttle progress reports to avoid flooding UI thread
            var now = DateTime.UtcNow;
            bool shouldReport = (now - _lastProgressReport).TotalMilliseconds >= PROGRESS_REPORT_THROTTLE_MS;
            
            // Always report special messages (TOTAL, DONE) immediately
            if (e.Data.Contains("::TOTAL::") || e.Data.Contains("::DONE::") || shouldReport)
            {
                _lastProgressReport = now;
                try { progress?.Report(e.Data); } catch { }
            }
        };
        proc.ErrorDataReceived += (s, e) =>
        {
            if (e.Data is null) return;
            try { stderr.AppendLine(e.Data); } catch { }
        };

        if (!proc.Start()) throw new InvalidOperationException("Python start failed.");
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try { await proc.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch { }
            throw;
        }

        var outText = stdout.ToString();
        var errText = stderr.ToString();
        Log("Python exited code=" + proc.ExitCode);
        if (!string.IsNullOrWhiteSpace(outText)) Log("STDOUT: " + (outText.Length > 800 ? outText[..800] + "..." : outText));
        if (!string.IsNullOrWhiteSpace(errText)) Log("STDERR: " + (errText.Length > 800 ? errText[..800] + "..." : errText));

        var outputPath = _bootstrapper.GetFlashcardsOutputPath();
        if (!File.Exists(outputPath)) { Log("flashcards.json not found"); progress?.Report("No flashcards produced (see log)"); return new List<FlashcardItem>(); }
        var json = await File.ReadAllTextAsync(outputPath, ct);
        Log("Read flashcards.json length=" + json.Length);
        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<FlashcardItem>>(json) ?? new();
            Log("Parsed cards count=" + items.Count);
            return items;
        }
        catch (Exception ex)
        { Log("JSON parse error: " + ex.Message); return new List<FlashcardItem>(); }
    }

    async Task<bool> CheckModuleAsync(string module)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _bootstrapper.PythonExePath,
                Arguments = $"-c \"import {module}\"",
                WorkingDirectory = _bootstrapper.PythonWorkDir,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
