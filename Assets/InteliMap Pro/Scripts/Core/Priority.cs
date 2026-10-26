using UnityEngine;

namespace InteliMapPro
{
    public class Priority
    {
        public void SetPriority(int priorityLevel, SparseSet prioritySet)
        {
            this.priorityLevel = priorityLevel;
            this.prioritySet = prioritySet;
        }

        public int priorityLevel = 0;
        public SparseSet prioritySet = null;
    }
}
