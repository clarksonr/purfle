using Purfle.App.Services;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Sessions;

namespace Purfle.App.Pages;

[QueryProperty(nameof(AgentId), "agentId")]
public partial class AgentRunPage : ContentPage
{
    private readonly AgentExecutorService _executor;

    private IInferenceAdapter? _adapter;
    private ConversationSession? _session;
    private bool _loaded;

    public string? AgentId { get; set; }

    public AgentRunPage(AgentExecutorService executor)
    {
        InitializeComponent();
        _executor = executor;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded || AgentId is null) return;
        _loaded = true;

        await LoadAgentAsync();
    }

    private async Task LoadAgentAsync()
    {
        ShowStatus("Loading agent…", busy: true);

        (IInferenceAdapter? adapter, string agentName, string description, string systemPrompt, string? error) result;
        try
        {
            result = await _executor.LoadAsync(AgentId!);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, busy: false, isError: true);
            return;
        }

        var (adapter, agentName, description, systemPrompt, error) = result;

        if (error is not null)
        {
            ShowStatus(error, busy: false, isError: true);
            return;
        }

        _adapter = adapter;
        _session = new ConversationSession(_adapter!, systemPrompt);

        Title = agentName;
        ShowChat();

        var welcome = string.IsNullOrWhiteSpace(description)
            ? $"Agent \"{agentName}\" loaded. Ask me anything."
            : $"{agentName}\n\n{description}";
        AddBubble(welcome, isUser: false);
    }

    private async void OnSend(object? sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text) || _session is null) return;

        MessageEntry.Text = string.Empty;
        SetInputEnabled(false);
        AddBubble(text, isUser: true);

        string reply;
        try
        {
            reply = await _session.SendAsync(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Purfle] AgentRunPage error:\n{ex}");
            reply = $"[Error: {ex.Message}]";
        }

        AddBubble(reply, isUser: false);
        SetInputEnabled(true);
        MessageEntry.Focus();
        await ScrollToBottomAsync();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void AddBubble(string text, bool isUser)
    {
        var label = new Label
        {
            Text      = text,
            FontSize  = 14,
            TextColor = isUser ? Colors.White : Colors.Black,
        };

        var border = new Border
        {
            Content          = label,
            BackgroundColor  = isUser ? Color.FromArgb("#5B5EA6") : Color.FromArgb("#E8E8E8"),
            HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
            MaximumWidthRequest = 360,
            Padding          = new Thickness(12, 8),
            StrokeShape      = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(12),
            },
            Stroke           = Colors.Transparent,
        };

        MessagesList.Add(border);
    }

    private async Task ScrollToBottomAsync()
    {
        if (MessagesList.Children.LastOrDefault() is View last)
            await MessagesScroll.ScrollToAsync(last, ScrollToPosition.End, animated: true);
    }

    private void ShowStatus(string message, bool busy, bool isError = false)
    {
        MessagesScroll.IsVisible = false;
        StatusView.IsVisible     = true;
        LoadingIndicator.IsRunning = busy;
        LoadingIndicator.IsVisible = busy;
        StatusLabel.Text      = message;
        StatusLabel.TextColor = isError ? Colors.Red : Colors.Gray;
    }

    private void ShowChat()
    {
        StatusView.IsVisible     = false;
        MessagesScroll.IsVisible = true;
        SetInputEnabled(true);
    }

    private void SetInputEnabled(bool enabled)
    {
        MessageEntry.IsEnabled = enabled;
        SendButton.IsEnabled   = enabled;
    }
}
