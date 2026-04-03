using Microsoft.Extensions.DependencyInjection;
using Purfle.App.Services;

namespace Purfle.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = new AppShell();
		var window = new Window(shell);

		shell.Navigated += async (s, e) =>
		{
			if (!Preferences.Get("setup_complete", false))
			{
				shell.Navigated -= null!; // one-shot
				await shell.GoToAsync("SetupWizardPage");
			}
		};

		return window;
	}

	protected override void OnAppLinkRequestReceived(Uri uri)
	{
		base.OnAppLinkRequestReceived(uri);

		if (uri.Scheme != "purfle") return;

		if (uri.Host == "install" || uri.AbsolutePath.TrimStart('/') == "install")
		{
			var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
			var agentId = query["id"];
			var bundleUrl = query["url"];

			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					if (!string.IsNullOrEmpty(agentId))
					{
						await HandleInstallById(agentId);
					}
					else if (!string.IsNullOrEmpty(bundleUrl))
					{
						await HandleInstallByUrl(bundleUrl);
					}
					else
					{
						await Shell.Current.DisplayAlertAsync("Purfle", "Invalid install link: no agent ID or URL provided.", "OK");
					}
				}
				catch (Exception ex)
				{
					await Shell.Current.DisplayAlertAsync("Install Error", ex.Message, "OK");
				}
			});
		}
	}

	private static async Task HandleInstallById(string agentId)
	{
		var marketplace = Shell.Current.Handler?.MauiContext?.Services.GetService<MarketplaceService>();
		if (marketplace is null)
		{
			await Shell.Current.DisplayAlertAsync("Error", "Marketplace service not available.", "OK");
			return;
		}

		var manifestJson = await marketplace.DownloadManifestAsync(agentId);
		if (manifestJson is null)
		{
			await Shell.Current.DisplayAlertAsync("Not Found", $"Agent \"{agentId}\" was not found in the marketplace.", "OK");
			return;
		}

		await Shell.Current.GoToAsync($"ConsentPage?manifestJson={Uri.EscapeDataString(manifestJson)}");
	}

	private static async Task HandleInstallByUrl(string bundleUrl)
	{
		using var http = new HttpClient();
		var manifestJson = await http.GetStringAsync(bundleUrl);

		if (string.IsNullOrWhiteSpace(manifestJson))
		{
			await Shell.Current.DisplayAlertAsync("Error", "Could not download manifest from the provided URL.", "OK");
			return;
		}

		await Shell.Current.GoToAsync($"ConsentPage?manifestJson={Uri.EscapeDataString(manifestJson)}");
	}
}
