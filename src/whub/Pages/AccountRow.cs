using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Whub.Core;

namespace Whub.Pages;

public sealed class AccountRow(AccountRecord record, bool ticked) : INotifyPropertyChanged
{
    string _note = "Ready";
    ImageSource? _portrait = Portraits.Load(record.Id);
    bool _ticked = ticked;
    bool _dragging;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AccountRecord Record { get; } = record;
    public string Name => Record.Name;
    public string Initial => Record.Name[..Math.Min(1, Record.Name.Length)].ToUpperInvariant();

    public string Note { get => _note; set => Set(ref _note, value); }
    public ImageSource? Portrait { get => _portrait; set => Set(ref _portrait, value); }
    public bool Ticked { get => _ticked; set => Set(ref _ticked, value); }
    public bool Dragging { get => _dragging; set => Set(ref _dragging, value); }

    void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
