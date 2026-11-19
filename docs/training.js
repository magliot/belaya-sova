// One row is 62.5 metres. The moving lane always leaves the opposite half open.
import {courseRoll} from './course.js';
export const ROW=1250,variants=['tube60B','tube60C','tube61A','tube61C'];
export function trainingRow(seed,n){
 const r=k=>courseRoll(seed,n,k),side=r(13)<.5?-1:1,y=n*ROW,objects=[];
 const obj=(type,x,dy,extra={})=>objects.push({type,x,y:y+dy,...extra});
 const coins=(x,start,num)=>{for(let j=0;j<num;j++)obj('coin',x,start+j*55,{r:9});};
 const offset=560+Math.floor(r(9)*260),open=200-side*(52+r(6)*34);
 const kind=n===0?'tube':n===1?'gate':n%12===2?'snowman':n%12===7?'cat':n%6===4?'rest':
   n%3===0?'tube':r(2)<.48?'gate':r(3)<.52?'tube':'snowman';
 if(!n)coins(200,220,4);
 if(kind==='tube'){
  const marker=y+offset,m={marker,side,phase:r(4),variant:variants[Math.floor(r(5)*4)]};
  obj('tube',200+side*85,offset,m);coins(open,offset-175,4);
 }else if(kind==='cat'){
  obj('cat',200+side*96,offset,{marker:y+offset,small:true,side,vy:60,w:46,h:74});coins(open,offset-145,4);
 }else if(kind==='gate'){
  obj('gate',open,offset,{gap:118,col:n%2});coins(open,offset-160,3);
 }else if(kind==='snowman'){
  const costume=n%12===2?Math.floor(n/12):Math.floor(r(8)*3);
  obj('snowman',200+side*108,offset,{variant:['snow26A','snow26B','snow26C'][(costume+seed)%3],r:12,quiet:true});coins(open,offset-160,3);
 }else{
  coins(200,280,6);
  if(n===4||r(11)<.48)obj(n===4||r(12)<.4?'helmet':'magnet',200,685,{r:10});
 }
 if(n>4&&n%5===1)obj('gate',open,1050,{gap:130,col:n%2});
 if(n%6===1)obj('thermos',open,950,{r:10});
 return objects;
}
export function trainingActorAt(o,playerY){
 const u=Math.max(0,Math.min(1,(playerY-o.marker+920)/1220)),t=u*u*(3-2*u);
 o.y=o.marker+(o.small?155:205)*t;
 if(!o.small){
  o.x=200+o.side*(126-94*t+7*Math.sin(o.phase*6.28+Math.PI*t));
  o.yaw=o.side*(.48+.68*Math.sin(Math.PI*t));
 }
}
