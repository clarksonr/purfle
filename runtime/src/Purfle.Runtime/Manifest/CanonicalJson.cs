using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Purfle.Runtime.Manifest;

/// <summary>
/// Produces canonical JSON for signing: all object keys in lexicographic order,
/// no whitespace, recursively. This is the deterministic byte sequence that is
/// signed and verified (see spec §5.1).
/// </summary>
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions s_noWhitespace = new() { WriteIndented = false };

    /// <summary>
    /// Serializes <paramref name="node"/> to canonical JSON bytes.
    /// </summary>
    public static byte[] Serialize(JsonNode? node)
    {
        var sorted = Sort(node);
        return JsonSerializer.SerializeToUtf8Bytes(sorted, s_noWhitespace);
    }

    /// <summary>
    /// Produces the canonical signing payload for a manifest: removes
    /// <c>identity.signature</c> then serializes to canonical form.
    /// </summary>
    public static byte[] ForSigning(string manifestJson)
    {
        var node = JsonNode.Parse(manifestJson)
            ?? throw new ArgumentException("Manifest JSON is not a JSON object.", nameof(manifestJson));

        if (node is JsonObject obj)
        {
            // Deep-clone so we don't mutate the caller's parse tree.
            var clone = (JsonObject)Sort(obj)!;
            if (clone["identity"] is JsonObject identity)
                identity.Remove("signature");
            return JsonSerializer.SerializeToUtf8Bytes(clone, s_noWhitespace);
        }

        throw new ArgumentException("Manifest JSON root must be an object.", nameof(manifestJson));
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj  => SortObject(obj),
        JsonArray  arr  => SortArray(arr),
        null            => null,
        _               => node.DeepClone(),   // JsonValue — leaf, copy as-is
    };

    private static JsonObject SortObject(JsonObject obj)
    {
        var result = new JsonObject();
        foreach (var key in obj.Select(kv => kv.Key).Order(StringComparer.Ordinal))
            result.Add(key, Sort(obj[key]));
        return result;
    }

    private static JsonArray SortArray(JsonArray arr)
    {
        var result = new JsonArray();
        foreach (var item in arr)
            result.Add(Sort(item));
        return result;
    }
}
