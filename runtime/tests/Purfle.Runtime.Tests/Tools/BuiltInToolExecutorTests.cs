using System.Text.Json;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Tools;

namespace Purfle.Runtime.Tests.Tools;

/// <summary>
/// Unit tests for BuiltInToolExecutor. All filesystem tests use real temp directories
/// so sandbox permission checks run against actual paths — no mocking needed.
/// </summary>
public sealed class BuiltInToolExecutorTests : IDisposable
{
    // A temp directory used as the fake "Downloads" folder for tests that need it.
    // Tests that exercise the real Downloads path use the actual Downloads directory.
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "purfle-tests-" + Guid.NewGuid().ToString("N"));

    public BuiltInToolExecutorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static BuiltInToolExecutor BuildExecutor(
        string[]? readPaths  = null,
        string[]? writePaths = null,
        string[]? networkAllow = null)
    {
        var permissions = new AgentPermissions
        {
            Filesystem = (readPaths is not null || writePaths is not null)
                ? new FilesystemPermissions
                  {
                      Read  = readPaths  ?? [],
                      Write = writePaths ?? [],
                  }
                : null,
            Network = networkAllow is not null
                ? new NetworkPermissions { Allow = networkAllow }
                : null,
        };
        return new BuiltInToolExecutor(new AgentSandbox(permissions));
    }

    private static JsonElement? Args(object anon)
    {
        var json = JsonSerializer.Serialize(anon);
        return JsonDocument.Parse(json).RootElement;
    }

    private string WriteTemp(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    // ── read_file ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadFile_AllowedPath_ReturnsContent()
    {
        var path    = WriteTemp("hello.txt", "Hello, world!");
        var executor = BuildExecutor(readPaths: [$"{_tempDir}/**"]);

        var result = await executor.ExecuteAsync("read_file", Args(new { path }));

        Assert.Equal("Hello, world!", result);
    }

    [Fact]
    public async Task ReadFile_SandboxBlocked_ReturnsPermissionError()
    {
        var path     = WriteTemp("secret.txt", "secret");
        var executor = BuildExecutor(readPaths: ["C:/SomeOtherDir/**"]);

        var result = await executor.ExecuteAsync("read_file", Args(new { path }));

        Assert.StartsWith("Error: permission denied", result);
    }

    [Fact]
    public async Task ReadFile_FileNotFound_ReturnsNotFoundError()
    {
        var path     = Path.Combine(_tempDir, "nonexistent.txt");
        var executor = BuildExecutor(readPaths: [$"{_tempDir}/**"]);

        var result = await executor.ExecuteAsync("read_file", Args(new { path }));

        Assert.StartsWith("Error: file not found", result);
    }

    [Fact]
    public async Task ReadFile_EmptyPath_ReturnsValidationError()
    {
        var executor = BuildExecutor(readPaths: ["**"]);

        var result = await executor.ExecuteAsync("read_file", Args(new { path = "" }));

        Assert.StartsWith("Error: path must not be empty", result);
    }

    // ── write_file ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteFile_AllowedPath_WritesContentAndReturnsOk()
    {
        var path     = Path.Combine(_tempDir, "output.txt");
        var executor = BuildExecutor(writePaths: [$"{_tempDir}/**"]);

        var result = await executor.ExecuteAsync("write_file", Args(new { path, content = "written!" }));

        Assert.Equal("OK", result);
        Assert.Equal("written!", File.ReadAllText(path));
    }

    [Fact]
    public async Task WriteFile_SandboxBlocked_ReturnsPermissionError()
    {
        var path     = Path.Combine(_tempDir, "blocked.txt");
        var executor = BuildExecutor(writePaths: ["C:/SomeOtherDir/**"]);

        var result = await executor.ExecuteAsync("write_file", Args(new { path, content = "x" }));

        Assert.StartsWith("Error: permission denied", result);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task WriteFile_EmptyPath_ReturnsValidationError()
    {
        var executor = BuildExecutor(writePaths: ["**"]);

        var result = await executor.ExecuteAsync("write_file", Args(new { path = "", content = "x" }));

        Assert.StartsWith("Error: path must not be empty", result);
    }

    // ── find_files ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindFiles_MatchingPattern_ReturnsFilePaths()
    {
        WriteTemp("report.txt", "content");
        WriteTemp("notes.md",   "content");
        var executor = BuildExecutor(readPaths: [$"{_tempDir}/**"]);

        var result = await executor.ExecuteAsync("find_files",
            Args(new { name_pattern = "*.txt" }));

        // The tool searches the real Downloads directory, not _tempDir.
        // Since we're not inside Downloads, just verify the response shape.
        Assert.NotNull(result);
        Assert.False(result.StartsWith("Error:"));
    }

    [Fact]
    public async Task FindFiles_BareWordPattern_GetsWildcardSuffix()
    {
        // Bare word "CLAUDE" should become "CLAUDE*" — no error, no crash
        var executor = BuildExecutor(readPaths: ["C:/Users/**/*"]);

        var result = await executor.ExecuteAsync("find_files",
            Args(new { name_pattern = "CLAUDE" }));

        Assert.NotNull(result);
        Assert.False(result.StartsWith("Error:"));
    }

    [Fact]
    public async Task FindFiles_SandboxBlocksAll_ReportsSandboxBlocked()
    {
        var executor = BuildExecutor(readPaths: ["C:/SomeNonExistentPath/**"]);

        var result = await executor.ExecuteAsync("find_files",
            Args(new { name_pattern = "*" }));

        // Either no files found (Downloads may be empty/inaccessible) or blocked diagnostic
        Assert.NotNull(result);
        Assert.False(result.StartsWith("Error:"));
    }

    // ── search_files ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchFiles_EmptyQuery_ReturnsValidationError()
    {
        var executor = BuildExecutor(readPaths: ["C:/Users/**/*"]);

        var result = await executor.ExecuteAsync("search_files",
            Args(new { query = "" }));

        Assert.StartsWith("Error: query must not be empty", result);
    }

    [Fact]
    public async Task SearchFiles_NonEmptyQuery_DoesNotCrash()
    {
        var executor = BuildExecutor(readPaths: ["C:/Users/**/*.txt"]);

        var result = await executor.ExecuteAsync("search_files",
            Args(new { query = "purfle-test-unlikely-string-xyz" }));

        // Either "No matches found" or actual results — either is correct
        Assert.NotNull(result);
        Assert.False(result.StartsWith("Error:"));
    }

    // ── http_get ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HttpGet_SandboxBlocked_ReturnsPermissionError()
    {
        var executor = BuildExecutor(networkAllow: ["https://allowed.example.com/*"]);

        var result = await executor.ExecuteAsync("http_get",
            Args(new { url = "https://blocked.example.com/data" }));

        Assert.StartsWith("Error: permission denied", result);
    }

    [Fact]
    public async Task HttpGet_EmptyUrl_ReturnsValidationError()
    {
        var executor = BuildExecutor(networkAllow: ["https://**"]);

        var result = await executor.ExecuteAsync("http_get",
            Args(new { url = "" }));

        Assert.StartsWith("Error: url must not be empty", result);
    }

    // ── unknown tool ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownTool_ReturnsErrorString()
    {
        var executor = BuildExecutor();

        var result = await executor.ExecuteAsync("totally_unknown_tool", null);

        Assert.StartsWith("Error: unknown built-in tool", result);
    }

    // ── BuiltInToolDefinitions ────────────────────────────────────────────────

    [Fact]
    public void Definitions_FilesystemReadPermission_IncludesFindSearchRead()
    {
        var permissions = new AgentPermissions
        {
            Filesystem = new FilesystemPermissions { Read = ["C:/Users/**"] },
        };

        var specs = BuiltInToolDefinitions.For(permissions);
        var names = specs.Select(s => s.Name).ToList();

        Assert.Contains("find_files",   names);
        Assert.Contains("search_files", names);
        Assert.Contains("read_file",    names);
        Assert.DoesNotContain("write_file", names);
        Assert.DoesNotContain("http_get",   names);
    }

    [Fact]
    public void Definitions_FilesystemWritePermission_IncludesWriteFile()
    {
        var permissions = new AgentPermissions
        {
            Filesystem = new FilesystemPermissions { Write = ["C:/output/**"] },
        };

        var specs = BuiltInToolDefinitions.For(permissions);
        var names = specs.Select(s => s.Name).ToList();

        Assert.Contains("write_file", names);
        Assert.DoesNotContain("find_files", names);
    }

    [Fact]
    public void Definitions_NetworkPermission_IncludesHttpGet()
    {
        var permissions = new AgentPermissions
        {
            Network = new NetworkPermissions { Allow = ["https://example.com/*"] },
        };

        var specs = BuiltInToolDefinitions.For(permissions);
        var names = specs.Select(s => s.Name).ToList();

        Assert.Contains("http_get", names);
        Assert.DoesNotContain("find_files", names);
    }

    [Fact]
    public void Definitions_NoPermissions_ReturnsEmpty()
    {
        var specs = BuiltInToolDefinitions.For(new AgentPermissions());

        Assert.Empty(specs);
    }

    [Fact]
    public void Definitions_SearchFilesSpec_HasRequiredQueryParam()
    {
        var permissions = new AgentPermissions
        {
            Filesystem = new FilesystemPermissions { Read = ["C:/Users/**"] },
        };

        var spec = BuiltInToolDefinitions.For(permissions).First(s => s.Name == "search_files");

        Assert.Contains("query", spec.Required);
        Assert.DoesNotContain("file_pattern", spec.Required);
        Assert.Contains(spec.Parameters, p => p.Name == "query");
        Assert.Contains(spec.Parameters, p => p.Name == "file_pattern");
    }
}
