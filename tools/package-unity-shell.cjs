// Make the two Pages entries share the compiled player and art. Cache the HTML's
// dependencies by content so a Telegram WebView can't mix two HUD releases.
const fs = require('node:fs');
const path = require('node:path');
const {createHash} = require('node:crypto');
const docs = path.resolve(__dirname, '../docs');
const site = path.join(docs, 'unity-test');
const live = path.join(docs, 'unity');
const testIndex = path.join(site, 'index.html');
const source = fs.readFileSync(testIndex, 'utf8');
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
fs.writeFileSync(path.join(live, 'index.html'), production);
