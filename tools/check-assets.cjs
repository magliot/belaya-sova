const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=path.resolve('asset-checks');fs.mkdirSync(out,{recursive:true});
const debug=`
loadHero('bear');loadHero('owl');loadTrackAssets();
window.__assetTest={
  ready:()=>trackReady&&Object.keys(WINTER).length===8&&collectibleModels.aid&&HEROMODELS.bear.glb&&HEROMODELS.owl.glb,
  check(){
    this.previewActors={};
    const caps={rock_wide:2000,rock_tall:2000,snowman:4500,dog:6000,snow_cannon:5000,gift:2000,magnet:1500,yeti:6000};
    const models=[];
    for(const [k,cap] of Object.entries(caps)){
      const m=winterClone(k);m.updateMatrixWorld(true);let triangles=0,finite=true;
      m.traverse(o=>{if(!o.isMesh)return;const p=o.geometry.attributes.position;triangles+=(o.geometry.index?.count||p.count)/3;
        for(let i=0;i<p.array.length;i++)if(!Number.isFinite(p.array[i]))finite=false;});
      const b=new THREE.Box3().setFromObject(m),size=b.getSize(new THREE.Vector3());
      models.push({k,cap,triangles,finite,size:size.toArray(),ground:b.min.y});
    }
    const a=FACT.dog({vx:120}),b=FACT.dog({vx:-120});let mesh;
    a.traverse(o=>{if(o.isSkinnedMesh)mesh=o;});
    const update=()=>{a.updateMatrixWorld(true);mesh.skeleton.update();};update();
    const pos=mesh.geometry.attributes.position,w=mesh.geometry.attributes.skinWeight;
    let toe=-1,head=-1;
    for(let i=0;i<pos.count;i++){if(toe<0&&w.getY(i)>.99&&pos.getY(i)<.2)toe=i;if(head<0&&pos.getY(i)>.85)head=i;}
    const point=i=>mesh.getVertexPosition(i,new THREE.Vector3());
    const toeBefore=point(toe),headBefore=point(head),other=b.getObjectByName('WinterLeg0').quaternion.clone();
    a.userData.mixer.update(.65/4);update();
    const animation={toeMotion:point(toe).distanceTo(toeBefore),headMotion:point(head).distanceTo(headBefore),
      independent:other.angleTo(b.getObjectByName('WinterLeg0').quaternion)<1e-8};
    this.previewActors.dog=a;
    const gestures=[];const oldG=G;G=newGame('pause');
    for(const kind of ['snowman','yeti']){const m=FACT[kind]({x:60}),o={type:kind,x:60,y:100,m,thrown:0};G.objs=[o];G.t=0;animObjs(0);
      const before=m.userData.worldArm.quaternion.clone();o.thrown=1;G.t=.3;animObjs(0);
      gestures.push({kind,motion:before.angleTo(m.userData.worldArm.quaternion)});this.previewActors[kind]=m;}
    G=oldG;
    return {models,animation,gestures};
  },
  preview(k,t=0){
    if(!this.view){const sc=new THREE.Scene();sc.background=new THREE.Color(0xeaf5ff);sc.add(new THREE.HemisphereLight(0xffffff,0xb0c7db,2));
      const dl=new THREE.DirectionalLight(0xffffff,2.1);dl.position.set(-3,6,-4);sc.add(dl);
      const r=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});r.setSize(375,812);r.setPixelRatio(1);r.outputColorSpace=THREE.SRGBColorSpace;
      document.body.replaceChildren(r.domElement);this.view={sc,r,camera:new THREE.PerspectiveCamera(35,375/812,.01,100),current:null};}
    const v=this.view;if(v.current)v.sc.remove(v.current);const m=this.previewActors?.[k]||winterClone(k);v.sc.add(m);v.current=m;
    if(k==='dog'&&!m.userData.mixer){const mixer=new THREE.AnimationMixer(m);mixer.clipAction(WINTER_CLIPS.dog[0]).play();mixer.update(t);}
    m.updateMatrixWorld(true);const box=new THREE.Box3().setFromObject(m),c=box.getCenter(new THREE.Vector3()),sz=box.getSize(new THREE.Vector3());
    const d=Math.max(sz.y,sz.x*2.3,sz.z*1.7);v.camera.position.copy(c).add(new THREE.Vector3(d*.8,d*.35,-d*2.5));v.camera.lookAt(c);v.r.render(v.sc,v.camera);
  }
};
`;
let html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.__freeze)return;');
const end=html.lastIndexOf('</script>');html=html.slice(0,end)+debug+html.slice(end);fs.writeFileSync('docs/test.html',html);
(async()=>{
  const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});const errors=[];
    page.on('pageerror',e=>errors.push(String(e)));await page.addInitScript(()=>window.__freeze=true);
    await page.goto('http://127.0.0.1:8766/test.html',{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__assetTest&&__assetTest.ready(),null,{timeout:90000});
    const result=await page.evaluate(()=>__assetTest.check());
    for(const m of result.models){assert(m.finite,`${m.k}: invalid vertices`);assert(m.triangles<=m.cap,`${m.k}: triangle budget exceeded`);
      assert(m.size.every(v=>Number.isFinite(v)&&v>0),`${m.k}: invalid bounds`);}
    assert(result.animation.independent,'Dog instances share a skeleton');assert(result.animation.toeMotion>.02,'Dog feet stay still');
    assert(result.animation.headMotion<1e-5,'Running distorts the dog head');
    for(const g of result.gestures)assert(g.motion>.02,`${g.kind}: gesture stays still`);

    for(const m of result.models){await page.evaluate(k=>__assetTest.preview(k,.65/4),m.k);await page.screenshot({path:path.join(out,m.k+'.png')});}
    assert.deepEqual(errors,[],'JavaScript errors');fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({viewport:{width:375,height:812},errors,...result},null,2));
    console.log('PASS: eight winter models, mobile triangle budgets, independent dog rigs, running feet, gestures.');
  }finally{await browser.close();fs.rmSync('docs/test.html',{force:true});}
})().catch(e=>{console.error(e);process.exitCode=1;});
