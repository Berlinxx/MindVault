using System;

namespace mindvault.Core.Logging
{
    public interface ICoreLogger
    {
        void Info(string message, string? category = null);
        void Warn(string message, string? category = null);
        void Error(string message, Exception? ex = null, string? category = null);
    }
}
