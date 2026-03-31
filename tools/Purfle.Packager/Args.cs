namespace Purfle.Packager;

internal sealed record Args(
    string ManifestPath,
    string? AssemblyPath,
    string OutputPath,
    string? SystemPromptPath,
    string? DepsPath)
{
    internal static Args? Parse(string[] argv)
    {
        string? manifest     = null;
        string? assembly     = null;
        string? output       = null;
        string? systemPrompt = null;
        string? deps         = null;

        for (int i = 0; i < argv.Length - 1; i++)
        {
            switch (argv[i])
            {
                case "--manifest":      manifest     = argv[++i]; break;
                case "--assembly":      assembly     = argv[++i]; break;
                case "--output":        output       = argv[++i]; break;
                case "--system-prompt": systemPrompt = argv[++i]; break;
                case "--deps":          deps         = argv[++i]; break;
            }
        }

        if (manifest is null)
            return null;

        // Derive default output path from manifest location.
        if (output is null)
        {
            var dir  = Path.GetDirectoryName(Path.GetFullPath(manifest)) ?? ".";
            output = Path.Combine(dir, Path.GetFileNameWithoutExtension(manifest) + ".purfle");
        }

        return new Args(manifest, assembly, output, systemPrompt, deps);
    }
}
