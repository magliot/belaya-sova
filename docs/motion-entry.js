// Telegram startapp=motion opens an isolated animation review in the same Mini App.
(function(){
  const query=new URLSearchParams(location.search);
  const hash=new URLSearchParams(location.hash.slice(1));
  const app=window.Telegram&&window.Telegram.WebApp;
  const start=(app&&app.initDataUnsafe&&app.initDataUnsafe.start_param)||query.get('tgWebAppStartParam')||hash.get('tgWebAppStartParam');
  if(start!=='motion'&&query.get('preview')!=='motion')return;
  query.set('v','2026-11-17d');
  location.replace('animation-test.html?'+query.toString()+location.hash);
})();
