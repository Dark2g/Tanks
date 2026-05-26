using Unity.Netcode;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Online ally NPC — server-authoritative two-state state machine.
    ///
    /// Patrolling: moves between waypoints; NetworkTransform replicates position to clients.
    /// Repairing:  stops and heals tanks that physically touch the contact trigger.
    ///
    /// Detection uses Physics.OverlapSphere (server-side) — no child trigger needed.
    /// Healing uses OnTriggerStay on a small contact trigger on this GameObject.
    ///
    /// Prefab collider setup (two colliders on the same GameObject):
    ///   1. SphereCollider  isTrigger=false  radius~0.5  → solid body.
    ///   2. SphereCollider  isTrigger=true   radius~1.0  → contact zone for healing.
    ///
    /// Also requires a NetworkTransform component on this GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkMinirobotAlly : NetworkBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Waypoints")]
        [Tooltip("GameObjects whose positions define the patrol route. " +
                 "If empty the robot stays in place.")]
        public GameObject[] m_Waypoints = new GameObject[0];

        [Header("Movement")]
        [Tooltip("Speed while patrolling (units/s).")]
        public float m_PatrolSpeed = 3f;

        [Tooltip("Distance at which the robot considers a waypoint reached.")]
        public float m_WaypointTolerance = 0.5f;

        [Tooltip("Rotation speed while steering towards a waypoint (degrees/s).")]
        public float m_RotationSpeed = 120f;

        [Header("Detection")]
        [Tooltip("Radius within which the robot detects damaged tanks and stops patrolling. " +
                 "Should be larger than the contact trigger radius.")]
        public float m_DetectionRadius = 3f;

        [Tooltip("LayerMask for the detection overlap. Should include the Players layer.")]
        public LayerMask m_TankMask;

        [Header("Healing")]
        [Tooltip("Health restored per second while a tank is physically in contact " +
                 "(inside the contact trigger collider).")]
        public float m_HealPerSecond = 2f;

        // ── State machine ──────────────────────────────────────────────────────

        private enum State { Patrolling, Repairing }

        private State m_CurrentState = State.Patrolling;

        // ── Runtime ────────────────────────────────────────────────────────────

        private int m_WaypointIndex;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        private void Update()
        {
            if (!IsServer)
                return;

            switch (m_CurrentState)
            {
                case State.Patrolling: UpdatePatrolling(); break;
                case State.Repairing:  UpdateRepairing();  break;
            }
        }

        // ── State updates ──────────────────────────────────────────────────────

        private void UpdatePatrolling()
        {
            if (HasDamagedTankNearby())
            {
                TransitionTo(State.Repairing);
                return;
            }

            if (m_Waypoints.Length == 0)
                return;

            GameObject waypointObj = m_Waypoints[m_WaypointIndex];
            if (waypointObj == null)
            {
                AdvanceWaypoint();
                return;
            }

            Vector3 toTarget = waypointObj.transform.position - transform.position;
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
            if (!HasDamagedTankNearby())
                TransitionTo(State.Patrolling);
        }

        // ── Healing (contact trigger) ──────────────────────────────────────────

        private void OnTriggerStay(Collider other)
        {
            if (!IsServer || m_CurrentState != State.Repairing)
                return;

            NetworkTankHealth health = other.GetComponent<NetworkTankHealth>();
            if (health == null || health.IsFullHealth)
                return;

            health.Heal(m_HealPerSecond * Time.deltaTime);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool HasDamagedTankNearby()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, m_DetectionRadius, m_TankMask);
            foreach (Collider col in hits)
            {
                NetworkTankHealth health = col.GetComponent<NetworkTankHealth>();
                if (health != null && !health.IsFullHealth)
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
            if (m_Waypoints.Length == 0)
                return;

            m_WaypointIndex = (m_WaypointIndex + 1) % m_Waypoints.Length;
        }
    }
}
