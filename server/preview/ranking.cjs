// Independent acceptance leaderboard. Only anonymous test aliases are public.
const http=require('node:http');
const {createHash}=require('node:crypto');
const {DatabaseSync}=require('node:sqlite');
const fs=require('node:fs');
const path=require('node:path');

function createRanking(filename){
  if(filename!==':memory:')fs.mkdirSync(path.dirname(filename),{recursive:true});
  const db=new DatabaseSync(filename);
  db.exec(`PRAGMA journal_mode=WAL;
    CREATE TABLE IF NOT EXISTS players(id INTEGER PRIMARY KEY, token_hash TEXT NOT NULL UNIQUE);
    CREATE TABLE IF NOT EXISTS rides(
      player INTEGER NOT NULL, run_id TEXT NOT NULL, score INTEGER NOT NULL, dist INTEGER NOT NULL,
      hero TEXT NOT NULL, date INTEGER NOT NULL, PRIMARY KEY(player,run_id));
    CREATE INDEX IF NOT EXISTS rides_score ON rides(player,score DESC,date);`);
  const find=db.prepare('SELECT id FROM players WHERE token_hash=?');
  const register=db.prepare('INSERT OR IGNORE INTO players(token_hash) VALUES (?)');
  const insert=db.prepare('INSERT OR IGNORE INTO rides(player,run_id,score,dist,hero,date) VALUES(?,?,?,?,?,?)');
  const ranks=db.prepare(`WITH attempts AS (
      SELECT *, ROW_NUMBER() OVER(PARTITION BY player ORDER BY score DESC,date,run_id) AS attempt
      FROM rides WHERE date>=?
    ), board AS (
      SELECT player,score,dist,hero,RANK() OVER(ORDER BY score DESC) AS rank FROM attempts WHERE attempt=1
    ) SELECT player,score,dist,hero,rank FROM board ORDER BY rank,player`);
  function board(since,player){
    const all=ranks.all(since),me=all.find(row=>row.player===player);
    return {rows:all.slice(0,30).map(({player,...row})=>({...row,name:'Тестировщик '+player})),
      me:me?{rank:me.rank,score:me.score}:null};
  }
  return {close:()=>db.close(),handle(body){
    if(!body||!['leaderboard','submit'].includes(body.action))throw Error('Действие не поддерживается');
    const key=body.tester||'';
    if(key&&!/^[a-f0-9]{32}$/.test(key))throw Error('Некорректный тестовый профиль');
    const hash=createHash('sha256').update(key).digest('hex');
    const now=Date.now(),shift=3*3600000,moscow=new Date(now+shift);
    const monday=Date.UTC(moscow.getUTCFullYear(),moscow.getUTCMonth(),moscow.getUTCDate()-(moscow.getUTCDay()+6)%7)-shift;
    if(body.action==='leaderboard')return {ok:true,...board(body.period==='week'?monday:0,find.get(hash)?.id)};
    if(!key||!validScore(body))throw Error('Результат не прошёл проверку');
    // No transaction crosses an event-loop turn. Retries use the same primary key.
    db.exec('BEGIN IMMEDIATE');
    try{
      register.run(hash);const id=find.get(hash).id;
      insert.run(id,body.runId,body.score,body.dist,body.hero,now);
      const all=board(0,id).me,week=board(monday,id).me;
      db.exec('COMMIT');return {ok:true,rank:all.rank,best:all.score,weekRank:week.rank,weekBest:week.score};
    }catch(error){db.exec('ROLLBACK');throw error;}
  }};
}
function validScore(s){
  const fields=['dist','coins','gates','rings','preciseGates','preciseRings','rules','score'];
  return s.scoring===2&&typeof s.runId==='string'&&/^[a-zA-Z0-9.-]{8,80}$/.test(s.runId)&&
    ['bear','owl'].includes(s.hero)&&fields.every(k=>Number.isSafeInteger(s[k])&&s[k]>=0)&&
    Number.isFinite(s.dur)&&s.dur>0&&s.dist<=s.dur*65+20&&s.rules<=10&&
    s.preciseGates<=s.gates&&s.preciseRings<=s.rings&&s.coins<=s.dist+10&&s.gates+s.rings<=s.dist/2+3&&
    s.score===s.dist+3*s.coins+8*s.gates+10*s.preciseGates+14*s.rings+14*s.preciseRings+25*s.rules;
}
if(require.main===module){
  const filename=process.argv[2],port=Number(process.argv[3]||18643);
  if(!filename)throw Error('Usage: node server/preview/ranking.cjs <test-db-file> [port]');
  const ranking=createRanking(path.resolve(filename));
  const server=http.createServer(async(req,res)=>{
    const reply=(status,body)=>{res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store','Access-Control-Allow-Origin':'*'});res.end(JSON.stringify(body));};
    const url=new URL(req.url,'http://localhost');
    if(url.pathname!=='/rank')return reply(404,{ok:false});
    try{
      if(req.method==='GET')return reply(200,ranking.handle({action:'leaderboard',period:url.searchParams.get('period')}));
      if(req.method!=='POST')return reply(405,{ok:false});
      const chunks=[];let size=0;
      for await(const chunk of req){size+=chunk.length;if(size>4096)return reply(413,{ok:false});chunks.push(chunk);}
      reply(200,ranking.handle(JSON.parse(Buffer.concat(chunks).toString('utf8'))));
    }catch{reply(400,{ok:false,error:'Некорректный тестовый результат'});}
  });
  server.requestTimeout=10000;
  server.listen(port,'127.0.0.1',()=>console.log('Preview ranking listening on '+port));
}
module.exports={createRanking,validScore};
