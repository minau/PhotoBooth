using System;
using System.IO;
using ReactiveUI;
using System.Reactive;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Photobooth.Services;
using Photobooth.Utils;
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
        CurrentView = new MainViewModel(onSelect: option =>
        {
            var preview = _previewFactory();
            CurrentView = new CaptureViewModel(preview, option, onExit: ShowMain);
        });
    }

    /*
    private void SavePictures()
    {
        var outDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Photomaton");
        Directory.CreateDirectory(outDir);
        string file = Path.Combine(outDir, $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
        if (PreviewImage != null)
        {
            WriteableBitmap bm = (WriteableBitmap) PreviewImage;
            bm.SaveAsJpeg(file);
        }
    }
    */
}
