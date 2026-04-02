import { mkdirSync, writeFileSync } from "fs";
import { join, dirname } from "path";

/**
 * Extracts a ZIP buffer to a directory.
 * Supports store-only (compression method 0) ZIPs — the format created by `purfle pack`.
 * Also supports deflate (method 8) via Node's built-in zlib.
 */
export function extractZip(data: Buffer, destDir: string): void {
  // Find the End of Central Directory record.
  let eocdOffset = -1;
  for (let i = data.length - 22; i >= 0; i--) {
    if (data.readUInt32LE(i) === 0x06054b50) {
      eocdOffset = i;
      break;
    }
  }
  if (eocdOffset < 0) throw new Error("Invalid ZIP: EOCD not found");

  const entryCount = data.readUInt16LE(eocdOffset + 10);
  const cdOffset = data.readUInt32LE(eocdOffset + 16);

  let pos = cdOffset;
  for (let i = 0; i < entryCount; i++) {
    if (data.readUInt32LE(pos) !== 0x02014b50) {
      throw new Error(`Invalid ZIP: bad central directory header at offset ${pos}`);
    }

    const compressionMethod = data.readUInt16LE(pos + 10);
    const compressedSize = data.readUInt32LE(pos + 20);
    const uncompressedSize = data.readUInt32LE(pos + 24);
    const nameLen = data.readUInt16LE(pos + 28);
    const extraLen = data.readUInt16LE(pos + 30);
    const commentLen = data.readUInt16LE(pos + 32);
    const localHeaderOffset = data.readUInt32LE(pos + 42);

    const name = data.subarray(pos + 46, pos + 46 + nameLen).toString("utf8");
    pos += 46 + nameLen + extraLen + commentLen;

    // Skip directory entries.
    if (name.endsWith("/")) continue;

    // Read from local file header to get actual data offset.
    const localNameLen = data.readUInt16LE(localHeaderOffset + 26);
    const localExtraLen = data.readUInt16LE(localHeaderOffset + 28);
    const dataOffset = localHeaderOffset + 30 + localNameLen + localExtraLen;

    let fileData: Buffer;
    if (compressionMethod === 0) {
      // Stored (no compression)
      fileData = data.subarray(dataOffset, dataOffset + compressedSize);
    } else if (compressionMethod === 8) {
      // Deflate
      const { inflateRawSync } = require("zlib");
      fileData = inflateRawSync(data.subarray(dataOffset, dataOffset + compressedSize));
    } else {
      throw new Error(`Unsupported compression method ${compressionMethod} for '${name}'`);
    }

    // Prevent path traversal.
    if (name.includes("..")) throw new Error(`ZIP path traversal attempt: ${name}`);

    const destPath = join(destDir, name);
    mkdirSync(dirname(destPath), { recursive: true });
    writeFileSync(destPath, fileData);
  }
}
