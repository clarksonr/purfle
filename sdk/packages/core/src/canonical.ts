/**
 * Canonical JSON serialization.
 * Keys are sorted lexicographically at every level; no whitespace.
 * See Purfle spec §5.1.
 */
export function canonicalize(obj: unknown): string {
  if (obj === null || typeof obj !== "object") return JSON.stringify(obj);
  if (Array.isArray(obj)) {
    return "[" + obj.map(canonicalize).join(",") + "]";
  }
  const o = obj as Record<string, unknown>;
  const keys = Object.keys(o).sort();
  return "{" + keys.map((k) => JSON.stringify(k) + ":" + canonicalize(o[k])).join(",") + "}";
}
