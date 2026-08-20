(() => {
  const canvas=document.querySelector('#game');
  const ctx=canvas.getContext('2d');
  const shop=document.querySelector('#shop');
  const mapImage=document.querySelector('.map-img');
  const W=768,H=1152;

  const TYPES={
    tank:{name:'왕관 방패병',cost:5,hp:250,damage:11,range:38,rate:850,radius:24,moveSpeed:.088,color:'#f6c744',role:'전열 · 넓은 충돌',mark:'◈'},
    melee:{name:'코랄 망치병',cost:4,hp:135,damage:33,range:45,rate:620,radius:19,moveSpeed:.12,color:'#ff7f68',role:'근접 · 높은 지속 피해',mark:'◆'},
    archer:{name:'민트 궁수',cost:4,hp:78,damage:24,range:170,rate:720,radius:16,moveSpeed:.112,color:'#5fe0b5',role:'원거리 · 빠른 투사체',mark:'➶'},
    aoe:{name:'별가루 범위 마법사',cost:6,hp:70,damage:28,range:150,rate:1180,radius:17,moveSpeed:.1,color:'#b983f4',role:'마법 · 범위 폭발',mark:'✦',splash:64},
    single:{name:'유리구슬 단일 마법사',cost:5,hp:65,damage:54,range:195,rate:1450,radius:16,moveSpeed:.105,color:'#55b8f3',role:'마법 · 정예 집중',mark:'●'}
  };

  const AUGMENTS=[
    {name:'단단한 방패',icon:'🛡️',desc:'방패병 최대 체력 강화'},
    {name:'정밀 조준',icon:'🎯',desc:'궁수·단일 마법사 공격력 강화'},
    {name:'별가루 확산',icon:'✨',desc:'범위 마법사의 폭발 반경 강화'},
    {name:'언덕의 맹세',icon:'⛰️',desc:'언덕 위 아군 공격력 강화'},
    {name:'긴급 증원',icon:'🪙',desc:'다음 라운드 시작 예산 증가'},
    {name:'빠른 손놀림',icon:'⚡',desc:'모든 아군 공격 속도 강화'}
  ];
  const tierClass={브론즈:'bronze',실버:'silver',골드:'gold',플레:'platinum',다이아:'diamond'};
  const transitions={
    브론즈:{실버:10,골드:20,플레:30,다이아:40},
    실버:{브론즈:12,골드:32,플레:38,다이아:18},
    골드:{브론즈:15,실버:25,플레:32,다이아:28},
    플레:{브론즈:18,실버:30,골드:32,다이아:20},
    다이아:{브론즈:12,실버:20,골드:28,플레:40}
  };
  const tierPower=tier=>({브론즈:1,실버:1.35,골드:1.75,플레:2.2,다이아:2.85}[tier]);

  const NAV={cols:128,rows:192,seed:[64,190],goal:[27,42]};
  const HILL_CONTROL=[[384,1140],[384,1030],[400,920],[420,840],[390,760],[330,690],[320,610],[330,540],[380,470],[365,405],[315,360],[250,320],[190,285]];
  let route=[],roadMask=null,groundMask=null,navReady=false,navSource='loading';
  let units=[],enemies=[],projectiles=[],effects=[],selected=[];
  const GATE_MAX_HP=600;
  let buyType=null,round=1,money=19,state='prep',gateSafe=true,gateHp=GATE_MAX_HP,gateFlash=0,lastTier='브론즈',stacks={};
  let spawnLeft=0,spawnClock=0,lastTime=performance.now(),toastTimer=0;
  let pointer=null,hoverPointer=null,commandPreview=null;

  const stackPower=name=>stacks[name]?.power||0;
  const stackCount=name=>stacks[name]?.count||0;
  const startBudget=()=>Math.round(16+round*3+stackPower('긴급 증원')*4);
  const navIndex=(x,y)=>y*NAV.cols+x;
  const clamp=(v,min,max)=>Math.max(min,Math.min(max,v));
  const canvasPos=ev=>{const r=canvas.getBoundingClientRect();return{x:(ev.clientX-r.left)*W/r.width,y:(ev.clientY-r.top)*H/r.height}};

  function toast(text){
    const el=document.querySelector('#toast');el.textContent=text;clearTimeout(toastTimer);
    toastTimer=setTimeout(()=>el.textContent=state==='prep'?'오르막을 막은 뒤 웨이브를 시작하세요.':'유닛을 계속 빼고 넣으며 입구를 지키세요.',2600);
  }

  function roadPixel(r,g,b){return r>100&&r>g*1.04&&r-b>45&&g-b>25}
  function groundPixel(r,g,b){return roadPixel(r,g,b)||(g>88&&g-r>11&&g-b>35)}

  function sampleHillSpline(){
    const result=[];
    for(let i=0;i<HILL_CONTROL.length-1;i++){
      const p0=HILL_CONTROL[Math.max(0,i-1)],p1=HILL_CONTROL[i],p2=HILL_CONTROL[i+1],p3=HILL_CONTROL[Math.min(HILL_CONTROL.length-1,i+2)];
      for(let step=0;step<14;step++){
        const t=step/14,t2=t*t,t3=t2*t;
        result.push([
          .5*((2*p1[0])+(-p0[0]+p2[0])*t+(2*p0[0]-5*p1[0]+4*p2[0]-p3[0])*t2+(-p0[0]+3*p1[0]-3*p2[0]+p3[0])*t3),
          .5*((2*p1[1])+(-p0[1]+p2[1])*t+(2*p0[1]-5*p1[1]+4*p2[1]-p3[1])*t2+(-p0[1]+3*p1[1]-3*p2[1]+p3[1])*t3)
        ]);
      }
    }
    result.push([...HILL_CONTROL.at(-1)]);return result;
  }

  function snapMask(seed,mask,maxRadius=30){
    for(let radius=0;radius<=maxRadius;radius++){
      let best=null;
      for(let y=Math.max(0,seed[1]-radius);y<=Math.min(NAV.rows-1,seed[1]+radius);y++)for(let x=Math.max(0,seed[0]-radius);x<=Math.min(NAV.cols-1,seed[0]+radius);x++){
        if(!mask[navIndex(x,y)])continue;
        const d=(x-seed[0])**2+(y-seed[1])**2;if(!best||d<best.d)best={x,y,d};
      }
      if(best)return[best.x,best.y];
    }
    return null;
  }

  function findRoute(){
    const start=snapMask(NAV.seed,roadMask),goal=snapMask(NAV.goal,roadMask);
    if(!start||!goal)return null;
    const total=NAV.cols*NAV.rows,prev=new Int32Array(total);prev.fill(-2);
    const queue=new Int32Array(total),startI=navIndex(...start),goalI=navIndex(...goal);
    let head=0,tail=0;queue[tail++]=startI;prev[startI]=-1;
    const dirs=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1],[1,-1],[-1,-1]];
    while(head<tail){
      const at=queue[head++];if(at===goalI)break;
      const x=at%NAV.cols,y=(at/NAV.cols)|0;
      for(const[dx,dy]of dirs){const nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=NAV.cols||ny>=NAV.rows)continue;const ni=navIndex(nx,ny);if(!roadMask[ni]||prev[ni]!==-2)continue;prev[ni]=at;queue[tail++]=ni}
    }
    if(prev[goalI]===-2)return null;
    const cells=[];for(let at=goalI;at!==-1;at=prev[at])cells.push([at%NAV.cols,(at/NAV.cols)|0]);cells.reverse();
    let points=cells.map(([x,y])=>[(x+.5)*W/NAV.cols,(y+.5)*H/NAV.rows]);
    for(let pass=0;pass<5;pass++)points=points.map((p,i)=>i===0||i===points.length-1?p:[(points[i-1][0]+p[0]*2+points[i+1][0])/4,(points[i-1][1]+p[1]*2+points[i+1][1])/4]);
    return points;
  }

  function useFallback(reason){
    roadMask=null;groundMask=null;route=sampleHillSpline();navReady=true;navSource='embedded-hill-spline';syncUI();
    window.__JELLY_DEBUG__={navSource,reason,routeLength:route.length};
  }

  function buildNavigation(){
    try{
      const scan=document.createElement('canvas');scan.width=NAV.cols;scan.height=NAV.rows;
      const sctx=scan.getContext('2d',{willReadFrequently:true});sctx.drawImage(mapImage,0,0,NAV.cols,NAV.rows);
      const data=sctx.getImageData(0,0,NAV.cols,NAV.rows).data;
      roadMask=new Uint8Array(NAV.cols*NAV.rows);groundMask=new Uint8Array(NAV.cols*NAV.rows);
      for(let i=0;i<roadMask.length;i++){const r=data[i*4],g=data[i*4+1],b=data[i*4+2];roadMask[i]=roadPixel(r,g,b)?1:0;groundMask[i]=groundPixel(r,g,b)?1:0}
      const spline=sampleHillSpline(),roadRatio=spline.filter(p=>{const x=clamp(Math.floor(p[0]/W*NAV.cols),0,NAV.cols-1),y=clamp(Math.floor(p[1]/H*NAV.rows),0,NAV.rows-1);return roadMask[navIndex(x,y)]}).length/spline.length;
      route=roadRatio>.82?spline:findRoute();if(!route||route.length<120)throw new Error('hill-route-connectivity-failed');
      navReady=true;navSource='hill-pixel-navmesh';syncUI();toast('오르막 경로 확인 완료 · 길 위에도 배치할 수 있습니다.');
      window.__JELLY_DEBUG__={navSource,routeLength:route.length,roadRatio,roadCells:roadMask.reduce((a,b)=>a+b,0),route};
    }catch(error){useFallback(error.message)}
  }

  function routeDistance(p){
    let best=Infinity;for(const q of route){const d=Math.hypot(p.x-q[0],p.y-q[1]);if(d<best)best=d}return best;
  }
  function validGround(p){
    if(p.x<35||p.x>733||p.y<225||p.y>1110)return false;
    if(groundMask){const x=clamp(Math.floor(p.x/W*NAV.cols),0,NAV.cols-1),y=clamp(Math.floor(p.y/H*NAV.rows),0,NAV.rows-1);return !!groundMask[navIndex(x,y)]}
    const plateau=((p.x-430)/315)**2+((p.y-310)/205)**2<1;
    return routeDistance(p)<105||plateau||(p.y>900&&p.x>95&&p.x<680);
  }
  function nearestGround(p){
    const base={x:clamp(p.x,36,732),y:clamp(p.y,226,1108)};if(validGround(base))return base;
    if(groundMask){const sx=Math.floor(base.x/W*NAV.cols),sy=Math.floor(base.y/H*NAV.rows);for(let rad=1;rad<22;rad++)for(let y=sy-rad;y<=sy+rad;y++)for(let x=sx-rad;x<=sx+rad;x++){if(x<0||y<0||x>=NAV.cols||y>=NAV.rows||!groundMask[navIndex(x,y)])continue;const q={x:(x+.5)*W/NAV.cols,y:(y+.5)*H/NAV.rows};if(validGround(q))return q}}
    let best={x:route[0]?.[0]||384,y:route[0]?.[1]||1000},dist=Infinity;for(const q of route){const d=Math.hypot(base.x-q[0],base.y-q[1]);if(d<dist){dist=d;best={x:q[0],y:q[1]}}}return best;
  }
  function validPlacement(p,radius){return validGround(p)&&!units.some(u=>Math.hypot(u.x-p.x,u.y-p.y)<u.radius+radius+4)}

  function renderShop(){
    shop.innerHTML='';Object.entries(TYPES).forEach(([key,t],index)=>{
      const b=document.createElement('button');b.className='unit-card';b.dataset.type=key;
      b.innerHTML=`<span class="portrait" style="background:${t.color}">${t.mark}</span><span><strong>${t.name}</strong><small>${index+1} · ${t.role}</small></span><b class="cost">${t.cost}</b>`;
      b.onclick=()=>{if(state!=='prep')return;if(buyType===key){cancelBuild();return}buyType=key;selected=[];syncUI();toast(`${t.name} 배치 모드 · 맵을 누르면 소환, ESC로 취소`)};shop.appendChild(b);
    });
  }

  function syncUI(){
    document.querySelector('#roundPill').textContent=(state==='battle'?'전투 ':'준비 ')+'라운드 '+round+(round%5===0?' · 보스':'');
    document.querySelector('#moneyText').textContent=Math.floor(money);const gateText=document.querySelector('#gateText');gateText.textContent=`${Math.max(0,Math.ceil(gateHp))} / ${GATE_MAX_HP}`;gateText.style.color=gateHp/GATE_MAX_HP>.5?'#ffffff':gateHp/GATE_MAX_HP>.2?'#ffe16b':'#ff8b78';
    document.querySelector('#selectionText').textContent=selected.length?`${selected.length}개 선택 · 드래그하거나 빈 곳을 눌러 이동`:'선택된 유닛 없음';
    document.querySelector('#startButton').disabled=state!=='prep'||!navReady;
    const mode=document.querySelector('#modeText'),cancel=document.querySelector('#cancelBuild');
    if(buyType){mode.textContent=`배치 모드 · ${TYPES[buyType].name}`;mode.classList.add('building');cancel.disabled=false;canvas.classList.add('place');canvas.classList.remove('control')}
    else{mode.textContent='조작 모드 · 선택/이동';mode.classList.remove('building');cancel.disabled=true;canvas.classList.remove('place');canvas.classList.add('control')}
    document.querySelector('#stopButton').disabled=!selected.length;
    document.querySelectorAll('.unit-card').forEach(b=>{b.classList.toggle('active',b.dataset.type===buyType);b.disabled=state!=='prep'});
    document.querySelector('#buildList').innerHTML=Object.keys(stacks).length?Object.entries(stacks).map(([name,v])=>`<span class="build-chip">${name} ×${v.count}</span>`).join(''):'<span class="build-chip">없음</span>';
  }

  function cancelBuild(){if(!buyType)return;buyType=null;syncUI();toast('배치 취소 · 유닛 선택과 이동이 가능합니다.')}
  function stopSelected(){selected.forEach(u=>{u.moving=false;u.targetX=u.x;u.targetY=u.y;u.vx=0;u.vy=0});toast('선택 유닛 정지');syncUI()}

  function addUnit(type,p){
    const t=TYPES[type];if(!t)return;if(money<t.cost){toast('코인이 부족합니다.');return}
    const spot=nearestGround(p);if(!validPlacement(spot,t.radius)){toast('언덕·오르막의 빈 공간에 배치하세요.');return}
    money-=t.cost;const hp=t.hp*(1+stackPower('단단한 방패')*(type==='tank'?.18:0));
    const unit={...t,type,x:spot.x,y:spot.y,targetX:spot.x,targetY:spot.y,vx:0,vy:0,moving:false,hp,maxHp:hp,lastAttack:0,phase:Math.random()*6.28};
    units.push(unit);selected=[unit];buyType=null;syncUI();toast(`${t.name} 배치 완료 · 바로 드래그해 이동할 수 있습니다.`);
  }

  function commandMove(target){
    if(!selected.length)return;const center={x:selected.reduce((s,u)=>s+u.x,0)/selected.length,y:selected.reduce((s,u)=>s+u.y,0)/selected.length};
    selected.forEach(u=>{const raw={x:target.x+(u.x-center.x),y:target.y+(u.y-center.y)},spot=nearestGround(raw);u.targetX=spot.x;u.targetY=spot.y;u.moving=Math.hypot(u.x-spot.x,u.y-spot.y)>3});
    effects.push({kind:'order',x:target.x,y:target.y,radius:30,color:'#7dffcf',life:26,max:26,width:3});
  }

  function unitAt(p){return [...units].reverse().find(u=>Math.hypot(u.x-p.x,u.y-p.y)<u.radius+8)}

  canvas.addEventListener('pointerdown',ev=>{
    const p=canvasPos(ev);hoverPointer=p;canvas.setPointerCapture(ev.pointerId);
    if(buyType){pointer={start:p,current:p,kind:'place',moved:false};return}
    const hit=unitAt(p);
    if(hit){if(!selected.includes(hit)){selected=ev.shiftKey?[...selected,hit]:[hit]}else if(ev.shiftKey){selected=selected.filter(u=>u!==hit)}pointer={start:p,current:p,kind:'unit',hit,moved:false};syncUI();return}
    pointer={start:p,current:p,kind:'empty',moved:false};
  });
  canvas.addEventListener('pointermove',ev=>{
    const p=canvasPos(ev);hoverPointer=p;if(!pointer)return;pointer.current=p;pointer.moved=Math.hypot(p.x-pointer.start.x,p.y-pointer.start.y)>8;
    commandPreview=pointer.kind==='unit'&&pointer.moved?p:null;
  });
  canvas.addEventListener('pointerup',ev=>{
    if(!pointer)return;const p=canvasPos(ev),action=pointer;pointer=null;commandPreview=null;
    if(action.kind==='place'){if(!action.moved)addUnit(buyType,p);return}
    if(action.kind==='unit'){if(action.moved)commandMove(p);syncUI();return}
    if(action.kind==='empty'&&action.moved){const x1=Math.min(action.start.x,p.x),x2=Math.max(action.start.x,p.x),y1=Math.min(action.start.y,p.y),y2=Math.max(action.start.y,p.y);selected=units.filter(u=>u.x>=x1&&u.x<=x2&&u.y>=y1&&u.y<=y2);syncUI();return}
    if(action.kind==='empty'){if(selected.length)commandMove(p);else selected=[];syncUI()}
  });
  canvas.addEventListener('pointercancel',()=>{pointer=null;commandPreview=null});
  canvas.addEventListener('contextmenu',ev=>{ev.preventDefault();if(buyType)cancelBuild();else if(selected.length)stopSelected()});
  window.addEventListener('keydown',ev=>{
    if(ev.key==='Escape'){cancelBuild();return}if(ev.key.toLowerCase()==='s'){stopSelected();return}
    const n=Number(ev.key);if(n>=1&&n<=5&&state==='prep'){const key=Object.keys(TYPES)[n-1];buyType=key;selected=[];syncUI();toast(`${TYPES[key].name} 배치 모드`)}
  });

  function pathTarget(enemy,index){
    const path=enemy.path,i=clamp(Math.round(Number.isFinite(index)?index:0),0,path.length-1),p=path[i],before=path[Math.max(0,i-2)],after=path[Math.min(path.length-1,i+2)];
    const tx=after[0]-before[0],ty=after[1]-before[1],length=Math.max(1,Math.hypot(tx,ty)),normalX=-ty/length,normalY=tx/length;
    let choke=1;if(p[1]>335&&p[1]<570)choke=.56;else if(p[1]<700)choke=.78;
    const lateral=Number.isFinite(enemy.lateral)?enemy.lateral:0,phase=Number.isFinite(enemy.wriggle)?enemy.wriggle:0,offset=lateral*choke+Math.sin(phase+i*.045)*3.8;
    return{x:p[0]+normalX*offset,y:p[1]+normalY*offset};
  }

  function spawnEnemy(index){
    const offsets=[-34,0,34,-17,17],lateral=offsets[index%offsets.length],boss=round%5===0&&index===0,maxHp=(75+round*16)*(boss?5:1),p=pathTarget({path:route,lateral,wriggle:0},0);
    enemies.push({x:p.x,y:p.y,drawX:p.x,drawY:p.y,path:route,node:0,lateral,wriggle:Math.random()*6.28,vx:0,vy:-1,hp:maxHp,maxHp,radius:boss?27:15,speed:boss?.033:.049,damage:boss?26:8,gateDamage:boss?210+round*10:42+round*4,boss,attackAt:0,angle:0,reachedGate:false});
  }

  function damageGate(enemy){
    if(enemy.reachedGate)return;enemy.reachedGate=true;gateHp=Math.max(0,gateHp-enemy.gateDamage);gateFlash=20;
    const gate=route[route.length-1];effects.push({kind:'burst',x:gate[0],y:gate[1],radius:72,color:'#ff6c55',width:9,life:28,max:28});syncUI();
    if(gateHp<=0){defeat();return}toast(`${enemy.boss?'보스':'적'} 성문 타격 · 내구도 ${Math.ceil(gateHp)} 남음`);
  }

  function updateUnits(dt){
    for(const u of units){
      u.phase+=dt*.008;if(!u.moving)continue;
      const dx=u.targetX-u.x,dy=u.targetY-u.y,d=Math.hypot(dx,dy);
      if(d<3){u.x=u.targetX;u.y=u.targetY;u.moving=false;u.vx=0;u.vy=0;continue}
      const desiredX=dx/d,desiredY=dy/d,turn=Math.min(1,dt*.014);u.vx+=(desiredX-u.vx)*turn;u.vy+=(desiredY-u.vy)*turn;
      const vl=Math.max(.001,Math.hypot(u.vx,u.vy));u.vx/=vl;u.vy/=vl;const step=Math.min(d,u.moveSpeed*dt),next={x:u.x+u.vx*step,y:u.y+u.vy*step};
      if(validGround(next)){u.x=next.x;u.y=next.y;continue}
      const slideX={x:u.x+u.vx*step,y:u.y},slideY={x:u.x,y:u.y+u.vy*step},options=[slideX,slideY].filter(validGround).sort((a,b)=>Math.hypot(a.x-u.targetX,a.y-u.targetY)-Math.hypot(b.x-u.targetX,b.y-u.targetY));
      if(options.length){u.x=options[0].x;u.y=options[0].y;u.vx=options[0]===slideX?Math.sign(u.vx):0;u.vy=options[0]===slideY?Math.sign(u.vy):0}
      else{u.moving=false;u.targetX=u.x;u.targetY=u.y;u.vx=0;u.vy=0}
    }
    for(let i=0;i<units.length;i++)for(let j=i+1;j<units.length;j++){const a=units[i],b=units[j],dx=b.x-a.x,dy=b.y-a.y,d=Math.max(.1,Math.hypot(dx,dy)),need=a.radius+b.radius+2;if(d>=need)continue;const push=(need-d)/2,nx=dx/d,ny=dy/d,ap={x:a.x-nx*push,y:a.y-ny*push},bp={x:b.x+nx*push,y:b.y+ny*push};if(validGround(ap)){a.x=ap.x;a.y=ap.y}if(validGround(bp)){b.x=bp.x;b.y=bp.y}}
  }

  function updateEnemies(dt,now){
    for(const e of enemies){
      e.wiggle+=dt*.012;const blocker=units.filter(u=>Math.hypot(u.x-e.x,u.y-e.y)<u.radius+e.radius+3).sort((a,b)=>b.radius-a.radius)[0];
      if(blocker){if(now-e.attackAt>720){blocker.hp-=e.damage;e.attackAt=now;effects.push({kind:'burst',x:blocker.x,y:blocker.y,radius:28,color:'#ff715f',width:4,life:16,max:16})}continue}
      const last=e.path.length-1,gate=pathTarget(e,last);
      while(e.node<last&&Math.hypot(pathTarget(e,e.node+1).x-e.x,pathTarget(e,e.node+1).y-e.y)<10)e.node++;
      if(e.node>=last-1&&Math.hypot(gate.x-e.x,gate.y-e.y)<13){damageGate(e);if(state==='lost')return;continue}
      const target=pathTarget(e,Math.min(last,e.node+6)),dx=target.x-e.x,dy=target.y-e.y,d=Math.max(1,Math.hypot(dx,dy));let desiredX=dx/d,desiredY=dy/d,avoidX=0,avoidY=0;
      for(const other of enemies){if(other===e)continue;const ox=e.x-other.x,oy=e.y-other.y,od=Math.hypot(ox,oy),space=e.radius+other.radius+5;if(od>0&&od<space){avoidX+=ox/od*(space-od)/space;avoidY+=oy/od*(space-od)/space}}
      desiredX+=avoidX*.22;desiredY+=avoidY*.22;const dl=Math.max(1,Math.hypot(desiredX,desiredY));desiredX/=dl;desiredY/=dl;const turn=Math.min(1,dt*.008);e.vx+=(desiredX-e.vx)*turn;e.vy+=(desiredY-e.vy)*turn;const vl=Math.max(.001,Math.hypot(e.vx,e.vy));e.vx/=vl;e.vy/=vl;e.x+=e.vx*e.speed*dt;e.y+=e.vy*e.speed*dt;e.angle=Math.atan2(e.vy,e.vx)+Math.PI/2;e.drawX=e.x;e.drawY=e.y;
    }
  }

  function hit(enemy,damage,splash=0){if(!enemies.includes(enemy))return;enemy.hp-=damage;if(splash){effects.push({kind:'burst',x:enemy.x,y:enemy.y,radius:splash,color:'#deb0ff',width:7,life:24,max:24});enemies.filter(e=>e!==enemy&&Math.hypot(e.x-enemy.x,e.y-enemy.y)<splash).forEach(e=>e.hp-=damage*.65)}}
  function attack(now){
    for(const u of units){if(u.moving)continue;const target=enemies.filter(e=>Math.hypot(e.x-u.x,e.y-u.y)<=u.range).sort((a,b)=>b.node-a.node)[0];if(!target||now-u.lastAttack<u.rate/(1+stackPower('빠른 손놀림')*.09))continue;u.lastAttack=now;const highGround=u.y<520?1+stackPower('언덕의 맹세')*.12:1,precision=(u.type==='archer'||u.type==='single')?1+stackPower('정밀 조준')*.14:1,damage=u.damage*highGround*precision;
      if(u.type==='melee'||u.type==='tank'){hit(target,damage);effects.push({kind:'burst',x:target.x,y:target.y,radius:30,color:u.color,width:5,life:18,max:18})}
      else{const dx=target.x-u.x,dy=target.y-u.y,d=Math.max(1,Math.hypot(dx,dy));projectiles.push({x:u.x,y:u.y,vx:dx/d,vy:dy/d,target,damage,color:u.color,splash:u.splash?u.splash+stackPower('별가루 확산')*16:0})}
    }
  }
  function updateProjectiles(dt){for(let i=projectiles.length-1;i>=0;i--){const p=projectiles[i];if(!enemies.includes(p.target)){projectiles.splice(i,1);continue}const dx=p.target.x-p.x,dy=p.target.y-p.y,d=Math.max(1,Math.hypot(dx,dy));p.vx=dx/d;p.vy=dy/d;p.x+=p.vx*dt*.52;p.y+=p.vy*dt*.52;if(d<11){hit(p.target,p.damage,p.splash);projectiles.splice(i,1)}}}
  function cleanup(){units=units.filter(u=>u.hp>0);selected=selected.filter(u=>units.includes(u));enemies=enemies.filter(e=>e.hp>0&&!e.reachedGate);effects.forEach(e=>e.life--);effects=effects.filter(e=>e.life>0)}

  function drawUnit(u,now,ghost=false){
    const bounce=u.moving?Math.sin(u.phase)*2:0,squash=u.moving?Math.sin(u.phase*1.3)*.05:0;ctx.save();ctx.globalAlpha=ghost?.5:1;ctx.translate(u.x,u.y+bounce);ctx.scale(1+squash,1-squash);ctx.fillStyle='#0005';ctx.beginPath();ctx.ellipse(4,u.radius*.78,u.radius*1.08,u.radius*.36,0,0,Math.PI*2);ctx.fill();ctx.fillStyle=u.color;ctx.beginPath();ctx.arc(0,0,u.radius,Math.PI,0);ctx.quadraticCurveTo(u.radius,u.radius*.9,0,u.radius*.76);ctx.quadraticCurveTo(-u.radius,u.radius*.9,-u.radius,0);ctx.fill();ctx.strokeStyle=selected.includes(u)?'#fff36e':'#34294d';ctx.lineWidth=selected.includes(u)?5:2.5;ctx.stroke();ctx.fillStyle='#302641';ctx.beginPath();ctx.arc(-u.radius*.32,-1,2.7,0,7);ctx.arc(u.radius*.32,-1,2.7,0,7);ctx.fill();ctx.font='bold 14px Arial';ctx.textAlign='center';ctx.fillText(u.mark,0,8);ctx.restore();if(!ghost){ctx.fillStyle='#17203c';ctx.fillRect(u.x-u.radius,u.y+u.radius+7,u.radius*2,4);ctx.fillStyle='#6ef0ad';ctx.fillRect(u.x-u.radius,u.y+u.radius+7,u.radius*2*Math.max(0,u.hp/u.maxHp),4)}}
  function drawEnemy(e){
    const pulse=Math.sin(e.wriggle),sx=1+pulse*.09,sy=1-pulse*.08;ctx.save();ctx.translate(e.drawX,e.drawY);ctx.rotate(e.angle*.08);ctx.fillStyle='#0005';ctx.beginPath();ctx.ellipse(3,e.radius*.88,e.radius*1.12,e.radius*.38,0,0,Math.PI*2);ctx.fill();ctx.scale(sx,sy);ctx.fillStyle=e.boss?'#7c2d98':'#b45ee0';ctx.beginPath();ctx.moveTo(-e.radius,1);ctx.quadraticCurveTo(-e.radius*.9,-e.radius,e.radius*.05,-e.radius);ctx.quadraticCurveTo(e.radius,-e.radius*.8,e.radius,2);ctx.quadraticCurveTo(e.radius*.8,e.radius,0,e.radius*.74);ctx.quadraticCurveTo(-e.radius*.8,e.radius,-e.radius,1);ctx.fill();ctx.fillStyle='#3c1750';ctx.beginPath();ctx.arc(-5,-1,2.5,0,7);ctx.arc(5,-1,2.5,0,7);ctx.fill();ctx.restore();ctx.fillStyle='#251331';ctx.fillRect(e.drawX-e.radius,e.drawY+e.radius+6,e.radius*2,4);ctx.fillStyle='#ff725f';ctx.fillRect(e.drawX-e.radius,e.drawY+e.radius+6,e.radius*2*Math.max(0,e.hp/e.maxHp),4);
  }
  function draw(){
    const now=performance.now();ctx.clearRect(0,0,W,H);
    effects.forEach(f=>{ctx.save();ctx.globalAlpha=Math.max(0,f.life/f.max);ctx.strokeStyle=f.color;ctx.shadowColor=f.color;ctx.shadowBlur=14;ctx.lineWidth=f.width;ctx.beginPath();ctx.arc(f.x,f.y,f.radius*(1-f.life/f.max),0,Math.PI*2);ctx.stroke();ctx.restore()});
    projectiles.forEach(p=>{ctx.save();ctx.strokeStyle=p.color;ctx.shadowColor=p.color;ctx.shadowBlur=18;ctx.lineWidth=5;ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x-p.vx*34,p.y-p.vy*34);ctx.stroke();ctx.fillStyle='#fff';ctx.beginPath();ctx.arc(p.x,p.y,5,0,7);ctx.fill();ctx.restore()});
    enemies.forEach(drawEnemy);units.forEach(u=>drawUnit(u,now));
    if(buyType&&hoverPointer){const t=TYPES[buyType],spot=nearestGround(hoverPointer),ok=validPlacement(spot,t.radius);ctx.save();ctx.globalAlpha=.58;ctx.fillStyle=ok?t.color:'#ff554d';ctx.beginPath();ctx.arc(spot.x,spot.y,t.radius,0,7);ctx.fill();ctx.strokeStyle=ok?'#fff36e':'#ff554d';ctx.lineWidth=4;ctx.stroke();ctx.restore()}
    if(pointer?.kind==='empty'&&pointer.moved){const p=pointer.current;ctx.fillStyle='#ffe36b22';ctx.strokeStyle='#ffe36b';ctx.setLineDash([8,5]);ctx.strokeRect(Math.min(pointer.start.x,p.x),Math.min(pointer.start.y,p.y),Math.abs(p.x-pointer.start.x),Math.abs(p.y-pointer.start.y));ctx.setLineDash([])}
    if(commandPreview&&selected.length){const c={x:selected.reduce((s,u)=>s+u.x,0)/selected.length,y:selected.reduce((s,u)=>s+u.y,0)/selected.length};ctx.strokeStyle='#7dffcf';ctx.lineWidth=3;ctx.setLineDash([8,6]);ctx.beginPath();ctx.moveTo(c.x,c.y);ctx.lineTo(commandPreview.x,commandPreview.y);ctx.stroke();ctx.setLineDash([]);ctx.strokeStyle='#7dffcf';ctx.beginPath();ctx.arc(commandPreview.x,commandPreview.y,22,0,7);ctx.stroke()}
    const gateRatio=Math.max(0,gateHp/GATE_MAX_HP),barX=78,barY=224,barW=230,barH=14;ctx.save();ctx.fillStyle='#111a36dd';ctx.beginPath();ctx.roundRect(barX-7,barY-24,barW+14,44,10);ctx.fill();ctx.fillStyle='#fff4c6';ctx.font='bold 12px Malgun Gothic, sans-serif';ctx.textAlign='left';ctx.fillText(`성문 내구도 ${Math.ceil(gateHp)} / ${GATE_MAX_HP}`,barX,barY-7);ctx.fillStyle='#301d2c';ctx.fillRect(barX,barY,barW,barH);ctx.fillStyle=gateRatio>.5?'#68dfb5':gateRatio>.2?'#ffd765':'#ff6b5e';ctx.fillRect(barX,barY,barW*gateRatio,barH);ctx.strokeStyle=gateFlash>0?'#ffffff':'#442d42';ctx.lineWidth=3;ctx.strokeRect(barX,barY,barW,barH);ctx.restore();if(gateFlash>0)gateFlash--;
  }

  function beginRound(){if(state!=='prep')return;if(!navReady){toast('언덕 경로를 확인하는 중입니다.');return}if(!units.length){toast('최소 한 명은 배치해야 합니다.');return}cancelBuild();state='battle';selected=[];spawnLeft=5+round*2+(round%5===0?2:0);spawnClock=0;spawnEnemy(0);spawnLeft--;syncUI();toast(round%5===0?'보스가 오르막으로 진입합니다!':'적 무리가 굽은 오르막을 올라옵니다.')}
  function defeat(){if(state==='lost')return;state='lost';gateSafe=false;gateHp=0;spawnLeft=0;syncUI();document.querySelector('#loseOverlay').classList.add('show')}
  function finishRound(){state='augment';units=[];selected=[];enemies=[];projectiles=[];effects=[];money=0;gateHp=Math.min(GATE_MAX_HP,gateHp+60);syncUI();showAugments()}
  function rollTier(boss){const weights={...transitions[lastTier]};if(boss){['골드','플레','다이아'].forEach(t=>{if(weights[t])weights[t]*={골드:1.25,플레:1.65,다이아:2.2}[t]})}const sum=Object.values(weights).reduce((a,b)=>a+b,0),roll=Math.random()*sum;let n=0;for(const[tier,w]of Object.entries(weights)){n+=w;if(roll<=n)return tier}return'실버'}
  function showAugments(){
    const boss=round%5===0,overlay=document.querySelector('#augmentOverlay'),grid=document.querySelector('#augmentGrid');document.querySelector('#augmentTitle').textContent=boss?'보스 격파!':'라운드 클리어!';document.querySelector('#augmentOdds').textContent=(boss?'보스 보상으로 상위 등급 확률이 증가했습니다. ':'')+'이전 증강 등급: '+lastTier;grid.innerHTML='';const used=[];
    for(let i=0;i<3;i++){let a;do{a=AUGMENTS[Math.floor(Math.random()*AUGMENTS.length)]}while(used.includes(a.name));used.push(a.name);const tier=rollTier(boss),power=tierPower(tier),b=document.createElement('button');b.className='aug '+tierClass[tier];b.innerHTML=`<b>${tier}</b><i>${a.icon}</i><strong>${a.name}</strong><small>${a.desc}<br>성능 계수 ×${power.toFixed(2)} · 현재 ${stackCount(a.name)}중첩</small>`;b.onclick=()=>{const old=stacks[a.name]||{count:0,power:0};stacks[a.name]={count:old.count+1,power:old.power+power};lastTier=tier;round++;money=startBudget();state='prep';gateSafe=gateHp>0;overlay.classList.remove('show');syncUI();toast(`라운드 ${round} · 성문 수리 +60 · 입구를 다시 막으세요.`)};grid.appendChild(b)}overlay.classList.add('show');
  }

  function frame(now){
    const dt=Math.min(34,now-lastTime);lastTime=now;updateUnits(dt);
    if(state==='battle'){if(spawnLeft>0){spawnClock+=dt;if(spawnClock>=520){spawnClock=0;spawnEnemy((5+round*2+(round%5===0?2:0))-spawnLeft);spawnLeft--}}updateEnemies(dt,now);attack(now);updateProjectiles(dt);cleanup();if(state==='battle'&&spawnLeft===0&&enemies.length===0)finishRound()}
    draw();requestAnimationFrame(frame);
  }

  document.querySelector('#startButton').onclick=beginRound;document.querySelector('#retryButton').onclick=()=>location.reload();document.querySelector('#cancelBuild').onclick=cancelBuild;document.querySelector('#stopButton').onclick=stopSelected;
  window.__JELLY_GAME__={getSnapshot:()=>({state,round,money,buyType,selected:selected.length,gateHp,gateMaxHp:GATE_MAX_HP,units:units.map(u=>({type:u.type,x:u.x,y:u.y,targetX:u.targetX,targetY:u.targetY,moving:u.moving})),enemies:enemies.map(e=>({x:e.x,y:e.y,node:e.node,gateDamage:e.gateDamage})),navSource,routeLength:route.length})};
  renderShop();money=startBudget();syncUI();if(mapImage.complete&&mapImage.naturalWidth)buildNavigation();else{mapImage.addEventListener('load',buildNavigation,{once:true});mapImage.addEventListener('error',()=>useFallback('map-image-load-failed'),{once:true})}requestAnimationFrame(frame);
})();
