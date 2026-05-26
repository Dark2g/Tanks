using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Complete
{
    /// <summary>
    /// Networked version of tank shooting.
    /// Only the owner reads input and charges the shot locally for responsive UI.
    /// The fire request is sent to the server via RPC, which spawns the shell authoritatively.
    /// </summary>
    public class NetworkTankShooting : NetworkBehaviour
    {
        public Rigidbody m_Shell;
        public Transform m_FireTransform;
        public Slider m_AimSlider;
        public AudioSource m_ShootingAudio;
        public AudioClip m_ChargingClip;
        public AudioClip m_FireClip;
        public float m_MinLaunchForce = 15f;
        public float m_MaxLaunchForce = 30f;
        public float m_MaxChargeTime = 0.75f;

        private const string FireButton = "Fire1";

        private float m_CurrentLaunchForce;
        private float m_ChargeSpeed;
        private bool m_Fired;
        private bool m_ControlEnabled;

        public override void OnNetworkSpawn()
        {
            m_ChargeSpeed = (m_MaxLaunchForce - m_MinLaunchForce) / m_MaxChargeTime;
            ResetCharge();
        }

        private void OnEnable()
        {
            ResetCharge();
        }

        /// <summary>Allows or disables player control. Called by OnlineGameManager on the server.</summary>
        public void SetControlEnabled(bool enabled)
        {
            SetControlEnabledClientRpc(enabled);
        }

        [ClientRpc]
        private void SetControlEnabledClientRpc(bool enabled)
        {
            m_ControlEnabled = enabled;

            if (!enabled)
                ResetCharge();
        }

        private void ResetCharge()
        {
            m_CurrentLaunchForce = m_MinLaunchForce;

            if (m_AimSlider != null)
                m_AimSlider.value = m_MinLaunchForce;
        }

        private void Update()
        {
            // Only the owning client reads input.
            if (!IsOwner || !m_ControlEnabled)
                return;

            if (m_AimSlider != null)
                m_AimSlider.value = m_MinLaunchForce;

            if (m_CurrentLaunchForce >= m_MaxLaunchForce && !m_Fired)
            {
                m_CurrentLaunchForce = m_MaxLaunchForce;
                RequestFireServerRpc(m_CurrentLaunchForce);
            }
            else if (Input.GetButtonDown(FireButton))
            {
                m_Fired = false;
                m_CurrentLaunchForce = m_MinLaunchForce;

                if (m_ShootingAudio != null)
                {
                    m_ShootingAudio.clip = m_ChargingClip;
                    m_ShootingAudio.Play();
                }
            }
            else if (Input.GetButton(FireButton) && !m_Fired)
            {
                m_CurrentLaunchForce += m_ChargeSpeed * Time.deltaTime;

                if (m_AimSlider != null)
                    m_AimSlider.value = m_CurrentLaunchForce;
            }
            else if (Input.GetButtonUp(FireButton) && !m_Fired)
            {
                RequestFireServerRpc(m_CurrentLaunchForce);
            }
        }

        [ServerRpc]
        private void RequestFireServerRpc(float launchForce)
        {
            m_Fired = true;
            m_CurrentLaunchForce = m_MinLaunchForce;

            Rigidbody shellInstance = Instantiate(m_Shell, m_FireTransform.position, m_FireTransform.rotation);
            shellInstance.GetComponent<NetworkObject>().Spawn(true);
            shellInstance.velocity = launchForce * m_FireTransform.forward;

            PlayFireAudioClientRpc();
        }

        [ClientRpc]
        private void PlayFireAudioClientRpc()
        {
            m_Fired = true;
            m_CurrentLaunchForce = m_MinLaunchForce;

            if (m_ShootingAudio != null)
            {
                m_ShootingAudio.clip = m_FireClip;
                m_ShootingAudio.Play();
            }
        }
    }
}
