import * as THREE from 'three';
const clamp=THREE.MathUtils.clamp;

// A turn loads the outside edge, then releases it before the next traverse.
// Visuals and collisions both use this integrated position, including avoidance.
export function advanceCarve(o,dt){
  o.rideAge=(o.rideAge||0)+dt;
  if(o.air>0){
    o.x=clamp(o.x+(o.carveV||0)*dt,30,370);
    return;
  }
  const t=o.rideAge*(o.w||1.18),phase=o.ph||0;
  const wave=t+phase+.32*Math.sin(t*.54+phase);
  const span=o.amp*(o.sb?1.20:1.08);
  const target=clamp(o.bx+span*Math.sin(wave),30,370);
  const old=o.carveV||0;
  const a=clamp((target-o.x)*9-old*4.6,-128,128);
  o.carveV=clamp(old+a*dt,-o.vy*.87,o.vy*.87);
  const before=o.x;o.x=clamp(o.x+o.carveV*dt,30,370);
  o.carveV=(o.x-before)/dt;
  const steer=Math.atan2(o.carveV,o.vy);
  o.pace=o.vy*(1-.16*Math.abs(Math.sin(steer)));
  o.bearing=-Math.atan2(o.carveV,o.pace);
  o.edge=THREE.MathUtils.lerp(o.edge||0,clamp(-a/130,-1,1),1-Math.exp(-5*dt));
}

// The resort visitors are textured rigid meshes. Build two reusable blend shapes
// in the riding frame once per source geometry. Boots, skis and board stay fixed.
const shapes=new WeakMap();
export function prepareRider(body){
  body.updateMatrixWorld(true);
  const bounds=new THREE.Box3().setFromObject(body),height=bounds.max.y-bounds.min.y;
  const moving=[];
  body.traverse(mesh=>{
    if(!mesh.isMesh||mesh.isSkinnedMesh||!height)return;
    let geometry=shapes.get(mesh.geometry);
    if(!geometry){
      const src=mesh.geometry,pos=src.attributes.position;
      geometry=src.clone();
      const flex=new Float32Array(pos.count*3),edge=new Float32Array(pos.count*3);
      const inv=new THREE.Matrix3().setFromMatrix4(mesh.matrixWorld).invert();
      const v=new THREE.Vector3(),delta=new THREE.Vector3();
      for(let i=0;i<pos.count;i++){
        v.fromBufferAttribute(pos,i).applyMatrix4(mesh.matrixWorld);
        const h=clamp((v.y-bounds.min.y)/height,0,1);
        const boot=THREE.MathUtils.smoothstep(h,.14,.54),chest=THREE.MathUtils.smoothstep(h,.43,.89);
        delta.set(0,-.105*boot,-.055*chest+.028*boot).multiplyScalar(height).applyMatrix3(inv).toArray(flex,i*3);
        delta.set(.105*chest+.025*boot,-.012*chest,0).multiplyScalar(height).applyMatrix3(inv).toArray(edge,i*3);
      }
      geometry.morphTargetsRelative=true;
      geometry.morphAttributes.position=[new THREE.BufferAttribute(flex,3),new THREE.BufferAttribute(edge,3)];
      shapes.set(src,geometry);
    }
    mesh.geometry=geometry;mesh.updateMorphTargets();moving.push(mesh);
  });
  return moving;
}
