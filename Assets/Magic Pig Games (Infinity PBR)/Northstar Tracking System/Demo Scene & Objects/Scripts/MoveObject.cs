using UnityEngine;

namespace MagicPigGames.Northstar
{
    public class MoveObject : MonoBehaviour
    {
        public float moveInterval = 6f; // X seconds
        public float moveDuration = 4f; // Y seconds
        public float range = 10f; // Range in meters

        private Vector3 targetPosition;
        private float timeSinceLastMove = 0f;
        private float lerpTime = 0f;
        private bool isMoving = false;

        private Rigidbody rb; // The Rigidbody component       

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            ScheduleNextMove();
        }

        private void FixedUpdate()
        {
            if (!isMoving && timeSinceLastMove >= moveInterval)
            {
                ScheduleNextMove();
                timeSinceLastMove = 0f;
            }

            if (isMoving)
            {
                lerpTime += Time.fixedDeltaTime / moveDuration;
                Vector3 newVelocity = Vector3.Lerp(rb.linearVelocity, (targetPosition - transform.position).normalized, lerpTime);
                // We only adjust the x and z velocities to allow gravity to manage the y-axis
                rb.linearVelocity = new Vector3(newVelocity.x, rb.linearVelocity.y, newVelocity.z);

                if (lerpTime >= 1f)
                {
                    isMoving = false;
                    lerpTime = 0f;
                }
            }
            else
            {
                timeSinceLastMove += Time.fixedDeltaTime;
            }
        }

        private void ScheduleNextMove()
        {
            targetPosition = transform.position + new Vector3(Random.Range(-range, range), 0, Random.Range(-range, range));
            isMoving = true;
        }
    }
}