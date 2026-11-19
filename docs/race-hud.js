// Important race information stays large; transient notifications live outside this module.
const plateImage=new Image(),helmetImage=new Image();
export function loadHudArt(){
 if(!plateImage.src)plateImage.src='art/slope/hud-59b.webp';
 if(!helmetImage.src)helmetImage.src='art/slope/helmet-59b.webp';
 return Promise.all([plateImage.decode(),helmetImage.decode()]).catch(err=>{
   plateImage.removeAttribute('src');helmetImage.removeAttribute('src');throw err;
 });
}
export function drawRaceHUD(c,d){
 const {width:W,height:H}=d,top=10+d.safeTop;
 const rr=(x,y,w,h,r)=>{r=Math.min(r,w/2,h/2);c.beginPath();c.moveTo(x+r,y);c.arcTo(x+w,y,x+w,y+h,r);c.arcTo(x+w,y+h,x,y+h,r);c.arcTo(x,y+h,x,y,r);c.arcTo(x,y,x+w,y,r);c.closePath();};
 const label=(s,x,y,size=13,color='#1d3f7a',align='left',weight=800)=>{c.font=weight+' '+size+'px Rubik,system-ui,sans-serif';c.textAlign=align;c.textBaseline='middle';c.fillStyle=color;c.fillText(String(s),x,y);};
 const plate=(x,y,w,h)=>{c.save();c.shadowColor='rgba(76,49,23,.18)';c.shadowBlur=5;c.shadowOffsetY=2;rr(x,y,w,h,13);c.fillStyle='rgba(255,248,234,.93)';c.fill();c.restore();c.lineWidth=1.5;c.strokeStyle='#d9b98a';rr(x,y,w,h,13);c.stroke();};
 c.save();
 const width=Math.min(156,W*.42),scale=width/302,x0=(W-width)/2;
 const at=(x,y)=>[x0+x*scale,top+y*scale];
 c.save();c.shadowColor='#ace8ff';c.shadowBlur=8;
 if(plateImage.naturalWidth)c.drawImage(plateImage,...at(0,0),width,223*scale);
 c.restore();
 const digits=String(d.score),size=Math.min(35,116/Math.max(3,digits.length)*1.65);
 label(digits,...at(151,86),size,'#0c3b5f','center',900);
 for(let i=0;i<3;i++){
   c.save();c.globalAlpha=i<d.lives?1:.23;
   if(helmetImage.naturalWidth)c.drawImage(helmetImage,...at(28+i*46,140),47*scale,44*scale);
   c.restore();
 }
 label(d.coins,...at(257,164),String(d.coins).length>3?12:15,'#103b57','center');
 plate(8,top+2,76,25);label(d.distance+' м',16,top+15,13);
 label(d.speed+' км/ч',11,top+43,12,d.boost?'#ab511b':'#214365');
 if(d.multiplier>1)label('×'+d.multiplier,W-18,top+63,16,'#825014','right');
 c.font='700 11px Rubik,system-ui,sans-serif';plate(10,top+121,Math.min(W*.56,c.measureText(d.stage).width+20),21);label(d.stage,20,top+132,11,'#315777','left',700);
 if(d.status){c.font='700 11px Rubik,system-ui,sans-serif';const w=Math.min(W-24,c.measureText(d.status).width+20);
  rr(12,top+145,w,21,9);c.fillStyle=d.warning?'rgba(255,239,228,.88)':'rgba(255,248,234,.8)';c.fill();label(d.status,22,top+156,11,d.warning?'#ad412b':'#89531f','left',700);}
 if(d.warning){c.fillStyle='rgba(183,62,45,.25)';c.fillRect(12,H-8,W-24,2);c.fillStyle='#b63e2d';c.fillRect(12,H-8,(W-24)*d.chaseK,2);}
 c.restore();
}
