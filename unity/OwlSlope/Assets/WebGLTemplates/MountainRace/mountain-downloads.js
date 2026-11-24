/* Stage the three immutable Unity files before starting WebAssembly. This
   lets the shell recover an interrupted transfer before the engine sees it. */
window.MountainDownloads=(()=>{
  'use strict';
  const blockSize=1024*1024, cacheName='belaya-sova-player-ranges-v1';
  const idleMs=12000, maxAttempts=3;
  const nativeFetch=window.fetch.bind(window);
  const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  class EncodedRange extends Error {}
  function transportEncoded(response){
    const encoding=response.headers.get('Content-Encoding');
    return response.ok&&encoding&&encoding.toLowerCase()!=='identity';
  }

  // Private WebViews can deny or indefinitely delay persistent storage.
  async function cacheOperation(work){
    let timer;
    try{return await Promise.race([work,new Promise(resolve=>{timer=setTimeout(()=>resolve(null),900);})]);}
    catch{return null;}finally{clearTimeout(timer);}
  }

  function open(urls,update){
    const files=new Map(),controllers=new Set(),objectUrls=[];
    let closed=false,doneBytes=0,lastPaint=-1000;
    for(const key of ['data','code','framework']){
      const size=urls.sizes?.[key];
      if(Number.isSafeInteger(size)&&size>0){
        const url=new URL(urls[key],document.baseURI).href,count=Math.ceil(size/blockSize);
        files.set(url,{key,url,size,count,progress:new Float64Array(count),whole:null});
      }
    }
    const total=Array.from(files.values()).reduce((sum,f)=>sum+f.size,0);
    const persistent=cacheOperation(Promise.resolve().then(()=>window.caches.open(cacheName)));
    function show(message=''){
      if(closed)return;
      const now=performance.now();
      if(!message&&doneBytes<total&&now-lastPaint<120)return;
      lastPaint=now;update(doneBytes,total,message);
    }
    function progress(file,i,value){
      doneBytes+=value-file.progress[i];file.progress[i]=value;show();
    }
    function cancel(){
      closed=true;for(const request of controllers)request.abort();controllers.clear();
      for(const url of objectUrls)URL.revokeObjectURL(url);
      objectUrls.length=0;
    }
    function keyFor(file,start,end){return file.url+'?owlpart='+start+'-'+end+'-'+file.size;}
    function headerSize(headers){return Number(headers.get('Content-Length'));}
    async function writePart(cache,url,blob){
      if(cache)await cacheOperation(cache.put(url,new Response(blob,{headers:{
        'Content-Length':String(blob.size),'X-Owl-Saved':String(Date.now())
      }})));
    }

    async function readNetwork(file,index,start,end,fullDecoded=false){
      const request=new AbortController();controllers.add(request);
      let timer;
      function heartbeat(){
        clearTimeout(timer);
        timer=setTimeout(()=>{if(document.hidden)heartbeat();else request.abort();},idleMs);
      }
      const headers=fullDecoded?{}:{Range:'bytes='+start+'-'+(end-1)};
      heartbeat();
      try{
        let response;
        try{response=await nativeFetch(file.url,{headers,signal:request.signal});}
        catch(error){
          // Chromium can reject a partial Brotli stream before exposing its headers.
          if(!fullDecoded&&error instanceof TypeError&&!request.signal.aborted){
            let metadata;
            try{metadata=await nativeFetch(file.url,{method:'HEAD',signal:request.signal});}catch{}
            if(metadata&&transportEncoded(metadata))throw new EncodedRange();
          }
          throw error;
        }
        heartbeat();
        // A host can serve a Unity file with HTTP Content-Encoding. The browser
        // can only decompress a complete stream, never an interior byte range.
        if(fullDecoded){
          if(response.status!==200)throw Error('Сервер не вернул полный файл.');
          const body=response.body.getReader(),chunks=[];
          try{
            for(;;){
              const {done,value}=await body.read();if(done)break;
              heartbeat();chunks.push(value);update(doneBytes,total,'',true);
            }
          }finally{body.releaseLock();}
          const blob=new Blob(chunks,{type:'application/octet-stream'});
          if(!blob.size)throw Error('Сервер вернул пустой файл.');
          return blob;
        }
        if(transportEncoded(response))throw new EncodedRange();
        const whole=response.status===200;
        if(!whole&&(response.status!==206||response.headers.get('Content-Range')!==
          'bytes '+start+'-'+(end-1)+'/'+file.size))throw Error('Сервер не вернул запрошенную часть файла.');
        // Hosts that ignore Range get a single ordinary GET for this file.
        if(whole&&file.whole){request.abort();return (await file.whole).subarray(start,end);}
        const size=whole?file.size:end-start;
        async function read(){
          const reader=response.body.getReader(),bytes=new Uint8Array(size);
          let offset=0;
          try{
            for(;;){
              const {done,value}=await reader.read();if(done)break;
              heartbeat();
              if(offset+value.byteLength>size)throw Error('Размер файла изменился.');
              bytes.set(value,offset);offset+=value.byteLength;
              if(whole){
                const last=Math.floor((offset-1)/blockSize);
                for(let i=Math.floor((offset-value.byteLength)/blockSize);i<=last;i++){
                  progress(file,i,Math.min(blockSize,file.size-i*blockSize,offset-i*blockSize));
                }
              }else progress(file,index,offset);
            }
            if(offset!==size)throw Error('Загрузка файла прервалась.');
            return bytes;
          }finally{reader.releaseLock();}
        }
        if(!whole)return await read();
        file.whole=read();
        try{return (await file.whole).subarray(start,end);}
        catch(error){file.whole=null;throw error;}
      }finally{clearTimeout(timer);request.abort();controllers.delete(request);}
    }

    async function part(file,index,cache){
      const start=index*blockSize,end=Math.min(file.size,start+blockSize),size=end-start;
      const url=keyFor(file,start,end);
      if(cache){
        const blob=await cacheOperation(cache.match(url).then(hit=>hit?.blob()));
        if(blob?.size===size){progress(file,index,size);return blob;}
      }
      for(let attempt=0;attempt<maxAttempts;attempt++){
        if(closed)throw Error('Загрузка остановлена.');
        try{
          const bytes=file.whole?(await file.whole).subarray(start,end):await readNetwork(file,index,start,end);
          const blob=new Blob([bytes]);progress(file,index,size);await writePart(cache,url,blob);return blob;
        }catch(error){
          if(closed||error instanceof EncodedRange||attempt+1===maxAttempts)throw error;
          progress(file,index,0);show('Восстанавливаем соединение…');
          await pause(350*(attempt+1));
        }
      }
    }

    async function assemble(file,cache){
      const pieces=new Array(file.count);
      pieces[0]=await part(file,0,cache);
      let next=1;
      async function worker(){
        while(next<file.count&&!closed){
          const index=next++;
          pieces[index]=await part(file,index,cache);
        }
      }
      const workers=Math.min(file.count-1,file.key==='data'?3:2);
      await Promise.all(Array.from({length:workers},worker));
      if(closed)throw Error('Загрузка остановлена.');
      file.whole=null;
      return new Blob(pieces,{type:'application/octet-stream'});
    }
    async function prepareFile(file){
      const cache=await persistent,decodedKey=keyFor(file,'decoded',file.size);
      let complete=cache&&await cacheOperation(cache.match(decodedKey).then(hit=>hit?.blob()));
      if(!complete&&cache){
        const old=await cacheOperation(window.caches.match(file.url));
        if(old?.ok&&headerSize(old.headers)===file.size){
          const blob=await cacheOperation(old.blob());
          if(blob&&(blob.size===file.size||old.headers.get('Content-Encoding')))complete=blob;
        }
      }
      if(!complete){
        try{return await assemble(file,cache);}
        catch(error){if(!(error instanceof EncodedRange))throw error;}
        for(let attempt=0;attempt<maxAttempts;attempt++){
          if(closed)throw Error('Загрузка остановлена.');
          try{complete=await readNetwork(file,0,0,file.size,true);break;}
          catch(error){
            if(closed||attempt+1===maxAttempts)throw error;
            show('Восстанавливаем соединение…');await pause(350*(attempt+1));
          }
        }
        await writePart(cache,decodedKey,complete);
      }
      for(let i=0;i<file.count;i++)progress(file,i,Math.min(blockSize,file.size-i*blockSize));
      return complete;
    }
    async function prepare(){
      const staged={...urls};
      try{
        await Promise.all(Array.from(files.values(),async file=>{
          const blob=await prepareFile(file);
          if(closed)throw Error('Загрузка остановлена.');
          staged[file.key]=URL.createObjectURL(blob);objectUrls.push(staged[file.key]);
        }));
        return staged;
      }catch(error){cancel();throw error;}
    }
    async function prune(){
      const cache=await persistent;if(!cache)return;
      const limit=Date.now()-7*86400000;
      const keys=await cacheOperation(cache.keys())||[];
      for(const request of keys){
        const url=new URL(request.url);
        if(!url.searchParams.has('owlpart'))continue;
        url.search='';
        if(files.has(url.href))continue;
        const cached=await cacheOperation(cache.match(request));
        if(cached&&Number(cached.headers.get('X-Owl-Saved'))<limit)await cacheOperation(cache.delete(request));
      }
    }
    return {prepare,cancel,prune,total};
  }
  return {open};
})();
