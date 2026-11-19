// Course coordinates: d = downhill metres, u = metres across the run.
// Gameplay distances/collisions remain in those coordinates; every visual uses this frame.
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
const smooth=x=>{x=clamp(x,0,1);return x*x*(3-2*x)};
const base=d=>36*(1-Math.cos(d/52))+16*(Math.cos(d/97)-1);
const slope=d=>36/52*Math.sin(d/52)-16/97*Math.sin(d/97);
const bend=d=>36/(52*52)*Math.cos(d/52)-16/(97*97)*Math.cos(d/97);
// Rows and turns use independent indexed streams: seeking never consumes randomness.
export function courseRoll(seed,index,stream=0){
 let n=(seed^Math.imul(index+1,0x9e3779b1)^Math.imul(stream+1,0x85ebca6b))>>>0;
 n=Math.imul(n^(n>>>16),0x21f0aaad);n=Math.imul(n^(n>>>15),0x735a2d97);
 return ((n^(n>>>15))>>>0)/4294967296;
}
let route=null,tailStart=0,tailX=0,lastD=NaN,cx=0,dx=0,ddx=0;
let tubingCourse=false;
export function configureCourse(seed,tubing=false){
 tubingCourse=tubing;
 lastD=NaN;route=null;if(seed==null)return;
 route=[];let d=0,x=0,i=0,side=courseRoll(seed,0)>.5?1:-1;
 const add=(length,end)=>{route.push({d,length,x,delta:end-x});d+=length;x=end;};
 add(22,0);
 while(d<1920){
  const r=k=>courseRoll(seed,i,k);side=Math.abs(x)>46?-Math.sign(x):(r(2)<.55?-side:side);
  add(100+r(3)*37,x+side*(17+r(4)*10));
  if(r(5)<.56)add(112+r(6)*32,x-side*(18+r(7)*10)); // linked turns, no full straight between them
  add(12+r(8)*42,x);i++;
 }
 tailStart=d;tailX=x;
}
function sample(d){
 if(lastD===d)return;lastD=d;
 if(!route||d>=2500){cx=base(d);dx=slope(d);ddx=bend(d);return;}
 if(d<=0){cx=dx=ddx=0;return;}
 let lo=0,hi=route.length;
 while(lo<hi){const m=(lo+hi)>>1;if(d>=route[m].d+route[m].length)lo=m+1;else hi=m;}
 const seg=route[lo],start=seg?.d??tailStart,length=seg?.length??(2500-tailStart);
 // Quintic blend and its first two downhill derivatives. All three vanish or
 // flatten at the join, keeping heading and surface banking continuous.
 const t=(d-start)/length,blend=t*t*t*(10+t*(-15+6*t));
 const velocity=30*t*t*(t-1)*(t-1)/length;
 const acceleration=60*t*(t-1)*(2*t-1)/(length*length);
 if(seg){
  cx=seg.x+seg.delta*blend;dx=seg.delta*velocity;ddx=seg.delta*acceleration;
 }else{
  const delta=base(d)-tailX;
  cx=tailX+blend*delta;
  dx=velocity*delta+blend*slope(d);
  ddx=acceleration*delta+2*velocity*slope(d)+blend*bend(d);
 }
}
export function courseCenter(d){sample(d);return cx;}
export function courseDerivative(d){sample(d);return dx;}
export function courseCurvature(d){sample(d);return ddx/Math.pow(1+dx*dx,1.5);}
export function courseHeading(z){return -Math.atan(courseDerivative(-z))}
export function courseGrade(d){return .29+.22*Math.sin(d/42)+.09*Math.sin(d/17+1)}
export function courseHeight(d){return -.29*d+9.24*(Math.cos(d/42)-1)+1.53*(Math.cos(d/17+1)-Math.cos(1))}
export function courseBank(z){return .12*Math.tanh(-courseCurvature(-z)*10/.12)}
// Blend cross-sections at stage boundaries; gameplay coordinates keep their existing scale.
export function courseTheme(d){const local=((d%20000)+20000)%20000,stage=Math.floor(local/2500),part=local%2500;
 return {stage,blend:smooth(part/35)*smooth((2500-part)/35)};}
export function courseWidth(d){if(tubingCourse)return 16.2;const t=courseTheme(d);return t.stage===1?21-7.5*t.blend:21;}
export function courseCrossHeight(u,z){
 const edge=smooth((Math.abs(u)-9.4)/5.8);
 const drift=3.4+.65*Math.sin(-z/23+(u<0?1.4:0))+.25*Math.cos(-z/9);
 const t=courseTheme(-z);let shoulder=edge*drift;
 if(t.stage===1)shoulder+=(2.8*smooth((Math.abs(u)-6.6)/5.8)-shoulder)*t.blend;
 if(t.stage===3)shoulder+=(4.2*smooth((Math.abs(u)-6)/6)-shoulder)*t.blend;
 if(tubingCourse){const x=Math.abs(u);shoulder+=.67*smooth((x-7.3)/.95)*(1-smooth((x-8.3)/1.6));}
 return courseBank(z)*clamp(u,-14,14)+shoulder;
}
export function courseCrossSlope(u,z){return (courseCrossHeight(u+.05,z)-courseCrossHeight(u-.05,z))/.1}
// y is the old centre-line height. Keep particle simulations in course space too.
export function coursePoint(out,u,y,z){
 const a=courseHeading(z),c=Math.cos(a),s=Math.sin(a);
 return out.set(courseCenter(-z)+u*c,y+courseCrossHeight(u,z),z-u*s);
}
export function courseGround(out,u,z,above=0){return coursePoint(out,u,courseHeight(-z)+above,z)}
export function coursePitch(u,z){
 const ahead=courseHeight(-z+.01)+courseCrossHeight(u,z-.01);
 const behind=courseHeight(-z-.01)+courseCrossHeight(u,z+.01);
 const p=courseDerivative(-z);
 return Math.atan((behind-ahead)/(.02*Math.sqrt(1+p*p)*(1-u*courseCurvature(-z))));
}
// A short grip response converts lateral acceleration into bounded slip speed (m/s).
export function courseSlip(d,u,speed){
 return clamp((-courseCurvature(d)*speed*speed-9.81*courseCrossSlope(u,-d))*.24,-1.8,1.8);
}
