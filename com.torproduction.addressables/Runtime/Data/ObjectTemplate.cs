using UnityEngine;

namespace TorProduction.AddressablesToolpack.Data
{
    public class ObjectTemplate : ScriptableObject, IObjectTemplate
    {
        [SerializeField] protected string m_customInteractableType;
        
        public string CustomInteractableType => m_customInteractableType ?? string.Empty;
        
        public string GetId { get; }

        public string Name { get; }
        public string Description { get; set; }
        
        public bool FieldPlaceholder { get; }

        public void SetId(string id)
        {
            throw new System.NotImplementedException();
        }
    }
}
