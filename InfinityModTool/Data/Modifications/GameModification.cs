using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data.Modifications
{
    public class GameModification
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
