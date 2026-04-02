using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurflePet;

/// <summary>
/// Purfle Pet — IPC agent that reads state from pet.json, applies time-based
/// decay, responds with ASCII art and personality text, and handles feed/play
/// tool calls over stdin/stdout JSON-line protocol.
/// </summary>
public class Program
{
    private static readonly string StateDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "state");

    private static readonly string StatePath = Path.Combine(StateDir, "pet.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task Main(string[] args)
    {
        var state = await LoadState();
        state = ApplyDecay(state);
        state = ResolveMood(state);
        await SaveState(state);

        // If run with no stdin (non-interactive), print status and exit
        if (args.Length > 0 && args[0] == "--status")
        {
            PrintStatus(state);
            return;
        }

        // IPC loop: read JSON-line commands from stdin, write JSON-line responses to stdout
        Console.Error.WriteLine($"Purfle Pet started. Mood: {state.Mood}");
        using var reader = new StreamReader(Console.OpenStandardInput());

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break; // EOF

            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var command = JsonSerializer.Deserialize<IpcCommand>(line, JsonOpts);
                if (command == null) continue;

                state = await HandleCommand(command, state);
                state = ResolveMood(state);
                await SaveState(state);

                var response = new IpcResponse
                {
                    Type = "response",
                    Face = AsciiArt.ForMood(state.Mood),
                    Mood = state.Mood,
                    Hunger = state.Hunger,
                    Energy = state.Energy,
                    Experience = state.Experience,
                    Message = GetPersonalityMessage(state)
                };

                var json = JsonSerializer.Serialize(response, JsonOpts);
                Console.WriteLine(json);
                Console.Out.Flush();
            }
            catch (JsonException)
            {
                var error = JsonSerializer.Serialize(new { type = "error", message = "Invalid JSON" }, JsonOpts);
                Console.WriteLine(error);
                Console.Out.Flush();
            }
        }
    }

    private static void PrintStatus(PetState state)
    {
        Console.WriteLine();
        Console.WriteLine($"  {AsciiArt.ForMood(state.Mood)}");
        Console.WriteLine();
        Console.WriteLine($"  Name:       {state.Name}");
        Console.WriteLine($"  Mood:       {state.Mood}");
        Console.WriteLine($"  Hunger:     {state.Hunger}/100");
        Console.WriteLine($"  Energy:     {state.Energy}/100");
        Console.WriteLine($"  Experience: {state.Experience}");
        Console.WriteLine();
        Console.WriteLine($"  {GetPersonalityMessage(state)}");
        Console.WriteLine();
    }

    private static Task<PetState> HandleCommand(IpcCommand command, PetState state)
    {
        switch (command.Action?.ToLowerInvariant())
        {
            case "feed":
                state.Hunger = Math.Max(0, state.Hunger - 30);
                state.Experience += 5;
                break;

            case "play":
                state.Energy = Math.Max(0, state.Energy - 15);
                state.Hunger = Math.Min(100, state.Hunger + 10);
                state.Experience += 10;
                break;

            case "rest":
                state.Energy = Math.Min(100, state.Energy + 25);
                state.Experience += 3;
                break;

            case "state":
            case "status":
                // No mutation, just return current state
                break;

            default:
                // Unknown command, treat as interaction
                state.Experience += 1;
                break;
        }

        return Task.FromResult(state);
    }

    private static PetState ApplyDecay(PetState state)
    {
        var now = DateTime.UtcNow;
        if (state.LastUpdated == default)
        {
            state.LastUpdated = now;
            return state;
        }

        var elapsed = now - state.LastUpdated;
        var minutes = elapsed.TotalMinutes;

        // Hunger increases by 1 per 10 minutes
        state.Hunger = Math.Min(100, state.Hunger + (int)(minutes / 10));

        // Energy decreases by 1 per 15 minutes
        state.Energy = Math.Max(0, state.Energy - (int)(minutes / 15));

        state.LastUpdated = now;
        return state;
    }

    private static PetState ResolveMood(PetState state)
    {
        state.Mood = state switch
        {
            { Hunger: > 70 } => "hungry",
            { Energy: < 20 } => "sleepy",
            { Hunger: < 20, Energy: > 80 } => "excited",
            { Hunger: < 30, Energy: > 60 } => "happy",
            _ => "sad"
        };
        return state;
    }

    private static string GetPersonalityMessage(PetState state) => state.Mood switch
    {
        "happy"   => "Purfle is feeling great! Life is good!",
        "excited" => "Purfle is SO EXCITED! Let's do something fun!",
        "hungry"  => "Purfle's tummy is rumbling... feed me please!",
        "sleepy"  => "Purfle is getting drowsy... maybe a little nap?",
        "sad"     => "Purfle misses you... come play with me!",
        _         => "Purfle is here!"
    };

    private static async Task<PetState> LoadState()
    {
        if (!File.Exists(StatePath))
        {
            return new PetState
            {
                Name = "Purfle",
                Mood = "happy",
                Hunger = 50,
                Energy = 80,
                Experience = 0,
                Created = DateTime.Parse("2026-04-01T00:00:00Z").ToUniversalTime(),
                LastUpdated = DateTime.UtcNow
            };
        }

        var json = await File.ReadAllTextAsync(StatePath);
        return JsonSerializer.Deserialize<PetState>(json, JsonOpts) ?? throw new InvalidOperationException("Failed to deserialize pet state");
    }

    private static async Task SaveState(PetState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var json = JsonSerializer.Serialize(state, JsonOpts);
        await File.WriteAllTextAsync(StatePath, json);
    }
}

public class PetState
{
    public string Name { get; set; } = "Purfle";
    public string Mood { get; set; } = "happy";
    public int Hunger { get; set; } = 50;
    public int Energy { get; set; } = 80;
    public int Experience { get; set; } = 0;
    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class IpcCommand
{
    public string? Action { get; set; }
    public Dictionary<string, object>? Params { get; set; }
}

public class IpcResponse
{
    public string Type { get; set; } = "response";
    public string Face { get; set; } = "";
    public string Mood { get; set; } = "";
    public int Hunger { get; set; }
    public int Energy { get; set; }
    public int Experience { get; set; }
    public string Message { get; set; } = "";
}
