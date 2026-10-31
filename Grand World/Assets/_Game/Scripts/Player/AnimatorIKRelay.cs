using UnityEngine;

namespace GrandWorld.Player
{
    /// <summary>Пересылает OnAnimatorIK с объекта, где висит Animator, в PlayerAnimation.</summary>
    public class AnimatorIKRelay : MonoBehaviour
    {
        public PlayerAnimation Target { get; set; }

        void OnAnimatorIK(int layerIndex)
        {
            if (Target != null) Target.OnAnimatorIKPass();
        }
    }
}
