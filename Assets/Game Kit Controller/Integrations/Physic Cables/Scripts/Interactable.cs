using UnityEngine;

namespace HInteractions
{
    [DisallowMultipleComponent]
    public class Interactable : MonoBehaviour
    {
        public bool ShowPointerOnInterract { get; private set; } = true;

        public bool IsSelected { get; private set; }

        protected virtual void Awake ()
        {
            Deselect ();
        }

        public virtual void Select ()
        {
            IsSelected = true;
        }

        public virtual void Deselect ()
        {
            IsSelected = false;
        }
    }
}