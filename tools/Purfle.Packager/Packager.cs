using System.IO.Compression;
using System.Text.Json;
using Purfle.Runtime.Manifest;

namespace Purfle.Packager;

/// <summary>
/// Creates a <c>.purfle</c> bundle (zip archive) from a manifest and a compiled agent DLL.
///
/// Bundle layout:
/// <code>
/// my-agent.purfle
/// ├── agent.manifest.json
/// ├── assemblies/
/// │   ├── agent.dll
/// │   └── agent.deps.json   (optional)
/// ├── prompts/
/// │   └── system.md         (optional)
/// └── META-INF/
///     └── purfle.json       (build metadata)
/// </code>
///
/// <para>
/// <strong>Important:</strong> <c>Purfle.Sdk.dll</c> is never included in the bundle.
/// The AIVM resolves it from its own default ALC at load time.
/// </para>
/// </summary>
internal static class Packager
{
    private const string SdkDllName = "Purfle.Sdk.dll";

    internal static int Pack(Args args)
    {
        // ── Validate inputs ───────────────────────────────────────────────────

        if (!File.Exists(args.ManifestPath))
        {
            Console.Error.WriteLine($"[error] Manifest not found: {args.ManifestPath}");
            return 1;
        }

        if (args.AssemblyPath is not null && !File.Exists(args.AssemblyPath))
        {
            Console.Error.WriteLine($"[error] Assembly not found: {args.AssemblyPath}");
            return 1;
        }

        if (args.SystemPromptPath is not null && !File.Exists(args.SystemPromptPath))
        {
            Console.Error.WriteLine($"[error] System prompt file not found: {args.SystemPromptPath}");
            return 1;
        }

        if (args.DepsPath is not null && !File.Exists(args.DepsPath))
        {
            Console.Error.WriteLine($"[error] Deps file not found: {args.DepsPath}");
            return 1;
        }

        // ── Validate and read manifest ────────────────────────────────────────

        var manifestJson = File.ReadAllText(args.ManifestPath);
        var loader = new ManifestLoader();
        var parsed = loader.Load(manifestJson);

        if (!parsed.Success)
        {
            Console.Error.WriteLine($"[error] Manifest validation failed [{parsed.FailureReason}]: {parsed.FailureMessage}");
            return 1;
        }

        var manifest = parsed.Manifest!;
        Console.WriteLine($"[manifest] {manifest.Name} v{manifest.Version} ({manifest.Id})");
        Console.WriteLine($"[engine]   {manifest.Runtime.Engine}");

        // ── Refuse to include Purfle.Sdk.dll ────────────────────────────────

        if (args.AssemblyPath is not null)
        {
            var asmFileName = Path.GetFileName(args.AssemblyPath);
            if (asmFileName.Equals(SdkDllName, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"[error] Cannot package '{SdkDllName}' as the agent assembly. " +
                                        "The AIVM resolves Purfle.Sdk from its own ALC.");
                return 1;
            }
        }

        // ── Create bundle directory ───────────────────────────────────────────

        var outputPath = Path.GetFullPath(args.OutputPath);
        var outputDir  = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        // ── Write zip ────────────────────────────────────────────────────────

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        // agent.manifest.json
        zip.CreateEntryFromFile(args.ManifestPath, "agent.manifest.json", CompressionLevel.Optimal);
        Console.WriteLine("[pack]     agent.manifest.json");

        // assemblies/agent.dll (optional — manifest-only agents have no assembly)
        if (args.AssemblyPath is not null)
        {
            zip.CreateEntryFromFile(args.AssemblyPath, "assemblies/agent.dll", CompressionLevel.Optimal);
            Console.WriteLine($"[pack]     assemblies/agent.dll  ← {Path.GetFileName(args.AssemblyPath)}");
        }

        // assemblies/agent.deps.json (optional)
        if (args.DepsPath is not null)
        {
            zip.CreateEntryFromFile(args.DepsPath, "assemblies/agent.deps.json", CompressionLevel.Optimal);
            Console.WriteLine("[pack]     assemblies/agent.deps.json");
        }

        // prompts/system.md (optional)
        if (args.SystemPromptPath is not null)
        {
            zip.CreateEntryFromFile(args.SystemPromptPath, "prompts/system.md", CompressionLevel.Optimal);
            Console.WriteLine("[pack]     prompts/system.md");
        }

        // META-INF/purfle.json (build metadata)
        var meta = JsonSerializer.Serialize(new
        {
            purfle_packager = "1.0.0",
            packed_at       = DateTimeOffset.UtcNow.ToString("O"),
            agent_id        = manifest.Id,
            agent_name      = manifest.Name,
            agent_version   = manifest.Version,
            engine          = manifest.Runtime.Engine.ToString(),
        }, new JsonSerializerOptions { WriteIndented = true });

        var metaEntry = zip.CreateEntry("META-INF/purfle.json", CompressionLevel.Optimal);
        using (var metaStream = metaEntry.Open())
        using (var writer = new StreamWriter(metaStream))
            writer.Write(meta);

        Console.WriteLine("[pack]     META-INF/purfle.json");

        // ── Done ──────────────────────────────────────────────────────────────

        var size = new FileInfo(outputPath).Length;
        Console.WriteLine();
        Console.WriteLine($"[done] {outputPath}  ({size / 1024.0:F1} KB)");

        return 0;
    }
}
