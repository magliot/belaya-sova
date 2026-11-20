const assert=require('node:assert/strict');
const {createRanking}=require('../server/preview/ranking.cjs');
const db=createRanking(':memory:');
const A='0'.repeat(32),B='1'.repeat(32);
const run={action:'submit',scoring:2,tester:A,runId:'attempt.0001',hero:'bear',dur:42,
  dist:414,coins:37,gates:2,preciseGates:1,rings:0,preciseRings:0,rules:1,score:576};
try{
  assert.throws(()=>db.handle({...run,score:575}));
  assert.throws(()=>db.handle({...run,dist:50000,score:50162}));
  assert.throws(()=>db.handle({...run,preciseGates:3,score:596}));
  assert.equal(db.handle(run).best,576);
  assert.equal(db.handle({...run,score:1076,dist:914}).best,576);
  assert.equal(db.handle({...run,tester:B,runId:'attempt.0002',dist:614,score:776}).rank,1);
  assert.equal(db.handle({...run,runId:'attempt.0003',dist:114,score:276}).rank,2);
  const result=db.handle({action:'leaderboard',tester:A,period:'week'});
  assert.deepEqual(result.rows.map(row=>[row.rank,row.score]),[[1,776],[2,576]]);
  assert.equal(result.me.rank,2);assert.equal(db.handle({action:'leaderboard'}).me,null);
  assert.equal(db.handle({...run,runId:'attempt.0004',dist:200414,score:200576,dur:9130}).rank,1);
  console.log('PASS two anonymous identities, public best per person, private rank, idempotent receipts, score validation');
}finally{db.close();}
