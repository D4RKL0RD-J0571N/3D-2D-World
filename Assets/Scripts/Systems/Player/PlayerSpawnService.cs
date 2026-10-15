namespace Systems.Player
{
    using Core;
    using Gameplay.World;
    using UnityEngine;

    public sealed class PlayerSpawnService : IGameService
    {
        public void Initialize()
        {
            GameEventManager.AddListener<PlayerSpawnRequestedEvent>(OnPlayerSpawnRequested);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<PlayerSpawnRequestedEvent>(OnPlayerSpawnRequested);
        }

        private void OnPlayerSpawnRequested(PlayerSpawnRequestedEvent gameEvent)
        {
            var player = GameObject.FindWithTag("Player"); // TODO: Later we can try to access the player using the Entity Registry
            if (player != null && player.TryGetComponent<CharacterController>(out var cc))
            {
                cc.enabled = false;
                player.transform.position = gameEvent.SpawnPosition;
                cc.enabled = true;
            }
        }
    }
}
