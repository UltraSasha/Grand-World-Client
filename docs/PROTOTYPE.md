# Grand World — прототип TPS: как устроено и как собрать

Требования: [prompts/2026-10-31_21-56_initial-prototype.md](../prompts/2026-10-31_21-56_initial-prototype.md) · план: [PLAN.md](PLAN.md)

## 1. Пакеты и ассеты

| Пакет | Зачем |
|---|---|
| Universal RP 17.6 | рендер, постобработка (Volume), SSAO, Render Graph |
| Input System 1.20 | ввод: клавиатура/мышь, геймпад, сенсор (On-Screen Controls) |
| Cinemachine 6.6 (`com.unity.cinemachine`) | камера от третьего лица с обходом препятствий |
| uGUI 2.x + TextMeshPro | интерфейс меню и паузы |

Внешних ассетов нет:
- **Персонаж** — своя модель и анимации, генерируются скриптом Blender
  [Tools/Blender/build_character.py](../Tools/Blender/build_character.py) и экспортируются в
  `Assets/_Game/Art/Characters/Player/Player.fbx` (Humanoid, 9 клипов).
- **Шрифт** — Noto Sans (SIL Open Font License), кириллица.
- **Текстуры UI, земли и камня, террейн, трава** — генерируются в редакторе (см. ниже).

## 2. Структура проекта

```
Grand World/Assets/_Game/
  Art/Characters/Player/Player.fbx   модель + анимации (из Blender)
  Art/Fonts/                         Noto Sans (TTF)
  Input/GameControls.inputactions    карты Player и UI
  Shaders/                           Grass, KawaseBlur, UIBlurPanel
  Scripts/
    Core/       SettingsManager, SceneLoader
    Player/     PlayerInputHandler, PlayerMovement, PlayerAnimation, AnimatorIKRelay
    Camera/     CameraController, MenuCameraOrbit
    UI/         MainMenuController, PauseMenuController, SettingsPanel, MenuScreen,
                MobileControlsUI, TouchLookArea, UIButtonFx, OptionSelector, ToggleSwitchFx, UITween
    Rendering/  GrassRenderer, UIBlurFeature, UIBlurPanel, GraphicsSettingsApplier
  Editor/       PlayerModelPostprocessor, Builder/ (генератор проекта)
  Animation/    Player.controller                 ← генерируется
  Generated/    текстуры, материалы, шрифты TMP, террейн  ← генерируется
  Prefabs/      Player, Environment               ← генерируется
  Rendering/    профили постобработки             ← генерируется
  Scenes/       MainMenu, Game                    ← генерируется
```

## 3. Сборка сцен

Всё, что помечено «генерируется», создаёт пункт меню **Tools → Grand World → Build All**:

1. **Project Setup** — Player Settings (`com.grandworld.burrbedy`, IL2CPP/ARM64, Android 8.0+, Vulkan+GLES3,
   альбомная ориентация), слой `Player`, уровни качества Low/Medium/High с отдельными URP-ассетами,
   Renderer Feature размытия UI, удаление шаблонных файлов.
2. **Build Content** — спрайты UI, шрифты TMP, материалы, профили постобработки (ПК/телефон/меню),
   Animator Controller, террейн с травой и границами, префабы Player и Environment.
3. **Build Scenes** — сцены MainMenu и Game и список сцен в Build Settings.

Шаги можно повторять — ассеты пересоздаются.

### Персонаж и Animator
- Импорт FBX настраивает `PlayerModelPostprocessor`: Humanoid-аватар, клипы `Idle, Walk, Run, Sprint,
  CrouchIdle, CrouchWalk, JumpStart, JumpAir, Land`, анимации «на месте» (корень запекается в позу).
- `Player.controller`: Blend Tree `Locomotion` по `Speed` (0 / 1.46 / 4.07 / 6.63 м/с),
  `Crouch` (0 / 1.22), состояния прыжка; IK Pass включён для стоп.
- Пороги Blend Tree равны естественной скорости клипов, а скорости в `PlayerMovement` — тем же значениям,
  поэтому ноги не скользят.

### Камера
`CameraRoot` (скрипт `CameraController`) догоняет персонажа и поворачивается по вводу;
`CM Player Camera` (Cinemachine Camera + Third Person Follow) стоит сзади-сверху и обходит препятствия.

### Графика
- Солнце с мягкими тенями, процедурное небо (атмосферное рассеивание), экспоненциальный туман.
- Volume: ACES, цветокоррекция, Bloom, Vignette, Motion Blur (отключаемый), в меню — Depth of Field;
  SSAO — Renderer Feature в рендерерах ПК/среднего качества.
- Трава — `GrassRenderer` + шейдер `GrandWorld/Grass`: GPU-инстансинг, ветер, вариации цвета,
  плотность и дальность зависят от качества.
- Сглаживание: TAA (высокое), SMAA (среднее), FXAA (низкое и телефоны).

## 4. Скрипты

Полный код — в `Assets/_Game/Scripts`. Все настраиваемые параметры — `[SerializeField]` с подсказками,
комментарии на русском. Настройки игрока хранятся в PlayerPrefs (`SettingsManager`).

## 5. Сборка под платформы

| Платформа | Что нужно |
|---|---|
| Linux x86_64 | модуль уже установлен. File → Build Profiles → Linux → Build |
| Windows x86_64 | Unity Hub → Installs → 6000.6.7f1 → Add modules → *Windows Build Support (Mono)* |
| Android ARM64 | *Android Build Support* + OpenJDK + Android SDK & NDK; IL2CPP и ARM64 уже выставлены |

## 6. Как проверить

1. Открыть `Assets/_Game/Scenes/MainMenu.unity`, нажать Play.
2. Меню: элементы плавно появляются, кнопки реагируют на мышь, стрелки, геймпад; «Настройки» → поменять
   громкость/качество → перезапустить Play — значения сохранились.
3. «Играть» → затемнение и экран загрузки → игровая сцена.
4. WASD — движение относительно камеры, мышь — обзор, Space — прыжок, Ctrl или двойное W — спринт,
   Shift — присед, Esc — пауза («Продолжить», «Настройки», «В главное меню»).
5. Геймпад: левый стик, правый стик, A, нажатие левого стика, B, Start.
6. Сенсорное управление в редакторе: на объекте `MobileControls` включить *Show In Editor*.
7. Камера не проходит сквозь валуны; персонаж не выходит за пределы участка 50×50 м.

## Особенности окружения разработки

Проект лежит в общей папке VMware (`vmhgfs-fuse`), которая не умеет корректно ставить время изменения
файлов — компиляция Unity на ней ломается. Поэтому `Library` смонтирована (bind mount) с диска VM:
`~/UnityLibrary/GrandWorld/Library` → `Grand World/Library` (запись в `/etc/fstab`).
