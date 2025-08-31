using UnityEngine;

namespace FGS
{
    [DisallowMultipleComponent]
    public sealed class DoorController : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            if (other.TryGetComponent<PlayerController>(out var _))
            {
                GameManager.Instance?.OnDoorEntered();
            }
        }
    }
}
