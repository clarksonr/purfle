using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Purfle.Runtime.Scheduling;

namespace Purfle.Desktop.Avalonia.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Scheduler _scheduler;

    public ObservableCollection<AgentCardViewModel> Agents { get; } = new();
    public bool HasAgents => Agents.Count > 0;
    public bool IsEmpty   => Agents.Count == 0;

    public ICommand RefreshCommand { get; }
    public ICommand SortCommand    { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(Scheduler scheduler)
    {
        _scheduler = scheduler;
        foreach (var runner in scheduler.Runners)
            Agents.Add(new AgentCardViewModel(runner));

        Agents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAgents));
            OnPropertyChanged(nameof(IsEmpty));
        };

        RefreshCommand = new RelayCommand(() =>
        {
            var existing = Agents.Select(a => a.Name).ToHashSet();
            foreach (var runner in _scheduler.Runners)
            {
                if (!existing.Contains(runner.Manifest.Name))
                    Agents.Add(new AgentCardViewModel(runner));
            }
        });

        SortCommand = new RelayCommand<string>(SortBy);
    }

    private void SortBy(string? criterion)
    {
        var sorted = criterion switch
        {
            "name"   => Agents.OrderBy(a => a.Name).ToList(),
            "status" => Agents.OrderByDescending(a => a.StatusText).ToList(),
            _        => Agents.ToList(),
        };

        Agents.Clear();
        foreach (var agent in sorted)
            Agents.Add(agent);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    public RelayCommand(Action<T?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
}
