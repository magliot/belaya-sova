// usage: node clip.mjs in.glb out.glb xmin ymax  — оставляет треугольники, у которых все вершины x>xmin и y<ymax
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { prune } from '@gltf-transform/functions';
const [,, inp, out, xminS, ymaxS] = process.argv;
const xmin = parseFloat(xminS), ymax = parseFloat(ymaxS);
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(inp);
for (const n of doc.getRoot().listNodes()) { const me = n.getMesh(); if (!me) continue; const M = n.getWorldMatrix();
  for (const p of me.listPrimitives()) { const pos = p.getAttribute('POSITION'), idx = p.getIndices(); const v = [0,0,0];
    const ok = new Uint8Array(pos.getCount());
    for (let i = 0; i < pos.getCount(); i++) { pos.getElement(i, v); const x = M[0]*v[0]+M[4]*v[1]+M[8]*v[2]+M[12], y = M[1]*v[0]+M[5]*v[1]+M[9]*v[2]+M[13]; ok[i] = x > xmin && y < ymax ? 1 : 0; }
    const src = idx.getArray(), keep = [];
    for (let t = 0; t < src.length; t += 3) if (ok[src[t]] && ok[src[t+1]] && ok[src[t+2]]) keep.push(src[t], src[t+1], src[t+2]);
    idx.setArray(new Uint32Array(keep));
    console.log('tris', src.length/3, '->', keep.length/3);
  } }
await doc.transform(prune());
await io.write(out, doc);
