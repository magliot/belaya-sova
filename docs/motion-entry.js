// Only the current navigation selects a game. The Telegram SDK also remembers older launches.
(function(){
  const pilot=document.currentScript?.hasAttribute('data-pilot')===true;
  const version='2026-11-20g';
  let navigating=false,subscribed=false,announced=false;

  function readLaunch(){
    const query=new URLSearchParams(location.search);
    let raw=location.hash.slice(1);
    // Telegram's SDK also accepts #/route?tgWebAppData=... and #?tgWebAppData=...
    const question=raw.indexOf('?');
    if(question!==-1&&!/[=&]/.test(raw.slice(0,question)))raw=raw.slice(question+1);
    const hash=new URLSearchParams(raw),context=new URLSearchParams();
    for(const params of [hash,query]){
      for(const [key,value] of params)if(key.startsWith('tgWebApp')&&!context.has(key))context.set(key,value);
    }
    // A current Telegram payload can arrive with a stale launch hint on the registered address.
    const nested=params=>new URLSearchParams(params.get('tgWebAppData')||'').get('start_param');
    const start=nested(hash)||nested(query)||query.get('tgWebAppStartParam')||hash.get('tgWebAppStartParam')||query.get('startapp');
    return {query,context,start};
  }

  function updateLinks(context){
    for(const link of document.querySelectorAll('a[data-full-unity]')){
      const target=new URL(link.href,location.href);
      if(target.origin!==location.origin)continue;
      target.hash=context.toString();link.href=target.href;
    }
  }
  function route(){
    if(navigating)return;
    const app=window.Telegram?.WebApp;
    if(app?.initData&&!announced){announced=true;app.ready();}
    if(app?.onEvent&&!subscribed){subscribed=true;app.onEvent('activated',route);}
    const {query,context,start}=readLaunch();
    updateLinks(context);
    window.courseLaunch=!pilot&&start==='course';
    let pathname,release;
    // Cached Three.js menus used these exact addresses for their secondary Unity button.
    const oldMenuLink=pilot&&!start&&!query.has('preview')&&['2026-11-20d','2026-11-20e'].includes(query.get('v'));
    if(start==='unitytest'||start==='unitytest_20g'||oldMenuLink){
      pathname=pilot?'../unity-test/':'./unity-test/';release=version;
    }else if(pilot||['play','course'].includes(query.get('preview')))return;
    else if(start==='unity'){pathname='./unity-slope/';release=version;}
    else if(start==='motion'||query.get('preview')==='motion'){pathname='./animation-test.html';release='2026-11-20b';}
    else return;

    const target=new URL(pathname,location.href);
    query.delete('tgWebAppData');query.set('v',release);
    target.search=query.toString();target.hash=context.toString();
    navigating=true;window.sovaEntryNavigating=true;location.replace(target.href);
  }
  window.addEventListener('hashchange',route);
  window.addEventListener('popstate',route);
  window.addEventListener('pageshow',event=>{
    if(event.persisted){navigating=false;window.sovaEntryNavigating=false;}
    route();
  });
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',route,{once:true});
  route();
})();
