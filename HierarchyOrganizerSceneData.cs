#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Dieses in der Szene gespeicherte Objekt ist nur für den Editor bestimmt.
// Native Referenzen bleiben auch beim Umbenennen und erneuten Öffnen erhalten.
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class HierarchyOrganizerSceneData : MonoBehaviour
{
    [System.Serializable]
    public class ObjectLink
    {
        public EntityId savedId;
        public GameObject target;
    }

    [HideInInspector] public string stateJson = "";
    [HideInInspector] public List<ObjectLink> objectLinks = new List<ObjectLink>();
}
#endif
