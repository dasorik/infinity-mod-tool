﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
    public class CharacterData
    {
        public string Name;
        public string Sku_Id;
        public string SteamDLCAppId;
        public string PCSKU;
        public string WINRTSKU;
        public string Icon;
        public string Description;
        public string VideoLink;
        public string ProgressionTree;
        public string CostumeCoin;
        public string MetaData;
        public bool ReplaceCharacter;
        public bool WriteToCharacterList;
        public string DisplayImage;
        public string DisplayColor;
        public string DisplayName;
    }

    public class CharacterModification
    {
        public CharacterData Data;
        public string ReplacementCharacter;
    }
}
