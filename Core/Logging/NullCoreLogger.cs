using System;

namespace mindvault.Core.Logging
{
    internal class NullCoreLogger : ICoreLogger
    {
        public void Info(string message, string? category = null) { }
        public void Warn(string message, string? category = null) { }
        public void Error(string message, Exception? ex = null, string? category = null) { }
    }
}
