using UnityEngine;
using UnityEngine.EventSystems;
using GrandWorld.Player;

namespace GrandWorld.UI
{
    /// <summary>
    /// Невидимая область на правой половине экрана: свайп пальцем поворачивает камеру.
    /// Поддерживает несколько касаний — учитывается только палец, начавший свайп в этой области.
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] PlayerInputHandler playerInput;

        int _pointerId = int.MinValue;

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId == int.MinValue) _pointerId = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId || playerInput == null) return;
            playerInput.AddTouchLook(e.delta);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointerId) _pointerId = int.MinValue;
        }

        void OnDisable() => _pointerId = int.MinValue;
    }
}
