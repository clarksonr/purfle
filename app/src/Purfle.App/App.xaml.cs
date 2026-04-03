using Microsoft.Extensions.DependencyInjection;

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
}
