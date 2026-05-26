using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Networked version of tank movement.
    /// Only the owner reads input and sends it to the server via RPC.
    /// The server applies physics authoritatively; NetworkTransform replicates
    /// position and rotation back to all clients.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkTankMovement : NetworkBehaviour
    {
        public float m_Speed = 12f;
        public float m_TurnSpeed = 180f;
        public AudioSource m_MovementAudio;
        public AudioClip m_EngineIdling;
        public AudioClip m_EngineDriving;
        public float m_PitchRange = 0.2f;

        private const string MovementAxis = "Vertical1";
        private const string TurnAxis = "Horizontal1";

        private Rigidbody m_Rigidbody;
        private float m_MovementInput;
        private float m_TurnInput;
        private float m_OriginalPitch;
        private ParticleSystem[] m_ParticleSystems;

        // Whether this tank's controls are active (disabled between rounds).
        private bool m_ControlEnabled;

        public override void OnNetworkSpawn()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_OriginalPitch = m_MovementAudio != null ? m_MovementAudio.pitch : 1f;
            m_ParticleSystems = GetComponentsInChildren<ParticleSystem>();

            // The server always needs an active Rigidbody so MoveServerRpc can apply
            // physics and collisions work for all tanks regardless of ownership.
            // Non-owner clients set it kinematic because NetworkTransform/NetworkRigidbody
            // drives position replication for them; local physics would fight it.
            m_Rigidbody.isKinematic = !IsServer && !IsOwner;
        }

        private void OnEnable()
        {
            // Restore the correct kinematic state. IsSpawned guards against OnEnable
            // firing before NGO sets ownership (e.g. when the object first activates).
            if (m_Rigidbody != null && IsSpawned)
                m_Rigidbody.isKinematic = !IsServer && !IsOwner;

            m_MovementInput = 0f;
            m_TurnInput = 0f;

            if (m_ParticleSystems != null)
            {
                foreach (ParticleSystem ps in m_ParticleSystems)
                    ps.Play();
            }
        }

        private void OnDisable()
        {
            if (m_Rigidbody != null)
                m_Rigidbody.isKinematic = true;

            if (m_ParticleSystems != null)
            {
                foreach (ParticleSystem ps in m_ParticleSystems)
                    ps.Stop();
            }
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
            {
                m_MovementInput = 0f;
                m_TurnInput = 0f;
            }
        }

        private void Update()
        {
            // Only the owning client reads input.
            if (!IsOwner || !m_ControlEnabled)
                return;

            m_MovementInput = Input.GetAxis(MovementAxis);
            m_TurnInput = Input.GetAxis(TurnAxis);

            HandleEngineAudio();
        }

        private void FixedUpdate()
        {
            if (!IsOwner || !m_ControlEnabled)
                return;

            MoveServerRpc(m_MovementInput, m_TurnInput);
        }

        [ServerRpc]
        private void MoveServerRpc(float moveInput, float turnInput)
        {
            Vector3 movement = transform.forward * moveInput * m_Speed * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + movement);

            float turn = turnInput * m_TurnSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * turnRotation);
        }

        private void HandleEngineAudio()
        {
            if (m_MovementAudio == null)
                return;

            bool isMoving = Mathf.Abs(m_MovementInput) > 0.1f || Mathf.Abs(m_TurnInput) > 0.1f;

            if (!isMoving && m_MovementAudio.clip == m_EngineDriving)
            {
                m_MovementAudio.clip = m_EngineIdling;
                m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                m_MovementAudio.Play();
            }
            else if (isMoving && m_MovementAudio.clip == m_EngineIdling)
            {
                m_MovementAudio.clip = m_EngineDriving;
                m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                m_MovementAudio.Play();
            }
        }
    }
}
