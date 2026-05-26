using Unity.Netcode;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Online pickup: runs exclusively on the server.
    /// Heals the tank that touches it and despawns itself so the
    /// NetworkHeartSpawner can replace it.
    /// </summary>
    public class NetworkHeartPickup : NetworkBehaviour
    {
        public const float HealAmount = 25f;

        private bool m_Collected;

        private void OnTriggerEnter(Collider other)
        {
            // All game logic runs on the server only.
            if (!IsServer || m_Collected)
                return;

            NetworkTankHealth health = other.GetComponent<NetworkTankHealth>();
            if (health == null)
                return;

            m_Collected = true;
            health.Heal(HealAmount);

            // Despawn so NetworkHeartSpawner's OnNetworkDespawn callback fires
            // and it can spawn a replacement heart.
            NetworkObject.Despawn(true);
        }
    }
}
