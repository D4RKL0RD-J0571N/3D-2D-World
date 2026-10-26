using UnityEngine;
using UnityEngine.EventSystems;

namespace SoftKitty.MasterNavigationMap
{
    public class PlayerControl : MonoBehaviour
    {
        CharacterController mController;
        public Transform Cam;
        public float Speed = 10F;
        Vector3 velocity;
        Vector3 forward;
        Quaternion rotation;
        float upForce = -9.8F;
        bool isMoving = false;
        bool sprint = false;
        float h;
        float v;

        void Start()
        {
            mController = GetComponent<CharacterController>();
            forward = transform.forward;
        }

        
        void Update()
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                h = Input.GetAxis("Horizontal");
                v = Input.GetAxis("Vertical");
                sprint = Input.GetKey(KeyCode.LeftShift);
                if (Input.GetKeyDown(KeyCode.Space) && mController.isGrounded) Jump();
            }
            else
            {
                h = 0F;
                v = 0F;
                sprint = false;
            }
            isMoving = (h != 0F || v != 0F);
            if(isMoving)forward = (IngoreY(Cam.right) * h + IngoreY(Cam.forward) * v).normalized;
            velocity = Vector3.Lerp(velocity, isMoving? Speed* IngoreY(transform.forward)*(sprint?2.5F:1F) :Vector3.zero, Time.deltaTime*10F);
            upForce =Mathf.Clamp(Mathf.MoveTowards(upForce, -100F , Time.deltaTime * 98F), mController.isGrounded ? -9.8F :-100F,100F);
            if(forward!=Vector3.zero) rotation = Quaternion.LookRotation(forward, Vector3.up);
            
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, Time.deltaTime * 200F);
        }

        private void FixedUpdate()
        {
            mController.Move((velocity+Vector3.up* upForce)*Time.fixedDeltaTime);

        }

        public void Jump()
        {
            upForce = sprint?40F: 35F;
        }

        private Vector3 IngoreY(Vector3 _dir) {
            return new Vector3(_dir.x,0F,_dir.z);
        }

    }
}
