namespace Core.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Structured diagnostic record captured by <see cref="IDiagnosticService"/>.
    /// </summary>
    public sealed class DiagnosticEntry
    {
        public string Context { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsError { get; }
        public DateTime TimestampUtc { get; }

        public DiagnosticEntry(string context, string message, Exception exception, bool isError)
        {
            Context = context ?? "Engine";
            Message = message ?? exception?.Message ?? string.Empty;
            Exception = exception;
            IsError = isError;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Engine Core diagnostic and fault-reporting service.
    /// Captures isolated event-bus subscriber exceptions, module lifecycle errors, and content validation warnings.
    /// </summary>
    public interface IDiagnosticService : IGameService
    {
        IReadOnlyList<DiagnosticEntry> Entries { get; }
        int ErrorCount { get; }
        int WarningCount { get; }

        void RecordWarning(string context, string message, bool logToUnityConsole = true);
        void RecordError(string context, string message, bool logToUnityConsole = true);
        void RecordException(string context, Exception exception, bool logToUnityConsole = true);
        void ClearEntries();
    }

    /// <summary>
    /// Default implementation of <see cref="IDiagnosticService"/>.
    /// </summary>
    public sealed class DiagnosticService : IDiagnosticService
    {
        private readonly List<DiagnosticEntry> _entries = new();
        private int _errorCount;
        private int _warningCount;

        public IReadOnlyList<DiagnosticEntry> Entries => _entries;
        public int ErrorCount => _errorCount;
        public int WarningCount => _warningCount;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            ClearEntries();
        }

        public void RecordWarning(string context, string message, bool logToUnityConsole = true)
        {
            _warningCount++;
            _entries.Add(new DiagnosticEntry(context, message, null, isError: false));
            if (logToUnityConsole)
            {
                Debug.LogWarning($"[{context}] {message}");
            }
        }

        public void RecordError(string context, string message, bool logToUnityConsole = true)
        {
            _errorCount++;
            _entries.Add(new DiagnosticEntry(context, message, null, isError: true));
            if (logToUnityConsole)
            {
                Debug.LogError($"[{context}] {message}");
            }
        }

        public void RecordException(string context, Exception exception, bool logToUnityConsole = true)
        {
            _errorCount++;
            _entries.Add(new DiagnosticEntry(context, exception?.Message, exception, isError: true));
            if (logToUnityConsole && exception != null)
            {
                Debug.LogError($"[{context}] Caught isolated exception: {exception.GetType().Name}: {exception.Message}");
            }
        }

        public void ClearEntries()
        {
            _entries.Clear();
            _errorCount = 0;
            _warningCount = 0;
        }
    }
}
