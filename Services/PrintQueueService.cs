using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace Photobooth.Services;

public sealed class PrintQueueService : ReactiveObject, IPrintQueueService, IDisposable
{
    private const int MaxQueueSize = 2;

    private readonly Queue<string> _jobs = new();
    private readonly object _sync = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private int _pendingTaskCount;
    public int PendingTaskCount
    {
        get => _pendingTaskCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _pendingTaskCount, value);
            this.RaisePropertyChanged(nameof(HasPendingTasks));
        }
    }

    public bool HasPendingTasks => PendingTaskCount > 0;

    public PrintQueueService()
    {
        _worker = Task.Run(ProcessQueueAsync);
    }

    public bool TryEnqueue(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        lock (_sync)
        {
            if (_jobs.Count >= MaxQueueSize)
            {
                return false;
            }

            _jobs.Enqueue(imagePath);
            PendingTaskCount++;
            _signal.Release();
            return true;
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            string? imagePath = null;
            lock (_sync)
            {
                if (_jobs.Count > 0)
                {
                    imagePath = _jobs.Dequeue();
                }
            }

            if (imagePath is null)
            {
                continue;
            }

            try
            {
                await ExecutePrintJobAsync(imagePath, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:O}] [PrintQueueService] Impression échouée: {ex.Message}");
            }
            finally
            {
                PendingTaskCount = Math.Max(PendingTaskCount - 1, 0);
            }
        }
    }

    private static Task ExecutePrintJobAsync(string imagePath, CancellationToken ct)
    {
        // TODO(dev): implémenter ici la logique métier d'impression :
        //  - connexion à l'imprimante
        //  - envoi du fichier image au spooler / driver
        //  - gestion des erreurs (offline, papier, timeout, etc.)
        Console.WriteLine($"[{DateTime.UtcNow:O}] [PrintQueueService] Job d'impression en attente d'implémentation pour: {imagePath}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _signal.Release();
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // no-op
        }

        _signal.Dispose();
        _cts.Dispose();
    }
}
