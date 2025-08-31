using UnityEngine;

namespace FGS
{
    [DisallowMultipleComponent]
    public sealed class KeyController : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            if (other.TryGetComponent<PlayerController>(out var _))
            {
                GameManager.Instance?.OnKeyCollected();
            }

        }
    }
}
