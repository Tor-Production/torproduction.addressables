using System;
using StansAssets.Foundation;

namespace TorProduction.AddressablesToolpack
{
    public class InteractableFactoryId
    {
        public static string GetUniqueId() {
            var ticks = DateTime.Now.Ticks;
            return string.Concat(IdFactory.RandomString, "_" ,Convert.ToString(ticks));
        }

        public static string GetRandomString => IdFactory.RandomString;
    }
}
