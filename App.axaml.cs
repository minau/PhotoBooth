using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Photobooth.Services;
using Photobooth.ViewModels;
using Photobooth.Views;
using Photomaton.Services;

namespace Photobooth;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigLoader.Load();
        
        var sharedVideoCapture = new SharedVideoCapture(0, 1280, 720, 30);
        var printQueueService = new PrintQueueService();
        try
        {
            sharedVideoCapture.WarmUp();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] [App] Camera warmup failed: {ex.Message}");
        }

        Func<IPreviewService> previewFactory = () => new LinuxOpenCvPreviewService(sharedVideoCapture, 0, 1280, 720, 30);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, _) =>
            {
                sharedVideoCapture.Dispose();
                printQueueService.Dispose();
            };
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(previewFactory, printQueueService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
