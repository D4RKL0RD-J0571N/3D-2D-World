#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.EditorTests;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	public sealed class CrystalSaveEditorAutomationSettingsTests
	{
		private FieldInfo overrideCacheField;
		private CrystalSaveAssetOverrides previousOverride;
		private CrystalSaveAssetOverrides testOverride;
		private SaveSettings settings;

		[SetUp]
		public void SetUp()
		{
			overrideCacheField = typeof(CrystalSaveOverrides).GetField(
				"cached",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(overrideCacheField, Is.Not.Null);

			previousOverride = overrideCacheField.GetValue(null) as CrystalSaveAssetOverrides;
			settings = ScriptableObject.CreateInstance<SaveSettings>();
			settings.hideFlags = HideFlags.HideAndDontSave;
			testOverride = ScriptableObject.CreateInstance<CrystalSaveAssetOverrides>();
			testOverride.hideFlags = HideFlags.HideAndDontSave;
			testOverride.saveSettings = settings;
			overrideCacheField.SetValue(null, testOverride);
			CrystalSaveEditorAutomationSettings.ClearCache();
		}

		[TearDown]
		public void TearDown()
		{
			overrideCacheField.SetValue(null, previousOverride);
			CrystalSaveEditorAutomationSettings.ClearCache();
			UnityEngine.Object.DestroyImmediate(testOverride);
			UnityEngine.Object.DestroyImmediate(settings);
		}

		[Test]
		public void FirstReadMigratesLegacyAutomationSettingsToEnabledDefaults()
		{
			settings.editorAutomationSettingsInitialized = false;
			settings.autoRegisterPrefabs = false;
			settings.showEditorHierarchyIcons = false;
			settings.enableDefineSymbolsAutomation = false;
			settings.enableLegacySceneObjectIDEditor = false;
			settings.enablePrefabRegistryAutoValidation = false;

			Assert.That(CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons, Is.True);
			Assert.That(settings.editorAutomationSettingsInitialized, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableDefineSymbolsAutomation, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableLegacySceneObjectIDEditor, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation, Is.True);
		}

		[Test]
		public void EnsureInitializedPreservesChoicesThatWereAlreadyInitialized()
		{
			settings.editorAutomationSettingsInitialized = true;
			settings.autoRegisterPrefabs = false;
			settings.showEditorHierarchyIcons = true;
			settings.enableDefineSymbolsAutomation = false;
			settings.enableLegacySceneObjectIDEditor = true;
			settings.enablePrefabRegistryAutoValidation = false;

			CrystalSaveEditorAutomationSettings.EnsureInitialized(settings);

			Assert.That(CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs, Is.False);
			Assert.That(CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableDefineSymbolsAutomation, Is.False);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableLegacySceneObjectIDEditor, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation, Is.False);
		}

		[Test]
		public void ReadsReflectLiveSettingsChangesWithoutClearingTheAssetCache()
		{
			settings.editorAutomationSettingsInitialized = true;
			settings.autoRegisterPrefabs = false;
			settings.showEditorHierarchyIcons = false;
			settings.enableDefineSymbolsAutomation = false;
			settings.enableLegacySceneObjectIDEditor = false;
			settings.enablePrefabRegistryAutoValidation = false;

			Assert.That(CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs, Is.False);
			Assert.That(CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons, Is.False);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableDefineSymbolsAutomation, Is.False);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableLegacySceneObjectIDEditor, Is.False);
			Assert.That(CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation, Is.False);

			settings.autoRegisterPrefabs = true;
			settings.showEditorHierarchyIcons = true;
			settings.enableDefineSymbolsAutomation = true;
			settings.enableLegacySceneObjectIDEditor = true;
			settings.enablePrefabRegistryAutoValidation = true;

			Assert.That(CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableDefineSymbolsAutomation, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnableLegacySceneObjectIDEditor, Is.True);
			Assert.That(CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation, Is.True);
		}

		[TestCase(LogLevel.Off, LogLevel.Error, false)]
		[TestCase(LogLevel.Error, LogLevel.Error, true)]
		[TestCase(LogLevel.Error, LogLevel.Warning, false)]
		[TestCase(LogLevel.Warning, LogLevel.Warning, true)]
		[TestCase(LogLevel.Warning, LogLevel.Info, false)]
		[TestCase(LogLevel.Info, LogLevel.Info, true)]
		public void EditorMessageGateHonorsCachedSaveSettingsLevel(
			LogLevel configuredLevel,
			LogLevel messageLevel,
			bool expected)
		{
			settings.logLevel = configuredLevel;
			CrystalSaveEditorAutomationSettings.UpdateEditorLogLevel(settings);

			Assert.That(
				CrystalSaveEditorAutomationSettings.ShouldLogEditorMessage(messageLevel),
				Is.EqualTo(expected));
		}

		[Test]
		public void EnsureInitializedRefreshesEditorLogLevelAfterLiveChange()
		{
			settings.editorAutomationSettingsInitialized = true;
			settings.logLevel = LogLevel.Error;
			CrystalSaveEditorAutomationSettings.EnsureInitialized(settings);
			Assert.That(
				CrystalSaveEditorAutomationSettings.ShouldLogEditorMessage(LogLevel.Info),
				Is.False);

			settings.logLevel = LogLevel.Info;
			CrystalSaveEditorAutomationSettings.EnsureInitialized(settings);
			Assert.That(
				CrystalSaveEditorAutomationSettings.ShouldLogEditorMessage(LogLevel.Info),
				Is.True);
		}

		[Test]
		public void RepeatedReadsOfInitializedSettingsAllocateNoManagedMemory()
		{
			settings.editorAutomationSettingsInitialized = true;
			settings.autoRegisterPrefabs = false;
			settings.showEditorHierarchyIcons = false;
			settings.enableDefineSymbolsAutomation = false;
			settings.enableLegacySceneObjectIDEditor = false;
			settings.enablePrefabRegistryAutoValidation = false;

			for (int i = 0; i < 16; i++)
			{
				_ = CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs;
				_ = CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons;
				_ = CrystalSaveEditorAutomationSettings.EnableDefineSymbolsAutomation;
				_ = CrystalSaveEditorAutomationSettings.EnableLegacySceneObjectIDEditor;
				_ = CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation;
			}

			bool observed = false;
			long allocations = CrystalSaveAllocationMeasurement.Count(() =>
			{
				for (int i = 0; i < 1000; i++)
				{
					observed ^= CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs;
					observed ^= CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons;
					observed ^= CrystalSaveEditorAutomationSettings.EnableDefineSymbolsAutomation;
					observed ^= CrystalSaveEditorAutomationSettings.EnableLegacySceneObjectIDEditor;
					observed ^= CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation;
				}
			});
			GC.KeepAlive(observed);

			Assert.That(allocations, Is.Zero);
		}
	}
}
#endif
