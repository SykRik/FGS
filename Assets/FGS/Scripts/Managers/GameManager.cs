using System;
using System.Collections.Generic;
using UnityEngine;

namespace FGS
{
    public enum GameState
    {
        Init,
        PreGame,
        Playing,
        GameOver
    }

    [Serializable]
    public class LevelConfig
    {
        public int A;
        public int B;
        public int C;
    }

    [DisallowMultipleComponent]
    public sealed class GameManager : MonoSingleton<GameManager>
    {
        #region === Serialized ===
        [Header("Game Config")]
        [SerializeField] private float preGameDuration = 10f;
        [SerializeField] private float gameDuration = 150f;
        [SerializeField] private float resultDuration = 10f;
        [SerializeField] private PlayerController playerController = null;

        [Header("Key/Door Objects")]
        [SerializeField] private GameObject keyObject = null;
        [SerializeField] private GameObject doorObject = null;

        [Header("Spawn Roots")]
        [SerializeField] private Transform keySpawnRoot = null;
        [SerializeField] private Transform doorSpawnRoot = null;

        [Header("Spawn Lists (Auto-filled)")]
        [SerializeField] private List<Transform> keySpawnPoints = new();
        [SerializeField] private List<Transform> doorSpawnPoints = new();

        [Header("Spawn Settings")]
        [SerializeField] private float spawnDoorMinDistance = 30f;
        [SerializeField] private float spawnKeyMinDistance = 30f;
        [SerializeField] private float keyRepositionInterval = 30f;
        #endregion

        #region === Runtime ===
        private GameState _state = GameState.Init;
        private float _time = 0f;
        private bool _isRunning = false;
        private bool _justEntered = false;
        private int _lastCountdown = -1;

        private bool _hasKey = false;
        private float _keyTimer = 0f;
        #endregion

        #region === Properties ===
        public PlayerController PlayerController => playerController;
        public float RemainingTime => Mathf.Max(0f, _time);
        public GameState CurrentState => _state;
        public int CurrentKill => EnemyManager.Instance?.TotalEnemiesKilled ?? 0;
        #endregion

        #region === Unity ===
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (keySpawnPoints == null) keySpawnPoints = new List<Transform>(8);
            else keySpawnPoints.Clear();

            if (doorSpawnPoints == null) doorSpawnPoints = new List<Transform>(8);
            else doorSpawnPoints.Clear();

            if (keySpawnRoot != null)
            {
                var all = keySpawnRoot.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null || t == keySpawnRoot) continue;
                    keySpawnPoints.Add(t);
                }
            }
            else
            {
                LogHelper.Warn(this, $"Missing {nameof(keySpawnRoot)}");
            }

            if (doorSpawnRoot != null)
            {
                var all = doorSpawnRoot.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null || t == doorSpawnRoot) continue;
                    doorSpawnPoints.Add(t);
                }
            }
            else
            {
                LogHelper.Warn(this, $"Missing {nameof(doorSpawnRoot)}");
            }
        }
#endif

        private void Start()
        {
            if (playerController == null) LogHelper.Error(this, $"Missing {nameof(playerController)}");
            if (keyObject == null) LogHelper.Warn(this, $"Missing {nameof(keyObject)}");
            if (doorObject == null) LogHelper.Warn(this, $"Missing {nameof(doorObject)}");
            ChangeState(GameState.PreGame);
        }

        private void Update()
        {
            if (!_isRunning) return;

            if (_justEntered)
            {
                _justEntered = false;
                return;
            }

            _time -= Time.deltaTime;

            switch (_state)
            {
                case GameState.PreGame: UpdatePreGame(); break;
                case GameState.Playing: UpdatePlaying(); break;
                case GameState.GameOver: UpdateResult(); break;
            }
        }
        #endregion

        #region === State Machine ===
        private void ChangeState(GameState next, bool isWin = false, string reason = "")
        {
            if (_state == next) return;

            LogHelper.Info(this, $"Changing state {_state} → {next}");
            ExitState(_state);
            _state = next;
            _justEntered = true;
            EnterState(next, isWin, reason);
        }

        private void EnterState(GameState s, bool isWin = false, string reason = "")
        {
            switch (s)
            {
                case GameState.PreGame:
                    LogHelper.Info(this, "PreGame started");
                    ResetPlayer();
                    EnemyManager.Instance?.ResetKillCount();
                    _hasKey = false;
                    SetTimer(preGameDuration);
                    _lastCountdown = -1;
                    _isRunning = true;

                    SetupKeyAtStart();
                    HideDoor();
                    break;

                case GameState.Playing:
                    LogHelper.Info(this, "Game Started");
                    SetTimer(gameDuration);
                    _keyTimer = keyRepositionInterval;
                    EnemyManager.Instance?.StartSpawning();
                    break;

                case GameState.GameOver:
                    LogHelper.Info(this, $"Game Over - {(isWin ? "Victory" : "Defeat")} - {reason}");
                    SetTimer(resultDuration);
                    _isRunning = true;
                    EnemyManager.Instance?.StopSpawning();
                    EnemyManager.Instance?.ReturnAllEnemiesToPool();
                    UIManager.Instance.ShowStatusMessage(isWin ? "Next Level" : "Game Over");
                    break;
            }
        }

        private void ExitState(GameState s)
        {
            if (s == GameState.GameOver)
                UIManager.Instance.HideStatusMessage();
        }
        #endregion

        #region === Updates ===
        private void UpdatePreGame()
        {
            if (_time <= 3f)
            {
                var countdown = Mathf.CeilToInt(_time);
                if (countdown != _lastCountdown && countdown > 0)
                {
                    _lastCountdown = countdown;
                    LogHelper.Info(this, $"Starting in: {countdown}");
                }
            }

            if (_time <= 0f)
                ChangeState(GameState.Playing);
        }

        private void UpdatePlaying()
        {
            if (playerController != null && playerController.CurrentHealth <= 0f)
            {
                ChangeState(GameState.GameOver, false, "Player Died");
                return;
            }

            if (_time <= 0f)
            {
                ChangeState(GameState.GameOver, false, "Time Expired");
                return;
            }

            if (!_hasKey && keyObject != null && keyObject.activeSelf)
            {
                _keyTimer -= Time.deltaTime;
                if (_keyTimer <= 0f)
                {
                    RepositionKey();
                    _keyTimer = keyRepositionInterval;
                }
            }
        }

        private void UpdateResult()
        {
            if (_time <= 0f)
                ChangeState(GameState.PreGame);
        }
        #endregion

        #region === Public Triggers ===

        public void OnKeyCollected()
        {
            if (_hasKey) return;
            _hasKey = true;

            if (keyObject != null) keyObject.SetActive(false);

            SpawnDoor();
        }

        public void OnDoorEntered()
        {
            if (_state != GameState.Playing) return;
            ChangeState(GameState.GameOver, true, "Reached Door");
        }
        #endregion

        #region === Key/Door Logic ===

        private void SetupKeyAtStart()
        {
            if (keyObject == null)
                return;
            if (playerController == null)
                return;

            if (TrySelectSpawnPoint(keySpawnPoints, playerController.transform.position, spawnKeyMinDistance, out var point))
            {
                keyObject.transform.SetPositionAndRotation(point.position, point.rotation);
                keyObject.SetActive(true);
            }
        }

        private void RepositionKey()
        {
            if (keyObject == null)
                return;
            if (playerController == null)
                return;

            if (TrySelectSpawnPoint(keySpawnPoints, playerController.transform.position, spawnKeyMinDistance, out var point))
            {
                keyObject.transform.SetPositionAndRotation(point.position, point.rotation);
            }
        }

        private void SpawnDoor()
        {
            if (doorObject == null)
                return;
            if (playerController == null)
                return;

            if (TrySelectSpawnPoint(doorSpawnPoints, playerController.transform.position, spawnDoorMinDistance, out var p))
            {
                doorObject.transform.SetPositionAndRotation(p.position, p.rotation);
                doorObject.SetActive(true);
            }
        }

        private void HideDoor()
        {
            if (doorObject == null) 
                return;
            doorObject.SetActive(false);
        }
        #endregion

        #region === Helpers ===
        private void SetTimer(float seconds)
        {
            _time = seconds;
        }

        private void ResetPlayer()
        {
            if (playerController == null) 
                return;

            playerController.ResetState();
            playerController.transform.position = Vector3.zero;
            playerController.gameObject.SetActive(true);
        }

        private bool TrySelectSpawnPoint(List<Transform> points, Vector3 center, float minDistance, out Transform point)
        {
            point = null;
            if (points == null || points.Count == 0) return false;

            var farthest = null as Transform;
            var maxDist = float.MinValue;

            var chosen = null as Transform;
            var count = 0;

            foreach (var sp in points)
            {
                if (sp == null) continue;

                var dist = Vector3.Distance(center, sp.position);

                if (dist > minDistance)
                {
                    count++;
                    if (UnityEngine.Random.Range(0, count) == 0) chosen = sp;
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
