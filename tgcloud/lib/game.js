import { api, db } from 'sdk';
import { eq, and, desc, sql } from 'sdk/db';
import { players, settings } from '../schema.js';

export const GAME_LINK = 'https://t.me/belaya_sovabot/play';
export const HERO = { bear: 'Мишаня', owl: 'Сова' };

// Неделя по московскому времени (UTC+3), ISO-номер: '2026-W46'
export function weekId(ms = Date.now()) {
  const d = new Date(ms + 3 * 3600 * 1000);
  const day = (d.getUTCDay() + 6) % 7;                // пн = 0
  d.setUTCDate(d.getUTCDate() - day + 3);             // четверг этой недели
  const y = d.getUTCFullYear();
  const jan4 = new Date(Date.UTC(y, 0, 4));
  const w = 1 + Math.round(((d - jan4) / 86400000 - 3 + ((jan4.getUTCDay() + 6) % 7)) / 7);
  return y + '-W' + String(w).padStart(2, '0');
}

export function playerName(u) {
  if (u.username) return '@' + u.username;
  return [u.first_name, u.last_name].filter(Boolean).join(' ') || ('Игрок ' + u.id);
}

export async function getSetting(key) {
  const r = await db.select().from(settings).where(eq(settings.key, key)).get();
  return r ? r.value : null;
}
export async function setSetting(key, value) {
  await db.insert(settings).values({ key, value: String(value) })
    .onConflictDoUpdate({ target: settings.key, set: { value: String(value) } }).run();
}

// Отправить сообщение в канал рекордов (если он подключён). Ошибки не ломают игру.
export async function postToChannel(text) {
  const ch = await getSetting('channel');
  if (!ch) return false;
  try {
    await api.sendMessage({
      chat_id: Number(ch), text, parse_mode: 'HTML', link_preview_options: { is_disabled: true },
      reply_markup: { inline_keyboard: [[{ text: '⛷ Играть', url: GAME_LINK }]] },
    });
    return true;
  } catch (e) { console.warn('channel post failed', e && e.description); return false; }
}

export const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

// Топ недели / всех времён
export async function top(period, limit = 20) {
  if (period === 'week') {
    const wk = weekId();
    return db.select().from(players).where(and(eq(players.week, wk), sql`${players.weekBest} > 0`))
      .orderBy(desc(players.weekBest)).limit(limit).all();
  }
  return db.select().from(players).where(sql`${players.best} > 0`).orderBy(desc(players.best)).limit(limit).all();
}

// Место игрока (1 = первый)
export async function rankOf(period, score) {
  if (!score) return null;
  const r = period === 'week'
    ? await db.get(`SELECT COUNT(*) AS n FROM players WHERE week = :w AND week_best > :s`, { ':w': weekId(), ':s': score })
    : await db.get(`SELECT COUNT(*) AS n FROM players WHERE best > :s`, { ':s': score });
  return (r ? Number(r.n) : 0) + 1;
}

// Смена недели: объявляем в канале победителей прошлой недели (один раз)
export async function rolloverWeek() {
  const wk = weekId();
  const prev = await getSetting('week');
  if (prev === wk) return;
  await setSetting('week', wk);
  if (!prev) return;
  const rows = await db.select().from(players).where(and(eq(players.week, prev), sql`${players.weekBest} > 0`))
    .orderBy(desc(players.weekBest)).limit(3).all();
  if (!rows.length) return;
  const medal = ['🥇', '🥈', '🥉'];
  const lines = rows.map((p, i) => `${medal[i]} <b>${esc(p.name)}</b> — ${p.weekBest} очков (${p.weekDist} м, ${HERO[p.weekHero] || ''})`);
  await postToChannel(`🏁 <b>Итоги недели ${prev.split('-W')[1]}</b>\n\n${lines.join('\n')}\n\nНовая неделя началась — рекорды обнулены. Кто будет первым?`);
}
