using System;

namespace TorProduction.AddressablesToolpack
{
    public class InteractableFactoryId
    {
        public static string GetUniqueId() {
            return string.Concat(GetRandomString, "_", DateTime.UtcNow.Ticks);
        }

        public static string GetRandomString => Guid.NewGuid().ToString("N");
    }
}
