// Exercise the actual exported animation in the game's vendored Three.js without a GPU.
// Only textures are omitted from this numerical check; skin and tracks are unchanged.
import fs from 'node:fs';import assert from 'node:assert/strict';import path from 'node:path';import {pathToFileURL}from 'node:url';
import * as THREE from '../docs/vendor/three/build/three.module.min.js';
const root=path.resolve('docs/vendor/three');const three=pathToFileURL(path.join(root,'build/three.module.min.js')).href;
const data=s=>'data:text/javascript;base64,'+Buffer.from(s).toString('base64');
const utils=data(fs.readFileSync(path.join(root,'examples/jsm/utils/BufferGeometryUtils.js'),'utf8').replaceAll("from 'three'",`from '${three}'`));
const loader=data(fs.readFileSync(path.join(root,'examples/jsm/loaders/GLTFLoader.js'),'utf8').replaceAll("from 'three'",`from '${three}'`).replaceAll("from '../utils/BufferGeometryUtils.js'",`from '${utils}'`));
const {GLTFLoader}=await import(loader);const input=fs.readFileSync('art/animation-pilot/mishanya-ski-study.glb');const n=input.readUInt32LE(12);const j=JSON.parse(input.subarray(20,20+n));
delete j.materials;delete j.images;delete j.textures;delete j.samplers;for(const m of j.meshes)for(const p of m.primitives)delete p.material;
const bytes=Buffer.from(JSON.stringify(j));const padded=Buffer.alloc(Math.ceil(bytes.length/4)*4,32);bytes.copy(padded);const binary=input.subarray(20+n);
const glb=Buffer.alloc(20+padded.length+binary.length);glb.write('glTF');glb.writeUInt32LE(2,4);glb.writeUInt32LE(glb.length,8);glb.writeUInt32LE(padded.length,12);glb.writeUInt32LE(0x4e4f534a,16);padded.copy(glb,20);binary.copy(glb,20+padded.length);
const model=await new Promise((resolve,reject)=>new GLTFLoader().parse(glb.buffer.slice(glb.byteOffset,glb.byteOffset+glb.length),'',resolve,reject));
const mixer=new THREE.AnimationMixer(model.scene);for(const c of model.animations)mixer.clipAction(c).play();
const feet=[];let mesh;model.scene.traverse(o=>{if(o.isBone&&/Foot$/.test(o.name))feet.push(o);if(o.isSkinnedMesh)mesh=o});assert.equal(feet.length,2);assert(mesh);assert(model.animations.length>0);
const samples=[];let drift=0,scaleError=0,bootShapeError=0;let base,bootSets;
for(let f=0;f<=180;f++){
 mixer.setTime(f/30);model.scene.updateMatrixWorld(true);mesh.skeleton.update();
 const points=feet.map(o=>o.getWorldPosition(new THREE.Vector3()).toArray());base??=points;
 const vertex=i=>mesh.localToWorld(mesh.getVertexPosition(i,new THREE.Vector3()));
 if(!bootSets){
  bootSets=[[],[]];
  for(let i=0;i<mesh.geometry.attributes.position.count;i++){const p=vertex(i);if(p.y<.145){const side=p.x>0?1:0;if(bootSets[side].length<24)bootSets[side].push({i,point:p});}}
  assert(bootSets.every(s=>s.length===24),'Boot region not found');
 }
 for(const set of bootSets){const anchor=vertex(set[0].i);for(const v of set)bootShapeError=Math.max(bootShapeError,Math.abs(vertex(v.i).distanceTo(anchor)-v.point.distanceTo(set[0].point)));}
 points.forEach((p,i)=>{drift=Math.max(drift,Math.abs(p[0]-base[i][0]),Math.abs(p[2]-base[i][2]))});
 model.scene.traverse(o=>{if(o.isBone)scaleError=Math.max(scaleError,...o.scale.toArray().map(x=>Math.abs(x-1)))});
 for(const c of model.animations)for(const t of c.tracks)assert(Array.from(t.values).every(Number.isFinite));
 samples.push({frame:f,feet:points});
}
assert(drift<1e-5,`Foot slide: ${drift}`);assert(scaleError<1e-5,`Bone scale: ${scaleError}`);
assert(bootShapeError<1e-5,`Boot shape deforms: ${bootShapeError}`);
assert(Math.max(...samples.map(s=>s.feet[0][1]))-base[0][1]>.25,'Jump missing');
const report={engine:THREE.REVISION,clips:model.animations.map(c=>({name:c.name,duration:c.duration,tracks:c.tracks.length})),maxFootSlide:drift,maxBoneScaleError:scaleError,maxBootShapeError:bootShapeError,frames:181,samples};
fs.writeFileSync('art/animation-pilot/export-check.json',JSON.stringify(report,null,2));console.log(JSON.stringify({...report,samples:undefined},null,2));
