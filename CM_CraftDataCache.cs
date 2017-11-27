﻿using System;
using System.IO;
using System.Collections.Generic;

using KatLib;

namespace CraftManager
{
    public class CraftDataCache
    {


        public string cache_path = Paths.joined(CraftManager.ksp_root, "GameData", "CraftManager", "craft_data.cache");
        public Dictionary<string, ConfigNode> craft_data = new Dictionary<string, ConfigNode>();

        public Dictionary<string, AvailablePart> part_data = new Dictionary<string, AvailablePart>();  //name->part lookup for available parts
        public List<string> locked_parts = new List<string>();

        public string installed_part_sig; //checksum signature of the installed parts, used to determine if the installed parts have changed since last time

        public List<string> ignore_fields = new List<string>{"selected_craft"};
        public List<string> dont_load = new List<string>{"locked_parts"};


        public CraftDataCache(){
            CraftManager.log("Initializing Cache");
            if(File.Exists(cache_path)){
                CraftManager.log("loading cached craft data from file");
                load(); 
            }

            if(part_data.Count == 0){
                locked_parts.Clear();
                CraftManager.log("caching game parts");
                List<string> part_names = new List<string>();
                foreach(AvailablePart part in PartLoader.LoadedPartsList){
                    part_data.Add(part.name, part);
                    part_names.Add(part.name);
                    if(!ResearchAndDevelopment.PartTechAvailable(part)){
                        locked_parts.AddUnique(part.name);
                    }
                }
                part_names.Sort();
                string s = "";
                foreach(string n in part_names){s = s + n;}
                installed_part_sig = Checksum.digest(s);
            }

            CraftManager.log("Cache Ready");
        }

        public AvailablePart fetch_part(string part_name){
            if(part_data.ContainsKey(part_name)){
                return part_data[part_name];
            }
            return null;
        }


        //takes a CraftData craft and creates a ConfigNode that contains all of it's public properties, ConfigNodes is held in 
        //a <string, ConfigNode> dict with the full path as the key. 
        public void write(CraftData craft){
            ConfigNode node = new ConfigNode();
            foreach(var prop in craft.GetType().GetProperties()){
                if(!ignore_fields.Contains(prop.Name)){
                    var value = prop.GetValue(craft, null);
                    if(value != null){
                        node.AddValue(prop.Name, value);
                    }
                }
            }
            if(craft_data.ContainsKey(craft.path)){
                craft_data[craft.path] = node;
            }else{
                craft_data.Add(craft.path,node);
            }
            save();
        }

        //Takes a CraftData craft object and if the cached data contains a matching path AND the checksum value matches
        //then the craft's properties are populated from the ConfigNode in the cache.  Returns true if matching data was
        //found, otherwise returns false, in which case the data will have to be interpreted from the .craft file.
        public bool try_fetch(CraftData craft){
            if(craft_data.ContainsKey(craft.path) && craft_data[craft.path].GetValue("checksum") == craft.checksum && craft_data[craft.path].GetValue("part_sig") ==installed_part_sig){
                try{
                    ConfigNode node = craft_data[craft.path];                    
                    foreach(var prop in craft.GetType().GetProperties()){               
                        if(prop.CanWrite){                            
                            var node_value = node.GetValue(prop.Name);
                            if(!String.IsNullOrEmpty(node_value)){
                                var type = prop.GetValue(craft, null);
                                if(type is float){
                                    prop.SetValue(craft, float.Parse(node_value), null);                                
                                }else if(type is int){
                                    prop.SetValue(craft, int.Parse(node_value), null);                                
                                }else if(type is bool){                 
                                    prop.SetValue(craft, bool.Parse(node_value), null);                                
                                }else{
                                    prop.SetValue(craft, node_value, null);                                
                                }
                            }
                        }
                    }
                    if(node.HasValue("locked_parts")){
                        craft.locked_parts_checked = true;
                    }
                    return true;
                }
                catch(Exception e){
                    CraftManager.log("try_fetch failed: " + e.Message + "\n" + e.StackTrace);
                    return false;
                }
            } else{
                return false;
            }
        }

        private void save(){
            ConfigNode nodes = new ConfigNode();
            ConfigNode craft_nodes = new ConfigNode();

            foreach(KeyValuePair<string, ConfigNode> pair in craft_data){
                ConfigNode node_to_save = pair.Value.CreateCopy();
                node_to_save.RemoveValue("locked_parts");
                craft_nodes.AddNode("CRAFT", node_to_save);
            }
            nodes.AddNode("CraftData", craft_nodes);
            nodes.Save(cache_path);
        }

        private void load(){
            craft_data.Clear();
            ConfigNode nodes = ConfigNode.Load(cache_path);
            ConfigNode craft_nodes = nodes.GetNode("CraftData");
            foreach(ConfigNode node in craft_nodes.nodes){
                craft_data.Add(node.GetValue("path"), node);
            }
        }
    }
}
