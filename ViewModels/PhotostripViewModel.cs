using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Photobooth.Services;
using Photobooth.Utils;
using ReactiveUI;

namespace Photobooth.ViewModels;

public sealed class PhotostripViewModel : ReactiveObject, IDisposable
{
    private readonly IPreviewService _previewService;
    private readonly Action _onExit;
    private readonly CancellationTokenSource _cts = new();
    
    public ObservableCollection<Bitmap?> Thumbnails { get; } = new(new Bitmap?[4]);
    
    private Bitmap? _photostripFrame1;
    public Bitmap? PhotostripFrame1
    {
        get => _photostripFrame1;
        set => this.RaiseAndSetIfChanged(ref _photostripFrame1, value);
    }
    
    private Bitmap? _photostripFrame2;
    public Bitmap? PhotostripFrame2
    {
        get => _photostripFrame2;
        set => this.RaiseAndSetIfChanged(ref _photostripFrame2, value);
    }
    
    private Bitmap? _photostripFrame3;
    public Bitmap? PhotostripFrame3
    {
        get => _photostripFrame3;
        set => this.RaiseAndSetIfChanged(ref _photostripFrame3, value);
    }
    
    private Bitmap? _photostripFrame4;
    public Bitmap? PhotostripFrame4
    {
        get => _photostripFrame4;
        set => this.RaiseAndSetIfChanged(ref _photostripFrame4, value);
    }

    private Bitmap? _frame;
    public Bitmap? Frame { get => _frame; set => this.RaiseAndSetIfChanged(ref _frame, value); }

    private int _remaining = 5;
    public int RemainingSeconds { get => _remaining; set => this.RaiseAndSetIfChanged(ref _remaining, value); }

    private int _step = 1;
    public int Step { get => _step; set => this.RaiseAndSetIfChanged(ref _step, value); }

    private bool _isCounting = true;
    public bool IsCounting  { get => _isCounting; set => this.RaiseAndSetIfChanged(ref _isCounting, value); }

    private bool _isFrozen;
    public bool IsFrozen { get => _isFrozen; set => this.RaiseAndSetIfChanged(ref _isFrozen, value); }

    private Bitmap? _finalPreview;
    public Bitmap? FinalPreview
    {
        get => _finalPreview;
        set => this.RaiseAndSetIfChanged(ref _finalPreview, value);
    }

    private bool _isFinalPreviewVisible;
    public bool IsFinalPreviewVisible
    {
        get => _isFinalPreviewVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isFinalPreviewVisible, value);
            this.RaisePropertyChanged(nameof(IsCameraPreviewVisible));
        }
    }

    public bool IsCameraPreviewVisible => !IsFinalPreviewVisible;
    
    public ReactiveCommand<Unit, Unit> CancelCmd { get; }
    public ReactiveCommand<Unit, Unit> RestartCmd { get; }
    public ReactiveCommand<Unit, Unit> PrintCmd { get; }

    public PhotostripViewModel(IPreviewService previewService, Action onExit)
    {
        _previewService = previewService;
        _onExit = onExit;

        _previewService.FrameReady += OnFrame;
        
        // lance la séquence automatiquement
        _ = RunSequenceAsync(_cts.Token);

        CancelCmd = ReactiveCommand.Create(CancelAndExit);
        RestartCmd = ReactiveCommand.Create(RestartSequence);
        PrintCmd = ReactiveCommand.Create(Print);
    }

    private void OnFrame(Bitmap? bmp)
    {
        if (IsFrozen) return;
        Frame = bmp;
    }

    private async Task RunSequenceAsync(CancellationToken ct)
    {
        _previewService.Start();
        IsFrozen = false;

        // Shot 1
        Step = 1;
        await LaunchCounter(ct);
        var thumb1 = FreezeAndCapture();
        await Dispatcher.UIThread.InvokeAsync(() => PhotostripFrame1 = thumb1);
        await RestartPreviewService(ct);
        Step = 2;
        await LaunchCounter(ct);
        var thumb2 = FreezeAndCapture();
        await Dispatcher.UIThread.InvokeAsync(() => PhotostripFrame2 = thumb2);
        await RestartPreviewService(ct);
        Step = 3;
        await LaunchCounter(ct);
        var thumb3 = FreezeAndCapture();
        await Dispatcher.UIThread.InvokeAsync(() => PhotostripFrame3 = thumb3);
        await RestartPreviewService(ct);
        Step = 4;
        await LaunchCounter(ct);
        var thumb4 = FreezeAndCapture();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PhotostripFrame4 = thumb4;
            BuildFinalPreview();
        });
    }

    private async Task LaunchCounter(CancellationToken ct)
    {
        IsCounting = true;
        // compte à rebours 5 → 1
        for (int t = 5; t >= 1; t--)
        {
            RemainingSeconds = t;
            try
            {
                await Task.Delay(1000, ct);
            }
            catch
            {
                return;
            }
        }
    }

    private async Task RestartPreviewService(CancellationToken ct)
    {
        // petite pause visuelle
        try
        {
            await Task.Delay(500, ct);
        }
        catch
        {
            return;
        }
        IsCounting = false;
        IsFrozen = false;
        _previewService.Start();
        // laisse 200ms pour que l’expo se cale
        try
        {
            await Task.Delay(200, ct);
        }
        catch
        {
            return;
        }
    }

    private Bitmap? FreezeAndCapture()
    {
        IsCounting = false;
        IsFrozen = true;
        _previewService.Stop();

        if (Frame is not null)
        {
            // Clone “propre” de la frame (pour conserver la vignette lorsque la source change)
            return CloneBitmap(Frame);
        }

        return null;
    }

    private static Bitmap CloneBitmap(Bitmap src)
    {
        using var ms = new MemoryStream();
        src.Save(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    private void CancelAndExit()
    {
        _cts.Cancel();
        _previewService.Stop();
        _onExit?.Invoke();
    }

    private void RestartSequence()
    {
        PhotostripFrame1 = null;
        PhotostripFrame2 = null;
        PhotostripFrame3 = null;
        PhotostripFrame4 = null;
        FinalPreview = null;
        IsFinalPreviewVisible = false;
        _ = RunSequenceAsync(_cts.Token);
    }

    private void BuildFinalPreview()
    {
        if (PhotostripFrame1 is null || PhotostripFrame2 is null || PhotostripFrame3 is null || PhotostripFrame4 is null)
            return;

        var frames = new List<Bitmap>() { PhotostripFrame1, PhotostripFrame2, PhotostripFrame3, PhotostripFrame4 };
        FinalPreview = TemplateRenderer.RenderToBitmap(
            templatePath: TemplateRenderer.Grid,
            photos: frames,
            slots: TemplateRenderer.PhotoStripSlots);
        IsFinalPreviewVisible = true;
    }

    private void Print()
    {
        var frames = new List<Bitmap>() { PhotostripFrame1, PhotostripFrame2, PhotostripFrame3, PhotostripFrame4 };

        string output = TemplateRenderer.BuildFromTemplate(
            templatePath: TemplateRenderer.Grid,
            photos: frames,
            slots: TemplateRenderer.PhotoStripSlots,
            outputFileName: $"photostrip_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
        );
        
        Console.WriteLine($"Photostrip sauvé : {output}");
        CancelAndExit();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _previewService.Stop();
        _previewService.FrameReady -= OnFrame;
        _previewService.Dispose();
    }
}
