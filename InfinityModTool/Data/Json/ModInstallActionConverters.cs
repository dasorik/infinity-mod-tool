using InfinityModTool.Data.InstallActions;
using InfinityModTool.Extension;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool
{
    public class ModInstallActionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(ModInstallAction).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader,
            Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            string actionType = (string)jObject["Action"] ?? string.Empty;

            ModInstallAction item = null;
            if (actionType.Equals("MoveFile", StringComparison.InvariantCultureIgnoreCase))
                item = new FileMoveAction();
            else if (actionType.Equals("DeleteFile", StringComparison.InvariantCultureIgnoreCase))
                item = new FileDeleteAction();
            else if (actionType.Equals("ReplaceFile", StringComparison.InvariantCultureIgnoreCase))
                item = new FileReplaceAction();
            else if (actionType.Equals("CopyFile", StringComparison.InvariantCultureIgnoreCase))
                item = new FileCopyAction();
            else if (actionType.Equals("WriteToFile", StringComparison.InvariantCultureIgnoreCase))
                item = new FileWriteAction();
            else if (actionType.Equals("QuickBMSExtract", StringComparison.InvariantCultureIgnoreCase))
                item = new QuickBMSExtractAction();
            else if (actionType.Equals("UnluacDecompile", StringComparison.InvariantCultureIgnoreCase))
                item = new UnluacDecompileAction();
            else
                item = new ModInstallAction();

            serializer.Populate(jObject.CreateReader(), item);

            return item;
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer,
            object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
