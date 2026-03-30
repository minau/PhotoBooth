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

    private object? _currentView;

    public object? CurrentView
    {
        get => _currentView; 
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public MainWindowViewModel(Func<IPreviewService> previewFactory)
    {
        _previewFactory = previewFactory;
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
                    CurrentView = new CaptureViewModel(preview, captureModel.ToString(), onExit: ShowMain);
                    break;
                case CaptureModel.Photostrip:
                    CurrentView = new PhotostripViewModel(preview, onExit: ShowMain);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(captureModel), captureModel, null);
            }
        });
    }
}
