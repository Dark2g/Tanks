using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Offline ally NPC — two-state state machine.
    ///
    /// Patrolling: moves between waypoints.
    /// Repairing:  stops and heals tanks that physically touch it.
    ///
    /// Detection uses Physics.OverlapSphere so no child trigger GameObject is needed.
    /// Actual healing uses OnTriggerStay on a small contact trigger (same GO).
    ///
    /// Prefab collider setup (two colliders on the same GameObject):
    ///   1. SphereCollider  isTrigger=false  radius~0.5  → solid body, tanks can't pass through.
    ///   2. SphereCollider  isTrigger=true   radius~1.0  → contact zone, drives healing.
    ///
    /// The detection radius (m_DetectionRadius) is larger than the contact trigger and is
    /// checked each frame via OverlapSphere — no additional collider required.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MinirobotAlly : MonoBehaviour
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
            switch (m_CurrentState)
            {
                case State.Patrolling: UpdatePatrolling(); break;
                case State.Repairing:  UpdateRepairing();  break;
            }
        }

        // ── State updates ──────────────────────────────────────────────────────

        private void UpdatePatrolling()
        {
            // Check whether a damaged tank is within detection range.
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
            // Resume patrol as soon as no damaged tank is in detection range.
            if (!HasDamagedTankNearby())
                TransitionTo(State.Patrolling);
        }

        // ── Healing (contact trigger) ──────────────────────────────────────────

        /// <summary>
        /// Fires while a tank's collider overlaps the contact trigger (isTrigger=true)
        /// on this GameObject. The robot must be in Repairing state.
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            if (m_CurrentState != State.Repairing)
                return;

            TankHealth health = other.GetComponent<TankHealth>();
            if (health == null || health.IsFullHealth)
                return;

            health.Heal(m_HealPerSecond * Time.deltaTime);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if at least one damaged tank is within m_DetectionRadius.
        /// Uses Physics.OverlapSphere so no separate trigger collider is needed for detection.
        /// </summary>
        private bool HasDamagedTankNearby()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, m_DetectionRadius, m_TankMask);
            foreach (Collider col in hits)
            {
                TankHealth health = col.GetComponent<TankHealth>();
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
