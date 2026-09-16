const API = process.env.API || 'http://localhost:5099/api';
const T = Date.now();
const j = async (url, opt) => {
  const r = await fetch(url, { headers: { 'Content-Type': 'application/json' }, ...opt });
  let b = null; try { b = await r.json(); } catch {}
  return { status: r.status, body: b };
};
const un = (x) => Array.isArray(x) ? x : (x?.$values ?? (x ? [x] : []));
const base = (numeroLote, formId, extra = {}) => ({
  numeroLote, proceso: 'PD-04 Fileteo', producto: 'Swordfish', clasificacion: '',
  pesoEntrada: 0, desperdicio: 0, tipoDesperdicio: '', estado: 'disponible',
  lotePadre: null, formId, templateId: '101', fecha: '2026-08-24', notas: '', ...extra });
const guardarPD04 = (lote, formId, peso) => j(`${API}/LotesInventario/bulk`, { method: 'POST',
  body: JSON.stringify({ lotes: [base(lote, formId, { pesoEntrada: peso })], loteOrigenConsumir: null }) });
const consumir = (numeroLote, cantidad, formId) => j(`${API}/LotesInventario/consumir-cantidad`,
  { method: 'POST', body: JSON.stringify({ numeroLote, cantidad, proceso: 'PD-07 Clasificacion', formId, notas: 'prueba 2 usuarios' }) });
const limpiar = async (pref) => { for (const l of un((await j(`${API}/LotesInventario`)).body).filter(x => String(x.numeroLote||'').startsWith(pref)))
  await j(`${API}/LotesInventario/numero/${encodeURIComponent(l.numeroLote)}`, { method: 'DELETE' }); };

let fallas = 0;
const chequeo = (bien, txt) => { if (!bien) fallas++; console.log(`  ${bien ? 'OK   ' : 'FALLA'} ${txt}`); };

// ── PRUEBA 1 ────────────────────────────────────────────────────────────────
// Dos operarios guardan al MISMO INSTANTE un PD-04 del mismo lote del dia.
const L1 = `TEST-2U-A-${T}`;
console.log(`\nPRUEBA 1 · dos operarios guardan a la vez el PD-04 del lote del dia`);
console.log(`           usuario 1: 400 lb   ·   usuario 2: 600 lb   ·   esperado: un lote de 1000 lb`);
const r1 = await Promise.all([ guardarPD04(L1, 910001, 400), guardarPD04(L1, 910002, 600) ]);
console.log(`           respuestas: ${r1.map(r => `HTTP ${r.status} creados=${r.body?.creados} sumados=${r.body?.sumados}`).join('  |  ')}`);
const filas1 = un((await j(`${API}/LotesInventario`)).body).filter(l => l.numeroLote === L1);
const total1 = filas1.reduce((a, l) => a + Number(l.pesoEntrada), 0);
console.log(`           quedaron ${filas1.length} fila(s) con ese numero, ${total1} lb en total`);
chequeo(filas1.length === 1, `una sola fila para el lote (hubo ${filas1.length})`);
chequeo(total1 === 1000, `el lote suma 1000 lb (tiene ${total1})`);
await limpiar(L1);

// ── PRUEBA 2 ────────────────────────────────────────────────────────────────
// Un lote de 100 lb y dos operarios que piden 80 lb cada uno al mismo instante
// desde los formularios que van DESPUES del PD-04.
const L2 = `TEST-2U-B-${T}`;
console.log(`\nPRUEBA 2 · lote de 100 lb, dos operarios descuentan 80 lb cada uno a la vez`);
console.log(`           esperado: uno pasa, al otro se le niega por saldo insuficiente`);
await guardarPD04(L2, 910003, 100);
const r2 = await Promise.all([ consumir(L2, 80, 910004), consumir(L2, 80, 910005) ]);
console.log(`           respuestas: ${r2.map(r => `HTTP ${r.status}`).join('  |  ')}`);
const lote2 = un((await j(`${API}/LotesInventario`)).body).find(l => l.numeroLote === L2);
const aceptados = r2.filter(r => r.status === 200).length;
console.log(`           saldo que quedo: ${lote2 ? Number(lote2.saldo) : '?'} lb   (aceptados: ${aceptados} de 2)`);
chequeo(aceptados === 1, `solo un descuento aceptado (pasaron ${aceptados})`);
chequeo(lote2 && Number(lote2.saldo) === 20, `el saldo queda en 20 lb (quedo ${lote2 ? lote2.saldo : '?'})`);
chequeo(lote2 && Number(lote2.saldo) >= 0, `el saldo nunca queda en negativo`);
await limpiar(L2);

// ── PRUEBA 3 ────────────────────────────────────────────────────────────────
// El usuario 2 abre el formulario siguiente justo despues de que el usuario 1
// cerro su PD-04: tiene que VER el lote disponible.
const L3 = `TEST-2U-C-${T}`;
console.log(`\nPRUEBA 3 · el usuario 1 cierra el PD-04 y el usuario 2 abre el formulario siguiente`);
await guardarPD04(L3, 910006, 250);
const disp = un((await j(`${API}/LotesInventario/disponibles`)).body).filter(l => l.numeroLote === L3);
console.log(`           el usuario 2 ve el lote en su lista de disponibles: ${disp.length ? 'si, ' + disp[0].saldo + ' lb' : 'NO'}`);
chequeo(disp.length === 1, `el lote aparece al otro usuario sin recargar nada`);
await limpiar(L3);

console.log(fallas === 0 ? '\n==> FUNCIONA CON DOS USUARIOS\n' : `\n==> ${fallas} PROBLEMA(S) CON DOS USUARIOS\n`);
