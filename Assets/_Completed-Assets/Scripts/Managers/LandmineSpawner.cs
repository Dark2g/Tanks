using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Complete
{
    /// <summary>
    /// Offline procedural landmine spawner.
    /// Uses the same grid + jitter + overlap-check algorithm as HeartSpawner.
    /// Keeps at most MaxMinesOnMap mines on the map simultaneously.
    /// When a mine explodes (destroyed) a replacement is spawned after RespawnDelay.
    /// </summary>
    public class LandmineSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("Landmine prefab — must have LandmineExplosion component.")]
        public GameObject m_MinePrefab;

        [Header("Spawn Area")]
        [Tooltip("Centre of the playfield in world space.")]
        public Vector3 m_AreaCenter = Vector3.zero;

        [Tooltip("Half-extents of the rectangular spawn area (X and Z).")]
        public Vector2 m_AreaHalfExtents = new Vector2(20f, 20f);

        [Tooltip("Height at which mines are placed.")]
        public float m_SpawnHeight = 0f;

        [Header("Grid")]
        [Tooltip("Number of cells per axis. Total cells = GridSize * GridSize.")]
        public int m_GridSize = 6;

        [Header("Collision Avoidance")]
        [Tooltip("Radius used for the overlap check to avoid spawning inside objects.")]
        public float m_OverlapCheckRadius = 1.5f;

        [Tooltip("LayerMask for the overlap check. Include Default, obstacles, etc.")]
        public LayerMask m_ObstacleMask = Physics.DefaultRaycastLayers;

        [Header("Rules")]
        [Tooltip("Maximum number of mines present on the map simultaneously.")]
        public int m_MaxMinesOnMap = 3;

        [Tooltip("Seconds before a replacement mine is spawned after an explosion.")]
        public float m_RespawnDelay = 10f;

        private readonly List<GameObject> m_ActiveMines = new List<GameObject>();

        private void Start()
        {
            for (int i = 0; i < m_MaxMinesOnMap; i++)
                SpawnMine();
        }

        private void SpawnMine()
        {
            Vector3? position = FindSpawnPosition();
            if (position == null)
            {
                Debug.LogWarning("[LandmineSpawner] Could not find a valid spawn position.");
                return;
            }

            GameObject instance = Instantiate(m_MinePrefab, position.Value, Quaternion.identity);
            m_ActiveMines.Add(instance);
            StartCoroutine(WatchForDestruction(instance));
        }

        private IEnumerator WatchForDestruction(GameObject mine)
        {
            yield return new WaitUntil(() => mine == null);

            m_ActiveMines.Remove(mine);
            yield return new WaitForSeconds(m_RespawnDelay);

            SpawnMine();
        }

        /// <summary>
        /// Grid-based placement with jitter.
        /// Check centre is raised above the ground plane so the sphere does not
        /// intersect the ground collider (which shares the Default layer).
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

                Vector3 checkCentre = new Vector3(x, m_SpawnHeight + m_OverlapCheckRadius + 0.1f, z);

                if (!Physics.CheckSphere(checkCentre, m_OverlapCheckRadius, m_ObstacleMask))
                    return new Vector3(x, m_SpawnHeight, z);
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
