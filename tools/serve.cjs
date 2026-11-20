const http=require('http'),fs=require('fs'),path=require('path');
const root=process.argv[2],port=+process.argv[3]||8765;
const T={'.html':'text/html; charset=utf-8','.js':'text/javascript','.glb':'model/gltf-binary','.woff2':'font/woff2','.jpg':'image/jpeg','.png':'image/png','.json':'application/json','.mp3':'audio/mpeg','.mp4':'video/mp4','.webp':'image/webp'};
http.createServer((q,r)=>{let p=decodeURIComponent(q.url.split('?')[0]);if(p.endsWith('/'))p+='index.html';
  const f=path.join(root,p);fs.readFile(f,(e,d)=>{if(e){r.writeHead(404);r.end();return;}r.writeHead(200,{'Content-Type':T[path.extname(f)]||'application/octet-stream','Cache-Control':'no-store'});r.end(d);});
}).listen(port,()=>console.log('serving',root,port));
