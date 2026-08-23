(function(){
  var B=window.__REHBER_BASE||'/rehber/';
  var inp=document.getElementById('arama'),box=document.getElementById('arama-sonuc'),idx=null;
  function yukle(){ if(idx) return Promise.resolve(idx); return fetch(B+'arama.json').then(function(r){return r.json();}).then(function(d){idx=d;return d;}); }
  function norm(s){ return (s||'').toLocaleLowerCase('tr-TR'); }
  function ara(q){ q=norm(q.trim()); if(q.length<2){box.hidden=true;box.innerHTML='';return;}
    yukle().then(function(d){
      var sonuc=[]; d.forEach(function(p){ var puan=0; if(norm(p.t).indexOf(q)>=0)puan+=10; if(norm(p.s).indexOf(q)>=0)puan+=4;
        var hb=(p.h||[]).filter(function(h){return norm(h).indexOf(q)>=0;}); puan+=hb.length*3; if(norm(p.x).indexOf(q)>=0)puan+=1;
        if(puan>0) sonuc.push({p:p,puan:puan,hb:hb}); });
      sonuc.sort(function(a,b){return b.puan-a.puan;}); sonuc=sonuc.slice(0,12);
      box.innerHTML=sonuc.length?sonuc.map(function(s){return '<a href="'+s.p.u+'"><b>'+s.p.t+'</b><small>'+s.p.g+(s.hb.length?' · '+s.hb.slice(0,2).join(', '):'')+'</small></a>';}).join(''):'<div class="bos">Sonuç yok</div>';
      box.hidden=false; });
  }
  if(inp){ inp.addEventListener('input',function(){ara(inp.value);}); inp.addEventListener('focus',function(){ if(inp.value) ara(inp.value); });
    document.addEventListener('click',function(e){ if(!box.contains(e.target)&&e.target!==inp) box.hidden=true; }); }
  var lb=document.getElementById('lightbox'); if(lb){ var im=lb.querySelector('img'), alt=lb.querySelector('.lb-alt');
    document.addEventListener('click',function(e){ var a=e.target.closest&&e.target.closest('a.buyut'); if(!a) return; e.preventDefault(); im.src=a.getAttribute('href'); alt.textContent=a.dataset.alt||''; lb.hidden=false; });
    lb.addEventListener('click',function(){lb.hidden=true;im.src='';}); document.addEventListener('keydown',function(e){ if(e.key==='Escape'){lb.hidden=true;} }); }
  var aktif=document.querySelector('aside nav a.active'); if(aktif) aktif.scrollIntoView({block:'center'});
})();
