const fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=process.argv[2]||'grip-checks';fs.mkdirSync(out,{recursive:true});
(async()=>{const b=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
try{const pg=await b.newPage({viewport:{width:375,height:812}});
await pg.goto('http://127.0.0.1:8766/animation-test.html');await pg.waitForFunction(()=>__motionTest?.ready()&&__motionTest.game.mode==='play');
const shots=await pg.evaluate(async()=>{const T=await import('three'),api=__motionTest,h=api.heroes.bear,s=new T.Scene();
s.background=new T.Color(0xdde9f2);s.add(new T.HemisphereLight(0xfff6e8,0x94adca,1.9));
for(const [x,z,k] of [[2,3,2],[-2,-2,1.4]]){const l=new T.DirectionalLight(0xfff5ea,k);l.position.set(x,3,z);s.add(l);}
const r=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});r.setSize(400,400);const imgs={};
for(const phase of ['ride','right','air','land']){
 api.testPose('bear',phase);const m=h.motion.model,parent=m.parent;s.add(m);m.updateMatrixWorld(true);
 for(const [which,sign] of [['Left',1],['Right',-1],['Body',1]]){
  const full=which==='Body',center=full?new T.Vector3(0,.48,0):m.getObjectByName(which+'_Grip').getWorldPosition(new T.Vector3());
  const half=full?.63:.17,c=new T.OrthographicCamera(-half,half,half,-half,.01,20);
  c.position.copy(center).add(new T.Vector3(sign*(full?1.6:1.05),full?.55:.72,full?3.0:1.8));c.lookAt(center);r.render(s,c);
  imgs[which+'-'+phase]=r.domElement.toDataURL('image/png').split(',')[1];}
 parent.add(m);}
const {poseHeroMotion}=await import('./hero-motion.js');api.testPose('bear','ride');
const live=h.motion.model,parent=live.parent;s.add(live);h.motion.active=false;h.motion.lastT=null;h.motion.state={};
const film=document.createElement('canvas');film.width=640;film.height=320;const ctx=film.getContext('2d');r.setSize(320,320);
for(let i=0;i<64;i++){
 const t=i/16,air=t>2&&t<2.8;poseHeroMotion(h,{ang:t<2?Math.sin(Math.PI*t)*.92:0,air:+air,airT:t-2,airMax:.8,rampH:0},'play',t);
 live.position.y=air?.11*Math.sin(Math.PI*(t-2)/.8):0;live.updateWorldMatrix(true,true);
 const target=live.getObjectByName('Right_Grip').getWorldPosition(new T.Vector3());
 for(const close of [0,1]){
  const half=close?.165:.64,c=new T.OrthographicCamera(-half,half,half,-half,.01,20);
  const center=close?target:new T.Vector3(0,.51,0),a=close?-.48:.38+.85*t;
  c.position.copy(center).add(new T.Vector3(2*Math.sin(a),close?.64:.55,2*Math.cos(a)));
  c.lookAt(center);r.render(s,c);ctx.drawImage(r.domElement,close*320,0);
 }imgs[String(i).padStart(3,'0')]=film.toDataURL('image/png').split(',')[1];}
live.position.y=0;parent.add(live);
r.dispose();return imgs;});for(const [k,v] of Object.entries(shots))fs.writeFileSync(path.join(out,k+'.png'),Buffer.from(v,'base64'));
}finally{await b.close()}})().catch(e=>{console.error(e);process.exitCode=1});
