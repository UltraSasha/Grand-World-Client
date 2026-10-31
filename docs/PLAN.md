# План: минимальный прототип TPS (Grand World)

Источник требований: [prompts/2026-10-31_21-56_initial-prototype.md](../prompts/2026-10-31_21-56_initial-prototype.md)

## Исходное состояние

- Unity **6000.6.7f1**, URP 17.6, есть PC/Mobile RP-ассеты (`Assets/Settings`).
- Input System 1.20 установлен, `activeInputHandler: 1` (только новая система).
- Android: IL2CPP, ARM64, minSdk 26 (Android 8.0) — уже выставлено.
- Cinemachine **не установлен**, DOTween нет.
- Unity MCP (mcp-for-unity 3.4.11) работает, редактор подключён; Blender 5.2.3 (Windows-хост) подключён,
  видит проект как `C:\Grand World RP\Client\...` — FBX можно экспортировать прямо в `Assets`.
- Генераторов 3D и библиотек ассетов в Blender нет → персонажа строим кодом.

## Принятые решения

| Вопрос | Решение |
|---|---|
| Персонаж | Своя модель в Blender — обычный парень в повседневной одежде, спокойный стиль под РП (см. этап 1), Humanoid-скелет (21 кость), свои анимации |
| Идентификатор | `com.grandworld.burrbedy` (Android и ПК), Company Name `Grand World`, Product Name `Grand World` |
| Шаблонные файлы | Удалить `SampleScene`, `TutorialInfo`, `Readme.asset`, `InputSystem_Actions.inputactions` (заменяются своими) |
| Модули сборки | Установлены только Linux (Mono) и WebGL → Windows и Android Build Support нужно доустановить в Unity Hub |
| Сборка сцен | Через Unity MCP прямо в редакторе |
| Камера | Cinemachine 3.x (`com.unity.cinemachine`) |
| UI | uGUI + TextMeshPro; анимации UI на корутинах (без DOTween) |
| Размытие фона панелей | URP Renderer Feature (Kawase blur) → RenderTexture под полупрозрачные панели; на Android — упрощённое |
| Трава | Свой GPU-instanced шейдер травы с ветром (лучше выглядит и быстрее, чем трава Terrain в URP) |
| Комментарии в коде | На русском |

## Этапы

### 1. Персонаж (Blender)
1. Скелет с именами под автомаппинг Humanoid — *сделан*. Черновой меш-манекен — *сделан*, переделывается
   под спокойный РП-стиль — обычный молодой парень, гражданская повседневная одежда:
   - естественные мужские пропорции (рост ~1.8 м, плечи чуть шире таза), без гипертрофии;
   - голова с простым стилизованным лицом (нос, брови, глаза без детализации), короткая стрижка;
   - одежда: худи или футболка поверх лонгслива, джинсы/карго, кроссовки, кисти рук открыты;
   - спокойная палитра: тёплый тон кожи, приглушённые цвета одежды (тёмно-синий, серый, бежевый, хаки),
     без неона и свечения;
   - без брони, шлемов и оружия; одежда — отдельные материалы, чтобы позже легко перекрашивать
     (задел под кастомизацию внешности в РП).
2. Анимации в спокойной, естественной манере — расслабленная стойка с лёгким дыханием, неторопливая
   походка, бег без «боевой» подачи (30 fps, in-place, ноги через аналитический IK):
   `Idle`, `Walk`, `Run`, `Sprint`, `CrouchIdle`, `CrouchWalk`, `JumpStart`, `JumpAir`, `Land`.
3. Экспорт `Assets/Art/Characters/Mannequin/Mannequin.fbx` (все action'ы как отдельные take'и).
4. Проверка превью-рендерами каждой анимации.

### 2. Пакеты и структура проекта
- Установить `com.unity.cinemachine` (uGUI с TextMeshPro уже есть; постобработка — встроенные URP Volume).
- Структура:
  ```
  Assets/_Game/
    Art/Characters/Mannequin/   Art/Environment/   Art/UI/
    Animation/                  (Animator Controller, клипы)
    Input/GameControls.inputactions
    Materials/  Shaders/ (Grass, UIBlur)
    Prefabs/  (Player, UI/MobileControls, UI/PauseMenu, UI/Settings)
    Rendering/ (Volume-профили PC/Mobile, Renderer Features)
    Scenes/ (Boot? → MainMenu, Game, Loading)
    Scripts/ Player/ Camera/ UI/ Core/ Rendering/
  ```
- Удалить шаблонные `SampleScene`, `TutorialInfo`, `Readme.asset`, `InputSystem_Actions.inputactions`.

### 3. Ввод
- `GameControls.inputactions`: карты `Player` (Move, Look, Jump, Sprint, Crouch, Pause) и `UI`.
- Схемы: Keyboard&Mouse, Gamepad, Touch. Двойное W → спринт обрабатывается в `PlayerInputHandler`.
- Сенсор: On-Screen Stick слева, свайп-обзор справа, экранные кнопки.

### 4. Скрипты
`PlayerInputHandler`, `PlayerMovement` (CharacterController, инерция, гравитация, склоны, coyote time),
`PlayerAnimation` (Blend Tree по скорости, наклон корпуса, Foot IK), `CameraController` (чувствительность
поверх Cinemachine OrbitalFollow + Deoccluder), `MobileControlsUI`, `MainMenuController`, `PauseMenuController`,
`SettingsManager` (PlayerPrefs: громкость, чувствительность, качество, полноэкранный, motion blur),
`SceneLoader` (async + экран загрузки + затемнение), вспомогательные `UIAnimator`, `MenuCameraOrbit`.

### 5. Animator
- Параметры: `Speed`, `Grounded`, `Crouch`, `VerticalSpeed`, `Lean`.
- Слой Base: Blend Tree Locomotion (Idle → Walk → Run → Sprint), Blend Tree Crouch (CrouchIdle → CrouchWalk),
  Jump: JumpStart → JumpAir → Land. IK Pass для стоп.

### 6. Сцена Game
- Участок 50×50 м (Terrain с лёгким рельефом), невидимые стены по краям.
- Трава: GPU-instancing, ветер, вариации цвета; плотность зависит от качества.
- Солнце с мягкими тенями, процедурный skybox с атмосферным рассеиванием, туман.
- Volume: Bloom, Color Adjustments/Tonemapping (ACES), SSAO (Renderer Feature), Vignette, Motion Blur (выключаемый).
- Игрок-префаб, Cinemachine-камера, UI паузы, мобильное управление (только на Android).

### 7. Сцена MainMenu
- 3D-фон: тот же участок травы с персонажем в Idle, медленный облёт камеры, Depth of Field.
- Экраны: Главное (Играть / Настройки / Выход), Настройки; появление элементов, hover/press (масштаб+свечение),
  переходы, затемнение. Навигация мышью, геймпадом (EventSystem + выделение по умолчанию) и касаниями.
- «Выход» скрыт на Android.

### 8. Качество и сборка
- Уровни качества Low / Medium / High → привязаны к Mobile_RPAsset / PC_RPAsset; на Android по умолчанию Medium.
- Сглаживание: ПК — TAA/SMAA, Android — FXAA.
- Build Settings: MainMenu (0), Game (1); профили сборки Windows x86_64, Linux x86_64, Android ARM64/IL2CPP.
- Идентификатор `com.grandworld.burrbedy` для Android и Standalone, Company/Product Name `Grand World`.
- Для сборок Windows и Android доустановить в Unity Hub модули *Windows Build Support (Mono)* и
  *Android Build Support* (+ OpenJDK, Android SDK & NDK). Linux (Mono) уже есть.

### 9. Проверка
- Компиляция без ошибок/предупреждений (read_console).
- Play Mode: меню → загрузка → игра → пауза → меню; настройки сохраняются после перезапуска.
- Пробная сборка Linux (локально). Windows/Android — после установки модулей в Hub.
- Итоговый README с инструкциями (формат ответа из промпта: пакеты, папки, настройка, код, сборка, проверка).

## Открытые вопросы
Нет — все решены (см. «Принятые решения»). Действие на вашей стороне: установить в Unity Hub
модули Windows Build Support (Mono) и Android Build Support для редактора 6000.6.7f1.
