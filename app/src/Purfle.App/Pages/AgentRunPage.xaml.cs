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
    private readonly List<(string Role, string Text, string? Meta)> _conversationLog = [];

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
        ShowStatus("Loading agent...", busy: true);

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
        _conversationLog.Add(("system", welcome, null));
    }

    private async void OnSend(object? sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text) || _session is null) return;

        MessageEntry.Text = string.Empty;
        SetInputEnabled(false);
        AddBubble(text, isUser: true);
        _conversationLog.Add(("user", text, null));

        var startTime = DateTime.UtcNow;
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

        var elapsed = DateTime.UtcNow - startTime;
        var tokenInfo = $"{elapsed.TotalSeconds:F1}s | Turn {_session.TurnCount}";

        // Check for tool calls in the response (marked with [Tool: ...])
        var (mainReply, toolCalls) = ExtractToolCalls(reply);

        if (toolCalls.Count > 0)
            AddToolCallBubble(toolCalls);

        AddBubble(mainReply, isUser: false, meta: tokenInfo);
        _conversationLog.Add(("assistant", mainReply, tokenInfo));

        SetInputEnabled(true);
        MessageEntry.Focus();
        await ScrollToBottomAsync();
    }

    private async void OnExport(object? sender, EventArgs e)
    {
        if (_conversationLog.Count == 0) return;

        var lines = new List<string> { $"# Conversation Export — {DateTime.UtcNow:O}", "" };
        foreach (var (role, text, meta) in _conversationLog)
        {
            lines.Add($"## [{role}]" + (meta != null ? $" ({meta})" : ""));
            lines.Add(text);
            lines.Add("");
        }

        var content = string.Join(Environment.NewLine, lines);
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "aivm", "exports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"conversation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        await File.WriteAllTextAsync(path, content);
        await DisplayAlertAsync("Exported", $"Conversation saved to:\n{path}", "OK");
    }

    // ── Tool call parsing ────────────────────────────────────────────────────

    private static (string reply, List<string> toolCalls) ExtractToolCalls(string text)
    {
        var toolCalls = new List<string>();
        var lines = text.Split('\n');
        var replyLines = new List<string>();
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("[Tool:") || line.TrimStart().StartsWith("Tool call:"))
                toolCalls.Add(line.Trim());
            else
                replyLines.Add(line);
        }
        return (string.Join('\n', replyLines).Trim(), toolCalls);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void AddBubble(string text, bool isUser, string? meta = null)
    {
        var content = new VerticalStackLayout { Spacing = 2 };

        content.Add(new Label
        {
            Text      = text,
            FontSize  = 14,
            TextColor = isUser ? Colors.White : Colors.Black,
        });

        if (meta != null)
        {
            content.Add(new Label
            {
                Text      = meta,
                FontSize  = 10,
                TextColor = isUser ? Color.FromArgb("#CCCCEE") : Colors.Gray,
                HorizontalTextAlignment = isUser ? TextAlignment.End : TextAlignment.Start,
            });
        }

        var border = new Border
        {
            Content          = content,
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

    private void AddToolCallBubble(List<string> toolCalls)
    {
        var header = new Label
        {
            Text = $"Tool calls ({toolCalls.Count})",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666"),
        };

        var detail = new Label
        {
            Text = string.Join("\n", toolCalls),
            FontSize = 10,
            FontFamily = "Courier New",
            TextColor = Color.FromArgb("#888"),
            IsVisible = false,
        };

        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Add(header);
        stack.Add(detail);

        // Tap to toggle
        header.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => detail.IsVisible = !detail.IsVisible),
        });

        var border = new Border
        {
            Content          = stack,
            BackgroundColor  = Color.FromArgb("#F0F0F0"),
            HorizontalOptions = LayoutOptions.Start,
            MaximumWidthRequest = 360,
            Padding          = new Thickness(10, 6),
            StrokeShape      = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(8),
            },
            Stroke           = Color.FromArgb("#DDD"),
            StrokeThickness  = 1,
        };

        MessagesList.Add(border);
        _conversationLog.Add(("tools", string.Join("\n", toolCalls), null));
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
