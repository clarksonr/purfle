using System.Text.Json;
using Purfle.Runtime.Ipc;

namespace Purfle.Runtime.Tests.Ipc;

public sealed class IpcProtocolTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ── IpcRequest serialization ─────────────────────────────────────────────

    [Fact]
    public void SerializeIpcRequest_ProducesExpectedJson()
    {
        var request = new IpcRequest
        {
            Type = "execute",
            Id = "req-001",
            Input = "Hello agent",
        };

        var json = JsonSerializer.Serialize(request);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("execute", root.GetProperty("type").GetString());
        Assert.Equal("req-001", root.GetProperty("id").GetString());
        Assert.Equal("Hello agent", root.GetProperty("input").GetString());
    }

    [Fact]
    public void DeserializeIpcRequest_FromJson_SetsAllFields()
    {
        const string json = """
            {
                "type": "execute",
                "id": "req-002",
                "input": "test input",
                "context": {
                    "conversationId": "conv-42"
                }
            }
            """;

        var request = JsonSerializer.Deserialize<IpcRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("execute", request.Type);
        Assert.Equal("req-002", request.Id);
        Assert.Equal("test input", request.Input);
        Assert.NotNull(request.Context);
        Assert.Equal("conv-42", request.Context.ConversationId);
    }

    [Fact]
    public void RoundTrip_IpcRequest_PreservesAllProperties()
    {
        var original = new IpcRequest
        {
            Type = "execute",
            Id = "req-round",
            Input = "round trip test",
            Context = new IpcContext
            {
                ConversationId = "conv-99",
            },
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<IpcRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Input, deserialized.Input);
        Assert.NotNull(deserialized.Context);
        Assert.Equal(original.Context.ConversationId, deserialized.Context.ConversationId);
    }

    // ── IpcResponse serialization ────────────────────────────────────────────

    [Fact]
    public void DeserializeIpcResponse_FromJson_SetsAllFields()
    {
        const string json = """
            {
                "type": "response",
                "id": "resp-001",
                "output": "Agent says hello",
                "done": true,
                "toolCalls": null
            }
            """;

        var response = JsonSerializer.Deserialize<IpcResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("response", response.Type);
        Assert.Equal("resp-001", response.Id);
        Assert.Equal("Agent says hello", response.Output);
        Assert.True(response.Done);
        Assert.Null(response.ToolCalls);
    }

    [Fact]
    public void RoundTrip_IpcResponse_WithToolCalls_PreservesAll()
    {
        var original = new IpcResponse
        {
            Type = "response",
            Id = "resp-rt",
            Output = "",
            Done = false,
            ToolCalls =
            [
                new IpcToolCall
                {
                    Id = "tc-1",
                    Tool = "read_file",
                    Arguments = new { path = "/tmp/test.txt" },
                },
                new IpcToolCall
                {
                    Id = "tc-2",
                    Tool = "http_get",
                    Arguments = new { url = "https://example.com" },
                },
            ],
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<IpcResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Output, deserialized.Output);
        Assert.Equal(original.Done, deserialized.Done);
        Assert.NotNull(deserialized.ToolCalls);
        Assert.Equal(2, deserialized.ToolCalls.Count);
        Assert.Equal("tc-1", deserialized.ToolCalls[0].Id);
        Assert.Equal("read_file", deserialized.ToolCalls[0].Tool);
        Assert.Equal("tc-2", deserialized.ToolCalls[1].Id);
        Assert.Equal("http_get", deserialized.ToolCalls[1].Tool);
    }

    // ── IpcToolCall serialization ────────────────────────────────────────────

    [Fact]
    public void SerializeIpcToolCall_ProducesExpectedJson()
    {
        var toolCall = new IpcToolCall
        {
            Id = "tc-ser",
            Tool = "write_file",
            Arguments = new { path = "/out/file.txt", content = "data" },
        };

        var json = JsonSerializer.Serialize(toolCall);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("tc-ser", root.GetProperty("id").GetString());
        Assert.Equal("write_file", root.GetProperty("tool").GetString());

        var args = root.GetProperty("arguments");
        Assert.Equal("/out/file.txt", args.GetProperty("path").GetString());
        Assert.Equal("data", args.GetProperty("content").GetString());
    }

    // ── IpcToolResult serialization ──────────────────────────────────────────

    [Fact]
    public void SerializeIpcToolResult_ProducesExpectedJson()
    {
        var toolResult = new IpcToolResult
        {
            Type = "toolResult",
            Id = "tr-001",
            Result = "file contents here",
        };

        var json = JsonSerializer.Serialize(toolResult);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("toolResult", root.GetProperty("type").GetString());
        Assert.Equal("tr-001", root.GetProperty("id").GetString());
        Assert.Equal("file contents here", root.GetProperty("result").GetString());
    }
}
