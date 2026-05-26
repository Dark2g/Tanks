using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Offline pickup: heals the first tank that enters the trigger,
    /// then notifies the HeartSpawner so it can replace this heart.
    /// </summary>
    public class HeartPickup : MonoBehaviour
    {
        public const float HealAmount = 25f;

        /// <summary>Raised when a tank collects this heart so the spawner can track the count.</summary>
        public event System.Action<HeartPickup> OnCollected;

        private bool m_Collected;

        private void OnTriggerEnter(Collider other)
        {
            if (m_Collected)
                return;

            TankHealth health = other.GetComponent<TankHealth>();
            if (health == null)
                return;

            m_Collected = true;
            health.Heal(HealAmount);
            OnCollected?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
