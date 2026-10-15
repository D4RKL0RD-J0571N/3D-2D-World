namespace Domain.Grid
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "BlockRegistry",
        menuName = "3D2DWorld/Entities/Grid/Block Registry"
    )]
    public sealed class BlockRegistry : ScriptableObject
    {
        [SerializeField]
        private List<BlockDefinition> _blocks = new();

        private Dictionary<ushort, BlockDefinition> _blockMap;
        private bool _initialized;

        private void OnEnable()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized && _blockMap != null)
                return;

            _blockMap = new Dictionary<ushort, BlockDefinition>();

            foreach (var block in _blocks.Where(b => b != null))
            {
                if (string.IsNullOrWhiteSpace(block.Id))
                {
                    Debug.LogError(
                        $"[BlockRegistry] Block '{block.DisplayName}' has no ID."
                    );

                    continue;
                }

                if (!_blockMap.TryAdd(block.RuntimeId, block))
                {
                    Debug.LogError(
                        $"[BlockRegistry] Duplicate RuntimeId {block.RuntimeId} " +
                        $"found on '{block.DisplayName}' (ID: {block.Id})."
                    );

                    continue;
                }
            }

            _initialized = true;

            Debug.Log(
                $"[BlockRegistry] Initialized with {_blockMap.Count} blocks."
            );
        }

        public BlockDefinition GetBlock(ushort id)
        {
            Initialize();

            if (_blockMap == null)
                return null;

            _blockMap.TryGetValue(id, out var definition);
            return definition;
        }

        public List<BlockDefinition> GetBlocksByRole(BlockRole role)
        {
            Initialize();

            return _blocks
                .Where(b =>
                    b != null &&
                    b.Roles != null &&
                    b.Roles.Contains(role))
                .ToList();
        }

        public List<BlockDefinition> GetAllBlocks()
        {
            Initialize();
            return _blocks.Where(b => b != null).ToList();
        }
    }
}