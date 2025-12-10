using System.IO;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;
using SLSKDONET.Configuration;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Manages download jobs and orchestrates the download process.
/// </summary>
public class DownloadManager : IDisposable
{
    private readonly ILogger<DownloadManager> _logger;
    private readonly AppConfig _config;
    private readonly SoulseekAdapter _soulseek;
    private readonly FileNameFormatter _fileNameFormatter;
    private readonly ConcurrentDictionary<string, DownloadJob> _jobs = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Channel<DownloadJob> _jobChannel;
    private CancellationTokenSource _cts = new();

    public event EventHandler<DownloadJob>? JobUpdated;
    public event EventHandler<DownloadJob>? JobCompleted;

    public DownloadManager(
        ILogger<DownloadManager> logger,
        AppConfig config,
        SoulseekAdapter soulseek,
        FileNameFormatter fileNameFormatter)
    {
        _logger = logger;
        _config = config;
        _soulseek = soulseek;
        _fileNameFormatter = fileNameFormatter;
        _concurrencySemaphore = new SemaphoreSlim(_config.MaxConcurrentDownloads);
        _jobChannel = Channel.CreateUnbounded<DownloadJob>();
    }

    /// <summary>
    /// Enqueues a track for download.
    /// </summary>
    public DownloadJob EnqueueDownload(Track track, string? outputPath = null)
    {
        var job = new DownloadJob
        {
            Track = track,
            OutputPath = outputPath ?? Path.Combine(
                _config.DownloadDirectory!, // App.xaml.cs ensures this is not null
                FormatFilename(track)
            )
        };

        _jobs.TryAdd(job.Id, job);
        _logger.LogInformation("Enqueued download: {TrackId}", job.Id);
        
        // Post the job to the channel for processing
        _jobChannel.Writer.TryWrite(job);

        return job;
    }

    /// <summary>
    /// Starts the long-running task that processes jobs from the channel.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _logger.LogInformation("Download manager started. Waiting for jobs...");

        try
        {
            // Continuously read from the channel until it's completed.
            await foreach (var job in _jobChannel.Reader.ReadAllAsync(_cts.Token))
            {
                // Don't wait for the job to finish, just start it.
                // The semaphore will limit concurrency.
                _ = ProcessJobAsync(job, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Download manager processing loop cancelled.");
        }
        _logger.LogInformation("Download manager stopped.");
    }

    /// <summary>
    /// Processes a single download job.
    /// </summary>
    private async Task ProcessJobAsync(DownloadJob job, CancellationToken ct)
    {        
        await _concurrencySemaphore.WaitAsync(ct);
        try
        {
            // Download the file
            var progress = new Progress<double>(p =>
            {
                job.Progress = p;
                job.BytesDownloaded = (long?)((job.Track.Size ?? 0) * p);
                JobUpdated?.Invoke(this, job);
            });

            job.State = DownloadState.Downloading;
            job.StartedAt = DateTime.UtcNow;
            JobUpdated?.Invoke(this, job);

            var success = await _soulseek.DownloadAsync(
                job.Track.Username!,
                job.Track.Filename!,
                job.OutputPath!,
                job.Track.Size,
                progress,
                ct
            );

            job.Progress = 1.0;
            job.State = success ? DownloadState.Completed : DownloadState.Failed;
            job.CompletedAt = DateTime.UtcNow;

            if (!success)
                job.ErrorMessage = "Download failed";

            _logger.LogInformation("Job completed: {JobId} - {State}", job.Id, job.State);
        }
        catch (OperationCanceledException)
        {
            job.State = DownloadState.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogWarning("Job cancelled: {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            job.State = DownloadState.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Job error: {JobId}", job.Id);
        }
        finally
        {
            JobCompleted?.Invoke(this, job);
            _concurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Cancels all pending downloads.
    /// </summary>
    public void CancelAll()
    {
        _logger.LogInformation("Cancelling all downloads");
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        // Complete the channel to stop the processing loop.
        _jobChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Gets all download jobs.
    /// </summary>
    public IEnumerable<DownloadJob> GetJobs() => _jobs.Values;

    /// <summary>
    /// Gets a specific download job.
    /// </summary>
    public DownloadJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    /// <summary>
    /// Formats a track filename using the configured name format.
    /// </summary>
    private string FormatFilename(Track track)
    {
        var template = _config.NameFormat ?? "{artist} - {title}";
        var filename = _fileNameFormatter.Format(template, track);

        var ext = track.GetExtension();
        return string.IsNullOrEmpty(ext) ? filename : $"{filename}.{ext}";
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _jobChannel.Writer.TryComplete();
        _concurrencySemaphore?.Dispose();
    }
}
