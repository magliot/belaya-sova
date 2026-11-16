// usage: node shrink.mjs in.glb out.glb ratio texSize
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify, textureCompress, prune, dedup } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import { spawnSync } from 'child_process';
import { writeFileSync, readFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';

const [,, inp, out, ratioS, texS] = process.argv;
const ratio = parseFloat(ratioS), tex = parseInt(texS, 10);
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(inp);
const root = doc.getRoot();
for (const e of root.listExtensionsUsed()) if (e.extensionName !== 'KHR_texture_transform') e.dispose();
for (const m of root.listMaterials()) {
  m.setNormalTexture(null); m.setMetallicRoughnessTexture(null);
  m.setOcclusionTexture(null); m.setEmissiveTexture(null);
  m.setMetallicFactor(0); m.setRoughnessFactor(0.9);
}
await MeshoptSimplifier.ready;
await doc.transform(
  dedup(), weld(),
  simplify({ simplifier: MeshoptSimplifier, ratio, error: 0.03 }),
  prune(),
);
for (const t of root.listTextures()) {
  const src = join(tmpdir(), 'shr_in_' + process.pid), dst = join(tmpdir(), 'shr_out_' + process.pid + '.jpg');
  writeFileSync(src, t.getImage());
  const ps = `Add-Type -AssemblyName System.Drawing;$i=[Drawing.Image]::FromFile('${src}');$b=New-Object Drawing.Bitmap ${tex},${tex};$g=[Drawing.Graphics]::FromImage($b);$g.InterpolationMode='HighQualityBicubic';$g.DrawImage($i,0,0,${tex},${tex});$c=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()|?{$_.MimeType -eq 'image/jpeg'};$p=New-Object Drawing.Imaging.EncoderParameters 1;$p.Param[0]=New-Object Drawing.Imaging.EncoderParameter([Drawing.Imaging.Encoder]::Quality,[long]82);$b.Save('${dst}',$c,$p);$i.Dispose()`;
  const r = spawnSync('powershell', ['-NoProfile', '-Command', ps], { encoding: 'utf8' });
  if (r.status !== 0) throw new Error(r.stderr);
  const buf = readFileSync(dst);
  t.setImage(new Uint8Array(buf)).setMimeType('image/jpeg').setURI(t.getURI().replace(/\.\w+$/, '.jpg'));
}
await io.write(out, doc);
let tris = 0; for (const me of root.listMeshes()) for (const p of me.listPrimitives()) tris += (p.getIndices()?.getCount() ?? 0) / 3;
console.log(out, 'tris', tris);
