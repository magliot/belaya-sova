// Упаковка GLB-моделей (или mp3) в JS-файл для игры: window.__GLB[key] = "base64…"
// Так модели грузятся обычным <script> без CORS и работают в Telegram WebView.
//
// usage:
//   node tools/pack-models.mjs docs/models/trees.js pine=src/pine_small.glb fir=src/fir_small.glb
//   node tools/pack-models.mjs docs/models/sounds.js --snd owl_start=voice/owl_start.mp3
//
// Ключи (pine, fir, …) — те, что ищет index.html (NPCM[key], PROPS[key], HEROMODELS через loadHeroes).
// Перед упаковкой GLB из Tripo сжимать: node tools/shrink.mjs in.glb out.glb 0.25 512
import { readFileSync, writeFileSync } from 'fs';

const args = process.argv.slice(2);
const out = args.shift();
let v = 'GLB';
if (args[0] === '--snd') { v = 'SND'; args.shift(); }
if (!out || !args.length) { console.error('usage: node pack-models.mjs out.js [--snd] key=file ...'); process.exit(1); }

let js = `window.__${v}=window.__${v}||{};\n`;
for (const a of args) {
  const i = a.indexOf('=');
  const key = a.slice(0, i), file = a.slice(i + 1);
  const b64 = readFileSync(file).toString('base64');
  js += `window.__${v}[${JSON.stringify(key)}]="${b64}";\n`;
  console.log(key, file, Math.round(b64.length / 1024) + ' KB base64');
}
writeFileSync(out, js);
console.log('->', out);
