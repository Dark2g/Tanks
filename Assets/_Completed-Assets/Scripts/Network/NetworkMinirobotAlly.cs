using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Online ally NPC — server-authoritative two-state state machine.
    ///
    /// Patrolling: moves between waypoints on the server; position is
    ///             replicated to clients via NetworkTransform.
    /// Repairing:  stops and heals every damaged tank in contact at
    ///             HealPerSecond HP/s via NetworkTankHealth.Heal().
    ///
    /// Transitions:
    ///   Patrolling -> Repairing  when a damaged tank enters the trigger (server).
    ///   Repairing  -> Patrolling when no damaged tanks remain in contact (server).
    ///
    /// Requires a NetworkTransform component on the same GameObject so that
    /// movement computed on the server is replicated to all clients.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkMinirobotAlly : NetworkBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Waypoints")]
        [Tooltip("World-space waypoints the robot patrols in order. " +
                 "If empty the robot stays in place.")]
        public List<Transform> m_Waypoints = new List<Transform>();

        [Header("Movement")]
        [Tooltip("Speed while patrolling (units/s).")]
        public float m_PatrolSpeed = 3f;

        [Tooltip("Distance at which the robot considers a waypoint reached.")]
        public float m_WaypointTolerance = 0.5f;

        [Tooltip("Rotation speed while steering towards a waypoint (degrees/s).")]
        public float m_RotationSpeed = 120f;

        [Header("Healing")]
        [Tooltip("Health restored per second while a tank is in contact.")]
        public float m_HealPerSecond = 2f;

        // ── State machine ──────────────────────────────────────────────────────

        private enum State { Patrolling, Repairing }

        private State m_CurrentState = State.Patrolling;

        // ── Runtime ────────────────────────────────────────────────────────────

        private Rigidbody m_Rigidbody;
        private int m_WaypointIndex;

        /// <summary>Tanks currently overlapping the trigger collider (server-only).</summary>
        private readonly HashSet<NetworkTankHealth> m_ContactTanks = new HashSet<NetworkTankHealth>();

        // ── Unity / NGO lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            // Position driven by script on the server; NetworkTransform replicates it.
            m_Rigidbody.isKinematic = true;
        }

        private void Update()
        {
            // Only the server runs movement and healing logic.
            if (!IsServer)
                return;

            switch (m_CurrentState)
            {
                case State.Patrolling: UpdatePatrolling(); break;
                case State.Repairing:  UpdateRepairing();  break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
                return;

            NetworkTankHealth health = other.GetComponent<NetworkTankHealth>();
            if (health == null)
                return;

            m_ContactTanks.Add(health);
            EvaluateTransition();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer)
                return;

            NetworkTankHealth health = other.GetComponent<NetworkTankHealth>();
            if (health == null)
                return;

            m_ContactTanks.Remove(health);
            EvaluateTransition();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsServer || m_CurrentState != State.Repairing)
                return;

            NetworkTankHealth health = other.GetComponent<NetworkTankHealth>();
            if (health == null || health.IsFullHealth)
                return;

            health.Heal(m_HealPerSecond * Time.deltaTime);
        }

        // ── State updates ──────────────────────────────────────────────────────

        private void UpdatePatrolling()
        {
            if (m_Waypoints.Count == 0)
                return;

            Transform target = m_Waypoints[m_WaypointIndex];
            if (target == null)
            {
                AdvanceWaypoint();
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, m_RotationSpeed * Time.deltaTime);
            }

            transform.position += transform.forward * m_PatrolSpeed * Time.deltaTime;

            if (toTarget.magnitude <= m_WaypointTolerance)
                AdvanceWaypoint();
        }

        private void UpdateRepairing()
        {
            // Remove stale references (tank died and was despawned).
            m_ContactTanks.RemoveWhere(h => h == null);

            if (!HasDamagedTankInContact())
                TransitionTo(State.Patrolling);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void EvaluateTransition()
        {
            TransitionTo(HasDamagedTankInContact() ? State.Repairing : State.Patrolling);
        }

        private bool HasDamagedTankInContact()
        {
            foreach (NetworkTankHealth h in m_ContactTanks)
            {
                if (h != null && !h.IsFullHealth)
                    return true;
            }
            return false;
        }

        private void TransitionTo(State next)
        {
            m_CurrentState = next;
        }

        private void AdvanceWaypoint()
        {
            if (m_Waypoints.Count == 0)
                return;

            m_WaypointIndex = (m_WaypointIndex + 1) % m_Waypoints.Count;
        }
    }
}
