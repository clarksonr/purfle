import { readFileSync, existsSync, readdirSync, statSync, writeFileSync } from "fs";
import { createHash } from "crypto";
import { join, relative } from "path";
import { parseManifest } from "@purfle/core";

interface PackOptions {
  output?: string;
}

export function packCommand(dir: string, options: PackOptions): void {
  const manifestPath = join(dir, "agent.json");
  if (!existsSync(manifestPath)) {
    console.error(`No agent.json found in '${dir}'.`);
    process.exit(1);
  }

  const manifestJson = readFileSync(manifestPath, "utf8");
  const manifest = parseManifest(manifestJson);

  if (!manifest.identity.signature) {
    console.error("Manifest is not signed. Run `purfle sign` first.");
    process.exit(1);
  }

  const outputName = options.output ?? `${manifest.id}-${manifest.version}.purfle`;
  const outputPath = join(dir, outputName);

  // Build ZIP using Node's built-in zlib + manual ZIP construction.
  // We create a simple ZIP containing all files in the agent directory.
  const files = collectFiles(dir);

  createZip(dir, files, outputPath);

  // Compute SHA-256 of the bundle
  const bundleData = readFileSync(outputPath);
  const sha256 = createHash("sha256").update(bundleData).digest("hex");

  console.log(`Packed ${manifest.name} v${manifest.version}`);
  console.log(`  Bundle: ${outputPath}`);
  console.log(`  Files:  ${files.length}`);
  console.log(`  SHA-256: ${sha256}`);

  // Write hash to a sidecar file for use during publish
  writeFileSync(outputPath + ".sha256", sha256 + "\n");
}

/** Recursively collect all files in a directory, excluding the output .purfle file. */
function collectFiles(dir: string, base?: string): string[] {
  base = base ?? dir;
  const files: string[] = [];
  for (const entry of readdirSync(dir)) {
    if (entry.endsWith(".purfle")) continue;
    if (entry === "node_modules" || entry === ".git") continue;
    const full = join(dir, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      files.push(...collectFiles(full, base));
    } else {
      files.push(relative(base, full));
    }
  }
  return files;
}

/**
 * Creates a ZIP file containing the listed files.
 * Uses a simple store-only ZIP (no compression needed — bundles are small).
 */
function createZip(baseDir: string, files: string[], outputPath: string): void {
  // Build ZIP in memory using the ZIP format spec (local file headers + central directory).
  const entries: { name: string; data: Buffer; offset: number }[] = [];
  const buffers: Buffer[] = [];
  let offset = 0;

  for (const relPath of files) {
    const data = readFileSync(join(baseDir, relPath));
    // Use forward slashes in ZIP paths (per spec)
    const name = relPath.replace(/\\/g, "/");
    const nameBytes = Buffer.from(name, "utf8");

    // Local file header (30 + name length + data length)
    const header = Buffer.alloc(30);
    header.writeUInt32LE(0x04034b50, 0);   // Local file header signature
    header.writeUInt16LE(20, 4);            // Version needed (2.0)
    header.writeUInt16LE(0, 6);             // General purpose bit flag
    header.writeUInt16LE(0, 8);             // Compression method (stored)
    header.writeUInt16LE(0, 10);            // Last mod file time
    header.writeUInt16LE(0, 12);            // Last mod file date
    header.writeUInt32LE(crc32(data), 14);  // CRC-32
    header.writeUInt32LE(data.length, 18);  // Compressed size
    header.writeUInt32LE(data.length, 22);  // Uncompressed size
    header.writeUInt16LE(nameBytes.length, 26); // File name length
    header.writeUInt16LE(0, 28);            // Extra field length

    entries.push({ name, data, offset });
    buffers.push(header, nameBytes, data);
    offset += 30 + nameBytes.length + data.length;
  }

  // Central directory
  const cdStart = offset;
  for (const entry of entries) {
    const nameBytes = Buffer.from(entry.name, "utf8");
    const cdHeader = Buffer.alloc(46);
    cdHeader.writeUInt32LE(0x02014b50, 0);   // Central directory header signature
    cdHeader.writeUInt16LE(20, 4);            // Version made by
    cdHeader.writeUInt16LE(20, 6);            // Version needed
    cdHeader.writeUInt16LE(0, 8);             // General purpose bit flag
    cdHeader.writeUInt16LE(0, 10);            // Compression method
    cdHeader.writeUInt16LE(0, 12);            // Last mod file time
    cdHeader.writeUInt16LE(0, 14);            // Last mod file date
    cdHeader.writeUInt32LE(crc32(entry.data), 16); // CRC-32
    cdHeader.writeUInt32LE(entry.data.length, 20); // Compressed size
    cdHeader.writeUInt32LE(entry.data.length, 24); // Uncompressed size
    cdHeader.writeUInt16LE(nameBytes.length, 28);  // File name length
    cdHeader.writeUInt16LE(0, 30);            // Extra field length
    cdHeader.writeUInt16LE(0, 32);            // File comment length
    cdHeader.writeUInt16LE(0, 34);            // Disk number start
    cdHeader.writeUInt16LE(0, 36);            // Internal file attributes
    cdHeader.writeUInt32LE(0, 38);            // External file attributes
    cdHeader.writeUInt32LE(entry.offset, 42); // Relative offset of local header

    buffers.push(cdHeader, nameBytes);
    offset += 46 + nameBytes.length;
  }

  const cdSize = offset - cdStart;

  // End of central directory record
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);        // EOCD signature
  eocd.writeUInt16LE(0, 4);                  // Disk number
  eocd.writeUInt16LE(0, 6);                  // Disk with central directory
  eocd.writeUInt16LE(entries.length, 8);      // Entries on this disk
  eocd.writeUInt16LE(entries.length, 10);     // Total entries
  eocd.writeUInt32LE(cdSize, 12);            // Size of central directory
  eocd.writeUInt32LE(cdStart, 16);           // Offset of central directory
  eocd.writeUInt16LE(0, 20);                 // Comment length

  buffers.push(eocd);

  writeFileSync(outputPath, Buffer.concat(buffers));
}

/** CRC-32 (IEEE) computation. */
function crc32(data: Buffer): number {
  let crc = 0xFFFFFFFF;
  for (let i = 0; i < data.length; i++) {
    crc ^= data[i];
    for (let j = 0; j < 8; j++) {
      crc = (crc >>> 1) ^ (crc & 1 ? 0xEDB88320 : 0);
    }
  }
  return (crc ^ 0xFFFFFFFF) >>> 0;
}
