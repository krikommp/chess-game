using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Player-only MVP round loop: sort by initiative, let unfinished players act,
    /// Space marks the selected player done, and a new round refills AP for everyone.
    /// </summary>
    public class CombatRoundManager : MonoBehaviour
    {
        public event Action SelectedPlayerChanged;
        public event Action RoundChanged;

        [Header("Refs")]
        [SerializeField] private MoveInputController moveInput;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private List<Player1Controller> players = new List<Player1Controller>();

        [Header("Controls")]
        [SerializeField] private KeyCode endTurnKey = KeyCode.Space;

        [Header("MVP Limits")]
        [SerializeField, Range(1, 4)] private int maxPartySize = 4;

        private readonly List<Player1Controller> _turnOrder = new List<Player1Controller>();

        public IReadOnlyList<Player1Controller> TurnOrder => _turnOrder;
        public Player1Controller SelectedPlayer { get; private set; }
        public int RoundCount { get; private set; }

        private void Awake()
        {
            if (moveInput == null) moveInput = FindObjectOfType<MoveInputController>();
            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
            CachePlayersIfNeeded();
        }

        private void Start()
        {
            StartCombat();
        }

        private void Update()
        {
            if (Input.GetKeyDown(endTurnKey))
            {
                TryEndSelectedPlayerRound();
            }

            for (int i = 0; i < _turnOrder.Count && i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    TrySelectPlayer(_turnOrder[i]);
                }
            }
        }

        public void StartCombat()
        {
            CachePlayersIfNeeded();
            BuildTurnOrder();
            RoundCount = 0;
            StartNextRound();
        }

        public bool TrySelectPlayer(Player1Controller player)
        {
            if (player == null || !_turnOrder.Contains(player) || player.HasEndedRound)
            {
                return false;
            }

            if (SelectedPlayer != null && SelectedPlayer != player)
            {
                SelectedPlayer.SetVisualState(PlayerVisualState.Default);
            }

            SelectedPlayer = player;
            SelectedPlayer.SetVisualState(PlayerVisualState.Selected);

            if (moveInput != null)
            {
                moveInput.SetPlayer(player);
            }

            if (cameraController != null)
            {
                cameraController.target = player.transform;
            }

            SelectedPlayerChanged?.Invoke();
            return true;
        }

        public bool TryEndSelectedPlayerRound()
        {
            if (SelectedPlayer == null || !SelectedPlayer.TryEndRound())
            {
                return false;
            }

            SelectNextAvailableOrStartNextRound();
            return true;
        }

        private void CachePlayersIfNeeded()
        {
            players.RemoveAll(player => player == null);
            if (players.Count > 0) return;

            players.AddRange(FindObjectsOfType<Player1Controller>());
        }

        private void BuildTurnOrder()
        {
            _turnOrder.Clear();
            _turnOrder.AddRange(players
                .Where(player => player != null && player.gameObject.activeInHierarchy)
                .OrderByDescending(player => player.Initiative)
                // TODO(Q-0003): replace party slot tie-breaker after initiative ties are resolved.
                .ThenBy(player => player.PartySlot)
                .Take(maxPartySize));
        }

        private void StartNextRound()
        {
            RoundCount++;
            foreach (Player1Controller player in _turnOrder)
            {
                player.BeginRound();
            }

            RoundChanged?.Invoke();
            SelectNextAvailableOrStartNextRound();
        }

        private void SelectNextAvailableOrStartNextRound()
        {
            Player1Controller next = _turnOrder.FirstOrDefault(player => !player.HasEndedRound);
            if (next != null)
            {
                TrySelectPlayer(next);
                return;
            }

            StartNextRound();
        }
    }
}
