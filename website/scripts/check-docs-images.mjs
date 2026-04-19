#!/usr/bin/env node
// Fails if any markdown file under website/content/*/docs references an image
// path under /images/docs/ that does not exist on disk.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const websiteRoot = path.resolve(__dirname, '..');
const contentRoot = path.join(websiteRoot, 'content');
const publicRoot = path.join(websiteRoot, 'public');

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(p, out);
    else if (entry.isFile() && p.endsWith('.md')) out.push(p);
  }
  return out;
}

function extractImageRefs(content) {
  const refs = new Set();
  // Markdown image syntax: ![alt](path)
  for (const m of content.matchAll(/!\[[^\]]*\]\(([^)\s]+)\)/g)) refs.add(m[1]);
  // HTML <img src="...">
  for (const m of content.matchAll(/<img[^>]+src=["']([^"']+)["']/g)) refs.add(m[1]);
  // data-theme-compare: before/after
  for (const m of content.matchAll(/data-(?:before|after)=["']([^"']+)["']/g)) refs.add(m[1]);
  return refs;
}

function main() {
  if (!fs.existsSync(contentRoot)) {
    console.error(`content dir not found: ${contentRoot}`);
    process.exit(1);
  }

  const files = walk(contentRoot);
  const missing = [];

  for (const file of files) {
    const content = fs.readFileSync(file, 'utf-8');
    for (const ref of extractImageRefs(content)) {
      if (!ref.startsWith('/images/docs/')) continue;
      const onDisk = path.join(publicRoot, ref);
      if (!fs.existsSync(onDisk)) {
        missing.push({ file: path.relative(websiteRoot, file), ref });
      }
    }
  }

  if (missing.length === 0) {
    console.log('check-docs-images: OK (all /images/docs/ refs resolve)');
    return;
  }

  console.error(`check-docs-images: ${missing.length} missing image ref(s):`);
  for (const { file, ref } of missing) {
    console.error(`  ${file}  →  ${ref}`);
  }
  process.exit(1);
}

main();
