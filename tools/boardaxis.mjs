import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(process.argv[2]);
const pts = [];
for (const n of doc.getRoot().listNodes()) { const me = n.getMesh(); if (!me) continue; const M = n.getWorldMatrix();
  for (const p of me.listPrimitives()) { const a = p.getAttribute('POSITION'); const v = [0,0,0];
    for (let i = 0; i < a.getCount(); i++) { a.getElement(i, v); const y = M[1]*v[0]+M[5]*v[1]+M[9]*v[2]+M[13]; if (y < 0.05) pts.push([M[0]*v[0]+M[4]*v[1]+M[8]*v[2]+M[12], M[2]*v[0]+M[6]*v[1]+M[10]*v[2]+M[14]]); } } }
const mx = pts.reduce((s,p)=>s+p[0],0)/pts.length, mz = pts.reduce((s,p)=>s+p[1],0)/pts.length;
let sxx=0,szz=0,sxz=0;for(const [x,z] of pts){sxx+=(x-mx)**2;szz+=(z-mz)**2;sxz+=(x-mx)*(z-mz);}
const ang = 0.5*Math.atan2(2*sxz, sxx-szz); // угол главной оси от +x к +z
console.log(process.argv[2].split(/[\\/]/).pop(), 'board axis deg from +x toward +z:', (ang*180/Math.PI).toFixed(1), 'center', mx.toFixed(3), mz.toFixed(3));
