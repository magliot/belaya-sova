import * as THREE from 'three';
import {courseCenter,courseHeight,courseHeading} from './course.js';

const folder='art/slope/';
export function createLandscape(group){
  const loader=new THREE.TextureLoader(),maps={},ridges=[],routes=[],auroras=[];
  let ready=false;
  function panel(map,w,h,parent){
    const mat=new THREE.MeshBasicMaterial({map,transparent:true,depthWrite:false,side:THREE.DoubleSide,fog:false,toneMapped:false});
    const p=new THREE.Mesh(new THREE.PlaneGeometry(w,h),mat);parent.add(p);return p;
  }
  // Tangent panels overlap only in the fading outer margin of their texture.
  async function load(){
    const names=['ridge-52','tubing-62','aurora-54'].flatMap(base=>[...'abc'].map(c=>base+c));
    await Promise.all(names.map(async n=>{
      const t=await loader.loadAsync(folder+n+'.webp');t.colorSpace=THREE.SRGBColorSpace;maps[n]=t;
    }));
    for(let i=0;i<9;i++){
      const a=(i-4)*Math.PI/5,p=panel(maps['ridge-52'+'abc'[i%3]],441,247,group);
      p.position.set(Math.sin(a)*600,-32,-Math.cos(a)*600);p.rotation.y=-a;
      p.renderOrder=-12;ridges.push(p);
    }
    for(let i=0;i<3;i++){
      const p=panel(maps['tubing-62'+'abc'[i]],620,235,group);
      p.position.set(0,40,-365-i*.2);p.renderOrder=-9+i;routes.push(p);
      const n=panel(maps['aurora-54'+'abc'[i]],300,112,group);
      n.position.set((i-1)*246,86,-365);n.rotation.y=-(i-1)*.39;n.renderOrder=-4;auroras.push(n);
    }
    ready=true;
  }
  function update(d,tubing,zone){
    if(!ready)return;
    const y=courseHeight(d),x=courseCenter(d);group.position.set(x,y-48,-d);
    const dusk=THREE.MathUtils.smoothstep(d,700,770),trees=THREE.MathUtils.smoothstep(d,305,365);
    const heading=courseHeading(-d)*.72;
    const weights=[1-trees,trees*(1-dusk),dusk];
    for(const [i,p] of routes.entries()){
      p.visible=!!tubing&&weights[i]>.001;p.material.opacity=weights[i];p.rotation.y=heading;
      const z=-365-i*.2,offset=i===2?35:0;
      p.position.set(z*Math.sin(heading)+offset*Math.cos(heading),i===2?78:62,z*Math.cos(heading)-offset*Math.sin(heading));
    }
    const night=zone===3||zone===4;
    for(const p of ridges){p.material.color.setHex(night?0x476486:zone===1?0xffe2d1:0xffffff);}
    for(const p of auroras)p.visible=night&&!tubing;
  }
  return {load,update};
}
