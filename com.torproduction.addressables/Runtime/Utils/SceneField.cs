using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TorProduction.AddressablesToolpack.Common
{
    [Serializable]
    public class SceneField
    {
        [SerializeField] private Object m_sceneAsset;
        [SerializeField] private string m_sceneName;
        public string SceneName => m_sceneName;
    }
}
