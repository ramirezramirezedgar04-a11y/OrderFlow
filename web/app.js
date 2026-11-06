const API_BASE='http://localhost:5080';
const $=id=>document.getElementById(id);
const money=n=>new Intl.NumberFormat('es-MX',{style:'currency',currency:'MXN'}).format(n);
const status=s=>({Pending:'Pendiente',Preparing:'En preparación',Ready:'Listo',Delivered:'Entregado',Cancelled:'Cancelado'}[s]||s);
async function get(path){const r=await fetch(API_BASE+path);if(!r.ok)throw Error('HTTP '+r.status);return r.json()}
async function load(){ $('error').textContent='';try{
 const [orders,tables,products]=await Promise.all([get('/api/orders'),get('/api/tables'),get('/api/products')]);
 $('active').textContent=orders.filter(o=>!['Delivered','Cancelled'].includes(o.status)).length;
 $('occupied').textContent=tables.filter(t=>t.status==='Occupied').length;
 $('count').textContent=products.filter(p=>p.isAvailable).length;
 $('ordersList').innerHTML=orders.length?orders.map(o=>`<div class="order"><div><b>Comanda #${o.id} · Mesa ${o.tableNumber}</b><div class="muted">${(o.items||[]).map(i=>i.quantity+' × '+i.productName).join(' · ')}</div></div><div><b>${status(o.status)}</b><div class="muted">${money(o.total)}</div></div></div>`).join(''):'Sin comandas registradas.';
 $('tablesList').innerHTML=tables.map(t=>`<div class="item"><span>Mesa ${t.tableNumber} (${t.capacity} lugares)</span><b>${t.status==='Occupied'?'Ocupada':t.status==='Reserved'?'Reservada':'Disponible'}</b></div>`).join('');
 $('productsList').innerHTML=products.map(p=>`<div class="item"><span>${p.name}</span><b>${money(p.price)}</b></div>`).join('');
 }catch(e){$('error').textContent='No se pudo conectar con la API. Verifica PostgreSQL, el backend y API_BASE. '+e.message;$('ordersList').textContent='Sin conexión.'}}
$('refresh').addEventListener('click',load);load();
