namespace Purfle.App.Pages;

[QueryProperty(nameof(OutputPath), "outputPath")]
public partial class LogViewPage : ContentPage
{
    public string? OutputPath { get; set; }

    public LogViewPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLogAsync();
    }

    private async void OnRefresh(object? sender, EventArgs e) => await LoadLogAsync();

    private async Task LoadLogAsync()
    {
        if (string.IsNullOrEmpty(OutputPath)) return;
        var logPath = Path.Combine(OutputPath, "run.log");
        LogLabel.Text = File.Exists(logPath)
            ? await File.ReadAllTextAsync(logPath)
            : "(no log yet)";
        await LogScroll.ScrollToAsync(0, double.MaxValue, animated: false);
    }
}
