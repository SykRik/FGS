using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FGS
{
    public class EnemyManager : MonoSingleton<EnemyManager>
    {







        #region ===== Serialized Fields =====

        [SerializeField] private PlayerController player = null;
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private EnemyPooler pooler = null;
        [SerializeField] private Transform spawnPointRoot;
        [SerializeField] private List<Transform> spawnPoints;

        #endregion

        #region ===== Runtime Fields =====

        private float spawnTimer = 0f;
        private bool isRunning = false;

        #endregion

        #region ===== Properties =====

        public int TotalEnemiesKilled { get; private set; }

        #endregion

        #region ===== Unity Methods =====

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (spawnPoints == null)
            {
                spawnPoints = new List<Transform>();
            }
            else
            {
                spawnPoints.Clear();
            }

            if (spawnPointRoot == null)
            {
                Debug.LogWarning($"Missing {nameof(spawnPointRoot)}");
            }
            else
            {
                foreach (Transform spawnPoint in spawnPointRoot)
                {
                    if (spawnPoint == null)
                        continue;
                    spawnPoints.Add(spawnPoint);
                }
            }
#endif
        }

        protected override void Awake()
        {
            base.Awake();
        }

        private void Update()
        {
            if (isRunning && player.CurrentHealth > 0f)
            {
                spawnTimer -= Time.deltaTime;

                if (spawnTimer > 0f) 
                    return;
                
                spawnTimer = spawnInterval;

                SpawnEnemy();
            }
        }

        #endregion

        #region ===== Public Methods =====

        public void Spawn(EnemyController enemyController)
        {
            var index = Random.Range(0, int.MaxValue) % spawnPoints.Count;
            var point = spawnPoints[index];

            enemyController.transform.SetPositionAndRotation(point.position, point.rotation);

            if (enemyController.TryGetComponent(out EnemyController enemyComp))
            {
                enemyComp.ResetState();
            }
        }

        public void StartSpawning()
        {
            isRunning = true;
            spawnTimer = spawnInterval;
            Debug.Log("[EnemyManager] Spawning started.");
        }

        public void StopSpawning()
        {
            isRunning = false;
            Debug.Log("[EnemyManager] Spawning stopped.");
        }

        public bool TryGetClosedEnemy(Vector3 position, float range, out EnemyController enemyController)
        {
            if (TryGetEnemyClosed(pooler, position, range, out enemyController))
                return true;

            enemyController = null;
            return false;
        }

        public void RegisterEnemyDeath()
        {
            TotalEnemiesKilled++;
        }

        public void ResetKillCount()
        {
            TotalEnemiesKilled = 0;
        }

        public void ReturnEnemyToPool(EnemyController enemy)
        {
            if (enemy == null) return;

            pooler.Return(enemy);
        }

        public void ReturnAllAliveEnemiesToPool()
        {
            ReturnAliveEnemiesInPool(pooler);
        }

        #endregion

        #region ===== Private Methods =====

        private void SpawnEnemy()
        {
            if (pooler.TryRequest(out var enemy))
            {
                Spawn(enemy);
            }
            else
            {
                Debug.LogWarning($"[EnemyManager] Failed to get enemy from pool.");
            }
        }

        private void ReturnAliveEnemiesInPool(EnemyPooler pooler)
        {
            var queueEnemy = new Queue<EnemyController>(pooler.Enemies);
            while (queueEnemy.Count > 0)
            {
                var enemy = queueEnemy.Dequeue();
                if (enemy != null && enemy.CurrentHealth > 0)
                {
                    ReturnEnemyToPool(enemy);
                }
            }
        }

        private bool TryGetEnemyClosed(EnemyPooler pooler, Vector3 position, float range, out EnemyController enemyController)
        {
            enemyController = pooler.Enemies
                .Where(x => x != null && x.CurrentHealth > 0 && Vector3.Distance(x.transform.position, position) < range)
                .OrderBy(x => Vector3.Distance(x.transform.position, position))
                .FirstOrDefault();

            return enemyController != null;
        }

        #endregion
    }
}
