using Avalonia.Controls;
using Avalonia.Interactivity;
using Purfle.Desktop.Avalonia.ViewModels;

namespace Purfle.Desktop.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnRunNowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AgentCardViewModel vm })
            vm.RunNow();
    }
}
