// Authored poses for the downhill run. Physics owns all global travel.
import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
export function authoredHeight(t){if(t<=3.25||t>=4.08)return 0;const u=(t-3.25)/.83;return 4*.28*u*(1-u);}
export function motionTime(p,state,t){
 const airborne=p.air>0;
 if(state.wasAir&&!airborne)state.landed=t;
 state.wasAir=airborne;
 if(airborne)return 3.25+.83*clamp((p.airT||0)/(p.airMax||1),0,.999);
 if(state.landed!=null&&t-state.landed<.77)return 4.08+Math.max(0,t-state.landed);
 if((p.rampH||0)>.025)return 2.7+.35*clamp(p.rampH/.4,0,1);
 const a=clamp(p.ang||0,-1,1);
 return a>=0?.8+.7*a:2.7+.55*a;
}
export async function attachHeroMotion(h){
 const original=h.holder.children[0],loader=new GLTFLoader();h.motionStatus='loading';
 try{
  const embedded=window.__MOTION_STUDIES?.[h.kind];
  const gltf=embedded?await new Promise((resolve,reject)=>{const u=Uint8Array.from(atob(embedded),c=>c.charCodeAt(0));loader.parse(u.buffer,'',resolve,reject)}):await loader.loadAsync('models/studies/'+h.kind+'.glb?v=2026-11-18a');
  const model=gltf.scene;model.visible=false;h.holder.add(model);
  model.traverse(o=>{if(o.isMesh){o.castShadow=true;o.frustumCulled=false;for(const m of (Array.isArray(o.material)?o.material:[o.material])){m.metalness=0;m.roughness=Math.max(.6,m.roughness);}}});
  // Remove the authored jump from the baked translation tracks themselves.
  // Blender exports frame 1 at 1/30 s. The rig rotates about Y, so Y remains vertical.
  // Correct both Hips and independent equipment, using each track's exact key time.
  for(const clip of gltf.animations)for(const track of clip.tracks){
   if(!/^(mixamorig:?Hips|Left_Ski|Right_Ski|Snowboard|Left_Binding|Right_Binding|Left_Pole|Right_Pole)\.position$/.test(track.name))continue;
   for(let i=0;i<track.times.length;i++)track.values[i*3+1]-=authoredHeight(track.times[i]-1/30);
  }
  const mixer=new THREE.AnimationMixer(model);gltf.animations.forEach(c=>mixer.clipAction(c).play());
  const nodes=[];model.traverse(o=>{if(o.isBone||['Left_Ski','Right_Ski','Snowboard','Left_Binding','Right_Binding','Left_Pole','Right_Pole'].includes(o.name))nodes.push({o,position:o.position.clone(),quaternion:o.quaternion.clone()})});
  mixer.setTime(1/30);model.updateWorldMatrix(true,true);
  const grips=[];
  for(const side of ['Left','Right']){
   let hand;model.traverse(o=>{if(o.isBone&&o.name.replace(/^mixamorig:?/,'')===side+'Hand')hand=o});
   const pole=model.getObjectByName(side+'_Pole');
   if(hand&&pole)grips.push({hand,pole,offset:hand.worldToLocal(pole.getWorldPosition(new THREE.Vector3())),point:new THREE.Vector3()});
  }
  h.motion={model,mixer,original,nodes,grips,state:{},active:false,lastT:null};h.motionStatus='ready';
 }catch(error){h.motionStatus='error';console.warn('Motion study unavailable; original hero retained',h.kind,error)}
}
export function poseHeroMotion(h,p,mode,t){
 const m=h.motion;if(!m)return false;
 const use=mode!=='menu'&&mode!=='crash'&&!(p.fall>0);
 m.model.visible=use;m.original.visible=!use;
 if(!use){m.active=false;m.lastT=null;m.state={};m.model.position.y=0;return false;}
 const dt=m.lastT==null?0:clamp(t-m.lastT,0,.1);if(m.lastT!=null&&t<m.lastT)m.state={};m.lastT=t;
 const sample=motionTime(p,m.state,t);m.mixer.setTime(sample+1/30);
 const alpha=m.active?1-Math.exp(-24*dt):1;
 for(const n of m.nodes){n.position.lerp(n.o.position,alpha);n.quaternion.slerp(n.o.quaternion,alpha);n.o.position.copy(n.position);n.o.quaternion.copy(n.quaternion);}
 // The clip is now in place; the parent follows the game's jump trajectory.
 m.model.position.y=0;m.active=true;h.holder.position.y=0;
 h.holder.rotation.set(-(h.slopeTilt||0),h.kind==='owl'?Math.PI/2:Math.PI,0,'YXZ');
 // Smoothing the skeleton and a separate prop can introduce wrist drift.
 // Keep the grip fixed in mitten space after blending, without scaling the shaft.
 m.model.updateWorldMatrix(true,true);
 for(const g of m.grips){g.point.copy(g.offset);g.hand.localToWorld(g.point);g.pole.parent.worldToLocal(g.point);g.pole.position.copy(g.point);}
 h.skis?.forEach(o=>o.visible=false);if(h.board)h.board.visible=false;h.poles?.forEach(p=>p.g.visible=false);
 return true;
}
