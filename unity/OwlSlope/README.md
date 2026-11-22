# Белая Сова в Unity 6.3 LTS и URP

Исходники полного спуска находятся в этой папке. Откройте её в Unity Hub с редактором **6000.3.27f1** и модулем Web Build Support. Игровая сцена — `Assets/Mountain.unity`. Сцену `Assets/Slope.unity` сохранили как исходник раннего 94-метрового пилота.

## Сборка и запуск

Выполните в корне рабочего дерева на Windows с установленным Node.js.

```powershell
powershell -ExecutionPolicy Bypass -File tools/build-unity-race.ps1 -Verify
powershell -ExecutionPolicy Bypass -File tools/build-unity-race.ps1
node tools/serve.cjs docs 8784
```

Откройте [обычный вход с seed 1911](http://127.0.0.1:8784/?seed=1911) или [тестовый профиль](http://127.0.0.1:8784/unity-test/?seed=1911). Кэш редактора создаётся в TEMP с ASCII-путём. Сборщик публикует общий плеер в `docs/unity-test/Build` и два входа. Внешний адрес рейтинга определяется конфигурацией входа.

## Управление и осмотр

Для руления ведите пальцем влево или вправо. Трамплин и передний край расщелины Free Ride подбрасывают героя. Для трюка коснитесь склона уже в воздухе. Можно продолжать рулить и тапнуть вторым пальцем. На клавиатуре работают A/D, стрелки и пробел. Для перехода к определённому метру выберите «Настройки» → «Осмотр трассы». Seed можно задать в настройках или URL. В «Замерах» после заезда доступны время подготовки меню, задержка от кнопки до первого кадра, частота кадров и время от движения пальца до конца кадра Unity.

Чтобы пройти выбранный участок самому, в осмотре задайте режим, метры и положение на трассе, затем нажмите «Проехать отсюда». Пауза позволяет повторить тренировку или вернуться к осмотру. Прогресс и рейтинг учитывают заезды, начатые из основного меню.

## Проверка Web

Команды выполняются из корня при работающем локальном сервере. Скрипты используют Playwright с Chromium. Переменная `DANCE_BROWSER_MODULE` позволяет указать установленный модуль.

```powershell
node tools/check-unity-entry.cjs
node tools/check-unity-platform.cjs
node tools/check-unity-platform-catalog.cjs
node tools/check-unity-host.cjs http://127.0.0.1:8784/unity/
node tools/check-unity-host.cjs http://127.0.0.1:8784/unity-test/
node tools/check-unity-entry-browser.cjs http://127.0.0.1:8784/
node tools/check-mountain-web.cjs http://127.0.0.1:8784/ --hardware
node tools/check-unity-platform-browser.cjs http://127.0.0.1:8784/unity-test/
node tools/check-mountain-audio.cjs
```

Основная кнопка Telegram и сайт ведут в полный спуск. Для испытаний работает отдельный параметр бота. [Играть](https://t.me/belaya_sovabot/play) · [Тестовый профиль](https://t.me/belaya_sovabot/play?startapp=unitytest).

[Проверка трасс](../../handoff/UNITY-ТРАССЫ.md) · [Физика и правила](../../handoff/UNITY-МЕХАНИКА.md) · [Сохранения](../../handoff/UNITY-ПРОГРЕСС.md) · [Возврат к архиву](../../handoff/АРХИВ_THREE.md).
