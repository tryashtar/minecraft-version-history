using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MinecraftVersionHistory;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace Tests;

[TestClass]
public class JsonSorting
{
    [TestMethod]
    public void SortAllEffects()
    {
        //var config = new YamlMappingNode(
        //    "path", new YamlSequenceNode("criteria", "all_effects", "conditions", "effects"),
        //    "operation", "sort_keys"
        //);
        //var sorter = JsonSorterFactory.Create(config);
        //var json = JObject.Parse(File.ReadAllText("all_effects.json"));
        //var relevant_object = (JObject)json["criteria"]["all_effects"]["conditions"]["effects"];
        //Assert.IsFalse(IsSorted(relevant_object));
        //sorter.Sort(json);
        //Assert.IsTrue(IsSorted(relevant_object));
    }

    [TestMethod]
    public void SortShipwreckSupply()
    {
        //var config = new YamlMappingNode(
        //    "path", new YamlSequenceNode("pools", new YamlMappingNode(), "entries", new YamlMappingNode("name", "minecraft:suspicious_stew"), "functions", new YamlMappingNode("function", "minecraft:set_stew_effect"), "effects"),
        //    "operation", "sort_by",
        //    "by", "type"
        //);
        //var sorter = JsonSorterFactory.Create(config);
        //var json = JObject.Parse(File.ReadAllText("shipwreck_supply.json"));
        //var relevant_object = (JArray)json["pools"][0]["entries"][5]["functions"][0]["effects"];
        //Assert.IsFalse(IsSorted(relevant_object, "type"));
        //sorter.Sort(json);
        //Assert.IsTrue(IsSorted(relevant_object, "type"));
    }

    [TestMethod]
    public void SortUpPath()
    {
        var config = new YamlMappingNode(
            "up_path", new YamlSequenceNode("alphabet"),
            "operation", "order",
            "order", new YamlSequenceNode("a", "b", "c")
        );
        var sorter = JsonSorterFactory.Create(config);
        var json = (JsonObject)JsonNode.Parse(File.ReadAllText("test_up_path.json"));
        sorter.Sort(json);
        File.WriteAllText("aaaaaa.json", json.ToString());
    }

    private bool IsSorted(JsonObject obj)
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

    private bool IsSorted(JsonArray array, string key)
    {
        string last = null;
        foreach (var item in array)
        {
            if (last != null && string.Compare(last, (string)item[key]) > 0)
                return false;
            last = (string)item[key];
        }
        return true;
    }
}
