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
        
        Func<IPreviewService> previewFactory = () => new LinuxOpenCvPreviewService(0, 1280, 720, 30);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(previewFactory),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}