// The SDK restores initData from session storage, even on a new plain launch.
// Only parameters carried by this navigation may select another scene.
(function(){
  if(window.Telegram?.WebApp?.initData)window.Telegram.WebApp.ready();
  const query=new URLSearchParams(location.search);
  const hash=new URLSearchParams(location.hash.slice(1));
  const start=query.get('tgWebAppStartParam')||hash.get('tgWebAppStartParam')||new URLSearchParams(hash.get('tgWebAppData')||'').get('start_param');
  window.courseLaunch=start==='course';
  if(['course','play'].includes(query.get('preview')))return;
  if(start==='unitytest'){
    location.replace('./unity-test/'+location.search+location.hash);
    return;
  }
  if(start==='unity'){
    query.set('v','2026-11-20d');
    location.replace('./unity-slope/?'+query+location.hash);
    return;
  }
  if(start!=='motion'&&query.get('preview')!=='motion')return;
  query.set('v','2026-11-20b');
  location.replace('animation-test.html?'+query.toString()+location.hash);
})();
