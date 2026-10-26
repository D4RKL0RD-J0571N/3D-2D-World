#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using MemoryPack;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
	public class CrystalSaveTestingGuideTests
	{
		[Test]
		public void CustomISaveablePayloadRoundTripsWithoutSaveManager()
		{
			GameObject go = new GameObject("Crystal Save ISaveable Test");
			var saveable = go.AddComponent<TestHealthSaveable>();

			try
			{
				saveable.health = 37;
				byte[] payload = saveable.SaveData();

				saveable.health = 1;
				saveable.LoadData(payload);

				Assert.AreEqual(37, saveable.health);
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		private sealed class TestHealthSaveable : MonoBehaviour, ISaveable
		{
			public int health = 100;

			public string UniqueIdentifier => "CrystalSaveTestingGuideTests_TestHealth";

			public byte[] SaveData()
			{
				return SaveDataSerializer.Instance.Serialize(new CrystalSaveTestingGuideTestHealthData
				{
					health = health
				});
			}

			public void LoadData(byte[] data)
			{
				CrystalSaveTestingGuideTestHealthData restored = SaveDataSerializer.Instance.Deserialize<CrystalSaveTestingGuideTestHealthData>(data);
				health = restored.health;
			}
		}
	}

	[MemoryPackable]
	public partial class CrystalSaveTestingGuideTestHealthData
	{
		public int health;
	}
}
#endif
