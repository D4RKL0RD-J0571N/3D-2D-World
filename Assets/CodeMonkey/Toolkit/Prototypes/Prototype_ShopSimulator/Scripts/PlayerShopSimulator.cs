using CodeMonkey.Toolkit.TFirstPersonController;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.ShopSimulatorDemo {

    public class PlayerShopSimulator : MonoBehaviour {


        public static PlayerShopSimulator Instance { get; private set; }


        [SerializeField] private Transform carryingObjectParentTransform;


        private PlayerInteractLookAt playerInteractLookAt;
        private FirstPersonController firstPersonController;
        private ContainerBox carryingContainerBox;


        private void Awake() {
            Instance = this;

            playerInteractLookAt = GetComponent<PlayerInteractLookAt>();
            firstPersonController = GetComponent<FirstPersonController>();
        }

        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isLeftMouseDown = Mouse.current.leftButton.wasPressedThisFrame;
#else
            bool isLeftMouseDown = Input.GetMouseButtonDown(0);
#endif
            if (isLeftMouseDown) {
                IInteractable interactable = playerInteractLookAt.GetInteractableObject();
                if (interactable != null) {
                    if (interactable.CanDoInteractAction(IInteractable.InteractAction.Stock)) {
                        interactable.Interact(IInteractable.InteractAction.Stock, transform);
                    }
                    if (interactable.CanDoInteractAction(IInteractable.InteractAction.ScanObject)) {
                        interactable.Interact(IInteractable.InteractAction.ScanObject, transform);
                    }
                }
            }
#if ENABLE_INPUT_SYSTEM
            bool isRightMouseDown = Mouse.current.rightButton.wasPressedThisFrame;
#else
            bool isRightMouseDown = Input.GetMouseButtonDown(1);
#endif
            if (isRightMouseDown) {
                IInteractable interactable = playerInteractLookAt.GetInteractableObject();
                if (interactable != null) {
                    interactable.Interact(IInteractable.InteractAction.Unstock, transform);
                }
            }

#if ENABLE_INPUT_SYSTEM
            bool isEKeyDown = Keyboard.current.eKey.wasPressedThisFrame;
#else
            bool isEKeyDown = Input.GetKeyDown(KeyCode.E);
#endif
            if (isEKeyDown) {
                if (!IsCarryingContainerBox()) {
                    // Not carrying anything, pick up box?
                    IInteractable interactable = playerInteractLookAt.GetInteractableObject();
                    if (interactable != null) {
                        if (interactable.CanDoInteractAction(IInteractable.InteractAction.PickUpBox)) {
                            interactable.Interact(IInteractable.InteractAction.PickUpBox, transform);
                        }
                    }
                } else {
                    // Carrying something, drop it
                    ClearCarryingContainerBox();
                }
            }
#if ENABLE_INPUT_SYSTEM
            bool isRKeyDown = Keyboard.current.rKey.wasPressedThisFrame;
#else
            bool isRKeyDown = Input.GetKeyDown(KeyCode.R);
#endif
            if (isRKeyDown) {
                IInteractable interactable = playerInteractLookAt.GetInteractableObject();
                if (interactable != null) {
                    interactable.Interact(IInteractable.InteractAction.ChangePrice, transform);
                }
            }
        }

        public void Freeze() {
            enabled = false;
            playerInteractLookAt.enabled = false;
            firstPersonController.UnlockMouse();
            firstPersonController.Disable();
        }

        public void Unfreeze() {
            enabled = true;
            playerInteractLookAt.enabled = true;
            firstPersonController.LockMouse();
            firstPersonController.Enable();
        }

        public bool IsCarryingContainerBox() {
            return carryingContainerBox != null;
        }

        public ContainerBox GetCarryingContainerBox() {
            return carryingContainerBox;
        }

        public void SetCarryingContainerBox(ContainerBox containerBox) {
            carryingContainerBox = containerBox;
            carryingContainerBox.GetTransform().parent = carryingObjectParentTransform;
            carryingContainerBox.GetTransform().localPosition = Vector3.zero;
            carryingContainerBox.SetState(ContainerBox.State.PickedUp);
        }

        public void ClearCarryingContainerBox() {
            if (carryingContainerBox == null) {
                // Not carrying anything
                return;
            }
            carryingContainerBox.SetState(ContainerBox.State.Ground);
            carryingContainerBox.GetTransform().parent = null;
            carryingContainerBox.GetTransform().position = new Vector3(
                carryingContainerBox.GetTransform().position.x,
                0f,
                carryingContainerBox.GetTransform().position.z
            );
            carryingContainerBox = null;
        }

    }

}