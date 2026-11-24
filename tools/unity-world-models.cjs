// Bake the approved Three.js factories once, with their original transforms and UVs.
// The resulting static mesh streams and blend shapes are read by Unity, without Three.js at runtime.
// Start tools/serve.cjs docs 8766, then run this file from the repository root.
// After rebaking trees, run python tools/unity-world-forest.py to rebuild the approved bent crowns.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=path.resolve('unity/OwlSlope/Assets/Resources/Race');
const models=path.join(root,'Models'),textures=path.join(root,'Textures');
const base=process.argv[2]||'http://127.0.0.1:8766/';
const include=process.argv[3]?new RegExp(process.argv[3]):null;
const specs={skier:['npc',{}],kid:['npc',{kid:true}],instructor:['npc',{instr:true}],sitter:['npc',{sit:true}],
 snowboarder:['npc',{sb:true}],boardKid:['npc',{sb:true,kid:true}],boardInstructor:['npc',{sb:true,instr:true}],
 rockWide:['rock',{rockWide:true}],rockTall:['rock',{rockWide:false}],
 rattForward:['cat',{vy:50}],rattOncoming:['cat',{vy:-50}],
 gateRed:['gate',{gap:100,col:0}],gateBlue:['gate',{gap:100,col:1}],stoneGate:['gate',{gap:78,stoneGate:true}],
 fence:['fence',{x1:0,x2:100}],finish:['finish',{}],boundaryPole:['boundaryPole',{}],dog:['dog',{vx:120}],yeti:['mogulYeti',{}],
 cannonLeft:['cannon',{x:38}],cannonRight:['cannon',{x:362}]};
// cat12A, snowmobile and playerSled are now authored in tools/build-unity-vehicles.py.
Object.assign(specs,{liftTower:['kit',{model:'tower16C'}],liftChair:['liftChair',{}],
 holidayFir:['holidayFir',{}],festivalFountain:['festivalFountain',{}],
 emptyChair:['emptyChair',{}],boardKid1:['npc',{sb:true,kid:true,vi:1}],walker:['npc',{walker:true}],
 fence1:['fence',{x1:0,x2:100,fenceStyle:1}],fence2:['fence',{x1:0,x2:100,fenceStyle:2}]});
for(let i=0;i<8;i++)specs['stageLamp'+i]=['stageLamp',{stage:i}];
for(const key of ['coin','gift','helmet','thermos','magnet','feather','aid','ball','snowman'])specs[key]=[key,{}];
// The four revised Unity tube riders are built with tools/build-unity-tube-riders.py.
for(const key of ['snow26A','snow26B','snow26C'])specs[key]=['snowman',{variant:key}];
// All nine entry arches, including the narrower start, are baked by build-unity-arches.py.
for(let i=0;i<3;i++){
 specs['ring'+i]=['airRing',{radius:1.55,ringStyle:i}];
 specs['mound'+i]=['mogul',{profile:i}];
 specs['lamp'+i]=['kit',{model:'lamp18'+'ABC'[i]}];
 for(let f=0;f<3;f++)specs['tree'+f+i]=['tree',{pine:f===1,fir:f===2,treeStyle:i}];
}
for(const ch of Array.from(new Set('EMCOСОВАШАХТЕРСК')))specs['letter'+ch.charCodeAt(0)]=['letter',{ch}];

// This function runs in the source game's module, so factory fixes flow through to the next bake.
async function exportModel(type,details){
 const o={x:200,y:0,...details};let model;
 if(type==='holidayFir'||type==='festivalFountain'){
  model=new THREE.Group();
  const jewel=(color,glow=0)=>new THREE.MeshStandardMaterial({color,emissive:color,emissiveIntensity:glow,roughness:.34});
  const gold=jewel(0xffc54b,.3),pearl=jewel(0xfff2bd,.65);
  function part(shape,paint,x,y,z){
   const mesh=new THREE.Mesh(shape,paint);mesh.position.set(x,y,z);model.add(mesh);return mesh;
  }
  function star(radius){
   const outline=new THREE.Shape();
   for(let i=0;i<10;i++){const a=Math.PI/2+i*Math.PI/5,r=i%2?radius*.44:radius;
    const x=r*Math.cos(a),y=r*Math.sin(a);if(i)outline.lineTo(x,y);else outline.moveTo(x,y);}
   outline.closePath();return new THREE.ExtrudeGeometry(outline,{depth:radius*.27,bevelEnabled:true,bevelThickness:.014,bevelSize:.015,bevelSegments:1});
  }
  if(type==='holidayFir'){
   const spruce=FACT.tree({...o,fir:false,pine:false,treeStyle:0});model.add(spruce);spruce.updateMatrixWorld(true);
   const ball=new THREE.SphereGeometry(.155,12,9),clasp=new THREE.CylinderGeometry(.045,.05,.07,8);
   const colors=[0xf13649,0x14cde3,0xffbe32,0xb145de,0x94e644].map(color=>jewel(color,.13));
   const ray=new THREE.Raycaster();
   // Hang each bauble on the first real branch facing out, preserving the snow-laden spruce mesh.
   for(const [tier,y,count] of [[0,.84,10],[1,1.46,9],[2,2.07,7],[3,2.65,5]]){
    for(let i=0;i<count;i++){
     const a=2*Math.PI*i/count+tier*1.17,radial=new THREE.Vector3(Math.cos(a),0,Math.sin(a));
     const start=radial.clone().multiplyScalar(3);start.y=y+.11*Math.sin(i*2.7);
     ray.set(start,radial.clone().negate());const hit=ray.intersectObject(spruce,true)[0];if(!hit)continue;
     const pos=hit.point.clone().addScaledVector(radial,.055);
     part(clasp,gold,pos.x,pos.y-.025,pos.z);part(ball,colors[(i+2*tier)%colors.length],pos.x,pos.y-.18,pos.z);
    }
   }
   part(new THREE.CylinderGeometry(.032,.04,.29,8),gold,0,3.63,0);
   const top=star(.38);part(top,pearl,0,3.93,-.055);part(top,gold,-.05,3.93,0).rotation.y=Math.PI/2;
  }else{
   const enamel=jewel(0x673591),navy=jewel(0x34344e),red=jewel(0xd82b55);
   const drum=(lower,upper,h,y,paint)=>part(new THREE.CylinderGeometry(upper,lower,h,12),paint,0,y,0);
   drum(.51,.49,.13,.065,navy);drum(.42,.29,.49,.36,enamel);
   drum(.421,.411,.08,.17,gold);drum(.315,.30,.08,.54,gold);
   drum(.42,.42,.095,.65,red);drum(.425,.36,.05,.719,gold);drum(.30,.24,.045,.756,navy);
   const emblem=star(.145);
   for(const a of [0,2.0944,4.1888]){
    const badge=part(emblem,pearl,.359*Math.sin(a),.37,.359*Math.cos(a));badge.rotation.y=a;
   }
  }
 }
 else if(type==='kit')model=kit[o.model].clone();
 else if(type==='stageLamp'){
  const l=lamps[0];styleLamp(l,o.stage);
  const glow=o.stage===6?1:o.stage===7?.65:0;
  beamMat.opacity=glow*.35;glowMat.opacity=glow*.55;
  model=l.clone();model.position.set(0,0,0);model.rotation.set(0,0,0);
 }
 else if(type==='liftChair'){
  model=new THREE.Group();const c=NPCM.chair.clone();c.position.y=-NPCM.chair.userData.h-.05;model.add(c);
 }
 else if(type==='emptyChair'){
  model=new THREE.Group();S3(mk(sharedGeo.cyl,steel,0,-1.1,0,model),.03,2.2,.03);
  S3(mk(sharedGeo.box,M(0x2b6cb0),0,-2.25,0,model),1.1,.08,.45);
  S3(mk(sharedGeo.box,M(0x2b6cb0),0,-1.95,.22,model),1.1,.55,.06);
 }
 else model=FACT[type](o);
 if(type==='dog')model.userData.dog.rotation.y=0;
 const bytes=a=>{const b=new Uint8Array(a.buffer,a.byteOffset,a.byteLength);let s='';for(let i=0;i<b.length;i+=8192)s+=String.fromCharCode(...b.subarray(i,i+8192));return btoa(s);};
 const parts=[],images={};model.updateMatrixWorld(true);
 // Flatten the original winter skins into sampled blend shapes, preserving hand and leg motion in WebGL.
 const poses=new Map(),rest=[];
 const capture=(name,pose)=>{
  model.traverse(n=>{if(n.isBone)rest.push([n,n.position.clone(),n.quaternion.clone(),n.scale.clone()]);});
  pose();model.updateMatrixWorld(true);
  model.traverseVisible(n=>{
   if(!n.isMesh)return;n.skeleton?.update();const v=new THREE.Vector3(),a=new Float32Array(n.geometry.attributes.position.count*3);
   for(let i=0;i<a.length/3;i++){n.getVertexPosition(i,v).applyMatrix4(n.matrixWorld);a.set([v.x,v.y,-v.z],i*3);}
   if(!poses.has(n))poses.set(n,{});poses.get(n)[name]=a;
  });
  for(const [n,p,q,s] of rest){n.position.copy(p);n.quaternion.copy(q);n.scale.copy(s);}rest.length=0;model.updateMatrixWorld(true);
 };
 if(type==='dog'&&model.userData.mixer){
  const action=model.userData.mixer._actions[0],duration=action.getClip().duration;
  for(let i=0;i<4;i++)capture('Run'+i,()=>model.userData.mixer.setTime(duration*i/4));
 }else if(type==='mogulYeti'||type==='snowman'&&!o.variant){
  const arms=['WinterArmL','WinterArmR'].map(n=>model.getObjectByName(n));
  if(arms.every(Boolean)){
   if(type==='mogulYeti'){
    for(const [name,sign] of [['StrideL',1],['StrideR',-1]])capture(name,()=>{arms[0].rotation.x=.57*sign;arms[1].rotation.x=-.57*sign;});
    for(let i=0;i<2;i++)capture('Wave'+i,()=>{arms[i].rotation.z=(i===0?1:-1)*1.75;});
   }else for(let i=0;i<2;i++)for(const [name,z,y] of [['Ready',1.8,.65],['Throw',1.15,-.7]]){
    capture(name+i,()=>{const sign=i===0?1:-1;arms[i].rotation.z=z*sign;arms[i].rotation.y=y*sign;});
   }
  }
 }
 // Bake static sprites in their authored facing plane. Gate/ring glow survives the export.
 model.traverseVisible(node=>{
  if(!node.isMesh&&!node.isSprite)return;
  // The Unity groomer mounts its optics on the actual cab. Legacy helper sprites
  // and spheres sit at the height of the vehicle's bounding box above its blade.
  if(type==='cat'&&node.parent===model&&(node.isSprite||node.userData.beacon||
      (node.geometry===sharedGeo.sphLo&&node.material?.isMeshBasicMaterial)))return;
  const spr=node.isSprite;
  const geometry=spr?new THREE.PlaneGeometry(1,1):node.geometry;
  const pos=geometry.attributes.position,uv=geometry.attributes.uv,col=geometry.attributes.color;
  const index=geometry.index?.array||Array.from({length:pos.count},(_,i)=>i);
  const matArray=Array.isArray(node.material)?node.material:[node.material];
  const groups=Array.isArray(node.material)?geometry.groups:[{start:0,count:index.length,materialIndex:0}];
  node.skeleton?.update();
  for(const group of groups){
   const mat=matArray[group.materialIndex];if(!mat||mat.visible===false||mat.opacity<.02)continue;
   const ids=new Map(),indices=[];
   for(let i=group.start;i<group.start+group.count;i++){const j=index[i];if(!ids.has(j))ids.set(j,ids.size);indices.push(ids.get(j));}
   const count=ids.size;if(!count)continue;
   const p=new Float32Array(count*3),n=new Int8Array(count*3),tex=new Float32Array(count*2),colors=new Uint8Array(count*4);
   const morph={};const shapes=node.morphTargetDictionary||{};
   for(const [name,slot] of Object.entries(shapes)){
    if(!geometry.morphAttributes.position?.[slot])continue;
    morph[name]=new Int16Array(count*3);
   }
   const v=new THREE.Vector3(),nor=new THREE.Vector3(),u=new THREE.Vector2(),c=new THREE.Color(),t=new THREE.Vector3();
   const nm=new THREE.Matrix3().getNormalMatrix(node.matrixWorld),linear=new THREE.Matrix3().setFromMatrix4(node.matrixWorld);
   mat.map?.updateMatrix();
   for(const [id,k] of ids){
    if(spr)v.fromBufferAttribute(pos,id);else node.getVertexPosition(id,v);
    v.applyMatrix4(node.matrixWorld);p.set([v.x,v.y,-v.z],k*3);
    if(geometry.attributes.normal)nor.fromBufferAttribute(geometry.attributes.normal,id).applyMatrix3(nm).normalize();else nor.set(0,1,0);
    n.set([nor.x*127,nor.y*127,-nor.z*127],k*3);
    if(uv)u.fromBufferAttribute(uv,id);else u.set(0,0);
    if(mat.map){u.applyMatrix3(mat.map.matrix);if(!mat.map.flipY)u.y=1-u.y;}tex.set([u.x,u.y],k*2);
    c.copy(mat.color||new THREE.Color(1,1,1));if(col&&mat.vertexColors)c.multiply(new THREE.Color(col.getX(id),col.getY(id),col.getZ(id)));
    c.convertLinearToSRGB();colors.set([c.r*255,c.g*255,c.b*255,(mat.opacity??1)*255],k*4);
    for(const [name,a] of Object.entries(morph)){
     t.fromBufferAttribute(geometry.morphAttributes.position[shapes[name]],id);
     if(!geometry.morphTargetsRelative)t.sub(new THREE.Vector3().fromBufferAttribute(pos,id));
     t.applyMatrix3(linear);a.set([t.x*10000,t.y*10000,-t.z*10000],k*3);
    }
   }
   for(const [name,data] of Object.entries(poses.get(node)||{})){
    const delta=new Int16Array(count*3);let active=false;
    for(const [id,k] of ids)for(let c=0;c<3;c++){const v=Math.round((data[id*3+c]-p[k*3+c])*10000);delta[k*3+c]=Math.max(-32768,Math.min(32767,v));if(v)active=true;}
    if(active)morph[name]=delta;
   }
   let imageKey='';
   if(mat.map?.image){
    imageKey=mat.map.uuid;
    if(!images[imageKey]){
     const im=mat.map.image,scale=Math.min(1,512/Math.max(im.width,im.height));
     const canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(im.width*scale));canvas.height=Math.max(1,Math.round(im.height*scale));
     canvas.getContext('2d').drawImage(im,0,0,canvas.width,canvas.height);
     images[imageKey]=canvas.toDataURL('image/png').split(',')[1];
    }
   }
   const idArray=Uint32Array.from(indices);
   for(let i=0;i<idArray.length;i+=3){const tmp=idArray[i+1];idArray[i+1]=idArray[i+2];idArray[i+2]=tmp;}
   parts.push({tag:mat.name||'',map:imageKey,blend:mat.blending===THREE.AdditiveBlending?2:mat.transparent?1:0,lit:!mat.isMeshBasicMaterial&&!spr,
     glow:mat.emissive?Math.min(1,Math.max(mat.emissive.r,mat.emissive.g,mat.emissive.b)*mat.emissiveIntensity):0,
     count,indices:bytes(idArray),pos:bytes(p),normal:bytes(n),uv:bytes(tex),color:bytes(colors),morph:Object.fromEntries(Object.entries(morph).map(([k,v])=>[k,bytes(v)]))});
  }
 });
 return {parts,images};
}

function encode(data){
 const chunks=[],i32=v=>{const b=Buffer.alloc(4);b.writeInt32LE(v);chunks.push(b);};
 const str=s=>{const b=Buffer.from(s,'utf8');i32(b.length);chunks.push(b);};
 for(const [k,s] of Object.entries(data.images)){
  const b=Buffer.from(s,'base64'),key=crypto.createHash('sha256').update(b).digest('hex').slice(0,16);
  fs.writeFileSync(path.join(textures,key+'.png'),b);data.images[k]=key;
 }
 i32(data.parts.length);
 for(const p of data.parts){
  str(data.images[p.map]||'');i32(p.blend);i32(p.lit?1:0);i32(Math.round(p.glow*255));i32(p.count);
  for(const k of ['pos','normal','uv','color'])chunks.push(Buffer.from(p[k],'base64'));
  const idx=Buffer.from(p.indices,'base64');i32(idx.length/4);chunks.push(idx);
  i32(Object.keys(p.morph).length);for(const [k,v] of Object.entries(p.morph)){str(k);chunks.push(Buffer.from(v,'base64'));}
 }
 return Buffer.concat(chunks);
}

(async()=>{
 fs.mkdirSync(models,{recursive:true});fs.mkdirSync(textures,{recursive:true});
 const b=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const p=await b.newPage({viewport:{width:375,height:667}});
  await p.route('https://script.google.com/**',r=>r.abort());
  const archived=require('./archived-three.cjs');
  await p.route('**/motion-entry.js*',r=>r.fulfill({contentType:'text/javascript',body:''}));
  await p.route('**/version.txt*',r=>r.fulfill({body:archived('version.txt')}));
  await p.route(base,r=>r.fulfill({contentType:'text/html',body:archived('index.html').replace('function tryTrick(idx)',`window.worldReady=()=>loadTrackAssets();window.worldExport=${exportModel};function tryTrick(idx)`)}));
  await p.goto(base,{waitUntil:'domcontentloaded'});await p.waitForFunction(()=>window.worldReady&&window.__g?.()?.mode==='menu');
  await p.evaluate(()=>worldReady());
  for(const [key,spec] of Object.entries(specs)){
   if(include&&!include.test(key))continue;
   const data=await p.evaluate(async ([type,props])=>worldExport(type,props),spec);
   if(!data.parts.length)throw Error('Empty model '+key);
   fs.writeFileSync(path.join(models,key+'.bytes'),encode(data));
  }
  console.log('Exported '+Object.keys(specs).filter(k=>!include||include.test(k)).length+' original on-course models.');
 }finally{await b.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
