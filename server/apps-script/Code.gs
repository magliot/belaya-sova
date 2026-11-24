/**
 * Белая Сова · Спуск — сервер рекордов на Google Apps Script.
 *
 * Что делает:
 *  - принимает результат заезда из игры (Telegram Mini App) и проверяет подпись Telegram (initData),
 *    поэтому имя и ID игрока подделать нельзя;
 *  - проверяет правдоподобие цифр, ведёт рекорды за неделю и за всё время на листе «Игроки»;
 *  - отдаёт таблицу рекордов в игру;
 *  - бот: /start, /top, /alltime, /channel; канал рекордов подключается, когда бота делают админом канала;
 *  - постит в канал нового лидера недели, рекорд игры и итоги прошлой недели.
 *
 * Токен бота хранится в «Свойствах скрипта» (BOT_TOKEN) — его вписывает владелец, в коде его нет.
 */

const GAME_LINK = 'https://t.me/belaya_sovabot/play';
const HERO = { bear: 'Мишаня', owl: 'Сова' };
const COLS = ['id', 'name', 'username', 'best', 'best_dist', 'best_hero', 'week', 'week_best', 'week_dist', 'week_hero', 'runs', 'last_submit', 'updated','recent_runs'];
const MAX_INITDATA_AGE = 7 * 24 * 3600; // сек: игру могут не закрывать долго

// ---------- вход ----------
function doGet() {
  return out({ ok: true, service: 'belaya-sova', apiRevision:'24c-polling', time: new Date().toISOString() });
}

function doPost(e) {
  const raw=e&&e.postData&&e.postData.contents;
  if(typeof raw!=='string'||!raw.length||raw.length>32768)return out({ok:false,error:'bad request'});
  let body;
  try { body=JSON.parse(raw); } catch (err) { return out({ ok:false,error:'bad json' }); }
  if(!body||typeof body!=='object'||Array.isArray(body))return out({ok:false,error:'bad request'});
  try {
    // Bot events come exclusively from getUpdates in the time-based trigger.
    if(body.update_id!==undefined)return out({ok:false,error:'unsupported action'});
    if(body.action==='leaderboard'&&!body.initData)return out(leaderboard(null,body.period));
    const user = verifyInitData(body.initData);
    if (!user) return out({ ok: false, error: 'unauthorized' });
    if (body.action === 'submit') return out(submitScore(user, body));
    if (body.action === 'leaderboard') return out(leaderboard(user, body.period));
    return out({ ok: false, error: 'unknown action' });
  } catch (err) {
    console.error(err && err.stack || err);
    if (!(err && err.userMessage)) logSheet('ошибка: ' + (err && err.message || err));
    return out({ ok: false, error: err && err.userMessage ? err.userMessage : 'server error' });
  }
}

function out(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}
function userError(msg) { const e = new Error(msg); e.userMessage = msg; return e; }

// ---------- проверка подписи Telegram (https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app) ----------
function token() {
  // имя свойства ищем без учёта пробелов и регистра («BOT_TOKEN », «bot_token»)
  const all = PropertiesService.getScriptProperties().getProperties();
  const key = Object.keys(all).filter(function (k) { return k.replace(/\s/g, '').toUpperCase() === 'BOT_TOKEN'; })[0];
  const raw = key ? all[key] : '';
  // убираем случайные пробелы, переносы строк и кавычки при копировании
  const t = String(raw || '').replace(/[\s"'«»]/g, '');
  if (!t) throw new Error('BOT_TOKEN не задан в свойствах скрипта');
  return t;
}
function hex(bytes) { return bytes.map(function (b) { return ('0' + (b & 255).toString(16)).slice(-2); }).join(''); }

function verifyInitData(initData) {
  if(typeof initData!=='string'||!initData.length||initData.length>16384)return null;
  const params=Object.create(null);
  try{
    for(const field of initData.split('&')){
      const cut=field.indexOf('=');if(cut<=0)return null;
      const key=decodeURIComponent(field.slice(0,cut));
      if(Object.prototype.hasOwnProperty.call(params,key))return null;
      params[key]=decodeURIComponent(field.slice(cut+1).replace(/\+/g,'%20'));
    }
  }catch(err){return null;}
  const hash = params.hash;
  if(!/^[a-f0-9]{64}$/.test(hash||'')||!/^\d{1,12}$/.test(params.auth_date||''))return null;
  const check = Object.keys(params).filter(function (k) { return k !== 'hash'; }).sort()
    .map(function (k) { return k + '=' + params[k]; }).join('\n');
  const secret = Utilities.computeHmacSha256Signature(token(), 'WebAppData');
  const sig = Utilities.computeHmacSha256Signature(Utilities.newBlob(check).getBytes(), secret);
  const expected=hex(sig);let different=0;
  for(let i=0;i<64;i++)different|=hash.charCodeAt(i)^expected.charCodeAt(i);
  if(different)return null;
  const age=Date.now()/1000-Number(params.auth_date);
  if(age< -60||age>=MAX_INITDATA_AGE)return null;
  try{
    const u=JSON.parse(params.user);
    return u&&Number.isSafeInteger(u.id)&&u.id>0?u:null;
  }catch(err){return null;}
}

// ---------- таблица ----------
function sheet(name, header) {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  let sh = ss.getSheetByName(name);
  if (!sh) { sh = ss.insertSheet(name); sh.appendRow(header); sh.setFrozenRows(1); sh.getRange(1, 1, 1, header.length).setFontWeight('bold'); }
  return sh;
}
function playersSheet() { return sheet('Игроки', COLS); }
function readPlayers() {
  const sh = playersSheet(), n = sh.getLastRow() - 1;
  if (n <= 0) return [];
  return sh.getRange(2, 1, n, COLS.length).getValues().map(function (r, i) {
    const o = { _row: i + 2 }; COLS.forEach(function (c, j) { o[c] = r[j]; }); return o;
  });
}
function sheetText(value){
  const text=String(value||'');
  return /^\s*=/.test(text)?"'"+text:text;
}
function writePlayer(p) {
  const sh = playersSheet(), row = COLS.map(function (c) {
    if(c==='name'||c==='username')return sheetText(p[c]);
    return p[c] === undefined ? '' : p[c];
  });
  if(!sh.getRange(1,COLS.length).getValue())sh.getRange(1,COLS.length).setValue('recent_runs');
  if (p._row) sh.getRange(p._row, 1, 1, COLS.length).setValues([row]); else sh.appendRow(row);
}
function getSetting(k) { return PropertiesService.getScriptProperties().getProperty('S_' + k); }
function setSetting(k, v) { PropertiesService.getScriptProperties().setProperty('S_' + k, String(v)); }
function configuredOwner(){
  const value=String(PropertiesService.getScriptProperties().getProperty('BOT_OWNER_ID')||'').trim();
  return /^[1-9]\d{0,15}$/.test(value)?value:'';
}

// ---------- игра ----------
function weekId(ms) {
  const d = new Date((ms || Date.now()) + 3 * 3600 * 1000); // Москва
  const day = (d.getUTCDay() + 6) % 7;
  d.setUTCDate(d.getUTCDate() - day + 3);
  const y = d.getUTCFullYear(), jan4 = new Date(Date.UTC(y, 0, 4));
  const w = 1 + Math.round(((d - jan4) / 86400000 - 3 + ((jan4.getUTCDay() + 6) % 7)) / 7);
  return y + '-W' + ('0' + w).slice(-2);
}
function playerName(u) {
  if (u.username) return '@' + u.username;
  return [u.first_name, u.last_name].filter(Boolean).join(' ') || ('Игрок ' + u.id);
}
function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }

function archivedReceipt(key) {
  const receipts=SpreadsheetApp.getActiveSpreadsheet().getSheetByName('Квитанции заездов');
  if(!receipts||receipts.getLastRow()<2)return false;
  return !!receipts.getRange(2,1,receipts.getLastRow()-1,1).createTextFinder(key).matchEntireCell(true).matchCase(true).findNext();
}
function submitScore(user, b) {
  if(b.scoring!==2)throw userError('Обнови игру для нового общего зачёта');
  const {score,dist,dur,hero}=b;
  const fields=['score','dist','coins','gates','rings','preciseGates','preciseRings','rules'];
  if(typeof b.runId!=='string'||!/^[a-zA-Z0-9.-]{8,80}$/.test(b.runId)||!fields.every(k=>Number.isSafeInteger(b[k])&&b[k]>=0)
    ||!Number.isFinite(dur)||dur<=0||!['bear','owl'].includes(hero)
    ||b.preciseGates>b.gates||b.preciseRings>b.rings||b.rules>10||b.coins>dist+10||b.gates+b.rings>dist/2+3
    ||score!==dist+3*b.coins+8*b.gates+10*b.preciseGates+14*b.rings+14*b.preciseRings+25*b.rules)throw userError('Неверная разбивка очков');
  if (dist > dur*65+20) throw userError('Слишком быстро');

  const lock = LockService.getScriptLock(); lock.waitLock(20000);
  try {
    rolloverWeek();
    const now = Date.now(), wk = weekId(now), all = readPlayers();
    let p = all.filter(function (x) { return String(x.id) === String(user.id); })[0];
    const recent=p&&p.recent_runs?String(p.recent_runs).split(','):[];
    if(recent.indexOf(b.runId)>=0||(recent.length>=32&&archivedReceipt(user.id+':'+b.runId))){
      return {ok:true,rank:(leaderboard(user,'all').me||{}).rank,weekRank:(leaderboard(user,'week').me||{}).rank,best:p.best};
    }
    const prevBest = p ? Number(p.best) || 0 : 0;
    const prevWeek = p && p.week === wk ? Number(p.week_best) || 0 : 0;
    const newBest = score > prevBest, newWeek = score > prevWeek;
    const leaderBefore = Number(getSetting('leader_' + wk) || 0);
    const np = {
      _row: p ? p._row : 0, id: user.id, name: playerName(user), username: user.username || '',
      best: Math.max(prevBest, score),
      best_dist: newBest ? dist : (p ? p.best_dist : 0), best_hero: newBest ? hero : (p ? p.best_hero : ''),
      week: wk, week_best: Math.max(prevWeek, score),
      week_dist: newWeek ? dist : (p && p.week === wk ? p.week_dist : 0), week_hero: newWeek ? hero : (p && p.week === wk ? p.week_hero : ''),
      runs: (p ? Number(p.runs) || 0 : 0) + 1, last_submit: now, updated: new Date(now),
      recent_runs:recent.concat([b.runId]).slice(-32).join(','),
    };
    // Archive before evicting the oldest in-row receipt. A failed sheet write can be retried under the same lock.
    if(recent.length>=32){
      const key=user.id+':'+recent[0];
      if(!archivedReceipt(key))sheet('Квитанции заездов',['player:run']).appendRow([key]);
    }
    writePlayer(np);
    if (p) Object.assign(p, np); else all.push(np);
    const rank = 1 + all.filter(function (x) { return Number(x.best) > np.best; }).length;
    const weekRank = 1 + all.filter(function (x) { return x.week === wk && Number(x.week_best) > np.week_best; }).length;

    if (newWeek && weekRank === 1 && score > leaderBefore) {
      setSetting('leader_' + wk, score);
      const allTime = Number(getSetting('record') || 0);
      if (score > allTime) setSetting('record', score);
      const head = score > allTime ? '🏆 <b>НОВЫЙ РЕКОРД ИГРЫ!</b>' : '⛷ <b>Новый лидер недели!</b>';
      postToChannel(head + '\n\n<b>' + esc(np.name) + '</b> — ' + score + ' очков\n' + dist + ' м · ' + HERO[hero] + '\n\nСможешь обогнать?');
    }
    return { ok: true, best: np.best, weekBest: np.week_best, rank: rank, weekRank: weekRank, newBest: newBest, newWeek: newWeek, name: np.name };
  } finally { lock.releaseLock(); }
}

function topRows(period, limit) {
  const wk = weekId(), all = readPlayers();
  const rows = period === 'week'
    ? all.filter(function (x) { return x.week === wk && Number(x.week_best) > 0; }).sort(function (a, b) { return b.week_best - a.week_best; })
    : all.filter(function (x) { return Number(x.best) > 0; }).sort(function (a, b) { return b.best - a.best; });
  return { all: all, rows: rows, top: rows.slice(0, limit || 20) };
}

function leaderboard(user, period) {
  period = period === 'all' ? 'all' : 'week';
  const t = topRows(period, 20), wk = weekId();
  const pick = function (p) {
    return period === 'week' ? { s: Number(p.week_best), d: p.week_dist, h: p.week_hero } : { s: Number(p.best), d: p.best_dist, h: p.best_hero };
  };
  let me = null;
  const mine = user && t.rows.filter(function (x) { return String(x.id) === String(user.id); })[0];
  if (mine) { const v = pick(mine); me = { id: mine.id, score: v.s, rank: 1 + t.rows.filter(function (x) { return pick(x).s > v.s; }).length }; }
  return {
    ok: true, period: period, week: wk, me: me,
    rows: t.top.map(function (p) { const v = pick(p); return { name: p.name, score: v.s, dist: Number(v.d) || 0, hero: v.h }; }),
  };
}

// Смена недели: итоги прошлой недели в канал (один раз)
function rolloverWeek() {
  const wk = weekId(), prev = getSetting('week');
  if (prev === wk) return;
  setSetting('week', wk);
  if (!prev) return;
  const rows = readPlayers().filter(function (x) { return x.week === prev && Number(x.week_best) > 0; })
    .sort(function (a, b) { return b.week_best - a.week_best; }).slice(0, 3);
  if (!rows.length) return;
  const medal = ['🥇', '🥈', '🥉'];
  postToChannel('🏁 <b>Итоги недели ' + prev.split('-W')[1] + '</b>\n\n' + rows.map(function (p, i) {
    return medal[i] + ' <b>' + esc(p.name) + '</b> — ' + p.week_best + ' очков (' + p.week_dist + ' м, ' + (HERO[p.week_hero] || '') + ')';
  }).join('\n') + '\n\nНовая неделя началась — рекорды недели обнулены. Кто будет первым?');
}

// ---------- Telegram Bot API ----------
// убрать токен бота из любого текста (ошибки UrlFetch содержат полный адрес запроса)
function redact(s) { return String(s).replace(/bot\d+:[A-Za-z0-9_-]+/g, 'bot***').replace(/\d{6,}:[A-Za-z0-9_-]{20,}/g, '***'); }
function tg(method, params) {
  let r;
  try {
    r = UrlFetchApp.fetch('https://api.telegram.org/bot' + token() + '/' + method, {
      method: 'post', contentType: 'application/json', payload: JSON.stringify(params), muteHttpExceptions: true,
    });
  } catch (e) { throw new Error(redact(e && e.message || e)); }
  const j = JSON.parse(r.getContentText() || '{}');
  if (!j.ok) { console.warn(method + ' failed: ' + r.getContentText()); logSheet(method + ': ' + (j.description || r.getResponseCode())); }
  return j;
}
// Журнал ошибок на отдельном листе таблицы (последние ~200 строк), без секретов
function logSheet(msg) {
  try {
    const sh = sheet('Журнал', ['время', 'событие']);
    sh.appendRow([new Date(), redact(msg).slice(0, 500)]);
    if (sh.getLastRow() > 220) sh.deleteRows(2, sh.getLastRow() - 200);
  } catch (e) { console.warn('log failed', e); }
}
// ---------- бот без вебхука: Apps Script отвечает на POST перенаправлением 302, Telegram считает это ошибкой
// и копит очередь. Поэтому сообщения бота забираем сами раз в минуту (триггер по времени).
function pollUpdates() {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(5000)) return;
  try {
    let offset = Number(getSetting('offset') || 0);
    const r = tg('getUpdates', { offset: offset, timeout: 0, limit: 50, allowed_updates: ['message', 'my_chat_member'] });
    if (!r.ok || !r.result) return;
    r.result.forEach(function (u) {
      try { handleUpdate(u); } catch (e) { logSheet('update ' + u.update_id + ': ' + (e && e.message || e)); }
      offset = u.update_id + 1;
      setSetting('offset', offset);
    });
  } finally { lock.releaseLock(); }
}

/** Запустить один раз: отключает вебхук и включает опрос Telegram каждую минуту. */
function setupPolling() {
  const d = tg('deleteWebhook', { drop_pending_updates: false });
  ScriptApp.getProjectTriggers().forEach(function (t) { if (t.getHandlerFunction() === 'pollUpdates') ScriptApp.deleteTrigger(t); });
  ScriptApp.newTrigger('pollUpdates').timeBased().everyMinutes(1).create();
  playersSheet();
  pollUpdates();
  console.log('Вебхук отключён: ' + JSON.stringify(d) + '\nОпрос Telegram включён: раз в минуту (функция pollUpdates).');
}

/** Очистить лист «Журнал» (например, если туда попало что-то лишнее). */
function clearLog() {
  const sh = SpreadsheetApp.getActiveSpreadsheet().getSheetByName('Журнал');
  if (sh && sh.getLastRow() > 1) sh.deleteRows(2, sh.getLastRow() - 1);
  console.log('Журнал очищен');
}

/** Диагностика бота: состояние вебхука у Telegram (без токена). */
function diag() {
  const me = tg('getMe', {}), w = tg('getWebhookInfo', {});
  const r = w.result || {};
  console.log('Бот: ' + (me.ok ? '@' + me.result.username : 'нет доступа') +
    '\nВебхук: ' + (r.url === WEBAPP_URL ? 'на этот скрипт' : (r.url ? 'ДРУГОЙ адрес' : 'не установлен (работает опрос)')) +
    '\nТриггеры опроса: ' + ScriptApp.getProjectTriggers().filter(function (t) { return t.getHandlerFunction() === 'pollUpdates'; }).length +
    '\nОжидают доставки: ' + r.pending_update_count +
    '\nПоследняя ошибка: ' + (r.last_error_message || 'нет') + (r.last_error_date ? ' (' + new Date(r.last_error_date * 1000) + ')' : ''));
}
const PLAY = { inline_keyboard: [[{ text: '⛷ Играть', url: GAME_LINK }]] };
function postToChannel(text) {
  const owner=configuredOwner(),ch=getSetting('channel');
  if(!owner||owner!==getSetting('owner')||!ch)return;
  tg('sendMessage', { chat_id: Number(ch), text: text, parse_mode: 'HTML', link_preview_options: { is_disabled: true }, reply_markup: PLAY });
}

function topText(period) {
  const t = topRows(period, 10).top;
  if (!t.length) return period === 'week' ? 'На этой неделе ещё никто не катался — будь первым!' : 'Рекордов пока нет — будь первым!';
  const medal = ['🥇', '🥈', '🥉'];
  return t.map(function (p, i) {
    const s = period === 'week' ? p.week_best : p.best, d = period === 'week' ? p.week_dist : p.best_dist, h = period === 'week' ? p.week_hero : p.best_hero;
    return (medal[i] || (i + 1) + '.') + ' <b>' + esc(p.name) + '</b> — ' + s + ' (' + d + ' м, ' + (HERO[h] || '') + ')';
  }).join('\n');
}

function handleUpdate(u) {
  const m = u.message;
  if (m && m.chat && m.chat.type === 'private' && m.text) {
    const cmd = m.text.trim().split(/[\s@]/)[0].toLowerCase();
    const reply = function (text) { tg('sendMessage', { chat_id: m.chat.id, text: text, parse_mode: 'HTML', link_preview_options: { is_disabled: true }, reply_markup: PLAY }); };
    if (cmd === '/top' || cmd === '/week') return reply('🏆 <b>Рекорды недели</b>\n\n' + topText('week'));
    if (cmd === '/alltime') return reply('🏆 <b>Рекорды за всё время</b>\n\n' + topText('all'));
    if (cmd === '/channel') {
      const owner = configuredOwner(), ch = getSetting('channel');
      if (owner && String(m.from.id) === owner) return reply(ch ? 'Рекорды отправляются в канал (id ' + ch + ').' : 'Канал не подключён.');
      return reply('Чтобы рекорды публиковались в канале, добавьте этого бота администратором канала (с правом публиковать сообщения).');
    }
    return reply('⛷ <b>Белая Сова · Спуск</b>\n\nГорнолыжный склон Шахтерска: катайся за Мишаню или Сову, проходи ворота, делай трюки и собирай 10 правил FIS.\n\n/top — рекорды недели\n/alltime — рекорды за всё время');
  }
  const cm = u.my_chat_member;
  if (cm && cm.chat && cm.chat.type === 'channel' && cm.from) {
    const owner=configuredOwner(),st=cm.new_chat_member&&cm.new_chat_member.status;
    if(!owner||String(cm.from.id)!==owner)return;
    if (st === 'administrator') {
      setSetting('channel', cm.chat.id);
      setSetting('owner',owner);
      tg('sendMessage', { chat_id: cm.chat.id, parse_mode: 'HTML', reply_markup: PLAY,
        text: '⛷ <b>Белая Сова · Спуск</b>\n\nСюда будут приходить новые лидеры недели, рекорды игры и итоги каждой недели.' });
    } else if (String(getSetting('channel')) === String(cm.chat.id)) {
      setSetting('channel', '');
    }
  }
}

// ---------- запускается вручную из редактора один раз ----------
/** После «Развертывания» как веб-приложения: подключает бота к скрипту (вебхук). */
// рабочий адрес веб-приложения (Развертывание → Управление развертываниями → URL, заканчивается на /exec)
const WEBAPP_URL = 'https://script.google.com/macros/s/AKfycbxlD0YW9fFAhinQsr-02Vc9q-9yOeSoD6yPyqhnQ9tsQvtBzepwTKftBhqW-54bHxvT/exec';

function setupWebhook() {
  throw new Error('Для этого бота используется setupPolling в редакторе Apps Script.');
}
