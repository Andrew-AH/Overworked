using Avalonia.Threading;

namespace Overworked.App.ViewModels;

/// <summary>The message bar along the bottom. Errors and work in progress stay; confirmations fade after a few seconds.</summary>
public sealed class StatusViewModel : ObservableObject
{
    readonly DispatcherTimer _fade = new() { Interval = TimeSpan.FromSeconds(4) };
    string _text = "";
    bool _isError, _isVisible;

    public StatusViewModel() => _fade.Tick += (_, _) => { _fade.Stop(); IsVisible = false; };

    public string Text { get => _text; private set => Set(ref _text, value); }
    public bool IsError { get => _isError; private set => Set(ref _isError, value); }
    public bool IsVisible { get => _isVisible; private set => Set(ref _isVisible, value); }

    public void Show(string text) => Display(text, isError: false, fades: true);

    public void ShowProgress(string text) => Display(text, isError: false, fades: false);

    public void ShowError(string text) => Display(text, isError: true, fades: false);

    void Display(string text, bool isError, bool fades)
    {
        Text = text;
        IsError = isError;
        IsVisible = true;
        _fade.Stop();
        if (fades) _fade.Start();
    }
}
