// CPU checks against actual course geometry and actual scene update functions.
// These do not replace the 375×812 WebGL checks before publication.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import * as THREE from '../docs/vendor/three/build/three.module.min.js';
import * as course from '../docs/course.js';
const {courseCenter,courseDerivative,courseCurvature,courseHeading,courseGrade,courseHeight,courseCrossHeight,courseCrossSlope,courseGround,coursePitch,courseSlip}=course;
const a=new THREE.Vector3(),b=new THREE.Vector3(),c=new THREE.Vector3();
let maxTurn=0,minMetric=Infinity,minGrade=Infinity,maxGrade=-Infinity;
for(let d=0;d<=20000;d+=.5){
 const eps=.01,p=courseDerivative(d),curv=courseCurvature(d),grade=courseGrade(d);
 assert(Math.abs((courseCenter(d+eps)-courseCenter(d-eps))/(2*eps)-p)<1e-7);
 assert(Math.abs((courseHeight(d-eps)-courseHeight(d+eps))/(2*eps)-grade)<1e-7);
 maxTurn=Math.max(maxTurn,Math.abs(courseHeading(-d)));minGrade=Math.min(minGrade,grade);maxGrade=Math.max(maxGrade,grade);
 for(const u of [-40,-15,-9,0,9,15,40]){
  minMetric=Math.min(minMetric,1-u*curv);assert(1-u*curv>.35,'Course folds at the outer forest edge');
  courseGround(a,u,-d);assert(a.toArray().every(Number.isFinite));
  courseGround(b,u,-d-eps);courseGround(c,u,-d+eps);
  const pitch=Math.atan2(c.y-b.y,Math.hypot(c.x-b.x,c.z-b.z));
  assert(Math.abs(pitch-coursePitch(u,-d))<1e-5,'Pitch disagrees with the actual bent surface');
  assert(Math.abs(courseCrossSlope(u,-d))<1.3,'Snowbank has a vertical wall');
 }
 courseGround(a,9,-d);courseGround(b,-9,-d);
 assert(Math.abs(Math.hypot(a.x-b.x,a.z-b.z)-18)<1e-9,'Playable corridor changes width');
 assert(Math.abs(courseSlip(d,0,40))<=1.8,'Slip exceeds control limit');
}
assert(maxTurn>.6,'Turns are too subtle');assert(maxGrade-minGrade>.55,'Slope needs visible grade changes');
// Exercise the real renderer positioning code without a GPU or invented duplicate formulas.
const source=fs.readFileSync('docs/index.html','utf8');
const slice=(from,to)=>source.slice(source.indexOf(from),source.indexOf(to,source.indexOf(from)));
const context={THREE,...course,console,Math,Number,Array,
 clamp:(v,a,b)=>Math.max(a,Math.min(b,v)),lerp:(a,b,t)=>a+(b-a)*t,rnd:(a,b)=>(a+b)/2,
 U:.05,X:x=>(x-200)*.05,Z:y=>-y*.05,Hz:z=>courseHeight(-z),
 scene:new THREE.Scene(),mogAt:()=>0,forestEdge:()=>12.2,hero:'bear',
 env:{glow:1,snow:1},beamMat:{},mountains:new THREE.Group(),aurora:new THREE.Group(),
 tmpM:new THREE.Matrix4(),tmpP:new THREE.Vector3(),tmpQ:new THREE.Quaternion(),tmpS:new THREE.Vector3(),tmpE:new THREE.Euler(),
 forest:[{userData:{x:14,z:-12},side:1,off:1.8,rot:.3,s:1,kind:0,idx:0}],
 forestIM:[new THREE.InstancedMesh(new THREE.BoxGeometry(),new THREE.MeshBasicMaterial(),1)],
 lamps:[Object.assign(new THREE.Group(),{userData:{z:-8,side:1,bx:-1}})],lampLights:[new THREE.PointLight()],
 TOWER_STEP:35,TOWERN:9,LIFT_X:[14.4,16.4],TOP:8.6,
 towers:Array.from({length:9},(_,i)=>{const t=new THREE.Group();t.userData={z:-35*i,cab:[new THREE.Mesh(new THREE.CylinderGeometry()),new THREE.Mesh(new THREE.CylinderGeometry())]};t.add(...t.userData.cab);return t}),
 chairs:[Object.assign(new THREE.Group(),{userData:{up:true,z:-12}})],
 HEROMODELS:{bear:{root:new THREE.Group(),pivot:new THREE.Group(),glb:true,kind:'bear'}},
 TRICKS:[{f:()=>[0,0,0]}],pivC:new THREE.Vector3(),fallPose:()=>null,poseGlb:()=>{},rivalSync:()=>{},racerOpacity:()=>{},
 sun:new THREE.DirectionalLight(),headlamp:new THREE.SpotLight(),
 SNOWN:2,snowGeo:new THREE.BufferGeometry().setAttribute('position',new THREE.BufferAttribute(new Float32Array(6),3)),snowPts:new THREE.Group(),
 camera:new THREE.PerspectiveCamera(70,375/812,.1,500),
 G:{mode:'play',y:0,t:0,camX:0,camY:2.4,wind:0,shake:0,p:{x:200,ang:0,air:0,alt:0,rampH:0,mh:0,trick:0,inv:0}},
};
vm.createContext(context);
vm.runInContext(slice('const terrainPoint=','const piste='),context);
vm.runInContext('var piste=slopePlane(21,240,160,new THREE.MeshBasicMaterial(),.02);var powder=slopePlane(80,240,160,new THREE.MeshBasicMaterial(),0);',context);
vm.runInContext(slice('function objectFrame(','const pickCol='),context);
vm.runInContext(slice('const v3=new THREE.Vector3();','// меню: нарисованный'),context);
const hm=context.HEROMODELS.bear,camera=context.camera;
let frameSamples=0;
for(const d of [0,35,80,150,240,420,800,2500,2600,5100,7500,7580,7900,13000,19999])for(const x of [20,200,380]){
 context.G.y=d*20;context.G.p.x=x;context.G.camX=(x-200)*.05;context.G.camY=courseHeight(d-6.4)+2.3;
 for(let i=0;i<30;i++)context.syncWorld(1/60);
 courseGround(a,(x-200)*.05,-d);assert(a.distanceTo(hm.root.position)<1e-8,'Hero floats or misses the track');
 camera.updateMatrixWorld(true);b.copy(hm.root.position).project(camera);
 assert(Math.abs(b.x)<1&&Math.abs(b.y)<1&&b.z<1,`Hero leaves viewport: d=${d}, x=${x}, ndc=${b.toArray()}`);
 assert(camera.position.y>courseHeight(d-6.4)+courseCrossHeight(context.G.camX*.75,6.4-d)+1,'Camera goes underground');
 const gate={type:'gate',x,y:d*20,m:new THREE.Group()};context.placeObj(gate);
 assert(gate.m.position.distanceTo(a)<1e-8,'Gate visuals disagree with collision coordinates');
 for(const t of context.towers){const z=t.userData.z;t.updateMatrixWorld(true);
  for(let j=0;j<2;j++){
   const cable=t.userData.cab[j];cable.updateMatrixWorld(true);
   b.set(0,-.5,0).applyMatrix4(cable.matrixWorld);c.set(0,.5,0).applyMatrix4(cable.matrixWorld);
   context.liftAnchor(a,context.LIFT_X[j],z);assert(b.distanceTo(a)<1e-7,'Cable misses its tower');
   context.liftAnchor(a,context.LIFT_X[j],z-35);assert(c.distanceTo(a)<1e-7,'Cable misses the next tower');
  }
 }
 for(const m of [context.piste,context.powder])assert(m.geometry.attributes.position.array.every(Number.isFinite));
 frameSamples++;
}
// Shared samples in recycled terrain must remain stationary after the mesh moves.
context.fitPlane(context.piste,-96);const old=context.piste.geometry.attributes.position.array.slice(),oldUV=context.piste.geometry.attributes.uv.array.slice();
context.fitPlane(context.piste,-108);const fresh=context.piste.geometry.attributes.position.array;
const verticesPerRow=context.piste.geometry.parameters.widthSegments+1;
// Plane rotation maps row 0 to z=-120: after a -12m move, row+8 is the same world point.
for(let row=0;row<152;row+=9)for(let col=0;col<verticesPerRow;col+=7){
 const i=(row*verticesPerRow+col)*3,j=((row+8)*verticesPerRow+col)*3;
 for(let k=0;k<3;k++)assert(Math.abs(old[i+k]-fresh[j+k])<.0001,'Terrain jumps when its chunk recycles');
 assert(Math.abs(oldUV[i/3*2+1]-context.piste.geometry.attributes.uv.array[j/3*2+1])<1e-6,'Snow texture slides during recycling');
}
// Repeated animation must not accumulate the course rotation on pooled objects.
Object.assign(context,{release:o=>{o.m=null},PN:0,parts3:[],partMesh:{instanceMatrix:{}},emit:()=>{},npcLift:()=>0,animNpc:(o,m)=>{m.rotation.y=.18;m.rotation.z=.04}});
context.G.texts=[];context.G.objs=['gate','ramp','coin','npc'].map((type,i)=>({type,x:100+i*50,y:context.G.y+400,m:new THREE.Group()}));
for(const o of context.G.objs)context.placeObj(o);
vm.runInContext(slice('function animObjs(dt){','function hit(kind,o){'),context);
for(let i=0;i<240;i++)context.animObjs(1/60);
for(const o of context.G.objs){
 const z=-o.y*.05;courseGround(a,(o.x-200)*.05,z);assert(a.distanceTo(o.m.position)<1e-8);
 assert(Math.abs(o.m.rotation.y-courseHeading(z)-(o.type==='npc'?.18:0))<1e-8,'Course yaw accumulates across frames');
}
// Autopilot follows existing generated gate series through the actual new steering block.
const steerBlock=slice('  const steerK=','  for(const k of[\'boost\'');
let playerGates=0,minGateMargin=Infinity;
for(const fps of [30,60])for(const speed of [230,500,800])for(const steer of [1,1.15])for(const start of [0,80,2500,13000]){
 let seed=19;const math=Object.create(Math);math.random=()=>((seed=(seed*1664525+1013904223)>>>0)/4294967296);
 const G={mode:'play',y:start*20,speed,wind:0,objs:[],lastGateX:200,gateCol:0,p:{x:200,vx:0,ang:0,air:0,fall:0}};
 const ctx={...course,G,COURSE_REVIEW:false,drag:{},keys:{},Math:math,U:.05,X:context.X,clamp:context.clamp,lerp:context.lerp,h:{steer},sensK:()=>1,rnd:(a,b)=>a+math.random()*(b-a),addObj:o=>G.objs.push(o),hit:()=>{throw Error('Steered player hit the edge');}};
 vm.createContext(ctx);vm.runInContext(slice('function addCourse(','function addGate(')+`\nfunction step(dt){const p=G.p,dist=G.y*.05;${steerBlock}}`,ctx);
 let end=G.y+speed*1.5;for(let k=0;k<4;k++)end+=ctx.addCourse(end,1)+700;
 const gates=G.objs.filter(o=>o.type==='gate');let next=0;
 while(next<gates.length){const gate=gates[next];G.tx=gate.x;ctx.step(1/fps);G.y+=speed/fps;
  if(G.y>=gate.y){const margin=gate.gap/2-Math.abs(G.p.x-gate.x);assert(margin>0,`Player cannot reach gate at speed ${speed}`);minGateMargin=Math.min(minGateMargin,margin);playerGates++;next++;}
 }
 G.p.air=1;ctx.step(1/fps);assert.equal(G.p.curveSlip,0,'No surface grip while airborne');
}
const unattended=[];
for(const fps of [30,60])for(const speed of [230,500,800])for(const start of [0,80,150,420,2500,13000]){
 const G={mode:'play',y:start*20,speed,wind:0,tx:200,p:{x:200,vx:0,ang:0,air:0,fall:0}};
 const ctx={...course,G,COURSE_REVIEW:false,drag:null,keys:{},Math,U:.05,X:context.X,clamp:context.clamp,lerp:context.lerp,h:{steer:1},sensK:()=>1,hit:kind=>{G.mode='crash';G.failKind=kind;}};
 vm.createContext(ctx);vm.runInContext(`function step(dt){const p=G.p,dist=G.y*.05;${steerBlock}}`,ctx);
 let seconds=0;while(G.mode==='play'&&seconds<30){ctx.step(1/fps);G.y+=speed/fps;seconds+=1/fps;}
 assert.equal(G.failKind,'edge','Releasing input must hit the snowbank without other obstacles');
 unattended.push({fps,speed,start,seconds});
}
console.log(JSON.stringify({passed:true,distanceTestedMetres:20000,sceneSamples:frameSamples,playerGates,minGateMargin,maxHeadingDegrees:maxTurn*180/Math.PI,minOffsetMetric:minMetric,gradeRange:[minGrade,maxGrade],unattendedEdgeCases:unattended.length,maxUnattendedSeconds:Math.max(...unattended.map(r=>r.seconds)),browserVisualCheck:'run check-course-browser.cjs and inspect PNGs'},null,2));
