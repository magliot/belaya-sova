// Select the public or isolated test profile from this Telegram navigation.
// Each destination boots the same Unity player; both load the SDK with its original signed data.
(function () {
  const pilot = document.currentScript.hasAttribute('data-pilot');
  const query = new URLSearchParams(location.search);
  let raw = location.hash.slice(1);
  const separator = raw.indexOf('?');
  if (separator >= 0 && !/[=&]/.test(raw.slice(0, separator))) raw = raw.slice(separator + 1);
  const fragment = new URLSearchParams(raw);
  const context = new URLSearchParams();
  for (const params of [fragment, query]) {
    for (const [key, value] of params) {
      if (key.startsWith('tgWebApp') && !context.has(key)) context.set(key, value);
    }
  }
  const signedStart = params => new URLSearchParams(params.get('tgWebAppData') || '').get('start_param');
  const start = signedStart(fragment) || signedStart(query) || query.get('tgWebAppStartParam') ||
    fragment.get('tgWebAppStartParam') || query.get('startapp');
  const trialStage = start === 'snowpark_1911' ? '3' : start === 'festival_1911' ? '7' :
    ['freeride_1911','freeridewide_1911','boulder_1911'].includes(start) ? '8' : '';
  const test = !!trialStage || start === 'unity' || /^unitytest(?:_[a-z0-9]+)?$/.test(start || '') || pilot;
  const destination = start === 'unitytest_progress21' ? 'progression-test/' : test ? 'unity-test/' : 'unity/';
  const target = new URL((pilot ? '../' : './') + destination, location.href);
  query.delete('tgWebAppData');
  if (start === 'unitytest_1911') query.set('seed','1911');
  if (trialStage) {
    query.set('reviewStage',trialStage);query.set('seed','1911');
    if(start==='freeridewide_1911')query.set('reviewOffset','165');
    if(start==='boulder_1911')query.set('reviewOffset','119');
  }
  query.set('v', '2026-11-24a');
  target.search = query.toString();
  target.hash = context.toString();
  location.replace(target.href);
})();
