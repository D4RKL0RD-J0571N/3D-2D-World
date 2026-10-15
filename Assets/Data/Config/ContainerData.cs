namespace Data.Config
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "NewContainerData", menuName = "3D2DWorld/Entities/Container Data")]
    public class ContainerData : EntityData
    {
        public override string CategoryPrefix => "CONT";

        [Header("Container Properties")]
        public int SlotCount = 10;
        public bool IsLocked = false;

        // This could later include default loot tables or keys required.
    }
}
