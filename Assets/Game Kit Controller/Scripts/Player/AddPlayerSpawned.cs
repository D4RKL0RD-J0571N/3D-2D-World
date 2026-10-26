using UnityEngine;

namespace GameKitController.Player
{
	public class AddPlayerSpawned : MonoBehaviour
	{
		private playerCharactersManager _playerCharactersManager;

		private void Start()
		{
			_playerCharactersManager = GKC_Utils.findObjectByComponent<playerCharactersManager>();

			AddNewPlayerSpawned(gameObject);
		}

		public void AddNewPlayerSpawned(GameObject gameObj)
		{
			if (gameObj == null) {
				gameObj = gameObject;
			}

			_playerCharactersManager.addNewPlayerSpawned(gameObj);
		}
	}
}
