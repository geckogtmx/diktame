#!/usr/bin/env node
// One-shot: read plans/screenshot-manifest.json, copy + resize each raw PNG into
// website/public/images/docs/. Produces a 1x full-width .png and a 720px @sm.png.
// Size budget: warn over 250KB, fail over 400KB.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '..', '..');
const manifestPath = path.join(repoRoot, 'plans', 'screenshot-manifest.json');
const outDir = path.join(repoRoot, 'website', 'public', 'images', 'docs');

const WARN_BYTES = 250 * 1024;
const FAIL_BYTES = 400 * 1024;
const SMALL_WIDTH = 720;

async function loadSharp() {
  try {
    const mod = await import('sharp');
    return mod.default;
  } catch {
    console.error('sharp is not installed. Run: cd website && npm install --save-dev sharp');
    process.exit(1);
  }
}

function humanBytes(n) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / 1024 / 1024).toFixed(2)} MB`;
}

async function main() {
  const sharp = await loadSharp();
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'));
  const sourceRoot = manifest.source;
  const files = manifest.files;

  if (!fs.existsSync(sourceRoot)) {
    console.error(`Source directory not found: ${sourceRoot}`);
    process.exit(1);
  }
  fs.mkdirSync(outDir, { recursive: true });

  const results = { ok: 0, warn: 0, fail: 0, missing: 0 };
  const failures = [];

  for (const [raw, finalName] of Object.entries(files)) {
    const srcPath = path.join(sourceRoot, raw);
    if (!fs.existsSync(srcPath)) {
      console.warn(`  MISSING   ${raw}`);
      results.missing++;
      continue;
    }

    const baseName = finalName.replace(/\.png$/, '');
    const outFull = path.join(outDir, `${baseName}.png`);
    const outSmall = path.join(outDir, `${baseName}@sm.png`);

    // Full: re-encode as optimized PNG (adaptive palette if possible, else lossless).
    await sharp(srcPath)
      .png({ compressionLevel: 9, palette: true, effort: 10 })
      .toFile(outFull);

    // Small: 720px wide, same optimization.
    await sharp(srcPath)
      .resize({ width: SMALL_WIDTH, withoutEnlargement: true })
      .png({ compressionLevel: 9, palette: true, effort: 10 })
      .toFile(outSmall);

    const fullSize = fs.statSync(outFull).size;
    const smallSize = fs.statSync(outSmall).size;
    const label = `${finalName}`.padEnd(52);

    let status = 'OK';
    if (fullSize > FAIL_BYTES) {
      status = 'FAIL';
      results.fail++;
      failures.push(`${finalName}: ${humanBytes(fullSize)} exceeds ${humanBytes(FAIL_BYTES)} budget`);
    } else if (fullSize > WARN_BYTES) {
      status = 'WARN';
      results.warn++;
    } else {
      results.ok++;
    }
    console.log(`  ${status.padEnd(4)}  ${label}  full=${humanBytes(fullSize)}  sm=${humanBytes(smallSize)}`);
  }

  console.log('');
  console.log(`Done. ok=${results.ok} warn=${results.warn} fail=${results.fail} missing=${results.missing}`);
  console.log(`Output: ${outDir}`);
  if (failures.length > 0) {
    console.error('\nBudget failures:');
    for (const f of failures) console.error(`  - ${f}`);
    process.exit(1);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
