// Make the two Pages entries share the compiled player and art. Cache the HTML's
// dependencies by content so a Telegram WebView can't mix two HUD releases.
const fs = require('node:fs');
const path = require('node:path');
const {createHash} = require('node:crypto');
const docs = path.resolve(__dirname, '../docs');
const preview = process.argv.includes('--preview');
const site = path.join(docs, preview ? 'progression-test' : 'unity-test');
const live = path.join(docs, 'unity');
const testIndex = path.join(site, 'index.html');
let source = fs.readFileSync(testIndex, 'utf8');
// File lengths describe the bytes actually served by this Web release.
const buildFile=kind=>{
  const match=source.match(new RegExp(kind+":'Build/([a-f0-9]{32}\\.[^']+)'"));
  if(!match)throw Error('Missing compiled '+kind+' URL');
  return match[1];
};
if(process.argv.includes('--shell-only')){
  // Repackage web UI updates against the live, byte-identical player.
  const template=path.resolve(__dirname,'../unity/OwlSlope/Assets/WebGLTemplates/MountainRace');
  let html=fs.readFileSync(path.join(template,'index.html'),'utf8');
  for(const [kind,token] of Object.entries({loader:'LOADER',data:'DATA',framework:'FRAMEWORK',code:'CODE'})){
    html=html.replace('{{{ '+token+'_FILENAME }}}',buildFile(kind));
  }
  for(const match of html.matchAll(/(?:src|href)="([\w./-]+\.(?:js|css))"/g)){
    const file=match[1];if(file==='release.js')continue;
    const sourceBytes=fs.readFileSync(path.join(template,file)),target=path.join(site,file);
    if(!fs.existsSync(target)||!sourceBytes.equals(fs.readFileSync(target)))fs.writeFileSync(target,sourceBytes);
  }
  source=html;
}
const bytes=kind=>fs.statSync(path.join(site,'Build',buildFile(kind))).size;
const lengths='sizes:{/* player bytes */data:'+bytes('data')+',code:'+bytes('code')+',framework:'+bytes('framework')+'}';
if(!source.includes('/* player bytes */'))throw Error('Missing Unity transfer size placeholder');
source=source.replace(/sizes:\{\/\* player bytes \*\/[^}]*\}/,lengths);
function version(url, directory) {
  const bytes = fs.readFileSync(path.join(directory, url));
  return url + '?v=' + createHash('sha256').update(bytes).digest('hex').slice(0, 12);
}
const html = source.replace(/((?:src|href)=["'])([\w./-]+\.(?:js|css))(?:\?v=[\da-f]+)?(["'])/g,
  (_, prefix, url, suffix) => prefix + version(url, site) + suffix);
if (!html.includes('<head>') || !/src="release\.js\?v=[\da-f]+"/.test(html))
  throw new Error('Unexpected Unity template: no head or release configuration');
const production = html.replace('<head>', '<head>\n  <base href="../unity-test/">')
  .replace(/src="release\.js\?v=[\da-f]+"/, 'src="../unity/' + version('release.js', live) + '"');
fs.writeFileSync(testIndex, html);
if (!preview) fs.writeFileSync(path.join(live, 'index.html'), production);
