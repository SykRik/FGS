using System.Collections.Generic;
using UnityEngine;

namespace FGS
{
    [DisallowMultipleComponent]
    public sealed class EnemyManager : MonoSingleton<EnemyManager>
    {
        #region ===== Serialized Fields =====
        [Header("References")]
        [SerializeField] private PlayerController player = null;
        [SerializeField] private EnemyPooler pooler = null;
        [SerializeField] private Transform spawnPointRoot = null;

        [Header("Settings")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 1f;

        [Header("Debug (Auto-filled)")]
        [SerializeField] private List<Transform> spawnPoints = new();
        #endregion

        #region ===== Runtime Fields =====
        private float _spawnTimer;
        private bool _isRunning;
        #endregion

        #region ===== Properties =====
        public int TotalEnemiesKilled { get; private set; }
        public bool IsRunning => _isRunning;
        #endregion

        #region ===== Unity Methods =====
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (spawnPoints == null) spawnPoints = new List<Transform>(8);
            else spawnPoints.Clear();

            if (spawnPointRoot == null)
            {
                LogHelper.Warn(this, $"Missing {nameof(spawnPointRoot)}");
                return;
            }

            var all = spawnPointRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t == spawnPointRoot) continue;
                spawnPoints.Add(t);
            }
        }
#endif

        protected override void Awake()
        {
            base.Awake();

            if (pooler == null) LogHelper.Error(this, $"Missing {nameof(pooler)}");
            if (player == null) LogHelper.Error(this, $"Missing {nameof(player)}");
            if (spawnPoints == null || spawnPoints.Count == 0) LogHelper.Warn(this, "No spawn points configured.");
        }

        private void Update()
        {
            if (!_isRunning) return;
            if (player == null || player.CurrentHealth <= 0f) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;

            _spawnTimer = spawnInterval;
            SpawnEnemy(player.transform.position);
        }
        #endregion

        #region ===== Public Methods =====
        public void StartSpawning()
        {
            if (_isRunning) return;
            _isRunning = true;
            _spawnTimer = spawnInterval;
            LogHelper.Info(this, "Spawning started.");
        }

        public void StopSpawning()
        {
            if (!_isRunning) return;
            _isRunning = false;
            LogHelper.Info(this, "Spawning stopped.");
        }

        public void RegisterEnemyDeath() => TotalEnemiesKilled++;
        public void ResetKillCount() => TotalEnemiesKilled = 0;

        public void ReturnEnemyToPool(EnemyController enemy)
        {
            if (enemy == null || pooler == null) return;
            pooler.Return(enemy);
        }

        public void ReturnAllEnemiesToPool()
        {
            if (pooler == null) return;

            pooler.ForceReset();
        }

        public bool TryGetClosedEnemy(Vector3 position, float range, out EnemyController enemy)
        {
            enemy = null;

            if (!TryGetClosedEnemy(position, out var closedEnemy))
                return false;

            if (Vector3.Distance(position, closedEnemy.transform.position) > range)
                return false;

            enemy = closedEnemy;
            return true;
        }


        public bool TryGetClosedEnemy(Vector3 position, out EnemyController enemy)
        {
            enemy = null;

            if (pooler == null || pooler.Enemies == null || pooler.Enemies.Count == 0)
                return false;

            var closedEnemy = null as EnemyController;
            var minDistance = float.MaxValue;

            foreach (var e in pooler.Enemies)
            {
                if (e == null) 
                    continue;

                var distance = Vector3.Distance(position, e.transform.position);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closedEnemy = e;
                }
            }

            enemy = closedEnemy;
            return enemy != null;
        }


        #endregion

        #region ===== Private Methods =====

        private void SpawnEnemy(Vector3 center, float minDistance = 10f)
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                LogHelper.Warn(this, "Cannot spawn: spawn points missing.");
                return;
            }

            if (pooler == null || !pooler.TryRequest(out var enemy))
            {
                LogHelper.Warn(this, "Failed to get enemy from pool.");
                return;
            }

            if (!TryGetSpawnPoint(center, minDistance, out var spawnPoint))
            {
                LogHelper.Warn(this, "Failed to get spawn point.");
                return;
            }

            enemy.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            enemy.ResetState();
        }

        private bool TryGetSpawnPoint(out Transform point)
        {
            point = null;

            if (spawnPoints == null || spawnPoints.Count == 0) return false;

            point = spawnPoints[Random.Range(0, spawnPoints.Count)];
            return point != null;
        }

        private bool TryGetSpawnPoint(Vector3 center, float minDistance, out Transform point)
        {
            point = null;

            if (spawnPoints == null || spawnPoints.Count == 0) return false;

            var farthest = null as Transform;
            var maxDist = float.MinValue;
            var chosen = null as Transform;

            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;

                var dist = Vector3.Distance(center, sp.position);

                if (dist > minDistance)
                {
                    if (Random.value > 0.5f) 
                        chosen = sp;
                }

                if (dist > maxDist)
                {
                    maxDist = dist;
                    farthest = sp;
                }
            }

            point = chosen ?? farthest;
            return point != null;
        }
        #endregion
    }
}
