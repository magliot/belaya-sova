// usage: node shrink.mjs in.glb out.glb ratio texSize
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dequantize, weld, simplify, prune, dedup } from '@gltf-transform/functions';
import { MeshoptDecoder, MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

const [,, inp, out, ratioS, texS] = process.argv;
const ratio = parseFloat(ratioS), tex = parseInt(texS, 10);
if (!inp || !out || !(ratio > 0 && ratio <= 1) || !Number.isInteger(tex) || tex < 64 || tex > 4096) {
  throw new Error('usage: node tools/shrink.mjs in.glb out.glb ratio(0..1) textureSize(64..4096)');
}
await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.decoder': MeshoptDecoder });
const doc = await io.read(inp);
const root = doc.getRoot();
await doc.transform(dequantize());
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
  const image = sharp(Buffer.from(t.getImage()));
  const { hasAlpha } = await image.metadata();
  const resized = image.pipelineColourspace('srgb').resize({ width: tex, height: tex, fit: 'inside', withoutEnlargement: true });
  const buf = await (hasAlpha ? resized.png() : resized.jpeg({ quality: 82 })).toBuffer();
  const ext = hasAlpha ? 'png' : 'jpg';
  t.setImage(new Uint8Array(buf)).setMimeType(hasAlpha ? 'image/png' : 'image/jpeg');
  if (t.getURI()) t.setURI(t.getURI().replace(/\.\w+$/, '.' + ext));
}
await io.write(out, doc);
let tris = 0; for (const me of root.listMeshes()) for (const p of me.listPrimitives()) tris += (p.getIndices()?.getCount() ?? 0) / 3;
console.log(out, 'tris', tris);
