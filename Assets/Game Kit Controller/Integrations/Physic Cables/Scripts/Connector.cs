using System.Collections;
using UnityEngine;

namespace HPhysic
{
    [RequireComponent (typeof (Rigidbody))]
    public class Connector : MonoBehaviour
    {
        public ConType ConnectionType { get; private set; } = ConType.Male;

        [SerializeField] private bool makeConnectionKinematic = false;
        private bool _wasConnectionKinematic;

        [SerializeField] private bool hideInteractableWhenIsConnected = false;

        [field: SerializeField] public Connector ConnectedTo { get; private set; }


        [Header ("Object to set")]
        public Transform connectionPoint;


        private FixedJoint _fixedJoint;
        public Rigidbody Rigidbody { get; private set; }

        public Vector3 ConnectionPosition => connectionPoint ? connectionPoint.position : transform.position;
        public Quaternion ConnectionRotation => connectionPoint ? connectionPoint.rotation : transform.rotation;
        public Quaternion RotationOffset => connectionPoint ? connectionPoint.localRotation : Quaternion.Euler (Vector3.zero);
        public Vector3 ConnectedOutOffset => connectionPoint ? connectionPoint.right : transform.right;

        public bool IsConnected => ConnectedTo != null;

        public enum ConType { Male, Female }

        private void Awake ()
        {
            Rigidbody = gameObject.GetComponent<Rigidbody> ();
        }

        private void Start ()
        {
            if (ConnectedTo != null) {
                Connector t = ConnectedTo;
                ConnectedTo = null;
                Connect (t);
            }
        }

        private void OnDisable () => Disconnect ();

        public void SetAsConnectedTo (Connector secondConnector)
        {
            ConnectedTo = secondConnector;

            _wasConnectionKinematic = secondConnector.Rigidbody.isKinematic;

            UpdateInteractableWhenIsConnected ();
        }

        public void Connect (Connector secondConnector)
        {
            if (secondConnector == null) {
                Debug.LogWarning ("Attempt to connect null");
                return;
            }

            if (IsConnected) {
                Disconnect (secondConnector);
            }

            secondConnector.transform.rotation = ConnectionRotation * secondConnector.RotationOffset;
            secondConnector.transform.position = ConnectionPosition - (secondConnector.ConnectionPosition - secondConnector.transform.position);

            _fixedJoint = gameObject.AddComponent<FixedJoint> ();

            _fixedJoint.connectedBody = secondConnector.Rigidbody;

            secondConnector.SetAsConnectedTo (this);

            _wasConnectionKinematic = secondConnector.Rigidbody.isKinematic;

            if (makeConnectionKinematic) {
                secondConnector.Rigidbody.isKinematic = true;
            }

            ConnectedTo = secondConnector;

            // disable outline on select
            UpdateInteractableWhenIsConnected ();
        }

        public void Disconnect (Connector onlyThis = null)
        {
            if (ConnectedTo == null || onlyThis != null && onlyThis != ConnectedTo) {
                return;
            }

            Destroy (_fixedJoint);

            // important to dont make recusrion
            Connector toDisconect = ConnectedTo;

            ConnectedTo = null;

            if (makeConnectionKinematic) {
                toDisconect.Rigidbody.isKinematic = _wasConnectionKinematic;
            }

            toDisconect.Disconnect (this);

            // enable outline on select
            UpdateInteractableWhenIsConnected ();
        }

        private void UpdateInteractableWhenIsConnected ()
        {
            if (hideInteractableWhenIsConnected) {
                if (TryGetComponent (out Collider collider)) {
                    collider.enabled = !IsConnected;
                }
            }
        }

        public bool CanConnect (Connector secondConnector) =>
            this != secondConnector
            && !this.IsConnected && !secondConnector.IsConnected
            && this.ConnectionType != secondConnector.ConnectionType;
    }
}