using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Offline landmine. Explodes only when a tank enters its trigger zone.
    /// Deals fixed damage to all tanks within the blast radius and plays the
    /// shared shell explosion effect.
    /// </summary>
    public class LandmineExplosion : MonoBehaviour
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
            if (m_Exploded)
                return;

            // Only tanks detonate the mine — ignore the ground, rocks, etc.
            if (other.GetComponent<TankHealth>() == null)
                return;

            Explode();
        }

        private void Explode()
        {
            m_Exploded = true;

            Collider[] colliders = Physics.OverlapSphere(transform.position, m_ExplosionRadius, m_TankMask);
            foreach (Collider col in colliders)
            {
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddExplosionForce(m_ExplosionForce, transform.position, m_ExplosionRadius);

                TankHealth health = col.GetComponent<TankHealth>();
                if (health != null)
                    health.TakeDamage(m_Damage);
            }

            PlayEffects();
            Destroy(gameObject);
        }

        private void PlayEffects()
        {
            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.parent = null;
                m_ExplosionParticles.Play();
                ParticleSystem.MainModule main = m_ExplosionParticles.main;
                Destroy(m_ExplosionParticles.gameObject, main.duration);
            }

            m_ExplosionAudio?.Play();
        }
    }
}
