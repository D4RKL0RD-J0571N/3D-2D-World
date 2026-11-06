namespace Domain.Modding
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a subrecord within a Bethesda TES4/Skyrim record (e.g. EDID, FULL, DATA, DESC).
    /// Modeled after the official Skyrim plugin format documented on UESP.
    /// </summary>
    [Serializable]
    public class TESSubrecord
    {
        public string Signature; // e.g. "EDID", "FULL", "DATA", "DESC"
        public byte[] Data;
        public string StringValue;

        public TESSubrecord(string signature, string stringValue)
        {
            Signature = signature;
            StringValue = stringValue;
        }

        public TESSubrecord(string signature, byte[] data)
        {
            Signature = signature;
            Data = data;
        }
    }

    /// <summary>
    /// Represents an individual record in a Skyrim plugin (.esp/.esm), identified by a 4-character signature
    /// and a 32-bit FormID (e.g. 0x00012EB7). Modeled after UESP Skyrim Mod File Format.
    /// </summary>
    [Serializable]
    public class TESRecord
    {
        public string Signature; // e.g. "WEAP", "ARMO", "ALCH", "ENCH", "SPEL", "NPC_", "CELL"
        public uint FormId;
        public uint Flags;
        public List<TESSubrecord> Subrecords = new();

        public string EditorId => GetSubrecordString("EDID");
        public string FullName => GetSubrecordString("FULL");

        public TESRecord(string signature, uint formId)
        {
            Signature = signature;
            FormId = formId;
        }

        public void AddSubrecord(string signature, string value)
        {
            Subrecords.Add(new TESSubrecord(signature, value));
        }

        public string GetSubrecordString(string signature)
        {
            for (int i = 0; i < Subrecords.Count; i++)
            {
                if (Subrecords[i].Signature == signature)
                {
                    return Subrecords[i].StringValue ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Represents a group (GRUP) of records in a Skyrim plugin sharing a common type signature.
    /// </summary>
    [Serializable]
    public class TESGroup
    {
        public string GroupType; // e.g. "WEAP", "ALCH"
        public List<TESRecord> Records = new();

        public TESGroup(string groupType)
        {
            GroupType = groupType;
        }
    }

    /// <summary>
    /// Represents a Skyrim plugin file (.esm / .esp / .esl) containing a TES4 header,
    /// master dependencies, and record groups.
    /// </summary>
    [Serializable]
    public class TESPlugin
    {
        public string FileName;
        public string Author;
        public string Description;
        public List<string> MasterFiles = new();
        public List<TESGroup> Groups = new();

        public TESPlugin(string fileName, string author = "Bethesda", string description = "")
        {
            FileName = fileName;
            Author = author;
            Description = description;
        }

        public TESRecord AddRecord(string groupType, uint formId, string editorId, string fullName = "")
        {
            TESGroup group = Groups.Find(g => g.GroupType == groupType);
            if (group == null)
            {
                group = new TESGroup(groupType);
                Groups.Add(group);
            }

            var record = new TESRecord(groupType, formId);
            if (!string.IsNullOrEmpty(editorId)) record.AddSubrecord("EDID", editorId);
            if (!string.IsNullOrEmpty(fullName)) record.AddSubrecord("FULL", fullName);

            group.Records.Add(record);
            return record;
        }
    }

    /// <summary>
    /// Implements SSEEdit / xEdit style load order conflict resolution.
    /// Later plugins in the load order override earlier records with matching FormIDs.
    /// </summary>
    public static class TESConflictResolver
    {
        /// <summary>
        /// Merges multiple plugins in load order sequence and determines winning records per FormID.
        /// </summary>
        public static Dictionary<uint, (TESRecord Record, string WinningPlugin)> ResolveLoadOrder(IReadOnlyList<TESPlugin> loadOrder)
        {
            var resolved = new Dictionary<uint, (TESRecord, string)>();
            if (loadOrder == null) return resolved;

            for (int p = 0; p < loadOrder.Count; p++)
            {
                var plugin = loadOrder[p];
                for (int g = 0; g < plugin.Groups.Count; g++)
                {
                    var group = plugin.Groups[g];
                    for (int r = 0; r < group.Records.Count; r++)
                    {
                        var record = group.Records[r];
                        resolved[record.FormId] = (record, plugin.FileName);
                    }
                }
            }

            return resolved;
        }
    }
}
