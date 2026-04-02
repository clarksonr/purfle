using Purfle.App.ViewModels;

namespace Purfle.App.Pages;

public partial class MyAgentsPage : ContentPage
{
    public MyAgentsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainViewModel vm)
        {
            foreach (var card in vm.Agents)
                card.StartPolling(Dispatcher);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is MainViewModel vm)
        {
            foreach (var card in vm.Agents)
                card.StopPolling();
        }
    }

    private async void OnBrowseMarketplace(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SearchPage");
    }
}
