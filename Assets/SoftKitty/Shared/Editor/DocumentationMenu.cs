using UnityEditor;
using UnityEngine;
using System.Collections;

namespace SoftKitty
{
	public class DocumentationMenu : ScriptableWizard
	{

		[MenuItem("Window/Soft Kitty/Documentation")]

		public static void CreateWizard()
		{
			Application.OpenURL("https://www.soft-kitty.com/");
		}

	}


}
