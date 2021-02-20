using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public class BedrockVersionOLD
    {
        private static HashSet<string> UsedNames = new HashSet<string>();
        private readonly string ZipPath;
        public BedrockVersionOLD(string zippath)
        {
            //using (ZipArchive zip = ZipFile.OpenRead(zippath))
            //{
            //    ZipPath = zippath;
            //    var mainappx = GetMainAppx(zip);
            //    var base_name = Path.GetFileName(mainappx.FullName).Split('_')[1];
            //    var name = base_name;
            //    for (int i = 2; UsedNames.Contains(name); i++)
            //    {
            //        name = $"{base_name} (v{i})";
            //    }
            //    Name = name;
            //    UsedNames.Add(name);
            //    ReleaseName = Name.Substring(0, Name.IndexOf('.', Name.IndexOf('.') + 1));
            //    ReleaseTime = zip.Entries[0].LastWriteTime.UtcDateTime;
            //}
        }

        public void ExtractData(string output)
        {
            string appxpath = Path.Combine(output, "appx.appx");
            using (ZipArchive zip = ZipFile.OpenRead(ZipPath))
            {
                var appx = GetMainAppx(zip);
                appx.ExtractToFile(appxpath);
            }

            using (ZipArchive zip = ZipFile.OpenRead(appxpath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("data/") && Path.GetExtension(entry.FullName) != ".zip")
                    {
                        Directory.CreateDirectory(Path.Combine(output, Path.GetDirectoryName(entry.FullName)));
                        entry.ExtractToFile(Path.Combine(output, entry.FullName));
                    }
                }
            }
            var merged = Path.Combine(output, "latest_packs");
            var latest_behavior = Path.Combine(merged, "behavior_pack");
            var latest_resource = Path.Combine(merged, "resource_pack");
            Directory.CreateDirectory(merged);
            Directory.CreateDirectory(latest_behavior);
            Directory.CreateDirectory(latest_resource);
            var bpacks = GetVanillaPacks(Path.Combine(output, "data", "behavior_packs"));
            var rpacks = GetVanillaPacks(Path.Combine(output, "data", "resource_packs"));
            OverwriteAndMerge(bpacks, latest_behavior);
            OverwriteAndMerge(rpacks, latest_resource);
            File.Delete(appxpath);
        }

        private void OverwriteAndMerge(IEnumerable<string> sourcepacks, string destination_folder)
        {
            Console.WriteLine("Merging vanilla packs");
            foreach (var pack in sourcepacks)
            {
                Console.WriteLine($"Applying pack {Path.GetFileName(pack)}");
                foreach (var file in Directory.GetFiles(pack, "*.*", SearchOption.AllDirectories))
                {
                    var relative = Util.RelativePath(pack, file);
                    var dest = Path.Combine(destination_folder, relative);
                    var pieces = Util.Split(relative);
                    var first = pieces.First();
                    var last = pieces.Last();
                    var extension = Path.GetExtension(last);
                    bool handled = false;
                    if (last == "contents.json" || last == "textures_list.json")
                        handled = true; //skip
                    else if (pieces.Length == 1) // stuff in root
                    {
                        if (first == "blocks.json")
                        {
                            MergeJsons(dest, file, ObjectStraightFrom, x => x);
                            handled = true;
                        }
                        else if (first == "biomes_client.json")
                        {
                            MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "biomes"), x =>
                             new JObject() { { "biomes", x } });
                            handled = true;
                        }
                        else if (first == "items_offsets_client.json")
                        {
                            MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "render_offsets"), x =>
                             new JObject() { { "render_offsets", x } });
                            handled = true;
                        }
                        else if (first == "sounds.json" && File.Exists(dest))
                        {
                            var existing = JObject.Parse(File.ReadAllText(dest));
                            var incoming = JObject.Parse(File.ReadAllText(file));
                            var both = new Dictionary<JObject, List<JObject>> { { existing, new List<JObject>() }, { incoming, new List<JObject>() } };
                            foreach (var item in both.Keys)
                            {
                                var stuff = both[item];
                                stuff.Add((JObject)PathTo(item, "individual_event_sounds", "events") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "entity_sounds", "defaults") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "entity_sounds", "entities") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "block_sounds") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "interactive_sounds", "block_sounds") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "interactive_sounds", "entity_sounds", "defaults") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "interactive_sounds", "entity_sounds", "entities") ?? new JObject());
                            }
                            foreach (var item in both[existing].Zip(both[incoming], (x, y) => Tuple.Create(x, y)))
                            {
                                MergeJsons(item.Item1, item.Item2);
                            }
                            File.WriteAllText(dest, Util.ToMinecraftJson(existing));
                            handled = true;
                        }
                    }
                    else
                    {
                        if (first == "sounds")
                        {
                            if (last == "sound_definitions.json")
                            {
                                MergeJsons(dest, file, SoundDefinitionsFrom, x =>
                                 new JObject() { { "format_version", "1.14.0" }, { "sound_definitions", x } });
                                handled = true;
                            }
                            else if (last == "music_definitions.json")
                            {
                                MergeJsons(dest, file, ObjectStraightFrom, x => x);
                                handled = true;
                            }
                        }
                        else if (first == "textures")
                        {
                            if (last == "flipbook_textures.json")
                            {
                                MergeJsons(dest, file, ArrayStraightFrom, x => x);
                                handled = true;
                            }
                            else if (last == "item_texture.json")
                            {
                                MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "texture_data"), x =>
                                    new JObject() { { "resource_pack_name", "vanilla" }, { "texture_name", "atlas.items" }, { "texture_data", x } });
                                handled = true;
                            }
                            else if (last == "terrain_texture.json")
                            {
                                MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "texture_data"), x =>
                                 new JObject() { { "resource_pack_name", "vanilla" }, { "texture_name", "atlas.terrain" }, { "padding", 8 }, { "num_mip_levels", 4 }, { "texture_data", x } });
                                handled = true;
                            }
                        }
                    }
                    if (!handled)
                        Util.Copy(file, dest);
                }
            }
        }

        private JToken PathTo(JObject top, params string[] subs)
        {
            foreach (var item in subs.Take(subs.Length - 1))
            {
                if (top.TryGetValue(item, out var sub) && sub is JObject obj)
                    top = obj;
                else
                    return null;
            }
            if (top.TryGetValue(subs.Last(), out var final))
                return final;
            else
                return null;
        }

        private JObject MergeJsons(JObject existing, JObject incoming)
        {
            foreach (var item in incoming)
            {
                existing[item.Key] = item.Value;
            }
            return existing;
        }

        private void MergeJsons(string existing_path, string incoming_path, Func<string, JObject> loader, Func<JObject, JObject> transformer)
        {
            if (!File.Exists(existing_path))
                Util.Copy(incoming_path, existing_path);
            else
            {
                var existing = loader(existing_path);
                var incoming = loader(incoming_path);
                existing = MergeJsons(existing, incoming);
                var final = transformer(existing);
                File.WriteAllText(existing_path, Util.ToMinecraftJson(final));
            }
        }

        private void MergeJsons(string existing_path, string incoming_path, Func<string, JArray> loader, Func<JArray, JArray> transformer)
        {
            if (!File.Exists(existing_path))
                Util.Copy(incoming_path, existing_path);
            else
            {
                var existing = loader(existing_path);
                var incoming = loader(incoming_path);
                foreach (var item in incoming)
                {
                    existing.Add(item);
                }
                var final = transformer(existing);
                File.WriteAllText(existing_path, Util.ToMinecraftJson(final));
            }
        }

        private JObject SoundDefinitionsFrom(string filepath)
        {
            var jobj = ObjectStraightFrom(filepath);
            JObject definitions;
            if (jobj.TryGetValue("sound_definitions", out var def))
                definitions = (JObject)def;
            else
                definitions = jobj;
            return definitions;
        }

        private JObject ObjectStraightFrom(string filepath)
        {
            var jobj = JObject.Parse(File.ReadAllText(filepath), new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
            return jobj;
        }

        private JArray ArrayStraightFrom(string filepath)
        {
            var jarr = JArray.Parse(File.ReadAllText(filepath), new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
            return jarr;
        }

        private IEnumerable<string> GetVanillaPacks(string packsfolder)
        {
            var vanilla = Path.Combine(packsfolder, "vanilla");
            if (Directory.Exists(vanilla))
                yield return vanilla;
            var rest = new List<Match>();
            foreach (var directory in Directory.EnumerateDirectories(packsfolder))
            {
                var name = Path.GetFileName(directory);
                var match = Regex.Match(name, @"^vanilla_(\d+)\.(\d+)(\.(\d+))?$");
                if (match.Success)
                    rest.Add(match);
            }
            var sorted = rest.OrderBy(x => int.Parse(x.Groups[1].Value))
                .ThenBy(x => int.Parse(x.Groups[2].Value))
                .ThenBy(x => x.Groups[4].Success ? int.Parse(x.Groups[4].Value) : 0)
                .Select(x => x.Value);
            foreach (var item in sorted)
            {
                yield return Path.Combine(packsfolder, item);
            }
        }

        private ZipArchiveEntry GetMainAppx(ZipArchive zip)
        {
            foreach (var entry in zip.Entries)
            {
                string filename = Path.GetFileName(entry.FullName);
                // example: Minecraft.Windows_1.1.0.0_x64_UAP.Release.appx
                if (filename.StartsWith("Minecraft.Windows") && Path.GetExtension(filename) == ".appx")
                    return entry;
            }
            throw new FileNotFoundException($"Could not find main APPX");
        }
    }
}
