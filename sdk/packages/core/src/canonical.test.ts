import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { canonicalize } from "./canonical.js";

describe("canonicalize", () => {
  it("serializes primitives unchanged", () => {
    assert.equal(canonicalize(null), "null");
    assert.equal(canonicalize(42), "42");
    assert.equal(canonicalize(true), "true");
    assert.equal(canonicalize("hello"), '"hello"');
  });

  it("sorts object keys lexicographically", () => {
    const result = canonicalize({ z: 1, a: 2, m: 3 });
    const parsed = JSON.parse(result);
    const keys = Object.keys(parsed);
    assert.deepStrictEqual(keys, ["a", "m", "z"]);
  });

  it("sorts keys at every nesting level", () => {
    const result = canonicalize({ b: { z: 1, a: 2 }, a: { y: 3, x: 4 } });
    const parsed = JSON.parse(result) as Record<string, Record<string, number>>;
    assert.deepStrictEqual(Object.keys(parsed), ["a", "b"]);
    assert.deepStrictEqual(Object.keys(parsed.a), ["x", "y"]);
    assert.deepStrictEqual(Object.keys(parsed.b), ["a", "z"]);
  });

  it("produces no whitespace between tokens", () => {
    const result = canonicalize({ key: "value", arr: [1, 2] });
    const withoutStrings = result.replace(/"[^"]*"/g, '""');
    assert.ok(!/\s/.test(withoutStrings), `unexpected whitespace in: ${result}`);
  });

  it("handles arrays without reordering elements", () => {
    const result = canonicalize([3, 1, 2]);
    assert.equal(result, "[3,1,2]");
  });

  it("handles arrays of objects, sorting each object's keys", () => {
    const result = canonicalize([{ z: 1, a: 2 }, { y: 3, b: 4 }]);
    const parsed = JSON.parse(result) as Array<Record<string, number>>;
    assert.deepStrictEqual(Object.keys(parsed[0]), ["a", "z"]);
    assert.deepStrictEqual(Object.keys(parsed[1]), ["b", "y"]);
  });

  it("produces deterministic output for the same input", () => {
    const obj = { name: "agent", version: "1.0.0", id: "abc" };
    assert.equal(canonicalize(obj), canonicalize(obj));
  });

  it("two objects with same keys in different insertion order produce the same output", () => {
    const a = { z: 1, a: 2, m: 3 };
    const b = { a: 2, m: 3, z: 1 };
    assert.equal(canonicalize(a), canonicalize(b));
  });

  it("handles empty object and array", () => {
    assert.equal(canonicalize({}), "{}");
    assert.equal(canonicalize([]), "[]");
  });

  it("handles nested empty structures", () => {
    assert.equal(canonicalize({ a: {}, b: [] }), '{"a":{},"b":[]}');
  });

  it("escapes special characters in keys and values", () => {
    const result = canonicalize({ 'key"quote': 'val\nnewline' });
    const parsed = JSON.parse(result) as Record<string, string>;
    assert.equal(parsed['key"quote'], 'val\nnewline');
  });
});
