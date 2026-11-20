// Authored poses for the downhill run. Physics owns all global travel.
import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
export function authoredHeight(t){if(t<=3.25||t>=4.08)return 0;const u=(t-3.25)/.83;return 4*.28*u*(1-u);}
export function motionTime(p,state,t){
 const airborne=p.air>0;
 if(state.wasAir&&!airborne)state.landed=t;
 state.wasAir=airborne;
 if(airborne&&p.trick>0&&p.style!=null)return 6+p.style+clamp(p.trick,0,.999);
 if(airborne)return 3.25+.83*clamp((p.airT||0)/(p.airMax||1),0,.999);
 if(state.landed!=null&&t-state.landed<.77)return 4.08+Math.max(0,t-state.landed);
 if((p.rampH||0)>.025)return 2.7+.51*clamp(p.popPhase??p.rampH/.65,0,1);
 const a=clamp(p.ang||0,-1,1);
 // In a straight glide, use the grounded compression keys from the same rig.
 // The two turn clips start in this stance, with boots and equipment attached.
 const turn=Math.abs(a);
 if(turn<.10){
   const settle=.09+.059*Math.sin(t*2.5)+.024*Math.sin(t*3.8)
     +.075*(p.tuck||0)+.06*clamp(Math.abs(p.mvz||0)/3,0,1);
   return 2.7+settle*(1-turn/.10);
 }
 const edge=(turn-.10)/.90;
 return a>0?.8+.7*edge:2.7-.55*edge;
}
function boardContactGeometry(model){
 const deck=model.getObjectByName('Snowboard');if(!deck)return null;
 const outline=new Map();deck.traverse(mesh=>{
  const vertices=mesh.geometry?.attributes.position;if(!vertices)return;
  for(let i=0;i<vertices.count;i++){
   const point=new THREE.Vector3().fromBufferAttribute(vertices,i);
   deck.worldToLocal(mesh.localToWorld(point));
   const key=point.x.toFixed(5)+':'+point.z.toFixed(5);
   if(!outline.has(key)||outline.get(key).y>point.y)outline.set(key,point);
  }
 });
 return {deck,points:[...outline.values()],sample:new THREE.Vector3()};
}
export function boardSurfaceLift(h,surface,normal){
 const contact=h.motion?.contact;if(!contact||!h.motion.active)return 0;
 const {deck,points,sample}=contact;deck.updateWorldMatrix(true,false);
 let clearance=Infinity;
 for(const point of points){
  sample.copy(point).applyMatrix4(deck.matrixWorld).sub(surface);
  clearance=Math.min(clearance,sample.dot(normal));
 }
 return Math.max(0,(.006-clearance)/normal.y);
}
export async function attachHeroMotion(h){
 const original=h.holder.children[0],loader=new GLTFLoader();h.motionStatus='loading';
 try{
  const embedded=window.__MOTION_STUDIES?.[h.kind];
  const gltf=embedded?await new Promise((resolve,reject)=>{const u=Uint8Array.from(atob(embedded),c=>c.charCodeAt(0));loader.parse(u.buffer,'',resolve,reject)}):await loader.loadAsync('models/studies/'+h.kind+'.glb?v=2026-11-19d');
  const model=gltf.scene;model.visible=false;h.holder.add(model);
  model.traverse(o=>{if(o.isMesh){o.castShadow=true;o.frustumCulled=false;for(const m of (Array.isArray(o.material)?o.material:[o.material])){m.metalness=0;m.roughness=Math.max(.6,m.roughness);}}});
  // Remove the authored jump from the baked translation tracks themselves.
  // Blender exports frame 1 at 1/30 s. The rig rotates about Y, so Y remains vertical.
  // Correct both Hips and independent equipment, using each track's exact key time.
  for(const clip of gltf.animations)for(const track of clip.tracks){
   if(!/^(mixamorig:?Hips|Left_Ski|Right_Ski|Snowboard|Left_Binding|Right_Binding)\.position$/.test(track.name))continue;
   for(let i=0;i<track.times.length;i++)track.values[i*3+1]-=authoredHeight(track.times[i]-1/30);
  }
  const mixer=new THREE.AnimationMixer(model);gltf.animations.forEach(c=>mixer.clipAction(c).play());
  const nodes=[];model.traverse(o=>{if(o.isBone||['Left_Ski','Right_Ski','Snowboard','Left_Binding','Right_Binding'].includes(o.name))nodes.push({o,position:o.position.clone(),quaternion:o.quaternion.clone(),samplePos:o.position.clone(),sampleRot:o.quaternion.clone()})});
  mixer.setTime(1/30);model.updateWorldMatrix(true,true);
  h.motion={model,mixer,original,nodes,contact:boardContactGeometry(model),state:{},active:false,lastT:null};h.motionStatus='ready';
 }catch(error){h.motionStatus='error';console.warn('Motion study unavailable; original hero retained',h.kind,error)}
}
export function poseHeroMotion(h,p,mode,t){
 const m=h.motion;if(!m)return false;
 const use=mode!=='menu'&&mode!=='crash'&&!(p.fall>0);
 m.model.visible=use;m.original.visible=!use;
 if(!use){m.active=false;m.lastT=null;m.state={};m.model.position.y=0;return false;}
 const dt=m.lastT==null?0:clamp(t-m.lastT,0,.1);if(m.lastT!=null&&t<m.lastT){m.state={};m.active=false;}m.lastT=t;
 // The mixer skips unchanged tracks. Return the previous raw sample before evaluating
 // so an intermediate blend cannot become the next target for a boot, hand or ski.
 if(m.sampled)for(const n of m.nodes){n.o.position.copy(n.samplePos);n.o.quaternion.copy(n.sampleRot);}
 const sample=motionTime(p,m.state,t);m.mixer.setTime(sample+1/30);m.sampled=true;
 const alpha=m.active?1-Math.exp(-(p.trick>0?37:26)*dt):1;
 for(const n of m.nodes){n.samplePos.copy(n.o.position);n.sampleRot.copy(n.o.quaternion);n.position.lerp(n.samplePos,alpha);n.quaternion.slerp(n.sampleRot,alpha);n.o.position.copy(n.position);n.o.quaternion.copy(n.quaternion);}
 // The clip is now in place; the parent follows the game's jump trajectory.
 m.model.position.y=0;m.active=true;h.holder.position.y=h.kind==='bear'?.022:0;
 h.holder.rotation.set(h.kind==='bear'?0:-(h.slopeTilt||0),h.kind==='owl'?Math.PI/2:Math.PI,0,'YXZ');
 h.skis?.forEach(o=>o.visible=false);if(h.board)h.board.visible=false;
 return true;
}
