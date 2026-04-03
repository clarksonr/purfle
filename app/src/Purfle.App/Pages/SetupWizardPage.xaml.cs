using Purfle.App.Services;

namespace Purfle.App.Pages;

public partial class SetupWizardPage : ContentPage
{
    private readonly AgentStore _store;
    private readonly MarketplaceService _marketplace;
    private int _currentStep = 1;
    private int _agentsInstalled;

    public SetupWizardPage(AgentStore store, MarketplaceService marketplace)
    {
        InitializeComponent();
        _store = store;
        _marketplace = marketplace;
        EnginePicker.SelectedIndexChanged += OnEngineChanged;
        EnginePicker.SelectedIndex = 0;
    }

    private void ShowStep(int step)
    {
        _currentStep = step;
        Step1.IsVisible = step == 1;
        Step2.IsVisible = step == 2;
        Step3.IsVisible = step == 3;
        Step4.IsVisible = step == 4;

        BackButton.IsVisible = step > 1;
        StepIndicator.Text = $"Step {step} of 4";

        NextButton.Text = step switch
        {
            1 => "Get Started",
            3 => "Skip",
            4 => "",
            _ => "Next",
        };
        NextButton.IsVisible = step < 4;

        if (step == 4)
            BuildSummary();
    }

    private void OnNext(object? sender, EventArgs e)
    {
        if (_currentStep == 2)
            SaveEngineChoice();

        if (_currentStep < 4)
            ShowStep(_currentStep + 1);
    }

    private void OnBack(object? sender, EventArgs e)
    {
        if (_currentStep > 1)
            ShowStep(_currentStep - 1);
    }

    private void OnEngineChanged(object? sender, EventArgs e)
    {
        bool isOllama = EnginePicker.SelectedIndex == 3;
        ApiKeyLabel.IsVisible = !isOllama;
        ApiKeyEntry.IsVisible = !isOllama;
        OllamaNote.IsVisible = isOllama;

        ApiKeyEntry.Placeholder = EnginePicker.SelectedIndex switch
        {
            0 => "sk-ant-...",
            1 => "sk-...",
            2 => "AIza...",
            _ => "",
        };
    }

    private void SaveEngineChoice()
    {
        var engineKey = EnginePicker.SelectedIndex switch
        {
            0 => "anthropic",
            1 => "openclaw",
            2 => "gemini",
            3 => "ollama",
            _ => "",
        };
        Preferences.Set("preferred_engine", engineKey);

        var apiKey = ApiKeyEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var storageKey = engineKey switch
            {
                "anthropic" => "anthropic_api_key",
                "openclaw"  => "openai_api_key",
                "gemini"    => "gemini_api_key",
                _           => null,
            };

            if (storageKey is not null)
            {
                SecureStorage.SetAsync(storageKey, apiKey).ConfigureAwait(false);

                var envVar = engineKey switch
                {
                    "anthropic" => "ANTHROPIC_API_KEY",
                    "openclaw"  => "OPENAI_API_KEY",
                    "gemini"    => "GEMINI_API_KEY",
                    _           => null,
                };
                if (envVar is not null)
                    Environment.SetEnvironmentVariable(envVar, apiKey);
            }
        }
    }

    private async void OnTestConnection(object? sender, EventArgs e)
    {
        TestResultLabel.Text = "";
        TestSpinner.IsVisible = true;
        TestSpinner.IsRunning = true;

        try
        {
            if (EnginePicker.SelectedIndex == 3)
            {
                // Ollama: check if server is reachable
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = await http.GetAsync("http://localhost:11434/api/tags");
                TestResultLabel.Text = resp.IsSuccessStatusCode ? "Ollama is running" : $"Ollama: HTTP {(int)resp.StatusCode}";
                TestResultLabel.TextColor = resp.IsSuccessStatusCode ? Colors.Green : Colors.Red;
                return;
            }

            var apiKey = ApiKeyEntry.Text?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                TestResultLabel.Text = "Enter an API key first.";
                TestResultLabel.TextColor = Colors.Orange;
                return;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            switch (EnginePicker.SelectedIndex)
            {
                case 0: // Anthropic
                    client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    var anthropicResp = await client.PostAsync(
                        "https://api.anthropic.com/v1/messages",
                        new StringContent(
                            """{"model":"claude-haiku-4-5-20251001","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""",
                            System.Text.Encoding.UTF8, "application/json"));
                    TestResultLabel.Text = anthropicResp.IsSuccessStatusCode ? "Connected to Anthropic" : $"Anthropic: HTTP {(int)anthropicResp.StatusCode}";
                    TestResultLabel.TextColor = anthropicResp.IsSuccessStatusCode ? Colors.Green : Colors.Red;
                    break;

                case 1: // OpenAI
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    var openaiResp = await client.PostAsync(
                        "https://api.openai.com/v1/chat/completions",
                        new StringContent(
                            """{"model":"gpt-4o-mini","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""",
                            System.Text.Encoding.UTF8, "application/json"));
                    TestResultLabel.Text = openaiResp.IsSuccessStatusCode ? "Connected to OpenAI" : $"OpenAI: HTTP {(int)openaiResp.StatusCode}";
                    TestResultLabel.TextColor = openaiResp.IsSuccessStatusCode ? Colors.Green : Colors.Red;
                    break;

                case 2: // Gemini
                    var geminiResp = await client.PostAsync(
                        $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}",
                        new StringContent(
                            """{"contents":[{"parts":[{"text":"hi"}]}],"generationConfig":{"maxOutputTokens":1}}""",
                            System.Text.Encoding.UTF8, "application/json"));
                    TestResultLabel.Text = geminiResp.IsSuccessStatusCode ? "Connected to Gemini" : $"Gemini: HTTP {(int)geminiResp.StatusCode}";
                    TestResultLabel.TextColor = geminiResp.IsSuccessStatusCode ? Colors.Green : Colors.Red;
                    break;
            }
        }
        catch (Exception ex)
        {
            TestResultLabel.Text = $"Error: {ex.Message}";
            TestResultLabel.TextColor = Colors.Red;
        }
        finally
        {
            TestSpinner.IsVisible = false;
            TestSpinner.IsRunning = false;
        }
    }

    private async void OnBrowseMarketplace(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SearchPage");
    }

    private async void OnInstallFromFile(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a .purfle bundle",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = [".purfle"],
                    [DevicePlatform.macOS] = ["purfle"],
                }),
            });

            if (result is null) return;

            string agentId;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(result.FullPath))
            {
                var entry = zip.GetEntry("agent.manifest.json")
                    ?? throw new InvalidOperationException("Bundle does not contain agent.manifest.json.");
                using var stream = entry.Open();
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
                agentId = doc.RootElement.GetProperty("id").GetString()
                          ?? throw new InvalidOperationException("Manifest has no 'id' field.");
            }

            _store.InstallBundle(agentId, result.FullPath);
            _agentsInstalled++;
            InstallStatusLabel.Text = $"Installed '{agentId}' successfully.";
            InstallStatusLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            InstallStatusLabel.Text = $"Error: {ex.Message}";
            InstallStatusLabel.TextColor = Colors.Red;
        }
    }

    private async void OnInstallFeatured(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string agentName) return;

        btn.IsEnabled = false;
        btn.Text = "Installing...";

        try
        {
            // Try marketplace first, then fall back to bundled dogfood manifests
            var manifestJson = await _marketplace.DownloadManifestAsync(agentName);
            if (manifestJson is null)
            {
                // Fall back to local dogfood manifests bundled with the app
                var localPath = Path.Combine(AppContext.BaseDirectory, "agents", agentName, "agent.manifest.json");
                if (File.Exists(localPath))
                    manifestJson = await File.ReadAllTextAsync(localPath);
            }

            if (manifestJson is null)
            {
                InstallStatusLabel.Text = $"Could not find '{agentName}' manifest.";
                InstallStatusLabel.TextColor = Colors.Orange;
                return;
            }

            _store.Install(agentName, manifestJson);
            _agentsInstalled++;
            btn.Text = "Installed";
            InstallStatusLabel.Text = $"'{agentName}' installed.";
            InstallStatusLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            InstallStatusLabel.Text = $"Error: {ex.Message}";
            InstallStatusLabel.TextColor = Colors.Red;
            btn.Text = "Install";
            btn.IsEnabled = true;
        }
    }

    private void BuildSummary()
    {
        var engine = EnginePicker.SelectedIndex switch
        {
            0 => "Anthropic (Claude)",
            1 => "OpenAI (GPT)",
            2 => "Google Gemini",
            3 => "Ollama (local)",
            _ => "None",
        };

        var installed = _store.ListInstalled();
        var lines = new List<string>
        {
            $"Engine: {engine}",
            $"Agents installed: {installed.Count}",
        };

        if (installed.Count > 0)
        {
            var names = string.Join(", ", installed.Select(a => a.Name));
            lines.Add($"({names})");
        }

        SummaryLabel.Text = string.Join("\n", lines);
    }

    private async void OnOpenDashboard(object? sender, EventArgs e)
    {
        Preferences.Set("setup_complete", true);
        await Shell.Current.GoToAsync("//MyAgentsPage");
    }
}
