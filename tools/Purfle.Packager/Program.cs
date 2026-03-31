using Purfle.Packager;

// ── Parse arguments ──────────────────────────────────────────────────────────

var packArgs = Args.Parse(Environment.GetCommandLineArgs()[1..]);

if (packArgs is null)
{
    Console.Error.WriteLine("""
        Usage:
          purfle-pack --manifest <path> --assembly <path> --output <path> [options]

        Required:
          --manifest <path>   Path to agent.manifest.json
          --assembly <path>   Path to the compiled agent DLL (will be stored as assemblies/agent.dll)

        Optional:
          --output <path>     Output .purfle bundle path (default: <manifest-dir>/<name>-<version>.purfle)
          --system-prompt <path>   Path to a system prompt .md file (stored as prompts/system.md)
          --deps <path>       Path to agent.deps.json (stored as assemblies/agent.deps.json)

        Example:
          purfle-pack \
            --manifest agents/chat.agent.json \
            --assembly agents/src/Purfle.Agents.Chat/bin/Release/net10.0/Purfle.Agents.Chat.dll \
            --output dist/chat-1.0.0.purfle
        """);
    return 1;
}

return Packager.Pack(packArgs);
