// Exercise the actual exported models and runtime pose controller in Three.js.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import * as THREE from '../docs/vendor/three/build/three.module.min.js';
const root=path.resolve('docs/vendor/three');
const three=pathToFileURL(path.join(root,'build/three.module.min.js')).href;
const data=s=>'data:text/javascript;base64,'+Buffer.from(s).toString('base64');
const utils=data(fs.readFileSync(path.join(root,'examples/jsm/utils/BufferGeometryUtils.js'),'utf8').replaceAll("from 'three'",`from '${three}'`));
const loader=data(fs.readFileSync(path.join(root,'examples/jsm/loaders/GLTFLoader.js'),'utf8').replaceAll("from 'three'",`from '${three}'`).replaceAll("from '../utils/BufferGeometryUtils.js'",`from '${utils}'`));
const module=data(fs.readFileSync('docs/hero-motion.js','utf8').replaceAll("from 'three'",`from '${three}'`).replaceAll("from 'three/addons/loaders/GLTFLoader.js'",`from '${loader}'`));
const {attachHeroMotion,poseHeroMotion}=await import(module);
function studyForNode(kind){
 const b=fs.readFileSync('docs/models/studies/'+kind+'.glb'),n=b.readUInt32LE(12),j=JSON.parse(b.subarray(20,20+n));
 const nodes=new Map(j.nodes.map(node=>[node.name,node]));
 for(const name of kind==='bear'?['Left_Ski','Right_Ski']:['Snowboard']){
  const deck=j.meshes[nodes.get(name)?.mesh];
  const top=deck?.primitives.find(p=>j.materials[p.material].pbrMetallicRoughness?.baseColorTexture);
  assert(top&&top.attributes.TEXCOORD_0!=null,`${kind}: ${name} lost its topsheet artwork`);
  const profile=j.accessors[top.attributes.POSITION];
  assert(profile.max[1]-profile.min[1]>=.045,`${kind}: ${name} no longer has lifted ends`);
 }
 for(const side of ['Left','Right'])assert(nodes.get(side+'_Binding')?.children?.length>=2,`${kind}: ${side} binding lacks hardware`);
 delete j.materials;delete j.images;delete j.textures;delete j.samplers;
 for(const m of j.meshes)for(const p of m.primitives)delete p.material;
 const bytes=Buffer.from(JSON.stringify(j)),pad=Buffer.alloc(Math.ceil(bytes.length/4)*4,32);bytes.copy(pad);
 const bin=b.subarray(20+n),glb=Buffer.alloc(20+pad.length+bin.length);
 glb.write('glTF');glb.writeUInt32LE(2,4);glb.writeUInt32LE(glb.length,8);glb.writeUInt32LE(pad.length,12);glb.writeUInt32LE(0x4e4f534a,16);pad.copy(glb,20);bin.copy(glb,20+pad.length);
 return glb.toString('base64');
}
globalThis.window={__MOTION_STUDIES:Object.fromEntries(['bear','owl'].map(k=>[k,studyForNode(k)]))};
const results=[];
for(const kind of ['bear','owl'])for(const fps of [30,60]){
 const holder=new THREE.Group(),original=new THREE.Group();holder.add(original);
 const h={kind,holder,skis:kind==='bear'?[new THREE.Group(),new THREE.Group()]:null,board:kind==='owl'?new THREE.Group():null,poles:[]};
 await attachHeroMotion(h);assert.equal(h.motionStatus,'ready');
 const m=h.motion,feet=[],bindings=[],hands=[],poles=[];
 m.model.traverse(o=>{if(o.isBone&&/Foot$/.test(o.name))feet.push(o);if(/_Binding$/.test(o.name))bindings.push(o);if(o.isBone&&/Hand$/.test(o.name))hands.push(o);if(/_Pole$/.test(o.name))poles.push(o)});
 const sort=a=>a.sort((a,b)=>a.name.localeCompare(b.name));[feet,bindings,hands,poles].forEach(sort);
 assert.equal(feet.length,2);assert.equal(bindings.length,2);
 const point=o=>o.getWorldPosition(new THREE.Vector3());
 const relative=(a,b)=>b.worldToLocal(point(a));
 let baseContacts,baseGrips,maxContactError=0,maxGripError=0,minY=Infinity,maxY=-Infinity,maxScaleError=0;
 for(let f=0;f<fps*7;f++){
  const t=f/fps,air=t>=2&&t<3.2;
  const p={ang:Math.sin(t*2)*.8,air:air?1:0,airT:air?t-2:0,airMax:1.2,rampH:t>=1.6&&t<2?(t-1.6):0};
  assert.equal(poseHeroMotion(h,p,'play',t),true);m.model.updateWorldMatrix(true,true);
  const contacts=feet.map((foot,i)=>relative(foot,bindings[i]));baseContacts??=contacts;
  contacts.forEach((p,i)=>maxContactError=Math.max(maxContactError,p.distanceTo(baseContacts[i])));
  const grips=hands.map((hand,i)=>poles[i]?relative(poles[i],hand):null);baseGrips??=grips;
  grips.forEach((p,i)=>{if(p)maxGripError=Math.max(maxGripError,p.distanceTo(baseGrips[i]))});
  for(const foot of feet){const y=point(foot).y;minY=Math.min(minY,y);maxY=Math.max(maxY,y)}
  m.model.traverse(o=>{assert(o.position.toArray().every(Number.isFinite));if(o.isBone)maxScaleError=Math.max(maxScaleError,...o.scale.toArray().map(x=>Math.abs(x-1)))});
 }
 assert(maxY-minY<.08,`${kind}: duplicate jump height ${maxY-minY}`);
 assert(maxContactError<.012,`${kind}: foot/binding drift ${maxContactError}`);
 assert(maxGripError<.012,`${kind}: grip drift ${maxGripError}`);
 assert(maxScaleError<1e-5);
 assert.equal(poseHeroMotion(h,{},'menu',8),false);assert(original.visible);assert(!m.model.visible);
 assert.equal(poseHeroMotion(h,{air:0},'play',0),true);assert(!original.visible);assert(m.model.visible);
 assert.equal(poseHeroMotion(h,{fall:1},'play',.1),false);assert(original.visible);
 assert.equal(poseHeroMotion(h,{},'crash',.2),false);
 results.push({kind,fps,maxContactError,maxGripError,localFootHeightRange:maxY-minY,maxScaleError});
}
fs.mkdirSync('art/animation-integration',{recursive:true});
fs.writeFileSync('art/animation-integration/runtime-check.json',JSON.stringify({engine:THREE.REVISION,results},null,2));
console.log(JSON.stringify(results,null,2));
