import { db, EndpointError } from 'sdk';
import { eq } from 'sdk/db';
import { players } from '../schema.js';
import { weekId, playerName, rankOf, rolloverWeek, postToChannel, getSetting, setSetting, esc, HERO } from '../lib/game.js';

// Результат заезда. Кто прислал — знает только платформа: ctx.initData проверен подписью Telegram,
// поддельный запрос сюда не доходит (401). Здесь проверяем, что сами цифры правдоподобны.
const int = (v) => (Number.isFinite(Number(v)) ? Math.floor(Number(v)) : -1);

export default async function (input, ctx) {
  const user = ctx.initData && ctx.initData.user;
  if (!user || !user.id) throw new EndpointError('Нет пользователя Telegram');

  const score = int(input.score), dist = int(input.dist), dur = Number(input.dur) || 0;
  const hero = input.hero === 'owl' ? 'owl' : 'bear';
  // правдоподобие: скорость ≤ 45 м/с, бонус ≥ 0 и не больше разумного предела на метр
  if (score < 0 || dist < 0 || dur <= 0) throw new EndpointError('Неверные данные');
  if (score > 5_000_000 || dist > 200_000) throw new EndpointError('Неверные данные');
  if (dist > dur * 45 + 20) throw new EndpointError('Слишком быстро');
  const bonus = score - dist;
  if (bonus < 0 || bonus > dist * 45 + 3000) throw new EndpointError('Очки не сходятся');

  await rolloverWeek();
  const now = Date.now(), wk = weekId(now), name = playerName(user);
  let p = await db.select().from(players).where(eq(players.id, user.id)).get();
  if (p && now - p.lastSubmit < 4000) throw new EndpointError('Слишком часто');

  const prevBest = p ? p.best : 0;
  const prevWeek = p && p.week === wk ? p.weekBest : 0;
  const newBest = score > prevBest, newWeek = score > prevWeek;

  // лидер недели до этого результата — чтобы понять, сменился ли он
  const leaderBefore = Number(await getSetting('leader:' + wk) || 0);

  const row = {
    id: user.id, name, username: user.username || null,
    best: Math.max(prevBest, score),
    bestDist: newBest ? dist : (p ? p.bestDist : 0),
    bestHero: newBest ? hero : (p ? p.bestHero : null),
    week: wk,
    weekBest: Math.max(prevWeek, score),
    weekDist: newWeek ? dist : (p && p.week === wk ? p.weekDist : 0),
    weekHero: newWeek ? hero : (p && p.week === wk ? p.weekHero : null),
    runs: (p ? p.runs : 0) + 1,
    lastSubmit: now, updated: now,
  };
  await db.insert(players).values(row).onConflictDoUpdate({ target: players.id, set: row }).run();

  const rank = await rankOf('all', row.best), weekRank = await rankOf('week', row.weekBest);

  // в канал: новый лидер недели (обогнал предыдущего) и новый абсолютный рекорд игры
  if (newWeek && weekRank === 1 && score > leaderBefore) {
    await setSetting('leader:' + wk, score);
    const allTime = Number(await getSetting('record') || 0);
    if (score > allTime) await setSetting('record', score);
    const head = score > allTime ? '🏆 <b>НОВЫЙ РЕКОРД ИГРЫ!</b>' : '⛷ <b>Новый лидер недели!</b>';
    await postToChannel(`${head}\n\n<b>${esc(name)}</b> — ${score} очков\n${dist} м · ${HERO[hero]}\n\nСможешь обогнать?`);
  }

  return { best: row.best, weekBest: row.weekBest, rank, weekRank, newBest, newWeek, name };
}
