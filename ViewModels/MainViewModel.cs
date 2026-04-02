using System;
using System.Reactive;
using Photobooth.Models;
using Photobooth.Services;
using ReactiveUI;

namespace Photobooth.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private readonly Action<CaptureModel> _onSelect;
    private readonly IPrintQueueService _printQueueService;
    public string Title { get; } = "Choisis un format";

    public bool IsCaptureAccessEnabled => !_printQueueService.HasPendingTasks;
    public bool IsPrintQueueBusy => _printQueueService.HasPendingTasks;

    public ReactiveCommand<Unit, Unit> OptionACmd { get; }
    public ReactiveCommand<Unit, Unit> OptionBCmd { get; }

    public MainViewModel(Action<CaptureModel> onSelect, IPrintQueueService printQueueService)
    {
        _onSelect = onSelect;
        _printQueueService = printQueueService;
        _printQueueService.PropertyChanged += OnPrintQueueServicePropertyChanged;

        var canAccessCapture = this.WhenAnyValue(x => x.IsCaptureAccessEnabled);
        OptionACmd = ReactiveCommand.Create(() => _onSelect(CaptureModel.Classic), canAccessCapture);
        OptionBCmd = ReactiveCommand.Create(() => _onSelect(CaptureModel.Photostrip), canAccessCapture);
    }

    private void OnPrintQueueServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPrintQueueService.HasPendingTasks) ||
            e.PropertyName == nameof(IPrintQueueService.PendingTaskCount))
        {
            this.RaisePropertyChanged(nameof(IsCaptureAccessEnabled));
            this.RaisePropertyChanged(nameof(IsPrintQueueBusy));
        }
    }
}
