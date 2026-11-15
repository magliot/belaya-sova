import { api } from 'sdk';
import { getSetting, setSetting, GAME_LINK } from '../lib/game.js';

// Бота добавили администратором в канал → это канал для рекордов.
// Первый, кто подключил канал, становится владельцем: менять канал потом может только он.
export default async function (upd) {
  const chat = upd.chat, st = upd.new_chat_member && upd.new_chat_member.status;
  if (!chat || chat.type !== 'channel' || !upd.from) return;
  const owner = await getSetting('owner');
  if (owner && String(upd.from.id) !== owner) { console.warn('channel change rejected from', upd.from.id); return; }

  if (st === 'administrator') {
    await setSetting('channel', chat.id);
    if (!owner) await setSetting('owner', upd.from.id);
    try {
      await api.sendMessage({
        chat_id: chat.id, parse_mode: 'HTML',
        text: '⛷ <b>Белая Сова · Спуск</b>\n\nСюда будут приходить новые лидеры недели, рекорды игры и итоги каждой недели.',
        reply_markup: { inline_keyboard: [[{ text: '⛷ Играть', url: GAME_LINK }]] },
      });
    } catch (e) { console.warn('cannot post to channel', e && e.description); }
  } else if ((st === 'left' || st === 'kicked' || st === 'member') && String(await getSetting('channel')) === String(chat.id)) {
    await setSetting('channel', '');
  }
}
