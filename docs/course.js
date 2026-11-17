// Course coordinates: d = downhill metres, u = metres across the run.
// Gameplay distances/collisions remain in those coordinates; every visual uses this frame.
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
const smooth=x=>{x=clamp(x,0,1);return x*x*(3-2*x)};
export function courseCenter(d){return 36*(1-Math.cos(d/52))+16*(Math.cos(d/97)-1)}
export function courseDerivative(d){return 36/52*Math.sin(d/52)-16/97*Math.sin(d/97)}
export function courseCurvature(d){const p=courseDerivative(d);return (36/(52*52)*Math.cos(d/52)-16/(97*97)*Math.cos(d/97))/Math.pow(1+p*p,1.5)}
export function courseHeading(z){return -Math.atan(courseDerivative(-z))}
export function courseGrade(d){return .29+.22*Math.sin(d/42)+.09*Math.sin(d/17+1)}
export function courseHeight(d){return -.29*d+9.24*(Math.cos(d/42)-1)+1.53*(Math.cos(d/17+1)-Math.cos(1))}
export function courseBank(z){return .12*Math.tanh(-courseCurvature(-z)*10/.12)}
export function courseCrossHeight(u,z){
 const edge=smooth((Math.abs(u)-9.4)/5.8);
 const drift=3.4+.65*Math.sin(-z/23+(u<0?1.4:0))+.25*Math.cos(-z/9);
 return courseBank(z)*clamp(u,-14,14)+edge*drift;
}
export function courseCrossSlope(u,z){return (courseCrossHeight(u+.05,z)-courseCrossHeight(u-.05,z))/.1}
// y is the old centre-line height. Keep particle simulations in course space too.
export function coursePoint(out,u,y,z){
 const a=courseHeading(z),c=Math.cos(a),s=Math.sin(a);
 return out.set(courseCenter(-z)+u*c,y+courseCrossHeight(u,z),z-u*s);
}
export function courseGround(out,u,z,above=0){return coursePoint(out,u,courseHeight(-z)+above,z)}
export function coursePitch(u,z){
 const ahead=courseHeight(-z+.1)+courseCrossHeight(u,z-.1);
 const behind=courseHeight(-z-.1)+courseCrossHeight(u,z+.1);
 const p=courseDerivative(-z);
 return Math.atan((behind-ahead)/(.2*Math.sqrt(1+p*p)*(1-u*courseCurvature(-z))));
}
// A short grip response converts lateral acceleration into bounded slip speed (m/s).
export function courseSlip(d,u,speed){
 return clamp((-courseCurvature(d)*speed*speed-9.81*courseCrossSlope(u,-d))*.24,-1.8,1.8);
}
