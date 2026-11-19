import assert from 'node:assert/strict';
import {configureCourse,courseCenter,courseCurvature,courseDerivative} from '../docs/course.js';
import {ROW,trainingActorAt,trainingRow} from '../docs/training.js';

const layout=seed=>Array.from({length:40},(_,i)=>trainingRow(seed,i)).flat();
const turns=seed=>{
  configureCourse(seed);
  return Array.from({length:125},(_,i)=>courseCenter(i*20));
};
const seed=1911,original=turns(seed);
assert.notDeepEqual(turns(seed+1),original);
assert.deepEqual(turns(seed),original);
assert.deepEqual(layout(seed),layout(seed));
assert.notDeepEqual(layout(seed),layout(seed+1));
let minMetric=1;
for(let s=0;s<256;s++){
  configureCourse(s);
  assert.equal(courseCenter(20),0);
  for(let d=0;d<=2500;d+=2)minMetric=Math.min(minMetric,1-40*Math.abs(courseCurvature(d)));
  assert(Math.abs(courseDerivative(2499.999)-courseDerivative(2500.001))<.0001);
  assert(Math.abs(courseCenter(2499.999)-courseCenter(2500.001))<.002);
  const actors=layout(s);
  assert.equal(actors.filter(o=>o.small).length,3);
  assert.equal(new Set(actors.filter(o=>o.type==='snowman').map(o=>o.variant)).size,3);
  assert(actors.every(o=>o.y>=0&&o.y<40*ROW&&o.x>20&&o.x<380));
  for(const o of actors.filter(o=>o.type==='tube'||o.small)){
    const start=o.marker,side=o.side;
    let last=start;
    for(let y=start-1200;y<start+1550;y+=10){
      trainingActorAt(o,y,1/30,y-10);
      assert(side*(o.x-200)>=-18,'marked escape lane must remain clear');
      assert(o.y>=last&&o.y-last<4);last=o.y;
      if(!o.small)assert(Math.abs(o.yaw-Math.PI)<1&&Math.abs(o.lean)<=1);
    }
  }
}
assert(minMetric>.12,'outer shoulder folds over itself');
console.log('PASS 256 seeds, 40 rows each, continuous join at 2500 m, clear bypass for all moving obstacles. Minimum shoulder metric',minMetric.toFixed(3));
