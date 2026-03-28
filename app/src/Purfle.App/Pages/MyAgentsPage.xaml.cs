using Purfle.App.Services;

namespace Purfle.App.Pages;

public partial class MyAgentsPage : ContentPage
{
    private readonly AgentStore _store;

    public MyAgentsPage(AgentStore store)
    {
        InitializeComponent();
        _store = store;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshList();
    }

    private void RefreshList()
    {
        AgentsList.ItemsSource = _store.ListInstalled();
    }

    private async void OnRemove(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string agentId)
        {
            var confirm = await DisplayAlertAsync("Remove Agent",
                $"Remove locally installed agent '{agentId}'?", "Remove", "Cancel");
            if (!confirm) return;

            _store.Uninstall(agentId);
            RefreshList();
        }
    }
}
