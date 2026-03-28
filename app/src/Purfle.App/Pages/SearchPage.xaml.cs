using Purfle.App.Services;
using Purfle.Marketplace.Shared;

namespace Purfle.App.Pages;

public partial class SearchPage : ContentPage
{
    private readonly MarketplaceService _marketplace;

    public SearchPage(MarketplaceService marketplace)
    {
        InitializeComponent();
        _marketplace = marketplace;
    }

    private async void OnSearch(object? sender, EventArgs e)
    {
        var query = SearchBar.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        try
        {
            var result = await _marketplace.SearchAsync(query);
            ResultsView.ItemsSource = result.Agents;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Search failed: {ex.Message}", "OK");
        }
    }

    private async void OnAgentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AgentSearchResult agent)
        {
            ResultsView.SelectedItem = null;
            await Shell.Current.GoToAsync($"AgentDetailPage?agentId={agent.AgentId}");
        }
    }
}
