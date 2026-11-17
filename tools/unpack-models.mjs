// Recover portable GLBs from the JS bundle used by the Telegram WebView.
// node tools/unpack-models.mjs docs/models/winter.js assets/tripo/optimized
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { runInNewContext } from 'node:vm';
const [, , input, output] = process.argv;
if (!input || !output) throw new Error('usage: node tools/unpack-models.mjs input.js output-directory');
const context = { window: {} };
runInNewContext(readFileSync(input, 'utf8'), context, { timeout: 1000 });
mkdirSync(output, { recursive: true });
for (const [key, value] of Object.entries(context.window.__GLB || {})) {
  if (!/^[a-z][a-z0-9_]*$/.test(key) || typeof value !== 'string') throw new Error('Invalid model key');
  const bytes = Buffer.from(value, 'base64');
  if (bytes.subarray(0, 4).toString() !== 'glTF') throw new Error('Invalid GLB: ' + key);
  const file = join(output, key + '.glb');writeFileSync(file, bytes);console.log(file);
}
