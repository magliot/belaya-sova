// The standalone Unity site can open the original model reference sheet without loading Three.js.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve('docs'),target=path.resolve('unity/OwlSlope/Assets/WebGLTemplates/MountainRace/catalog.json');
const source=fs.readFileSync(path.join(root,'models.html'),'utf8'),items=[],images={};
function include(name){
  const p=path.resolve(root,name);
  assert(p.startsWith(root+path.sep)&&/\.(webp|jpg|png)$/i.test(p),'Unexpected model guide image '+name);
  const mime=p.endsWith('.jpg')?'jpeg':path.extname(p).slice(1);
  if(!images[name])images[name]='data:image/'+mime+';base64,'+fs.readFileSync(p).toString('base64');
  return name;
}
function plain(s){return s.replace(/<[^>]*>/g,'').replaceAll('&amp;','&').replaceAll('&nbsp;',' ');}
for(const [,group,html] of source.matchAll(/<figure data-group="(\d+)">([\s\S]*?)<\/figure>/g)){
  const name=plain(html.match(/<h2>(.*?)<\/h2>/)[1]);
  const thumb=include(html.match(/<img src="([^"]+)"/)[1]);
  const details=[...html.matchAll(/<p>(.*?)<\/p>/g)].map(m=>plain(m[1]));
  const views=[...html.matchAll(/<a href="([^"]+\.(?:webp|jpg|png))">(.*?)<\/a>/g)].map(m=>[include(m[1]),plain(m[2])]);
  items.push({name,group:Number(group),thumb,details,views});
}
const json=JSON.stringify({items,images});
if(process.argv.includes('--write'))fs.writeFileSync(target,json);
else assert.equal(fs.readFileSync(target,'utf8'),json,'Refresh the Unity model guide with --write');
console.log('PASS '+items.length+' original model / procedural object entries, '+Object.keys(images).length+' reference shots');
