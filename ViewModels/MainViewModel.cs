using System;
using System.Reactive;
using ReactiveUI;

namespace Photobooth.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private readonly Action<string>? _onSelect;
    public string Title { get; } = "Choisis un format";
    
    public ReactiveCommand<Unit, Unit> OptionACmd { get; }
    public ReactiveCommand<Unit, Unit> OptionBCmd { get; }

    public MainViewModel(Action<string>? onSelect)
    {
        _onSelect = onSelect;
        OptionACmd = ReactiveCommand.Create(() => _onSelect("OptionA"));
        OptionBCmd = ReactiveCommand.Create(() => _onSelect("OptionB"));
    }
}