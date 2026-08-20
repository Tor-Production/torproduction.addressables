using System;

namespace TorProduction.AddressablesToolpack
{
    /// <summary>
    /// The state represented as int in the Value field. The default value is -1
    /// </summary>
    [Serializable]
    public struct AppState
    {
        // TODO: find a better way to sync and convert to/from enums
        public int StateValue;
    }
}
