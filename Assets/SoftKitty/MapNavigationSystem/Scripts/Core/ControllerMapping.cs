using UnityEngine;


namespace SoftKitty.MasterNavigationMap
{
    /// <summary>
    /// Inherit your script from ControllerMapping, replace the one attached on the map interface prefab  
    /// and assign it to the 'ControllerMappingScript' slot of the <MapInteractive> component.
    /// don't forget to assign 'VirtualCursor' RectTransform.
    /// </summary>
    public class ControllerMapping : MonoBehaviour
    {
        /// <summary>
        /// Enable/Disable Virtual Cursor.
        /// </summary>
        public bool UseVirtualCursor = false;
        /// <summary>
        /// The image represent the virtual cursor
        /// </summary>
        public RectTransform VirtualCursor;

        private void Update()
        {
            if (UseVirtualCursor && VirtualCursor)
            {
                ControlVirtualCursor();
            }
           
        }


        /// <summary>
        /// When map gets enable, this function will be called.
        /// </summary>
        /// <param name="_type"></param>
        public virtual void OnMapInit(MapInteractive.MapTypes _type)
        {

        }

        /// <summary>
        /// Set the visibility of the Virtual Cursor.
        /// </summary>
        /// <param name="_visible"></param>
        public virtual void SetVirtualCursorVisible(bool _visible)
        {
            VirtualCursor.gameObject.SetActive(_visible);
        }

        /// <summary>
        /// Override the position of the Virtual Cursor.
        /// </summary>
        /// <param name="_pos"></param>
        public virtual void SetVirtualCursorPosition(Vector3 _pos)
        {
            VirtualCursor.position=_pos;
        }

        /// <summary>
        /// Control the Virtual Cursor in your way.
        /// </summary>
        public virtual void ControlVirtualCursor()
        {
            //Put your code to control the VirtualCursor here.
        }

        /// <summary>
        /// Retrieve whether a rect transform is overlapping with the Virtual Cursor
        /// </summary>
        /// <param name="_pos"></param>
        /// <param name="_size"></param>
        /// <returns></returns>
        public virtual bool isVirtualCursorOver(RectTransform _rectTrans)
        {
            if (VirtualCursor == null || !UseVirtualCursor) return false;
            return (Vector3.Distance(VirtualCursor.transform.position, _rectTrans.position) < Mathf.Max(_rectTrans.sizeDelta.x, _rectTrans.sizeDelta.y));
        }

        /// <summary>
        /// Retrieve the position of the mouse pointer, if Virtual Cursor is enabled, return the position of the Virtual Cursor insteaded.
        /// </summary>
        /// <returns></returns>
        public virtual Vector3 GetPointerPosition()
        {
            if (UseVirtualCursor && VirtualCursor != null) return VirtualCursor.position;
            return InputProxy.mousePosition;
        }


        /// <summary>
        /// Retrieve whether the zoom in button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetZoomInKey()
        {
            return false;
        }

        /// <summary>
        /// Retrieve whether the zoom out button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetZoomOutKey()
        {
            return false;
        }

        /// <summary>
        /// Retrieve whether the place map marker button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetPlaceMarkerKey()
        {
            return InputProxy.GetMouseButtonUp((int)MapManager.CurrentSetting.MapMarkerButton) && InputProxy.GetKey(MapManager.CurrentSetting.MapMarkerKey);
        }

        /// <summary>
        /// Retrieve the hint text for place map marker button, override your controller button name here.
        /// </summary>
        /// <returns></returns>
        public virtual string GetPlaceMarkerKeyHint()
        {
            return "Hold " + MapManager.CurrentSetting.MapMarkerKey.ToString().Replace("_", " ") + " Key & Press " + MapManager.CurrentSetting.MapMarkerButton.ToString().Replace("_", " ") + " to place Marker";
        }

        /// <summary>
        /// Retrieve the hint text for place map custom marker button, override your controller button name here.
        /// </summary>
        /// <returns></returns>
        public virtual string GetPlaceCustomMarkerHint()
        {
            return "Press " + MapManager.CurrentSetting.CustomMarkerConfirmButton.ToString().Replace("_", " ") + "to Confirm, " + MapManager.CurrentSetting.CustomMarkerCancelButton.ToString().Replace("_", " ") + " to Cancel";
        }

        /// <summary>
        /// Retrieve whether the remove map marker button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetRemoveMarkerKey()
        {
            return false;
        }

        /// <summary>
        /// Retrieve whether the custom map marker confirm button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetConfirmCustomMarkerKey()
        {
            return InputProxy.GetMouseButtonUp((int)MapManager.CurrentSetting.CustomMarkerConfirmButton) ;
        }

        /// <summary>
        /// Retrieve whether the custom map marker cancel button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetCancelCustomMarkerKey()
        {
            return InputProxy.GetMouseButtonUp((int)MapManager.CurrentSetting.CustomMarkerCancelButton) || InputProxy.GetKeyDown(MapManager.CurrentSetting.CustomMarkerCancelKey);
        }

        /// <summary>
        /// Retrieve the hint text for remove map marker button, override your controller button name here.
        /// </summary>
        /// <returns></returns>
        public virtual string GetRemoveMarkerKeyHint()
        {
            return "Press " + MapManager.CurrentSetting.MapMarkerButton.ToString().Replace("_", " ") + " to remove Marker";
        }

        /// <summary>
        /// Retrieve whether the navigate button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetNavigateKey()
        {
            return InputProxy.GetMouseButtonUp((int)MapManager.CurrentSetting.StartNavigationMouseButton);
        }

        /// <summary>
        /// Retrieve the hint text for navigate button, override your controller button name here.
        /// </summary>
        /// <returns></returns>
        public virtual string GetNavigateKeyHint()
        {
            return "Press " + MapManager.CurrentSetting.StartNavigationMouseButton.ToString().Replace("_", " ") + " to Navigate";
        }

        /// <summary>
        /// Retrieve whether the scroll map up button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetScrollUpKey()
        {
            return InputProxy.GetKey(MapManager.CurrentSetting.MapScrollUpKey);
        }

        /// <summary>
        /// Retrieve whether the scroll map down button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetScrollDownKey()
        {
            return InputProxy.GetKey(MapManager.CurrentSetting.MapScrollDownKey);
        }

        /// <summary>
        /// Retrieve whether the scroll map left button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetScrollLeftKey()
        {
            return InputProxy.GetKey(MapManager.CurrentSetting.MapScrollLeftKey);
        }

        /// <summary>
        /// Retrieve whether the scroll map right button is pressed, override your controller button press code here.
        /// </summary>
        /// <returns></returns>
        public virtual bool GetScrollRightKey()
        {
            return InputProxy.GetKey(MapManager.CurrentSetting.MapScrollRightKey);
        }
    }
}