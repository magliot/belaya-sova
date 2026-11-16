# Telegram-бот, хостинг и сервер рекордов

## Хостинг игры — GitHub Pages
- Репозиторий: https://github.com/magliot/belaya-sova (ветка `main`, публикуется папка `/docs`).
- Адрес игры: https://magliot.github.io/belaya-sova/
- Обновить: правка → смена `VER` в `docs/index.html` и `docs/version.txt` → `git add -A` → `git commit -m "…"` → `git push`. Через 1–2 минуты сайт обновлён, Telegram подтянет сам.
- Push на Windows шёл через Git Credential Manager: `git -c credential.helper=manager push`.

## Бот и мини-приложение
- Бот: **@belaya_sovabot** (создан в @BotFather). Мини-приложение: short name `play` → https://t.me/belaya_sovabot/play (в игре это `TG_LINK`).
- В BotFather: Web App URL = адрес GitHub Pages; Menu Button «Играть» ведёт туда же.
- Токен бота хранится только в Google Apps Script (Свойства скрипта → `BOT_TOKEN`). В репозитории токена нет и быть не должно. Если токен перевыпущен в BotFather — обновить свойство.

## Сервер рекордов — Google Apps Script (работает)
- Код: `server/apps-script/Code.gs`. Живёт как скрипт при Google Таблице «Белая Сова — рекорды» в Google-аккаунте владельца (листы «Игроки» и «Журнал»).
- Развёрнут как «Веб-приложение» (доступ: все), адрес `…/exec` вписан в игру как `API_URL`.
- Что делает:
  - `submit` — проверяет подпись Telegram `initData` (HMAC-SHA256 с ключом от BOT_TOKEN), правдоподобие очков, пишет рекорд, рекорд недели, постит в канал нового лидера;
  - `leaderboard` — топ недели / всех времён;
  - бот: `/start`, `/top`, `/alltime`, `/channel`; канал подключается, если сделать бота админом канала (первый подключивший — владелец).
- Бот получает сообщения **опросом** раз в минуту (`pollUpdates`, триггер ставит `setupPolling`). Вебхук не используется: Apps Script отвечает Telegram 302 и очередь копится.
- Служебные функции в редакторе: `diag` (состояние очереди и последней ошибки, без токена), `clearLog` (очистить «Журнал»), `setupPolling` (пересоздать триггер).
- Обновить код: открыть таблицу → Расширения → Apps Script → в `Code.gs` нажать Ctrl+A, Delete, вставить новый код, Ctrl+S → «Начать развертывание → Управление развертываниями → ✏️ → Версия: Новая версия → Начать развертывание». Адрес `/exec` не меняется.

## Запасной вариант — Telegram Serverless (не запущен)
Код в `tgcloud/` + `tgcloud.jsonc` + `package.json`. На момент работы был лист ожидания. Когда дадут доступ: `npm install` → `npx tgcloud login` (Access token из BotFather → Serverless → CLI Access) → `npx tgcloud push` → `npx tgcloud migrate`, затем в BotFather заменить адрес мини-приложения на выданный `https://app….tgcloud.ai/`.
