using UnityEngine;

namespace GrandWorld.Rendering
{
    /// <summary>
    /// Помечает панель интерфейса, которой нужен размытый фон.
    /// Пока панель активна, UIBlurFeature размывает кадр; когда панелей нет — размытие не считается.
    /// </summary>
    public class UIBlurPanel : MonoBehaviour
    {
        void OnEnable() => UIBlurFeature.ActivePanels++;
        void OnDisable() => UIBlurFeature.ActivePanels = Mathf.Max(0, UIBlurFeature.ActivePanels - 1);
    }
}
