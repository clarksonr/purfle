using Purfle.App.Services;

namespace Purfle.App.Pages;

[QueryProperty(nameof(AgentId), "agentId")]
public partial class AgentDetailPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private readonly AgentStore _store;

    public string AgentId { get; set; } = "";

    public AgentDetailPage(MarketplaceService marketplace, AgentStore store)
    {
        InitializeComponent();
        _marketplace = marketplace;
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAgent();
    }

    private async Task LoadAgent()
    {
        if (string.IsNullOrEmpty(AgentId)) return;

        try
        {
            var detail = await _marketplace.GetAgentAsync(AgentId);
            if (detail is null)
            {
                await DisplayAlertAsync("Not Found", "Agent not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            AgentName.Text = detail.Name;
            PublisherLabel.Text = $"by {detail.PublisherName}";
            DescriptionLabel.Text = detail.Description;
            VersionsList.ItemsSource = detail.Versions;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnInstall(object? sender, EventArgs e)
    {
        InstallButton.IsEnabled = false;
        InstallButton.Text = "Installing...";

        try
        {
            var manifestJson = await _marketplace.DownloadManifestAsync(AgentId);
            if (manifestJson is null)
            {
                await DisplayAlertAsync("Error", "Failed to download agent.", "OK");
                return;
            }

            var path = _store.Install(AgentId, manifestJson);
            await DisplayAlertAsync("Installed", $"Agent installed to:\n{path}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            InstallButton.IsEnabled = true;
            InstallButton.Text = "Install Latest";
        }
    }
}
