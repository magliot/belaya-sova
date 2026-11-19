// Run the current game locally on port 8766. Capture the actual skinned GLB after posing.
const fs=require('node:fs'),path=require('node:path'),{chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=process.argv[2]||'bear-checks';fs.mkdirSync(out,{recursive:true});
(async()=>{const b=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
try{const page=await b.newPage({viewport:{width:375,height:812}});
if(process.argv[3])await page.route('**/models/studies/bear.glb*',route=>route.fulfill({path:path.resolve(process.argv[3]),contentType:'model/gltf-binary'}));
await page.goto('http://127.0.0.1:8766/animation-test.html');await page.waitForFunction(()=>window.__motionTest?.game?.mode==='play'&&window.__motionTest.ready());
const images=await page.evaluate(async()=>{
const T=await import('three'),api=__motionTest,h=api.heroes.bear;
const s=new T.Scene();s.background=new T.Color(0xe2ecf3);s.add(new T.HemisphereLight(0xfffaf0,0x8fa4c2,1.85));
for(const [x,z,k] of [[2,3,2],[-2,-1,1.2]]){const l=new T.DirectionalLight(0xfff5e8,k);l.position.set(x,3,z);s.add(l);}
const r=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});r.setSize(320,320);
const c=new T.OrthographicCamera(-.65,.65,.65,-.65,.01,20),imgs={};
// Same runtime pose sampler on both versions. Ground travel is suppressed in the studio.
const {poseHeroMotion}=await import('./hero-motion.js');
api.testPose('bear','ride');const live=h.motion.model,originalParent=live.parent;
s.add(live);h.motion.active=false;h.motion.lastT=null;h.motion.state={};
c.position.set(1.15,1.1,3);c.lookAt(0,.52,0);
for(let i=0;i<84;i++){
 const t=i/18,air=t>=2.45&&t<3.25;
 const p={ang:t>.5&&t<2.45?Math.sin((t-.5)*Math.PI*2/1.95)*.96:0,air:+air,airT:t-2.45,airMax:.8,rampH:0};
 poseHeroMotion(h,p,'play',t);live.position.y=air?.14*Math.sin(Math.PI*(t-2.45)/.8):0;
 r.render(s,c);imgs[String(i).padStart(3,'0')]=r.domElement.toDataURL('image/png').split(',')[1];}
live.position.y=0;originalParent.add(live);
for(const p of ['ride','left','right','land','air']){
api.testPose('bear',p);const m=h.motion.model,parent=m.parent;s.add(m);m.updateMatrixWorld(true);
for(const [name,x,z] of [['front',0,3],['side',3,0],['back',0,-3]]){
c.position.set(x,1,z);c.lookAt(0,.5,0);r.render(s,c);imgs[p+'-'+name]=r.domElement.toDataURL('image/png').split(',')[1];}
parent.add(m);}
r.dispose();return imgs;});for(const [name,buf] of Object.entries(images))fs.writeFileSync(path.join(out,name+'.png'),Buffer.from(buf,'base64'));
}finally{await b.close();}})().catch(e=>{console.error(e);process.exitCode=1});
