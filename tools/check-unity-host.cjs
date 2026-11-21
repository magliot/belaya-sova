// Check the exact player payload served by a static host, including the decompression fallback.
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const {createHash}=require('node:crypto');
const {gunzipSync}=require('node:zlib');
const base=new URL(process.argv[2]||'http://127.0.0.1:8784/unity-test/');
const exact=process.argv.includes('--exact-sha');
const preview=base.pathname.endsWith('/progression-test/');
const site=path.resolve('docs',preview?'progression-test':'unity-test');
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
async function main(){
  const page=await fetch(base,{cache:'no-store'});
  assert.equal(page.status,200);
  const html=await page.text();
  const assetBase=new URL(html.match(/<base\s+href="([^"]+)"/)?.[1]||'.',base);
  const names=Array.from(html.matchAll(/['"](Build\/[a-f0-9]{32}\.[^'"]+)['"]/g),m=>m[1]);
  assert.equal(new Set(names).size,4,'Expected the hashed loader, data, WebAssembly and JavaScript framework');
  let bytes=0;
  const changed=[];
  for(const name of names){
    const response=await fetch(new URL(name,assetBase));
    assert.equal(response.status,200,name);
    const served=Buffer.from(await response.arrayBuffer()),expected=fs.readFileSync(path.join(site,name));
    if(exact){
      const actual=hash(served),local=hash(expected);
      const payload=buffer=>buffer[0]===0x1f&&buffer[1]===0x8b?gunzipSync(buffer):buffer;
      console.log('SHA256',name,'served',actual,'local',local,'decoded',hash(payload(served)),'local-decoded',hash(payload(expected)));
      if(actual!==local)changed.push(name);
    }
    if(name.endsWith('.js')){
      assert.equal(served.toString().replace(/\r\n/g,'\n'),expected.toString().replace(/\r\n/g,'\n'),name);
    }else{
      // Unity names gzip files after the player payload. A cache can retain the
      // same payload with a previous build time in the four-byte gzip header.
      const normalized=buffer=>{
        if(buffer[0]!==0x1f||buffer[1]!==0x8b)return buffer;
        const copy=Buffer.from(buffer);copy.fill(0,4,8);return copy;
      };
      assert.equal(hash(normalized(served)),hash(normalized(expected)),name);
    }
    bytes+=served.length;
    console.log('OK',name,served.length);
  }
  const dependency=name=>{
    const match=Array.from(html.matchAll(/(?:src|href)="([^"]+)"/g),m=>m[1])
      .find(url=>url.split('?')[0].split('/').pop()===name);
    assert(match,'Expected page dependency '+name);
    return new URL(match,assetBase);
  };
  const css=await fetch(dependency('race.css'));
  assert(css.ok&&css.headers.get('content-type')?.includes('text/css'),'CSS content type');
  const release=await fetch(dependency('release.js'));
  assert(release.ok,'Release config');
  const config=await release.text();
  const live=base.pathname.endsWith('/unity/');
  assert(config.includes(live?"channel: 'production'":preview?"channel: 'progression'":"channel: 'test'"),'Unexpected profile');
  if(preview)assert(config.includes("previewBoard: 'https://"),'Missing separate test board');
  assert(config.includes(live?"leaderboardEndpoint: 'https://script.google.com/":"leaderboardEndpoint: ''"),'Unexpected leaderboard');
  assert.equal(changed.length,0,'Raw files differ: '+changed.join(', '));
  console.log('PASS',base.href,bytes,'bytes of verified player files');
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
