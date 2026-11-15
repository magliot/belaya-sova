import { api } from 'sdk';
import { top, esc, HERO, GAME_LINK, getSetting } from '../lib/game.js';

const PLAY = { inline_keyboard: [[{ text: '⛷ Играть', url: GAME_LINK }]] };

async function topText(period) {
  const rows = await top(period, 10);
  if (!rows.length) return period === 'week' ? 'На этой неделе ещё никто не катался — будь первым!' : 'Рекордов пока нет — будь первым!';
  const medal = ['🥇', '🥈', '🥉'];
  return rows.map((p, i) => {
    const s = period === 'week' ? p.weekBest : p.best, d = period === 'week' ? p.weekDist : p.bestDist, h = period === 'week' ? p.weekHero : p.bestHero;
    return `${medal[i] || (i + 1) + '.'} <b>${esc(p.name)}</b> — ${s} (${d} м, ${HERO[h] || ''})`;
  }).join('\n');
}

export default async function (message) {
  if (!message.chat || message.chat.type !== 'private' || !message.text) return;
  const cmd = message.text.trim().split(/[\s@]/)[0].toLowerCase();
  const reply = (text, extra = {}) => api.sendMessage({ chat_id: message.chat.id, text, parse_mode: 'HTML', link_preview_options: { is_disabled: true }, ...extra });

  if (cmd === '/top' || cmd === '/week') return reply(`🏆 <b>Рекорды недели</b>\n\n${await topText('week')}`, { reply_markup: PLAY });
  if (cmd === '/alltime') return reply(`🏆 <b>Рекорды за всё время</b>\n\n${await topText('all')}`, { reply_markup: PLAY });
  if (cmd === '/channel') {
    const ch = await getSetting('channel'), owner = await getSetting('owner');
    if (owner && String(message.from.id) === owner) return reply(ch ? `Рекорды отправляются в канал (id ${ch}).` : 'Канал не подключён.');
    return reply('Чтобы рекорды публиковались в канале, добавьте этого бота администратором канала (с правом публиковать сообщения).');
  }
  // /start и всё остальное
  return reply(
    '⛷ <b>Белая Сова · Спуск</b>\n\nГорнолыжный склон Шахтерска: катайся за Мишаню или Сову, проходи ворота, делай трюки и собирай 10 правил FIS.\n\n' +
    '/top — рекорды недели\n/alltime — рекорды за всё время',
    { reply_markup: PLAY });
}
