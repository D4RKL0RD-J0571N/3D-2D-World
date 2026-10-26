using UnityEngine;
using System.Security.Cryptography;
using System.Text;


namespace SoftKitty
{
    /// <summary>
    /// This is a ScriptableObject data class that could assigned to Soft Kitty database in ProjectSettings > SoftKitty > Data Settings
    /// You could inherit from this class to make your own data object.
    /// </summary>
    public class DataObject : ScriptableObject
    {
        /// <summary>
        /// The unique hash of the data content.
        /// </summary>
        [HideInInspector]
        public string Hash = "";
        /// <summary>
        /// This will be displayed as the name of the Data in the Data Object List.
        /// </summary>
        /// <returns></returns>
        public virtual string DataName(){return "BaseObject";}
        /// <summary>
        /// This string must be exactly same as your class name.
        /// </summary>
        /// <returns></returns>
        public virtual string TypeString() { return "DataObject"; }

        private void OnValidate()
        {
            GenerateUniqueHash();
        }
        /// <summary>
        /// Call this method in Editor mode whenever the data is modified to generate a unique hash string based on the data content.
        /// Other systems that access this data object use the hash to determine whether they need to update their content in response to your changes..
        /// </summary>
        public void GenerateUniqueHash()
        {
            if (GetDataCount() <= 0)
            {
                Hash = "";
                return;
            }
            string json = GetDataJson();
            byte[] dataBytes = Encoding.UTF8.GetBytes(json);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(dataBytes);
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    builder.Append(hashBytes[i].ToString("x2"));
                }
                Hash = builder.ToString().Substring(0, 32);
            }
        }
        /// <summary>
        /// Override this function with a json string of your data content.
        /// </summary>
        /// <returns></returns>
        public virtual string GetDataJson()
        {
            return "";
        }
        /// <summary>
        /// Override this function with the count number of your data content.
        /// </summary>
        /// <returns></returns>
        public virtual int GetDataCount()
        {
            return 0;
        }

       

    }

}
