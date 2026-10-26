using UnityEngine;

namespace SoftKitty
{
    public class DestroyLater : MonoBehaviour
    {
        public float WaitTime = 3F;

        
        void Update()
        {
            WaitTime -= Time.deltaTime;
            if (WaitTime <= 0F) Destroy(gameObject);
        }
    }
}
