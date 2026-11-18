import * as THREE from 'three';

const lights = new Map();

// One transparent drawing per palette. The middle stays clear even close to the camera.
function lightTexture(palette, canvasTex) {
  if (lights.has(palette)) return lights.get(palette);
  const ring = palette === 'ring';
  const colors = ring ? ['#883eff','#ff48a5','#ffcd55','#75ffeb','#883eff']
    : palette === 'blue' ? ['#245eed','#38d4ff','#eaffef','#38d4ff','#245eed']
    : ['#d72d5b','#ff8b49','#fff497','#ff8b49','#d72d5b'];
  const tex = canvasTex(512,512,(ctx,w,h)=>{
    const c=w/2,r=192,from=ring?0:Math.PI,to=Math.PI*2;
    const spectrum=ctx.createLinearGradient(c-r,0,c+r,0);
    colors.forEach((color,i)=>spectrum.addColorStop(i/(colors.length-1),color));
    function stroke(radius,width,color,alpha,start=from,end=to){
      ctx.globalAlpha=alpha;ctx.strokeStyle=color;ctx.lineWidth=width;
      ctx.beginPath();ctx.arc(c,c,radius,start,end);ctx.stroke();
    }
    // A thin saturated rim keeps the white-hot centre readable against sunlit snow.
    for(const [width,alpha] of [[83,.06],[59,.10],[40,.18],[25,.40],[12,.96]])stroke(r,width,spectrum,alpha);
    stroke(r+6,2,ring?'#7a4aab':'#42618b',.70);
    stroke(r,3.5,'#fffbe3',1);
    const n=ring?12:8;
    for(let i=0;i<n;i++){
      const a=from+(i+.5)*(to-from)/n;
      stroke(r+14,2.5,spectrum,.94,a-.065,a+.065);
      const x=c+Math.cos(a)*(r+16),y=c+Math.sin(a)*(r+16);
      const s=i%3===0?9:3.3;
      ctx.globalAlpha=.96;ctx.fillStyle='#fffce6';ctx.beginPath();
      for(let j=0;j<8;j++){
        const angle=j*Math.PI/4,rad=j%2?s*.23:s;
        const px=x+Math.cos(angle)*rad,py=y+Math.sin(angle)*rad;
        if(j)ctx.lineTo(px,py);else ctx.moveTo(px,py);
      }
      ctx.closePath();ctx.fill();
    }
    ctx.globalAlpha=1;
  });
  lights.set(palette,tex);
  return tex;
}

export function raceLight(group,rx,ry,palette,canvasTex){
  const material=new THREE.MeshBasicMaterial({map:lightTexture(palette,canvasTex),
    transparent:true,depthWrite:false,toneMapped:false,side:THREE.DoubleSide});
  const light=new THREE.Mesh(new THREE.PlaneGeometry(rx*512/192,ry*512/192),material);
  light.position.set(0,palette==='ring'?0:.08,.025);
  group.add(light);
  return light;
}
