using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Photobooth.Services;
using Photobooth.Utils;
using ReactiveUI;

namespace Photobooth.ViewModels;

public sealed class CaptureViewModel : ReactiveObject, IDisposable
{
    private readonly IPreviewService  _previewService;
    private readonly Action _onExit;
    private readonly CancellationTokenSource _cts = new();
    public string Mode { get; }

    // Etat video
    private Bitmap? _frame;

    public Bitmap? Frame
    {
        get => _frame;
        set => this.RaiseAndSetIfChanged(ref _frame, value);
    }
    
    //Compte à revours
    private int _remaining = 5;
    public int RemainingSeconds { get => _remaining; set => this.RaiseAndSetIfChanged(ref _remaining, value); }

    private bool _isCounting = true;
    public bool IsCounting  { get => _isCounting; set => this.RaiseAndSetIfChanged(ref _isCounting, value); }

    private bool _isFrozen;
    public bool IsFrozen { get => _isFrozen; set => this.RaiseAndSetIfChanged(ref _isFrozen, value); }
    public ReactiveCommand<Unit, Unit> StopCmd { get; }
    public ReactiveCommand<Unit, Unit> RestartCmd { get; }
    public ReactiveCommand<Unit, Unit> PrintCmd { get; }

    public CaptureViewModel(IPreviewService previewService, string mode, Action? onExit = null)
    {
        _previewService = previewService;
        Mode = mode;
        _onExit = onExit;

        _previewService.FrameReady += OnFrameReady;
        
        _previewService.Start();
        
        _ = RunCountdownAndFreezeAsync(_cts.Token);
        
        StopCmd = ReactiveCommand.Create(StopAndExit);
        RestartCmd = ReactiveCommand.Create(Restart);
        PrintCmd = ReactiveCommand.Create(Print);
    }

    private void OnFrameReady(Bitmap? bitmap)
    {
        if (IsFrozen) return;

        // Le service de preview peut réutiliser la même instance de WriteableBitmap
        // pour réduire les allocations. Dans ce cas, RaiseAndSetIfChanged ne déclenche
        // pas de notification (référence identique), donc on force le rafraîchissement UI.
        if (ReferenceEquals(_frame, bitmap))
        {
            this.RaisePropertyChanged(nameof(Frame));
            return;
        }

        Frame = bitmap;
    }

    private async Task RunCountdownAndFreezeAsync(CancellationToken token)
    {
        IsCounting = true;
        for (int t = 5; t >= 1; t--)
        {
            RemainingSeconds = t;
            try
            {
                await Task.Delay(1000, token);
            }
            catch
            {
                return;
            }
        }
        Freeze();
    }

    private void Freeze()
    {
        if (IsFrozen) return;
        IsFrozen = true;
        IsCounting = false;
        _previewService.Stop();
    }

    private void StopAndExit()
    {
        _cts.Cancel();
        _previewService.Stop();
        _onExit?.Invoke();
    }

    private void Restart()
    {
        RemainingSeconds = 5;
        IsFrozen = false;
        IsCounting = true;
        _previewService.Start();
        _ = RunCountdownAndFreezeAsync(_cts.Token);
        
    }
    
    private void Print()
    {
        var frames = new List<Bitmap>() { Frame };
        
        // coordonnées à adapter à ton template réel
        

        string output = TemplateRenderer.BuildFromTemplate(
            templatePath: TemplateRenderer.Simple,
            photos: frames,
            slots: TemplateRenderer.PhotoClassicSlots,
            outputFileName: $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
        );
        
        Console.WriteLine($"Photo sauvé : {output}");
        StopAndExit();
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _previewService.FrameReady -= OnFrameReady;
        _previewService.Dispose();
        _cts.Dispose();
    }
}
