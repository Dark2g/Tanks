using Unity.Netcode;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Online landmine — server-authoritative.
    /// Only tanks detonate the mine. The server deals damage via NetworkTankHealth,
    /// tells all clients to play the effect, then despawns the NetworkObject.
    /// </summary>
    public class NetworkLandmineExplosion : NetworkBehaviour
    {
        [Header("Damage")]
        [Tooltip("Fixed damage dealt to every tank caught in the blast.")]
        public float m_Damage = 40f;

        [Tooltip("LayerMask for tanks. Should be the 'Players' layer.")]
        public LayerMask m_TankMask;

        [Tooltip("Radius of the explosion blast.")]
        public float m_ExplosionRadius = 5f;

        [Tooltip("Force applied to tank rigidbodies at the explosion centre.")]
        public float m_ExplosionForce = 1000f;

        [Header("Effects")]
        [Tooltip("Particle system for the explosion visual. Will be unparented and played.")]
        public ParticleSystem m_ExplosionParticles;

        [Tooltip("Audio source for the explosion sound.")]
        public AudioSource m_ExplosionAudio;

        private bool m_Exploded;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || m_Exploded)
                return;

            // Only tanks detonate the mine — ignore the ground, rocks, etc.
            if (other.GetComponent<NetworkTankHealth>() == null)
                return;

            m_Exploded = true;

            Collider[] colliders = Physics.OverlapSphere(transform.position, m_ExplosionRadius, m_TankMask);
            foreach (Collider col in colliders)
            {
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddExplosionForce(m_ExplosionForce, transform.position, m_ExplosionRadius);

                NetworkTankHealth health = col.GetComponent<NetworkTankHealth>();
                if (health != null)
                    health.TakeDamage(m_Damage);
            }

            ExplodeClientRpc(transform.position);
            NetworkObject.Despawn(true);
        }

        [ClientRpc]
        private void ExplodeClientRpc(Vector3 position)
        {
            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.parent = null;
                m_ExplosionParticles.transform.position = position;
                m_ExplosionParticles.Play();
                ParticleSystem.MainModule main = m_ExplosionParticles.main;
                Destroy(m_ExplosionParticles.gameObject, main.duration);
            }

            m_ExplosionAudio?.Play();
        }
    }
}
