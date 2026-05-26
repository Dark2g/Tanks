using System.Collections.Generic;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Offline procedural heart spawner.
    ///
    /// Algorithm:
    ///   1. Subdivide the play area into an NxN grid.
    ///   2. Shuffle the grid cells randomly.
    ///   3. For each candidate cell, apply a random jitter within the cell.
    ///   4. Reject positions that overlap existing colliders (Physics.OverlapSphere).
    ///   5. Spawn up to MaxHeartsOnMap hearts. When a heart is collected a
    ///      replacement is spawned after RespawnDelay seconds.
    /// </summary>
    public class HeartSpawner : MonoBehaviour
    {
        [Header("Prefab")]
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

        private readonly List<HeartPickup> m_ActiveHearts = new List<HeartPickup>();
        private int m_PendingRespawns;

        private void Start()
        {
            for (int i = 0; i < m_MaxHeartsOnMap; i++)
                SpawnHeart();
        }

        private void SpawnHeart()
        {
            Vector3? position = FindSpawnPosition();
            if (position == null)
            {
                Debug.LogWarning("[HeartSpawner] Could not find a valid spawn position.");
                return;
            }

            GameObject instance = Instantiate(m_HeartPrefab, position.Value, Quaternion.identity);
            HeartPickup pickup = instance.GetComponent<HeartPickup>();
            if (pickup != null)
            {
                pickup.OnCollected += OnHeartCollected;
                m_ActiveHearts.Add(pickup);
            }
        }

        private void OnHeartCollected(HeartPickup heart)
        {
            m_ActiveHearts.Remove(heart);
            Invoke(nameof(RespawnHeart), m_RespawnDelay);
        }

        private void RespawnHeart()
        {
            SpawnHeart();
        }

        /// <summary>
        /// Procedural placement: shuffle grid cells, jitter within each cell,
        /// then validate with an overlap sphere.
        /// The check is performed at a raised Y offset so it does not collide with
        /// the ground plane, which shares the Default layer with obstacles.
        /// </summary>
        private Vector3? FindSpawnPosition()
        {
            float cellWidth  = (m_AreaHalfExtents.x * 2f) / m_GridSize;
            float cellHeight = (m_AreaHalfExtents.y * 2f) / m_GridSize;

            // Build and shuffle cell indices.
            int total = m_GridSize * m_GridSize;
            int[] indices = new int[total];
            for (int i = 0; i < total; i++)
                indices[i] = i;

            Shuffle(indices);

            foreach (int idx in indices)
            {
                int col = idx % m_GridSize;
                int row = idx / m_GridSize;

                // Bottom-left corner of this cell.
                float originX = m_AreaCenter.x - m_AreaHalfExtents.x + col * cellWidth;
                float originZ = m_AreaCenter.z - m_AreaHalfExtents.y + row * cellHeight;

                // Random jitter inside the cell (with a small inset to avoid edges).
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
