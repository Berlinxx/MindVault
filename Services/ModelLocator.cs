using System;
using System.IO;

namespace mindvault.Services
{
    public static class ModelLocator
    {
        public static string? GetShippedModelPath()
        {
            const string modelName = "mindvault_qwen2_0.5b_q4_k_m.gguf"; // updated model name
            // 1. AppData path (installer placement)
            var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MindVault", modelName);
            if (File.Exists(localAppData)) return localAppData;

            // 2. Base directory (debug / portable)
            var baseDirModel = Path.Combine(AppContext.BaseDirectory, "Models", modelName);
            if (File.Exists(baseDirModel)) return baseDirModel;

            // 3. Fallback relative
            var relativeModel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", modelName);
            if (File.Exists(relativeModel)) return relativeModel;

            return null;
        }
    }
}
