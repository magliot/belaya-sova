import * as THREE from 'three';
import {courseRoll} from './course.js';

// Each spark has a bright head and delayed samples of the same ballistic path.
// A whole shell, including its rising rocket, uses one draw call.
function burstGeometry(style){
  const velocity=[],trail=[],color=[],life=[];
  const palettes=[[0xff586d,0xffb45a,0xffedb5],[0x5df8e3,0x84c1ff,0xffa9f6],[0xffad57,0xfff18e,0xff79a8]];
  const palette=palettes[style];
  function spark(v,c,end){
    for(let k=0;k<9;k++){
      velocity.push(...v);trail.push(k/8);color.push(c.r,c.g,c.b);life.push(end);
    }
  }
  for(let i=0;i<68;i++){
    const roll=k=>courseRoll(82+style,i,k);
    const inner=i>=48;
    const angle=2*Math.PI*((inner?i-48:i)+.45*(roll(1)-.5))/(inner?20:48);
    // A chrysanthemum opens at mixed depths, a peony has a distinct outer
    // ring, and the golden willow starts higher and falls in longer trails.
    const spread=style===1?.88+.19*roll(2):.55+.58*roll(2);
    const speed=(inner?5.6:10.8)*spread;
    spark([Math.cos(angle)*speed,Math.sin(angle)*speed*(style===1?.83:1)+(style===2?2.6:0),2*roll(3)-1],
      new THREE.Color(palette[inner?2:i%3===0?1:0]),1.15+.64*roll(4));
  }
  spark([0,0,0],new THREE.Color(0xffdb95),-1); // launch rocket
  const g=new THREE.BufferGeometry();
  g.setAttribute('position',new THREE.Float32BufferAttribute(velocity,3));
  g.setAttribute('lag',new THREE.Float32BufferAttribute(trail,1));
  g.setAttribute('tint',new THREE.Float32BufferAttribute(color,3));
  g.setAttribute('life',new THREE.Float32BufferAttribute(life,1));
  return g;
}
export function createFireworks(scene){
  const shells=Array.from({length:3},(_,style)=>{
    const material=new THREE.ShaderMaterial({
      transparent:true,depthWrite:false,toneMapped:false,
      uniforms:{age:{value:0},pixelScale:{value:1},willow:{value:style===2?1:0}},
      vertexShader:`
        attribute float lag,life;
        attribute vec3 tint;
        uniform float age,pixelScale,willow;
        varying vec3 hue;varying float brightness;
        void main(){
          float t=age-.29,delay=lag*(.24+.12*willow);
          vec3 offset=vec3(0.);
          if(life<0.){
            float u=clamp((age-lag*.064)/.29,0.,1.);
            offset.y=-8.*(1.-u)*(1.-u);
            brightness=(1.-lag*.88)*smoothstep(0.,.035,age)*(1.-smoothstep(.27,.32,age));
          }else{
            float u=max(0.,t-delay);
            offset=position*(1.-exp(-1.85*u))*1.14;
            offset.y-=(2.35+2.8*willow)*u*u;
            float glitter=.84+.16*sin(u*23.+position.x*6.);
            brightness=smoothstep(0.,.045,t-delay)*(1.-smoothstep(life*.46,life,u))*glitter*(1.-.85*lag);
          }
          vec4 eye=modelViewMatrix*vec4(0.,0.,0.,1.);
          eye.xyz+=offset;
          gl_Position=projectionMatrix*eye;
          gl_PointSize=clamp(pixelScale*(life<0.?4.8:5.9)*(1.-.60*lag)*110./max(15.,-eye.z),1.,20.);
          hue=tint;
        }`,
      fragmentShader:`
        varying vec3 hue;varying float brightness;
        void main(){
          float r=length(gl_PointCoord*2.-1.);
          float alpha=brightness*(1.-smoothstep(.18,1.,r));
          if(alpha<.018)discard;
          gl_FragColor=vec4(mix(hue,vec3(1.),.86*(1.-smoothstep(.03,.43,r))*brightness),alpha);
          #include <colorspace_fragment>
        }`
    });
    const shell=new THREE.Points(burstGeometry(style),material);
    shell.visible=false;shell.frustumCulled=false;scene.add(shell);return shell;
  });
  const origin=new THREE.Vector3();
  function update(dt,active,game,camera,pixelScale){
    if(!active){for(const shell of shells)shell.visible=false;return;}
    game.fw=(game.fw??0)-dt;
    if(game.fw<=0){
      const n=game.shellCount||0;game.shellCount=n+1;
      const roll=k=>courseRoll(game.seed??19,n,k),shell=shells[n%3];
      origin.set((n%2?1:-1)*(.61+.10*roll(6)),.78+.09*roll(7),.5).unproject(camera);
      origin.sub(camera.position).normalize();shell.position.copy(camera.position).addScaledVector(origin,123);
      shell.visible=true;shell.material.uniforms.age.value=0;
      game.fw=.86+.42*roll(8);
    }
    for(const shell of shells)if(shell.visible){
      const u=shell.material.uniforms;u.age.value+=dt;u.pixelScale.value=pixelScale;
      shell.visible=u.age.value<2.14;
    }
  }
  return {shells,update};
}
