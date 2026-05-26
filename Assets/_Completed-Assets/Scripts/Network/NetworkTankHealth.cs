using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Complete
{
    /// <summary>
    /// Networked version of tank health.
    /// Health is a server-authoritative NetworkVariable. Damage is applied server-side
    /// via TakeDamage(), and death/UI updates are replicated via ClientRpc.
    /// </summary>
    public class NetworkTankHealth : NetworkBehaviour
    {
        public float m_StartingHealth = 100f;
        public Slider m_Slider;
        public Image m_FillImage;
        public Color m_FullHealthColor = Color.green;
        public Color m_ZeroHealthColor = Color.red;
        public GameObject m_ExplosionPrefab;

        /// <summary>Fired on the server when this tank's health reaches zero.</summary>
        public event Action OnTankDied;

        // Server-authoritative health value — all clients receive changes automatically.
        private NetworkVariable<float> m_CurrentHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private AudioSource m_ExplosionAudio;
        private ParticleSystem m_ExplosionParticles;
        private bool m_Dead;

        public override void OnNetworkSpawn()
        {
            // Subscribe to health changes to update UI on all clients.
            m_CurrentHealth.OnValueChanged += OnHealthChanged;

            if (IsServer)
                m_CurrentHealth.Value = m_StartingHealth;

            SetHealthUI(m_CurrentHealth.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_CurrentHealth.OnValueChanged -= OnHealthChanged;
        }

        private void Awake()
        {
            if (m_ExplosionPrefab != null)
            {
                m_ExplosionParticles = Instantiate(m_ExplosionPrefab).GetComponent<ParticleSystem>();
                m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource>();
                m_ExplosionParticles.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            m_Dead = false;

            if (IsServer)
                m_CurrentHealth.Value = m_StartingHealth;

            SetHealthUI(m_StartingHealth);
        }

        private void OnHealthChanged(float previousValue, float newValue)
        {
            SetHealthUI(newValue);
        }

        /// <summary>
        /// Called by NetworkShellExplosion on the server to deal damage.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (!IsServer || m_Dead)
                return;

            m_CurrentHealth.Value = Mathf.Max(0f, m_CurrentHealth.Value - amount);

            if (m_CurrentHealth.Value <= 0f)
                OnDeathServerSide();
        }

        /// <summary>Returns true when the tank is alive and at full health. Safe to read on any peer.</summary>
        public bool IsFullHealth => !m_Dead && m_CurrentHealth.Value >= m_StartingHealth;

        /// <summary>Restores health up to the starting maximum. Called on the server only.</summary>
        public void Heal(float amount)
        {
            if (!IsServer || m_Dead)
                return;

            m_CurrentHealth.Value = Mathf.Min(m_StartingHealth, m_CurrentHealth.Value + amount);
        }

        private void SetHealthUI(float currentHealth)
        {
            if (m_Slider != null)
                m_Slider.value = currentHealth;

            if (m_FillImage != null)
                m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, currentHealth / m_StartingHealth);
        }

        private void OnDeathServerSide()
        {
            m_Dead = true;
            // Notify OnlineGameManager so it can track round state.
            // Despawn is deferred to OnlineGameManager.DespawnTank() to avoid
            // racing with in-flight NetworkTransform messages on clients.
            OnTankDied?.Invoke();
            PlayDeathEffectsClientRpc(transform.position);
        }

        [ClientRpc]
        private void PlayDeathEffectsClientRpc(Vector3 explosionPosition)
        {
            // Hide the tank visuals locally on every client without destroying
            // the NetworkObject — NGO despawn will clean it up server-side.
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;

            foreach (Canvas c in GetComponentsInChildren<Canvas>())
                c.enabled = false;

            if (m_ExplosionParticles == null)
                return;

            m_ExplosionParticles.transform.position = explosionPosition;
            m_ExplosionParticles.gameObject.SetActive(true);
            m_ExplosionParticles.Play();
            m_ExplosionAudio?.Play();
        }
    }
}
