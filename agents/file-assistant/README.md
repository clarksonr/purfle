# File Assistant Agent

A Purfle agent that reads, lists, searches, and summarizes local files.

## Capabilities

- **read_file** — reads the contents of a file in the workspace
- **list_directory** — lists files in a directory
- **search_files** — searches for files matching a glob pattern

## Workspace

The agent is sandboxed to the `./workspace` directory. Place files there for the agent to access.

## Running

```bash
# Start the MCP file tools server
cd tools/mcp-file-server && npm start

# Run the agent
purfle run agents/file-assistant/
```
