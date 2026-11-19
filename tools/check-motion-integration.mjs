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
 if(kind==='bear'&&fps===30){
  const bones={};m.model.traverse(o=>{if(o.isBone)bones[o.name.replace(/^mixamorig:?/,'')]=o;});
  const local=n=>m.model.worldToLocal(point(bones[n]));
  const body=m.model.getObjectByName('Mishanya_Mesh');assert(body?.isSkinnedMesh);
  const pos=body.geometry.attributes.position,seams=new Map(),v=new THREE.Vector3();
  for(let i=0;i<pos.count;i++){v.fromBufferAttribute(pos,i);const key=v.toArray().map(x=>Math.round(x*1e5)).join(',');if(!seams.has(key))seams.set(key,[]);seams.get(key).push(i);}
  const pairs=[...seams.values()].filter(a=>a.length>1);assert(pairs.length>100);
  const poses=[];
  const fit=[];
  for(const [label,ang] of [['straight',0],['left',-.9],['right',.9],['land',0],['air',0]]){
   m.active=false;m.lastT=null;m.state=label==='land'?{landed:9.85}:{};
   poseHeroMotion(h,{ang,air:label==='air'?1:0,airT:.4,airMax:.8},'play',10);
   m.model.updateWorldMatrix(true,true);
   const L=local('LeftArm'),R=local('RightArm'),head=local('Head'),hips=local('Hips');
   for(const side of ['Left','Right']){
    const get=k=>m.model.getObjectByName(side+'_'+k),grip=get('Grip'),pads=[get('FingerPad'),get('ThumbPad')];
    const center=grip.localToWorld(new THREE.Vector3(0,.004,0));
    const ray=new THREE.Raycaster(),hits=[];
    for(let i=0;i<12;i++){
     const a=(i+.25)*Math.PI/6,dir=grip.localToWorld(new THREE.Vector3(Math.cos(a),.004,Math.sin(a))).sub(center).normalize();
     ray.set(center,dir);const d=ray.intersectObjects(pads,false)[0]?.distance;
     if(d!=null&&d>.004&&d<.034)hits.push(d);
    }
    const sh=point(bones[side+'Arm']),el=point(bones[side+'ForeArm']),wr=point(bones[side+'Hand']);
    const flex=180-THREE.MathUtils.radToDeg(sh.clone().sub(el).angleTo(wr.clone().sub(el)));
    const wristAngle=THREE.MathUtils.radToDeg(wr.clone().sub(el).angleTo(center.clone().sub(wr)));
    assert(hits.length>=10,side+' open glove: '+hits.length+'/12 '+label);
    assert(hits.filter(d=>d<=.016).length>=3,side+' fingers not touching the 14 mm grip');
    assert(flex>46&&flex<95&&wristAngle<35,JSON.stringify({side,label,flex,wristAngle}));
    fit.push({side,label,raysInsideGlove:hits.length,elbowFlexDeg:+flex.toFixed(1),wristBendDeg:+wristAngle.toFixed(1)});
   }
   const mid=(local('LeftFoot').x+local('RightFoot').x)/2;
   let gap=0;const a=new THREE.Vector3(),b=new THREE.Vector3();
   for(const group of pairs){body.getVertexPosition(group[0],a);for(const i of group.slice(1))gap=Math.max(gap,a.distanceTo(body.getVertexPosition(i,b)));}
   assert(gap<.00015,label+' open seam '+gap);
   if(!ang){assert(Math.abs(L.y-R.y)<.002,label+' shoulders');assert(Math.abs(head.x-mid)<.006,label+' head');}
   poses.push({label,shoulderHeightDifference:L.y-R.y,headOffset:head.x-mid,hipOffset:hips.x-mid,seamGap:gap});
  }
  assert(poses[1].hipOffset*poses[2].hipOffset<-.0003,'carves must load opposite skis');
  assert(Math.abs(fit[0].elbowFlexDeg-fit[1].elbowFlexDeg)<2,'uneven elbows in neutral');
  fs.writeFileSync('art/animation-pilot/pose-check.json',JSON.stringify({coincidentGroups:pairs.length,poses},null,2)+'\n');
  fs.writeFileSync('art/animation-pilot/grip-check.json',JSON.stringify(fit,null,2)+'\n');
  m.active=false;m.lastT=null;m.state={};
 }
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
 const tricks=[];
 for(const style of [0,1]){
  m.active=false;m.state={};m.lastT=null;
  poseHeroMotion(h,{air:1,airT:.5,airMax:1,trick:.5,tt:5,style},'play',20+style);
  m.model.updateWorldMatrix(true,true);
  const errors=feet.map((f,i)=>relative(f,bindings[i]).distanceTo(baseContacts[i]));
  assert(Math.max(...errors)<.012,JSON.stringify({kind,style,errors}));
  if(kind==='bear'){
   for(let i=0;i<2;i++)assert(relative(poles[i],hands[i]).distanceTo(baseGrips[i])<.001);
   tricks.push({style,footGap:Math.max(...errors),stance:point(feet[0]).distanceTo(point(feet[1]))});
  }else{
   const wrist=hands[style===0?1:0],local=m.contact.deck.worldToLocal(point(wrist));
   const deck=m.contact.deck,sign=style===0?1:-1,faces=[];
   deck.traverse(mesh=>{
    if(!mesh.isMesh)return;
    const geo=mesh.geometry,idx=geo.index,position=geo.attributes.position;
    const vertex=i=>deck.worldToLocal(mesh.localToWorld(new THREE.Vector3().fromBufferAttribute(position,idx?idx.getX(i):i)));
    for(let i=0;i<(idx?.count??position.count);i+=3){
     const tri=new THREE.Triangle(vertex(i),vertex(i+1),vertex(i+2));
     if([tri.a,tri.b,tri.c].every(v=>v.z*sign>.11&&Math.abs(v.x)<.38))faces.push(tri);
    }
   });
   let edgeGap=Infinity;const v=new THREE.Vector3(),nearest=new THREE.Vector3();
   m.model.traverse(mesh=>{
    if(!mesh.isSkinnedMesh)return;
    mesh.skeleton.update();const hand=mesh.skeleton.bones.indexOf(wrist);
    const {skinIndex,skinWeight}=mesh.geometry.attributes;
    for(let i=0;i<skinIndex.count;i++){
     if(!['getX','getY','getZ','getW'].some(get=>skinIndex[get](i)===hand&&skinWeight[get](i)>.35))continue;
     deck.worldToLocal(mesh.localToWorld(mesh.getVertexPosition(i,v)));
     for(const face of faces)edgeGap=Math.min(edgeGap,v.distanceTo(face.closestPointToPoint(v,nearest)));
    }
   });
   tricks.push({style,footGap:Math.max(...errors),edgeGap,wrist:local.toArray()});
   assert(edgeGap<.022,JSON.stringify(tricks));
  }
 }
 assert.equal(poseHeroMotion(h,{},'menu',8),false);assert(original.visible);assert(!m.model.visible);
 assert.equal(poseHeroMotion(h,{air:0},'play',0),true);assert(!original.visible);assert(m.model.visible);
 assert.equal(poseHeroMotion(h,{fall:1},'play',.1),false);assert(original.visible);
 assert.equal(poseHeroMotion(h,{},'crash',.2),false);
 results.push({kind,fps,maxContactError,maxGripError,localFootHeightRange:maxY-minY,maxScaleError,tricks});
}
fs.mkdirSync('art/animation-integration',{recursive:true});
fs.writeFileSync('art/animation-integration/runtime-check.json',JSON.stringify({engine:THREE.REVISION,results},null,2));
console.log(JSON.stringify(results,null,2));
