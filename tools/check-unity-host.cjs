// Check the exact player payload served by a static host, including the decompression fallback.
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const {createHash}=require('node:crypto');
const site=path.resolve('docs/unity-test');
const base=new URL(process.argv[2]||'http://127.0.0.1:8784/unity-test/');
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
async function main(){
  const page=await fetch(base,{cache:'no-store'});
  assert.equal(page.status,200);
  const html=await page.text();
  const names=Array.from(html.matchAll(/['"](Build\/[a-f0-9]{32}\.[^'"]+)['"]/g),m=>m[1]);
  assert.equal(new Set(names).size,4,'Expected the hashed loader, data, WebAssembly and JavaScript framework');
  let bytes=0;
  for(const name of names){
    const response=await fetch(new URL(name,base));
    assert.equal(response.status,200,name);
    const served=Buffer.from(await response.arrayBuffer()),expected=fs.readFileSync(path.join(site,name));
    if(name.endsWith('.js')){
      assert.equal(served.toString().replace(/\r\n/g,'\n'),expected.toString().replace(/\r\n/g,'\n'),name);
    }else assert.equal(hash(served),hash(expected),name);
    bytes+=served.length;
    console.log('OK',name,served.length);
  }
  const css=await fetch(new URL('race.css',base));
  assert(css.ok&&css.headers.get('content-type')?.includes('text/css'),'CSS content type');
  const release=await fetch(new URL('release.js',base));
  assert(release.ok&&/channel:\s*'test'/.test(await release.text()),'This address must use the test profile');
  console.log('PASS',base.href,bytes,'bytes of verified player files');
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
