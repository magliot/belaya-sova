import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(process.argv[2]);
const pts = [];
for (const n of doc.getRoot().listNodes()) { const me = n.getMesh(); if (!me) continue; const M = n.getWorldMatrix();
  for (const p of me.listPrimitives()) { const a = p.getAttribute('POSITION'); const v = [0,0,0];
    for (let i = 0; i < a.getCount(); i++) { a.getElement(i, v); pts.push([M[0]*v[0]+M[4]*v[1]+M[8]*v[2]+M[12], M[1]*v[0]+M[5]*v[1]+M[9]*v[2]+M[13], M[2]*v[0]+M[6]*v[1]+M[10]*v[2]+M[14]]); } } }
const low = pts.filter(p => p[1] < 0.06);
const ext = a => [Math.min(...a).toFixed(3), Math.max(...a).toFixed(3)];
console.log('board/feet pts', low.length, 'x', ext(low.map(p=>p[0])), 'z', ext(low.map(p=>p[2])));
// occupancy grid x (cols) vs y (rows), top row = high y
const NX = 24, NY = 16, g = Array.from({length: NY}, () => Array(NX).fill(0));
for (const p of pts) { const cx = Math.min(NX-1, Math.floor((p[0]+0.5)*NX)); const cy = Math.min(NY-1, Math.floor(p[1]*NY)); g[NY-1-cy][cx]++; }
console.log('x-y occupancy (x from -0.5 to 0.5):');
for (const r of g) console.log(r.map(c => c===0?'.':c<20?':':'#').join(''));
const gz = Array.from({length: NY}, () => Array(NX).fill(0));
for (const p of pts) { const cz = Math.min(NX-1, Math.floor((p[2]+0.5)*NX)); const cy = Math.min(NY-1, Math.floor(p[1]*NY)); gz[NY-1-cy][cz]++; }
console.log('z-y occupancy (z from -0.5 to 0.5):');
for (const r of gz) console.log(r.map(c => c===0?'.':c<20?':':'#').join(''));
