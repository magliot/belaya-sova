// Exercise the actual Apps Script against an in-memory copy of the original 13-column sheet.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');

class Sheet {
  constructor(rows=[]){this.rows=rows;}
  getLastRow(){return this.rows.length;}
  appendRow(row){this.rows.push([...row]);}
  setFrozenRows(){}
  getRange(row,column,height=1,width=1){
    const data=this.rows,missing='',values=()=>Array.from({length:height},(_,y)=>
      Array.from({length:width},(_,x)=>data[row+y-1]?.[column+x-1]??missing));
    function write(table){
      for(const [y,line] of table.entries()){
        const dest=(data[row+y-1]??=[]);
        line.forEach((v,x)=>dest[column+x-1]=v);
      }
    }
    return {getValues:values,getValue:()=>values()[0][0],setValue:v=>write([[v]]),setValues:write,
      setFontWeight(){},createTextFinder(key){return {matchEntireCell(){
        return {findNext:()=>values().some(line=>line.includes(key))?{}:null};
      }};}};
  }
}
const properties=new Map(),sheets=new Map(),book={getSheetByName:name=>sheets.get(name),
  insertSheet(name){const sh=new Sheet();sheets.set(name,sh);return sh;}};
let locks=0;
const ctx=vm.createContext({console,SpreadsheetApp:{getActiveSpreadsheet:()=>book},
  LockService:{getScriptLock:()=>({waitLock(){assert.equal(locks++,0);},releaseLock(){assert.equal(--locks,0);}})},
  PropertiesService:{getScriptProperties:()=>({getProperty:k=>properties.get(k),setProperty:(k,v)=>properties.set(k,v)})},
  ContentService:{MimeType:{JSON:'json'},createTextOutput:text=>({setMimeType:()=>JSON.parse(text)})}});
vm.runInContext(fs.readFileSync(path.join(__dirname,'../server/apps-script/Code.gs'),'utf8'),ctx);
const week=ctx.weekId(),oldHeader=['id','name','username','best','best_dist','best_hero','week','week_best','week_dist','week_hero','runs','last_submit','updated'];
const me={id:571,first_name:'Тест'},prior=[571,'Старое имя','',1420,1000,'bear',week,600,490,'owl',17,0,'2026-01-10'];
const rival=[891,'Лидер','',1750,1200,'owl',week,800,670,'bear',23,0,'2026-01-10'];
const players=new Sheet([oldHeader.slice(),prior.slice(),rival.slice()]);sheets.set('Игроки',players);
const table=()=>ctx.readPlayers().find(p=>p.id===me.id);
const guest=ctx.doPost({postData:{contents:JSON.stringify({action:'leaderboard',period:'all'})}});
assert.deepEqual(guest.rows.map(p=>[p.name,p.score]),[['Лидер',1750],['Старое имя',1420]]);
assert.ok(guest.rows.every(p=>!Object.hasOwn(p,'id')));
const attempt={runId:'check-1911-0001',scoring:2,score:1257,dist:1000,dur:61,hero:'owl',
  coins:35,gates:3,preciseGates:1,rings:2,preciseRings:1,rules:3};
// 1000 + 105 + 24 + 10 + 28 + 14 + 75 = 1256.
assert.throws(()=>ctx.submitScore(me,attempt),/разбивка/);attempt.score=1256;
for(const change of [{runId:undefined},{score:1256.5},{dist:1000.2},{hero:'bad'},{preciseGates:4},{rules:11}]){
  assert.throws(()=>ctx.submitScore(me,{...attempt,...change}),/разбивка/);
}
assert.equal(players.getLastRow(),3);assert.equal(table().runs,17);
const first=ctx.submitScore(me,attempt);
assert.equal(first.rank,2);assert.equal(first.best,1420);assert.equal(first.weekBest,1256);
assert.equal(table().best_dist,1000);assert.equal(table().best_hero,'bear');
assert.equal(players.rows[0][13],'recent_runs');assert.deepEqual(players.rows[2],rival);
assert.equal(ctx.submitScore(me,attempt).best,1420);assert.equal(table().runs,18);
for(let i=2;i<=39;i++)ctx.submitScore(me,{...attempt,runId:'check-1911-'+String(i).padStart(4,'0')});
assert.equal(table().runs,56);ctx.submitScore(me,attempt);assert.equal(table().runs,56);
const winning={...attempt,runId:'check-1911-win',score:1906,dist:1650};
assert.equal(ctx.submitScore(me,winning).rank,1);assert.equal(table().best,1906);
assert.equal(table().runs,57);assert.equal(locks,0);
assert.equal(ctx.leaderboard(null,'all').rows[0].score,1906);
console.log('Apps Script VM PASS · original rows 1420/1750 · ranked 1256, 1906 · retry after 38 newer rides ignored · existing records/weekly ranks preserved');
