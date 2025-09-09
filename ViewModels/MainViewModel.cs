using System;
using System.Reactive;
using Photobooth.Models;
using ReactiveUI;

namespace Photobooth.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private readonly Action<CaptureModel> _onSelect;
    public string Title { get; } = "Choisis un format";
    
    public ReactiveCommand<Unit, Unit> OptionACmd { get; }
    public ReactiveCommand<Unit, Unit> OptionBCmd { get; }

    public MainViewModel(Action<CaptureModel> onSelect)
    {
        _onSelect = onSelect;
        OptionACmd = ReactiveCommand.Create(() => _onSelect(CaptureModel.Classic));
        OptionBCmd = ReactiveCommand.Create(() => _onSelect(CaptureModel.Photostrip));
    }
}