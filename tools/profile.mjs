import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(process.argv[2]);
const pts = [];
for (const n of doc.getRoot().listNodes()) { const me = n.getMesh(); if (!me) continue; const M = n.getWorldMatrix();
  for (const p of me.listPrimitives()) { const a = p.getAttribute('POSITION'); const v = [0,0,0];
    for (let i = 0; i < a.getCount(); i++) { a.getElement(i, v); const x=M[0]*v[0]+M[4]*v[1]+M[8]*v[2]+M[12], y=M[1]*v[0]+M[5]*v[1]+M[9]*v[2]+M[13], z=M[2]*v[0]+M[6]*v[1]+M[10]*v[2]+M[14]; pts.push([x,y,z]); } } }
const mn=[0,1,2].map(k=>Math.min(...pts.map(p=>p[k]))), mx=[0,1,2].map(k=>Math.max(...pts.map(p=>p[k])));
console.log('min',mn.map(v=>v.toFixed(3)),'max',mx.map(v=>v.toFixed(3)));
for (const ax of [0,2]) { const bins=Array(8).fill(-1e9);
  for (const p of pts){const b=Math.min(7,Math.floor((p[ax]-mn[ax])/(mx[ax]-mn[ax])*8));bins[b]=Math.max(bins[b],p[1]);}
  console.log('maxY along', ax?'z':'x', bins.map(v=>v.toFixed(2)).join(' ')); }
