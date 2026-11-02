namespace Systems.Creator
{
    using System;
    using Core.Ids;
    using Domain.Creator;
    using UnityEngine;

    /// <summary>
    /// Places a new object into the In-Game Editor workspace with a deterministic <see cref="InstanceGuid"/>.
    /// Undoing destroys the placed object; redoing respawns it with the exact same <see cref="InstanceGuid"/> (<c>EDT-01</c>).
    /// </summary>
    public sealed class PlaceObjectCommand : IEditorCommand
    {
        private readonly CreatorStudioService _studio;
        private readonly PlacedObjectRecord _recordSnapshot;

        public InstanceGuid InstanceId => _recordSnapshot.InstanceId;
        public string Description => $"Place '{_recordSnapshot.itemId}' ({_recordSnapshot.InstanceId})";

        public PlaceObjectCommand(
            CreatorStudioService studio,
            string itemId,
            Vector3 position,
            Quaternion rotation,
            Vector3? scale = null,
            InstanceGuid? explicitGuid = null)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _recordSnapshot = new PlacedObjectRecord
            {
                InstanceId = explicitGuid.HasValue && !explicitGuid.Value.IsEmpty ? explicitGuid.Value : InstanceGuid.NewGuid(),
                itemId = itemId
            };
            _recordSnapshot.SetPosition(position);
            _recordSnapshot.SetRotation(rotation);
            _recordSnapshot.SetScale(scale ?? Vector3.one);
        }

        public PlaceObjectCommand(CreatorStudioService studio, PlacedObjectRecord record)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _recordSnapshot = (record ?? throw new ArgumentNullException(nameof(record))).Clone();
            if (_recordSnapshot.InstanceId.IsEmpty)
            {
                _recordSnapshot.InstanceId = InstanceGuid.NewGuid();
            }
        }

        public void Execute()
        {
            _studio.SpawnFromRecord(_recordSnapshot.Clone());
        }

        public void Undo()
        {
            _studio.RemovePlacedObjectByGuid(_recordSnapshot.InstanceId, destroyGameObject: true);
        }
    }

    /// <summary>
    /// Moves, rotates, or scales an existing placed object identified by its <see cref="InstanceGuid"/> (<c>EDT-01</c>).
    /// </summary>
    public sealed class TransformObjectCommand : IEditorCommand
    {
        private readonly CreatorStudioService _studio;
        private readonly InstanceGuid _targetGuid;
        private readonly Vector3 _beforePos;
        private readonly Quaternion _beforeRot;
        private readonly Vector3 _beforeScale;
        private readonly Vector3 _afterPos;
        private readonly Quaternion _afterRot;
        private readonly Vector3 _afterScale;

        public string Description => $"Transform ({_targetGuid})";

        public TransformObjectCommand(
            CreatorStudioService studio,
            InstanceGuid targetGuid,
            Vector3 beforePos,
            Quaternion beforeRot,
            Vector3 beforeScale,
            Vector3 afterPos,
            Quaternion afterRot,
            Vector3 afterScale)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _targetGuid = targetGuid;
            _beforePos = beforePos;
            _beforeRot = beforeRot;
            _beforeScale = beforeScale;
            _afterPos = afterPos;
            _afterRot = afterRot;
            _afterScale = afterScale;
        }

        public TransformObjectCommand(
            CreatorStudioService studio,
            InstanceGuid targetGuid,
            Vector3 afterPos,
            Quaternion? afterRot = null,
            Vector3? afterScale = null)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _targetGuid = targetGuid;

            if (_studio.TryGetPlacedObject(targetGuid, out GameObject go) && go != null)
            {
                _beforePos = go.transform.position;
                _beforeRot = go.transform.rotation;
                _beforeScale = go.transform.localScale;
            }
            else
            {
                _beforePos = Vector3.zero;
                _beforeRot = Quaternion.identity;
                _beforeScale = Vector3.one;
            }

            _afterPos = afterPos;
            _afterRot = afterRot ?? _beforeRot;
            _afterScale = afterScale ?? _beforeScale;
        }

        public void Execute()
        {
            _studio.SetTransformByGuid(_targetGuid, _afterPos, _afterRot, _afterScale);
        }

        public void Undo()
        {
            _studio.SetTransformByGuid(_targetGuid, _beforePos, _beforeRot, _beforeScale);
        }
    }

    /// <summary>
    /// Deletes a placed object while capturing its complete <see cref="PlacedObjectRecord"/> snapshot.
    /// Undoing respawns the object with the exact same <see cref="InstanceGuid"/>, transform, overrides, and links (<c>EDT-01</c>).
    /// </summary>
    public sealed class DeleteObjectCommand : IEditorCommand
    {
        private readonly CreatorStudioService _studio;
        private readonly InstanceGuid _targetGuid;
        private PlacedObjectRecord _capturedSnapshot;

        public string Description => $"Delete ({_targetGuid})";

        public DeleteObjectCommand(CreatorStudioService studio, InstanceGuid targetGuid)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _targetGuid = targetGuid;
        }

        public void Execute()
        {
            if (_studio.TryCaptureRecord(_targetGuid, out PlacedObjectRecord snapshot))
            {
                _capturedSnapshot = snapshot.Clone();
            }

            _studio.RemovePlacedObjectByGuid(_targetGuid, destroyGameObject: true);
        }

        public void Undo()
        {
            if (_capturedSnapshot != null)
            {
                _studio.SpawnFromRecord(_capturedSnapshot.Clone());
            }
        }
    }

    /// <summary>
    /// Modifies a per-instance property override on a placed object.
    /// </summary>
    public sealed class ModifyPropertyCommand : IEditorCommand
    {
        private readonly CreatorStudioService _studio;
        private readonly InstanceGuid _targetGuid;
        private readonly string _key;
        private readonly string _newValue;
        private bool _hadOldValue;
        private string _oldValue;

        public string Description => $"Modify '{_key}' on ({_targetGuid})";

        public ModifyPropertyCommand(CreatorStudioService studio, InstanceGuid targetGuid, string key, string newValue)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _targetGuid = targetGuid;
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _newValue = newValue;
        }

        public void Execute()
        {
            if (_studio.TryGetPlacedObject(_targetGuid, out GameObject go) && go != null)
            {
                var tag = go.GetComponent<EditorPlacedEntityTag>();
                if (tag != null)
                {
                    _hadOldValue = tag.PropertyOverrides.TryGetValue(_key, out _oldValue);
                    tag.PropertyOverrides[_key] = _newValue;
                }
            }
        }

        public void Undo()
        {
            if (_studio.TryGetPlacedObject(_targetGuid, out GameObject go) && go != null)
            {
                var tag = go.GetComponent<EditorPlacedEntityTag>();
                if (tag != null)
                {
                    if (_hadOldValue)
                    {
                        tag.PropertyOverrides[_key] = _oldValue;
                    }
                    else
                    {
                        tag.PropertyOverrides.Remove(_key);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Wires a <see cref="CrossObjectLink"/> from a source object (<see cref="InstanceGuid"/>) to a target object (<see cref="InstanceGuid"/>).
    /// </summary>
    public sealed class LinkObjectCommand : IEditorCommand
    {
        private readonly CreatorStudioService _studio;
        private readonly InstanceGuid _sourceGuid;
        private readonly CrossObjectLink _link;

        public string Description => $"Wire '{_link.OutputPortName}' -> ({_link.TargetGuid})";

        public LinkObjectCommand(CreatorStudioService studio, InstanceGuid sourceGuid, CrossObjectLink link)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
            _sourceGuid = sourceGuid;
            _link = (link ?? throw new ArgumentNullException(nameof(link))).Clone();
        }

        public void Execute()
        {
            _studio.AddLink(_sourceGuid, _link.Clone());
        }

        public void Undo()
        {
            _studio.RemoveLink(_sourceGuid, _link);
        }
    }
}
