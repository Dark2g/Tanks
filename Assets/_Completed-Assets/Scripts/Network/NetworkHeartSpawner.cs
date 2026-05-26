using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Online procedural heart spawner. Server-authoritative.
    ///
    /// Plain MonoBehaviour — no NetworkObject needed on this GameObject.
    /// It checks IsServer at runtime so it only runs spawn logic on the host/server.
    /// Hearts themselves are NetworkObjects and appear on all clients automatically.
    ///
    /// Algorithm:
    ///   1. Subdivide the play area into an NxN grid.
    ///   2. Shuffle the grid cells (Fisher-Yates).
    ///   3. Apply random jitter inside each candidate cell.
    ///   4. Reject positions that overlap existing colliders (Physics.CheckSphere).
    ///   5. Keep at most MaxHeartsOnMap hearts alive. Replace each collected heart
    ///      after RespawnDelay seconds.
    /// </summary>
    public class NetworkHeartSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("Heart prefab — must have NetworkObject and NetworkHeartPickup components.")]
        public GameObject m_HeartPrefab;

        [Header("Spawn Area")]
        [Tooltip("Centre of the playfield in world space.")]
        public Vector3 m_AreaCenter = Vector3.zero;

        [Tooltip("Half-extents of the rectangular spawn area (X and Z).")]
        public Vector2 m_AreaHalfExtents = new Vector2(20f, 20f);

        [Tooltip("Height at which hearts are placed.")]
        public float m_SpawnHeight = 0.5f;

        [Header("Grid")]
        [Tooltip("Number of cells per axis. Total cells = GridSize * GridSize.")]
        public int m_GridSize = 6;

        [Header("Collision Avoidance")]
        [Tooltip("Radius used for the overlap check to avoid spawning inside objects.")]
        public float m_OverlapCheckRadius = 1.5f;

        [Tooltip("LayerMask for the overlap check. Include Default, obstacles, etc.")]
        public LayerMask m_ObstacleMask = Physics.DefaultRaycastLayers;

        [Header("Rules")]
        [Tooltip("Maximum number of hearts present on the map simultaneously.")]
        public int m_MaxHeartsOnMap = 2;

        [Tooltip("Seconds to wait before spawning a replacement after a heart is collected.")]
        public float m_RespawnDelay = 5f;

        private readonly List<NetworkObject> m_ActiveHearts = new List<NetworkObject>();

        private void Start()
        {
            // Only the server runs the spawn logic.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            for (int i = 0; i < m_MaxHeartsOnMap; i++)
                SpawnHeart();
        }

        private void SpawnHeart()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            Vector3? position = FindSpawnPosition();
            if (position == null)
            {
                Debug.LogWarning("[NetworkHeartSpawner] Could not find a valid spawn position.");
                return;
            }

            GameObject instance = Instantiate(m_HeartPrefab, position.Value, Quaternion.identity);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            netObj.Spawn(true);

            m_ActiveHearts.Add(netObj);
            StartCoroutine(WatchForDespawn(netObj));
        }

        /// <summary>
        /// Waits until the heart NetworkObject is gone (collected or destroyed),
        /// then schedules a replacement after RespawnDelay seconds.
        /// </summary>
        private IEnumerator WatchForDespawn(NetworkObject netObj)
        {
            yield return new WaitUntil(() => netObj == null || !netObj.IsSpawned);

            m_ActiveHearts.Remove(netObj);

            yield return new WaitForSeconds(m_RespawnDelay);

            SpawnHeart();
        }

        /// <summary>
        /// Procedural placement: shuffle grid cells, apply jitter within each cell,
        /// then validate with Physics.CheckSphere.
        /// The check is performed at a raised Y offset so it does not collide with
        /// the ground plane, which shares the Default layer with obstacles.
        /// </summary>
        private Vector3? FindSpawnPosition()
        {
            float cellWidth  = (m_AreaHalfExtents.x * 2f) / m_GridSize;
            float cellHeight = (m_AreaHalfExtents.y * 2f) / m_GridSize;

            int total = m_GridSize * m_GridSize;
            int[] indices = new int[total];
            for (int i = 0; i < total; i++)
                indices[i] = i;

            Shuffle(indices);

            foreach (int idx in indices)
            {
                int col = idx % m_GridSize;
                int row = idx / m_GridSize;

                float originX = m_AreaCenter.x - m_AreaHalfExtents.x + col * cellWidth;
                float originZ = m_AreaCenter.z - m_AreaHalfExtents.y + row * cellHeight;

                float inset = m_OverlapCheckRadius;
                float x = Random.Range(originX + inset, originX + cellWidth  - inset);
                float z = Random.Range(originZ + inset, originZ + cellHeight - inset);

                // Raise the check centre above the ground plane (Y ≈ 0) so the sphere
                // does not intersect with the ground collider, which is on the Default layer.
                Vector3 checkCentre = new Vector3(x, m_SpawnHeight + m_OverlapCheckRadius + 0.1f, z);

                if (!Physics.CheckSphere(checkCentre, m_OverlapCheckRadius, m_ObstacleMask))
                {
                    return new Vector3(x, m_SpawnHeight, z);
                }
            }

            return null;
        }

        private static void Shuffle(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
    /// Uses the same grid + jitter + overlap-check algorithm as HeartSpawner
    /// but spawns NetworkObjects so hearts appear on all clients.
    /// Keeps at most MaxHeartsOnMap hearts alive simultaneously.
    /// When a heart is collected (despawned by NetworkHeartPickup) a
    /// replacement is spawned after RespawnDelay seconds.
    /// </summary>
