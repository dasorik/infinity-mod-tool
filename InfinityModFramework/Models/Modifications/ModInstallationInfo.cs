using System.Collections.Generic;

namespace InfinityModFramework.Models
{
    public class ModInstallationInfo
    {
        public BaseModConfiguration Config;
        public Dictionary<string, string> Parameters;

        public T GetConfig<T>()
            where T : BaseModConfiguration
        {
            return Config as T;
        }
    }
}
