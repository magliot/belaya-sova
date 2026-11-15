import { db } from 'sdk';
import { eq } from 'sdk/db';
import { players } from '../schema.js';
import { top, rankOf, weekId, rolloverWeek } from '../lib/game.js';

// Таблица рекордов: period = 'week' | 'all'. Плюс место текущего игрока.
export default async function (input, ctx) {
  await rolloverWeek();
  const period = input && input.period === 'all' ? 'all' : 'week';
  const rows = await top(period, 20);
  const user = ctx.initData && ctx.initData.user;
  let me = null;
  if (user) {
    const p = await db.select().from(players).where(eq(players.id, user.id)).get();
    if (p) {
      const s = period === 'week' ? (p.week === weekId() ? p.weekBest : 0) : p.best;
      me = { id: p.id, score: s, rank: s ? await rankOf(period, s) : null };
    }
  }
  return {
    period, week: weekId(), me,
    rows: rows.map((p) => ({
      id: p.id, name: p.name,
      score: period === 'week' ? p.weekBest : p.best,
      dist: period === 'week' ? p.weekDist : p.bestDist,
      hero: period === 'week' ? p.weekHero : p.bestHero,
    })),
  };
}
