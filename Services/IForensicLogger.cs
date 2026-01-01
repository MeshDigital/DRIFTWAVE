using System;

namespace SLSKDONET.Services;

/// <summary>
/// Interface for forensic logging with correlation tracking.
/// </summary>
public interface IForensicLogger
{
    void Debug(string correlationId, string stage, string message, string? trackId = null, object? data = null);
    void Info(string correlationId, string stage, string message, string? trackId = null, object? data = null);
    void Warning(string correlationId, string stage, string message, string? trackId = null, object? data = null);
    void Error(string correlationId, string stage, string message, string? trackId = null, Exception? ex = null, object? data = null);
    
    /// <summary>
    /// Starts a timed operation scope. Disposing the return value ends the scope and logs duration.
    /// </summary>
    IDisposable TimedOperation(string correlationId, string stage, string operation, string? trackId = null);
}
