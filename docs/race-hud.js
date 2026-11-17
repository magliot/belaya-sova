// Important race information stays large; transient notifications live outside this module.
export function drawRaceHUD(c,d){
 const {width:W,height:H}=d,top=10+d.safeTop;
 const rr=(x,y,w,h,r)=>{r=Math.min(r,w/2,h/2);c.beginPath();c.moveTo(x+r,y);c.arcTo(x+w,y,x+w,y+h,r);c.arcTo(x+w,y+h,x,y+h,r);c.arcTo(x,y+h,x,y,r);c.arcTo(x,y,x+w,y,r);c.closePath();};
 const label=(s,x,y,size=13,color='#1d3f7a',align='left',weight=800)=>{c.font=weight+' '+size+'px Rubik,system-ui,sans-serif';c.textAlign=align;c.textBaseline='middle';c.fillStyle=color;c.fillText(String(s),x,y);};
 const plate=(x,y,w,h)=>{c.save();c.shadowColor='rgba(76,49,23,.18)';c.shadowBlur=5;c.shadowOffsetY=2;rr(x,y,w,h,13);c.fillStyle='rgba(255,248,234,.93)';c.fill();c.restore();c.lineWidth=1.5;c.strokeStyle='#d9b98a';rr(x,y,w,h,13);c.stroke();};
 c.save();
 // A warm glow behind the score, confined to the top of the screen.
 const sw=Math.min(144,Math.max(110,W-218)),sx=(W-sw)/2;
 const glow=c.createRadialGradient(W/2,top+26,5,W/2,top+26,sw*.72);
 glow.addColorStop(0,'rgba(255,220,123,.62)');glow.addColorStop(1,'rgba(255,225,140,0)');
 c.fillStyle=glow;c.fillRect(sx-20,top-10,sw+40,82);
 plate(sx,top,sw,57);label('ОЧКИ',W/2,top+11,9,'#96703d','center');
 c.save();c.shadowColor='#ffda73';c.shadowBlur=12+Math.sin(d.time*2)*3;
 const size=Math.min(35,(sw-14)/Math.max(1,String(d.score).length)*1.65);
 label(d.score,W/2,top+35,size,'#10345f','center',900);c.restore();
 if(d.multiplier>1){rr(sx+sw-25,top+42,31,21,9);c.fillStyle='#ffb53e';c.fill();label('×'+d.multiplier,sx+sw-10,top+53,12,'#66330d','center');}
 plate(10,top+3,84,27);label(d.distance+' м',22,top+17,14);
 plate(10,top+42,89,37);
 // Drawn mandarin: independent of emoji fonts on the device.
 const ox=29,oy=top+61;c.save();c.translate(ox,oy);const fruit=c.createRadialGradient(-4,-5,1,0,0,13);
 fruit.addColorStop(0,'#ffc951');fruit.addColorStop(1,'#ef7418');c.fillStyle=fruit;c.beginPath();c.ellipse(0,0,12,11,0,0,Math.PI*2);c.fill();
 c.strokeStyle='#d46615';c.lineWidth=1;c.stroke();c.fillStyle='#3b8745';c.beginPath();c.ellipse(4,-12,7,3,-.4,0,Math.PI*2);c.fill();c.restore();
 label(d.coins,48,top+62,String(d.coins).length>4?14:19);
 label(d.speed+' км/ч',W/2,top+73,14,d.boost?'#b55a17':'#274e76','center');
 // Three individual helmet slots with a shell, visor, stripe and chin strap.
 for(let i=0;i<3;i++){const x=W-92+i*31,y=top+64;c.save();c.translate(x,y);c.globalAlpha=i<d.lives?1:.25;
  c.shadowColor='rgba(32,61,92,.2)';c.shadowBlur=3;c.fillStyle='#fff9eb';c.strokeStyle='#385b7a';c.lineWidth=1.6;
  c.beginPath();c.moveTo(-12,5);c.lineTo(-12,-1);c.bezierCurveTo(-12,-17,12,-17,12,-1);c.lineTo(12,5);c.closePath();c.fill();c.stroke();
  c.shadowBlur=0;c.strokeStyle='#ffa12c';c.lineWidth=3;c.beginPath();c.moveTo(1,-12);c.lineTo(2,-3);c.stroke();
  rr(-11,-2,22,7,3);c.fillStyle='#315274';c.fill();rr(-8,-1,8,2,1);c.fillStyle='#a4ddf3';c.fill();
  c.strokeStyle='#385b7a';c.lineWidth=1.5;c.beginPath();c.moveTo(-8,6);c.quadraticCurveTo(0,14,8,6);c.stroke();c.restore();}
 c.font='700 11px Rubik,system-ui,sans-serif';plate(10,top+90,Math.min(W*.56,c.measureText(d.stage).width+20),21);label(d.stage,20,top+101,11,'#315777','left',700);
 if(d.rival){plate(W-120,top+90,110,21);label(d.rival,W-20,top+101,11,'#315777','right',700);}
 if(d.status){c.font='700 11px Rubik,system-ui,sans-serif';const w=Math.min(W-24,c.measureText(d.status).width+20);
  rr(12,top+116,w,21,9);c.fillStyle=d.warning?'rgba(255,239,228,.88)':'rgba(255,248,234,.8)';c.fill();label(d.status,22,top+127,11,d.warning?'#ad412b':'#89531f','left',700);}
 if(d.warning){c.fillStyle='rgba(183,62,45,.25)';c.fillRect(12,H-8,W-24,2);c.fillStyle='#b63e2d';c.fillRect(12,H-8,(W-24)*d.chaseK,2);}
 c.restore();
}
