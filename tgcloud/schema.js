import { table, integer, text, index } from 'sdk/db';

// Игроки: id = ID Telegram-аккаунта (проверен платформой). Лучший результат за всё время и за текущую неделю.
export const players = table('players', {
  id:         integer('id').primaryKey(),
  name:       text('name').notNull(),
  username:   text('username'),
  best:       integer('best').notNull().default(0),
  bestDist:   integer('best_dist').notNull().default(0),
  bestHero:   text('best_hero'),
  week:       text('week'),
  weekBest:   integer('week_best').notNull().default(0),
  weekDist:   integer('week_dist').notNull().default(0),
  weekHero:   text('week_hero'),
  runs:       integer('runs').notNull().default(0),
  lastSubmit: integer('last_submit').notNull().default(0),
  updated:    integer('updated').notNull().default(0),
}, (t) => ({
  bestIdx: index('idx_players_best').on(t.best),
  weekIdx: index('idx_players_week').on(t.week, t.weekBest),
}));

// Настройки бота: канал для рекордов, его владелец, текущая неделя, рекорды для сравнения.
export const settings = table('settings', {
  key:   text('key').primaryKey(),
  value: text('value'),
});
