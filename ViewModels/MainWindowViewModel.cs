using System;
using System.IO;
using ReactiveUI;
using System.Reactive;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Photobooth.Models;
using Photobooth.Services;
using Photobooth.Utils;
using Photobooth.Views;
using Photomaton.Services;

namespace Photobooth.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly Func<IPreviewService> _previewFactory;
    private readonly IPrintQueueService _printQueueService;

    private object? _currentView;

    public object? CurrentView
    {
        get => _currentView; 
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public MainWindowViewModel(Func<IPreviewService> previewFactory, IPrintQueueService printQueueService)
    {
        _previewFactory = previewFactory;
        _printQueueService = printQueueService;
        ShowMain();
    }

    private void ShowMain()
    {
        // Vue principale avec 2 boutons
        CurrentView = new MainViewModel(onSelect: captureModel =>
        {
            var preview = _previewFactory();
            switch (captureModel)
            {
                case CaptureModel.Classic:
                    CurrentView = new CaptureViewModel(preview, _printQueueService, captureModel.ToString(), onExit: ShowMain);
                    break;
                case CaptureModel.Photostrip:
                    CurrentView = new PhotostripViewModel(preview, _printQueueService, onExit: ShowMain);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(captureModel), captureModel, null);
            }
        }, _printQueueService);
    }
}
