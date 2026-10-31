using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GrandWorld.Core;

namespace GrandWorld.Player
{
    /// <summary>
    /// Единая точка ввода игрока: клавиатура/мышь, геймпад и сенсорный экран.
    /// Экранный джойстик и кнопки (On-Screen Controls) эмулируют геймпад, поэтому
    /// используют те же действия; обзор свайпом передаётся через <see cref="AddTouchLook"/>.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Действия ввода")]
        [Tooltip("Ассет GameControls.inputactions")]
        [SerializeField] InputActionAsset actions;

        [Header("Спринт двойным нажатием W")]
        [Tooltip("Максимальная пауза между двумя нажатиями W, сек.")]
        [SerializeField] float doubleTapWindow = 0.3f;

        [Header("Чувствительность обзора (базовая, умножается на настройки)")]
        [Tooltip("Градусов поворота камеры на один пиксель движения мыши")]
        [SerializeField] float mouseDegreesPerPixel = 0.08f;
        [Tooltip("Градусов в секунду при полностью отклонённом правом стике")]
        [SerializeField] float stickDegreesPerSecond = 160f;
        [Tooltip("Градусов поворота камеры на один пиксель свайпа (при 1080p по высоте)")]
        [SerializeField] float touchDegreesPerPixel = 0.16f;

        /// <summary>Направление движения (-1..1), Y — вперёд.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Поворот камеры за этот кадр в градусах: X — рыскание, Y — тангаж.</summary>
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }

        /// <summary>Событие паузы (Esc / Start / экранная кнопка).</summary>
        public event Action PausePressed;

        InputActionMap _player;
        InputAction _move, _look, _lookStick, _jump, _sprint, _sprintToggle, _crouch, _crouchToggle, _pause;

        bool _jumpQueued;
        bool _sprintLatched;     // спринт, включённый двойным W или нажатием левого стика
        bool _crouchToggled;     // присед, включённый кнопкой B / экранной кнопкой
        float _lastWTap = -10f;
        Vector2 _touchLook;      // накопленный свайп за кадр
        bool _gameplayEnabled = true;

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("PlayerInputHandler: не назначен ассет действий ввода", this);
                enabled = false;
                return;
            }
            _player = actions.FindActionMap("Player", true);
            _move = _player.FindAction("Move", true);
            _look = _player.FindAction("Look", true);
            _lookStick = _player.FindAction("LookStick", true);
            _jump = _player.FindAction("Jump", true);
            _sprint = _player.FindAction("Sprint", true);
            _sprintToggle = _player.FindAction("SprintToggle", true);
            _crouch = _player.FindAction("Crouch", true);
            _crouchToggle = _player.FindAction("CrouchToggle", true);
            _pause = _player.FindAction("Pause", true);
        }

        void OnEnable()
        {
            if (_player == null) return;
            _player.Enable();
            _jump.performed += OnJump;
            _sprintToggle.performed += OnSprintToggle;
            _crouchToggle.performed += OnCrouchToggle;
            _pause.performed += OnPause;
        }

        void OnDisable()
        {
            if (_player == null) return;
            _jump.performed -= OnJump;
            _sprintToggle.performed -= OnSprintToggle;
            _crouchToggle.performed -= OnCrouchToggle;
            _pause.performed -= OnPause;
            _player.Disable();
        }

        /// <summary>Включает/выключает игровой ввод (например, на паузе). Пауза остаётся доступной.</summary>
        public void SetGameplayEnabled(bool value)
        {
            _gameplayEnabled = value;
            if (!value)
            {
                Move = Vector2.zero;
                LookDelta = Vector2.zero;
                _jumpQueued = false;
                _sprintLatched = false;
                _touchLook = Vector2.zero;
            }
        }

        /// <summary>Свайп по правой половине экрана (пиксели). Вызывается из TouchLookArea.</summary>
        public void AddTouchLook(Vector2 pixelDelta)
        {
            _touchLook += pixelDelta;
        }

        /// <summary>Экранная кнопка паузы на телефоне.</summary>
        public void RequestPause() => PausePressed?.Invoke();

        /// <summary>Забирает нажатие прыжка (одноразово).</summary>
        public bool ConsumeJump()
        {
            bool j = _jumpQueued;
            _jumpQueued = false;
            return j;
        }

        void Update()
        {
            if (!_gameplayEnabled)
            {
                _touchLook = Vector2.zero;
                return;
            }

            Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);

            // --- Обзор: мышь (пиксели), стик (скорость) и свайп (пиксели) приводим к градусам
            Vector2 look = _look.ReadValue<Vector2>() * (mouseDegreesPerPixel * SettingsManager.MouseSensitivity);
            look += _lookStick.ReadValue<Vector2>() * (stickDegreesPerSecond * SettingsManager.StickSensitivity * Time.unscaledDeltaTime);
            float touchScale = touchDegreesPerPixel * SettingsManager.TouchSensitivity * (1080f / Mathf.Max(1, Screen.height));
            look += _touchLook * touchScale;
            _touchLook = Vector2.zero;
            if (SettingsManager.InvertY) look.y = -look.y;
            LookDelta = look;

            // --- Двойное нажатие W включает спринт до остановки
            var kb = Keyboard.current;
            if (kb != null && kb.wKey.wasPressedThisFrame)
            {
                if (Time.unscaledTime - _lastWTap <= doubleTapWindow) _sprintLatched = true;
                _lastWTap = Time.unscaledTime;
            }
            // Спринт сбрасывается, когда игрок перестаёт идти вперёд
            if (Move.y < 0.3f) _sprintLatched = false;

            SprintHeld = _sprint.IsPressed() || _sprintLatched;
            CrouchHeld = _crouch.IsPressed() || _crouchToggled;
            if (SprintHeld && _crouchToggled) _crouchToggled = false; // спринт выводит из приседа
        }

        void OnJump(InputAction.CallbackContext _)
        {
            if (!_gameplayEnabled) return;
            _jumpQueued = true;
            _crouchToggled = false; // прыжок выводит из приседа
        }

        void OnSprintToggle(InputAction.CallbackContext _)
        {
            if (!_gameplayEnabled) return;
            _sprintLatched = !_sprintLatched;
        }

        void OnCrouchToggle(InputAction.CallbackContext _)
        {
            if (!_gameplayEnabled) return;
            _crouchToggled = !_crouchToggled;
            if (_crouchToggled) _sprintLatched = false;
        }

        void OnPause(InputAction.CallbackContext _)
        {
            if (SceneLoader.IsLoading) return;
            PausePressed?.Invoke();
        }
    }
}
