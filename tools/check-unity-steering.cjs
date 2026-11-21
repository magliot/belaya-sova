// Drive the live Unity Web player from the ordinary menu using distance-triggered touch input.
// node tools/check-unity-steering.cjs http://127.0.0.1:8784/ local expected.wasm.unityweb --check-follow
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { chromium } = require(process.env.DANCE_BROWSER_MODULE || 'playwright');

const viewport = { width: 390, height: 780 };
const [entry = 'http://127.0.0.1:8784/', label = 'current', expectedWasm] = process.argv.slice(2);
assert.match(label, /^[a-z0-9-]+$/i);
const output = path.resolve('course-checks', 'camera-21d', label);
const gesture = [
  [2, 'touchStart', 195], [5, 'touchMove', 275], [18, 'touchMove', 115], [25, 'touchEnd'],
  [30, 'touchStart', 195], [32, 'touchMove', 265], [36, 'touchEnd']
];

function saveMovie(frames, hero) {
  const directory = path.join(output, hero);
  fs.mkdirSync(directory, { recursive: true });
  frames.sort((a, b) => a.time - b.time);
  const concat = ['ffconcat version 1.0'];
  frames.forEach((frame, index) => {
    const filename = String(index).padStart(5, '0') + '.jpg';
    fs.writeFileSync(path.join(directory, filename), frame.bytes);
    concat.push('file ' + filename);
    if (index + 1 < frames.length) {
      const duration = frames[index + 1].time - frame.time;
      concat.push('duration ' + Math.max(.001, duration).toFixed(4));
    }
  });
  const frameList = path.join(directory, 'frames.txt');
  fs.writeFileSync(frameList, concat.join('\n'));
  fs.writeFileSync(path.join(directory, 'timestamps.json'), JSON.stringify(frames.map(frame => frame.time)));
  execFileSync('ffmpeg', [
    '-y', '-loglevel', 'error', '-f', 'concat', '-safe', '1', '-i', frameList, '-vf', 'fps=30',
    '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
    path.join(output, hero + '.mp4')
  ], { windowsHide: true });
}

async function ride(browser, hero) {
  const context = await browser.newContext({ viewport, hasTouch: true, serviceWorkers: 'block' });
  try {
    // Keep test data on this fresh profile even when exercising the public release.
    await context.route('https://script.google.com/**', route => route.abort());
    const page = await context.newPage();
    const cdp = await context.newCDPSession(page);
    const frames = [];
    const inputs = [];
    let dispatch = Promise.resolve();
    await page.exposeFunction('steerAtDistance', (metres, time) => {
      while (inputs.length < gesture.length && metres >= gesture[inputs.length][0]) {
        const [at, type, x] = gesture[inputs.length];
        inputs.push({ at, type, x, metres, time });
        const touchPoints = type === 'touchEnd' ? [] : [{ x, y: 550 }];
        dispatch = dispatch.then(() => cdp.send('Input.dispatchTouchEvent', { type, touchPoints }));
      }
    });
    await page.addInitScript(({ width, height }) => {
      window.cameraSamples = [];
      let callback;
      Object.defineProperty(window, 'onMountainState', {
        get() { return callback; },
        set(listener) {
          callback = state => {
            if (state.phase === 'play' || state.phase === 'over') {
              window.cameraSamples.push({
                wall: Date.now() / 1000, d: state.metres, t: state.elapsed,
                x: state.riderScreenX * width, y: state.riderScreenY * height,
                lane: state.riderLane, h: state.helmets, phase: state.phase
              });
              window.steerAtDistance(state.metres, state.elapsed);
            }
            listener(state);
          };
        }
      });
    }, viewport);

    const url = new URL(entry);
    url.searchParams.set('seed', '1911');
    await page.goto(url.href, { waitUntil: 'commit' });
    const start = page.locator('#play');
    await start.waitFor({ timeout: 180000 });
    const wasm = await page.evaluate(() => {
      const resource = performance.getEntriesByType('resource').find(item => item.name.includes('.wasm.'));
      return resource?.name.split('/').at(-1);
    });
    if (expectedWasm) assert.equal(wasm, expectedWasm, 'Unexpected compiled player');
    await page.locator('[data-hero="' + hero + '"]').tap();
    cdp.on('Page.screencastFrame', event => {
      frames.push({ time: event.metadata.timestamp, bytes: Buffer.from(event.data, 'base64') });
      cdp.send('Page.screencastFrameAck', { sessionId: event.sessionId }).catch(() => {});
    });
    await cdp.send('Page.startScreencast', {
      format: 'jpeg', quality: 93, maxWidth: viewport.width, maxHeight: viewport.height, everyNthFrame: 1
    });
    await start.tap();
    await page.waitForFunction(() => {
      const last = window.cameraSamples.at(-1);
      return last?.d >= 46 || last?.phase === 'over';
    }, null, { timeout: 90000 });
    await cdp.send('Page.stopScreencast');
    await dispatch;
    const samples = await page.evaluate(() => window.cameraSamples);
    assert.equal(samples[0].d, 0);
    assert(samples.at(-1).d >= 46 && samples.at(-1).phase === 'play', 'Ride interrupted');
    assert(samples.every(s => s.h === samples[0].h), 'Helmet lost during the measured ride');
    assert.equal(inputs.length, gesture.length);

    const active = samples.filter(s => s.d >= 4 && s.d <= 45);
    const xs = active.map(s => s.x);
    const ys = active.map(s => s.y);
    const lanes = active.map(s => s.lane);
    const stats = {
      hero, wasm, samples: active.length, clipFrames: frames.length,
      xMin: Math.min(...xs), xMax: Math.max(...xs), xSpan: Math.max(...xs) - Math.min(...xs),
      xTravel: xs.slice(1).reduce((total, x, i) => total + Math.abs(x - xs[i]), 0),
      ySpan: Math.max(...ys) - Math.min(...ys), laneMin: Math.min(...lanes), laneMax: Math.max(...lanes)
    };
    assert(stats.laneMax > 3 && stats.laneMin < -3, 'The gesture did not traverse both sides');
    if (process.argv.includes('--check-follow')) {
      const released = active.filter(s => s.t >= inputs.at(-1).time);
      assert(released.length >= 3, 'Missing samples after the last release');
      for (let i = 1; i < released.length; i++) {
        const previous = released[i - 1];
        const current = released[i];
        assert(current.lane > previous.lane, 'Expected rightward travel during release');
        assert(current.x >= previous.x - 1, 'The camera pulled the rider back after release');
      }
    }
    saveMovie(frames, hero);
    fs.writeFileSync(path.join(output, hero + '.json'), JSON.stringify({ stats, inputs, samples }, null, 2));
    console.log(JSON.stringify(stats));
  } finally {
    await context.close();
  }
}

async function main() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ args: ['--enable-unsafe-swiftshader'] });
  try {
    for (const hero of ['bear', 'owl']) await ride(browser, hero);
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
