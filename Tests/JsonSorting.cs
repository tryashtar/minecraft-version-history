using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MinecraftVersionHistory;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace Tests
{
    [TestClass]
    public class JsonSorting
    {
        [TestMethod]
        public void SortAllEffects()
        {
            var config = new YamlMappingNode(
                "path", new YamlSequenceNode("criteria", "all_effects", "conditions", "effects"),
                "operation", "sort_keys"
            );
            var sorter = new JsonSorter(config);
            var json = JObject.Parse(File.ReadAllText("all_effects.json"));
            var relevant_object = (JObject)json["criteria"]["all_effects"]["conditions"]["effects"];
            Assert.IsFalse(IsSorted(relevant_object));
            sorter.Sort(json);
            Assert.IsTrue(IsSorted(relevant_object));
        }

        private bool IsSorted(JObject obj)
        {
            string last = null;
            foreach (var item in obj)
            {
                if (last != null && string.Compare(last, item.Key) > 0)
                    return false;
                last = item.Key;
            }
            return true;
        }
    }
}
