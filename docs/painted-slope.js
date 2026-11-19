import * as THREE from 'three';
import {courseGround,courseCenter,courseHeight,courseHeading} from './course.js';

const folder='art/slope/';
export function createLandscape(group){
  const loader=new THREE.TextureLoader(),maps={},lodges=[],ridges=[],village=[],routes=[],auroras=[];
  let ready=false;
  function panel(map,w,h,parent){
    const mat=new THREE.MeshBasicMaterial({map,transparent:true,depthWrite:false,side:THREE.DoubleSide,fog:false,toneMapped:false});
    const p=new THREE.Mesh(new THREE.PlaneGeometry(w,h),mat);parent.add(p);return p;
  }
  // Tangent panels overlap only in the fading outer margin of their texture.
  async function load(){
    const names=['village-51c',...['ridge-52','tubing-62','aurora-54'].flatMap(base=>[... 'abc'].map(c=>base+c))];
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
      n.position.set((i-1)*246,126,-365);n.rotation.y=-(i-1)*.39;n.renderOrder=-4;auroras.push(n);
    }
    for(let i=0;i<3;i++){
      const p=panel(maps['village-51c'],[38,31,25][i],[27,22,17.8][i],group);
      p.renderOrder=-3;village.push(p);
    }
    for(let i=0;i<4;i++){
      const p=panel(maps['village-51c'],i%2?15:21,i%2?11.2:15.6,group);p.renderOrder=-2;lodges.push(p);
    }
    ready=true;
  }
  // Lodges stay fixed at actual course coordinates until they are behind the camera.
  // The skyline follows the skier; the closer lodges move in parallax.
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
    for(let i=0;i<village.length;i++){
      const p=village[i],side=i===0?-1:1,lateral=side*[26,72,103][i];
      const z=-245-i*14;
      p.position.set(lateral*Math.cos(heading)+z*Math.sin(heading),[17,14,12][i],z*Math.cos(heading)-lateral*Math.sin(heading));
      p.rotation.y=heading;p.visible=!tubing;
      p.material.color.setHex(zone===3||zone===4?0x8591ac:0xfff4e9);
    }
    const night=zone===3||zone===4;
    for(const p of ridges){p.material.color.setHex(night?0x476486:zone===1?0xffe2d1:0xffffff);}
    for(const p of auroras)p.visible=night&&!tubing;
    for(let i=0;i<lodges.length;i++){
      const site=Math.floor((d+65)/430)+Math.floor(i/2),small=i%2;
      const at=site*430+145+small*19,side=site%2?-1:1;
      const u=side*(small?30:19),p=lodges[i],delta=at-d;
      p.visible=delta>-18&&delta<270&&(!tubing||d>700);
      if(!p.visible)continue;
      courseGround(p.position,u,-at,small?4.8:6.9);p.position.sub(group.position);
      p.rotation.y=side*-.10;p.material.color.setHex(night?0xb4bedf:0xffffff);
      p.material.opacity=1-THREE.MathUtils.smoothstep(delta,210,270);
    }
  }
  return {load,update};
}
