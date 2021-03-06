﻿using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KatLib;


namespace CraftManager
{
    public class CMSettings
    {
        protected string plugin_data_dir = Paths.joined(CraftManager.ksp_root, "GameData", "CraftManager", "PluginData");
        protected string settings_path = Paths.joined(CraftManager.ksp_root, "GameData", "CraftManager", "PluginData", "settings.cfg");
        protected Dictionary<string, string> settings = new Dictionary<string, string>();

        public string craft_sort;

        public CMSettings(){
            ensure_plugin_data_dir_exists();
            import_old_data();

            //default settings. These will populate settings.cfg if the file doesn't exist and also provides
            //a reference of which values to try and fetch from the confignode.
            settings.Add("show_initial_setup_dialog", "True"); 
            settings.Add("KerbalX_integration_enabled", "False");
            settings.Add("ask_to_populate_new_save", "True");
            settings.Add("replace_editor_load_button", "True");
            settings.Add("use_stock_toolbar", "True");
            settings.Add("use_editor_key_shortcuts", "True");
            settings.Add("screenshot_dir", "<ksp_install>/Screenshots");
            settings.Add("show_craft_icon_in_details", "False");
            settings.Add("show_quick_tag_on_toolbar", "True");
            settings.Add("main_ui_height_scale", "0.8");
            settings.Add("main_ui_width", "1000");

            settings.Add("clear_save_state_delay", "500");

            settings.Add("compact_mode", "False");
            settings.Add("exclude_stock_craft", "False"); 
            settings.Add("craft_sort_reverse", "False");
            settings.Add("craft_sort", "name");
            settings.Add("sort_tags_by", "name");
            settings.Add("tag_filter_mode", "AND");
            settings.Add("tag_states", "");
            settings.Add("quick_tag_pos", "auto");

            if(File.Exists(settings_path)){
                ConfigNode settings_raw = ConfigNode.Load(settings_path);
                ConfigNode settings_data = settings_raw.GetNode("SETTINGS");
                List<string> keys = new List<string>(settings.Keys);
                foreach(string key in keys){
                    if(!String.IsNullOrEmpty(settings_data.GetValue(key))){
                        settings[key] = settings_data.GetValue(key);
                    }
                }
                save();
            } else{
                save();
            }

            CraftManager.kerbalx_integration_enabled = bool.Parse(get("KerbalX_integration_enabled"));
            CraftManager.replace_editor_load_button = bool.Parse(get("replace_editor_load_button"));
            CraftManager.use_editor_key_shortcuts = bool.Parse(get("use_editor_key_shortcuts"));

            CraftManager.screenshot_dir = get("screenshot_dir");
            CraftManager.screenshot_dir = CraftManager.screenshot_dir.Replace("<ksp_install>", KSPUtil.ApplicationRootPath);

        }

        public string get(string key){
            if(settings.ContainsKey(key)){
                return settings[key];
            } else{
                return "";
            }
        }

        public void set(string key, string value){
            if(settings.ContainsKey(key)){
                settings.Remove(key);
            }
            settings.Add(key, value);
            save();
            CraftManager.settings = new CMSettings();
                
        }

        protected void save(){
            ConfigNode settings_data = new ConfigNode();
            ConfigNode settings_node = new ConfigNode();
            settings_data.AddNode("SETTINGS", settings_node);
                
            List<string> keys = new List<string>(settings.Keys);
            foreach(string key in keys){
                settings_node.AddValue(key, settings[key]);
            }
            ensure_plugin_data_dir_exists();
            settings_data.Save(settings_path);
            File.AppendAllText(settings_path, "//see the wiki for info about what each setting does - https://github.com/Sujimichi/CraftManager/wiki/Settings" + Environment.NewLine);
        }

        public void ensure_plugin_data_dir_exists(){
            if(!Directory.Exists(plugin_data_dir)){
                Directory.CreateDirectory(plugin_data_dir);
            }
        }

        //move settings.cfg and craft_data.cache files, if they exist, from the Mod root to PluginData folder
        //Enable upgrading from 1.0.3/1.1.3 to 1.0.4/1.1.4 without loss of settings
        protected void import_old_data(){
            string old_settings_path = Paths.joined(CraftManager.ksp_root, "GameData", "CraftManager", "settings.cfg");
            string old_cache_path = Paths.joined(CraftManager.ksp_root, "GameData", "CraftManager", "craft_data.cache");
            if(File.Exists(old_settings_path)){
                FileInfo file = new FileInfo(old_settings_path);
                file.MoveTo(settings_path);
            }
            if(File.Exists(old_cache_path)){
                FileInfo file = new FileInfo(old_cache_path);
                file.MoveTo(CraftDataCache.cache_path);
            }

        }

    }


    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class InitialSetup : CMUI
    {
        private void Start(){
            if(bool.Parse(CraftManager.settings.get("show_initial_setup_dialog"))){
                window_title = "Craft Manager Setup";
                float inner_width = 700;
                window_pos = new Rect(Screen.width -750, 50, inner_width, 5);                
            } else{
                GameObject.Destroy(this);
            }
        }

        protected override void WindowContent(int win_id) {
            label("Welcome to Craft Manager", "h1");
            label("Do you want to enable KerbalX.com Integration?", "h2");
            v_section("dialog.section", () =>{
                label(
                    "KerbalX Integration is disabled by default. You don't need it to use the core CraftManager features.\n" + 
                    "Enabling KerbalX Integration will let you share your craft on KerbalX & download craft"
                );
                label("You will need to have a KerbalX account and CraftManager will need to make requests to KerbalX.com");
                label("you can always enable/disable KerbalX integration in the CraftManager settings", "small");
                
            });
            section(() =>{
                button("Yes, Enable KerbalX Integration", "button.large", () =>{
                    CraftManager.settings.set("KerbalX_integration_enabled", "True");
                    CraftManager.settings.set("show_initial_setup_dialog", "False");
                    KerbalX.api.login();
                    GameObject.Destroy(this);
                });
                button("No, just use CraftManager core features", "button.large", () =>{
                    CraftManager.settings.set("KerbalX_integration_enabled", "False");
                    CraftManager.settings.set("show_initial_setup_dialog", "False");
                    GameObject.Destroy(this);
                });
            });
            section(() =>{
                fspace();
                label(StyleSheet.assets["logo_large"], 250f, 50f);
            });

        }
        
    }

    public class SettingsUI : CMUI
    {
        bool opt = false;
        float lhs_width = 600;
        float rhs_width = 80;
        float inner_width = 0;
        CMSettings settings = CraftManager.settings;
        string new_screenshot_location = "";
        string setting_error_open_opts = "";
        string setting_error_screenshot_dir = "";
        Vector2 settings_scroll = new Vector2();
        float new_height_scale = 0.8f;
        int new_width = 1000;

        private void Start(){
            inner_width = lhs_width + rhs_width+40;
            window_title = "Craft Manager Settings";
            if(CraftManager.main_ui != null){                
                window_pos = new Rect(CraftManager.main_ui.window_pos.x + CraftManager.main_ui.window_pos.width / 2 - inner_width / 2, CraftManager.main_ui.window_pos.y + 100, inner_width, 5);
            } else{
                window_pos = new Rect(Screen.width/2 - inner_width/2, Screen.height/3, inner_width, 5);
            }
            CraftManager.settings_ui = this;
            new_screenshot_location = settings.get("screenshot_dir");
            new_height_scale = float.Parse(settings.get("main_ui_height_scale"));
            new_width = int.Parse(settings.get("main_ui_width"));
        }

        protected override void WindowContent(int win_id) { 
            settings_scroll = scroll(settings_scroll, "settings.scroll", inner_width-8, 500, (w) =>{

                v_section("dialog.section", () =>{
                    setting_section("KerbalX_integration_enabled", "KerbalX Integration", "Enabled", "Enable",                     
                        "Enables you to share your craft on KerbalX, download them and fetch other users craft.",
                        "(requires a KerbalX account & enables the mod to make requests to https://KerbalX.com)"
                    );
                    if(bool.Parse(settings.get("KerbalX_integration_enabled"))){
                        setting_section("ask_to_populate_new_save", "Auto populate new save", 
                            "When you create a new save Craft Manager will ask you if you want to fetch craft from KerbalX"
                        );
                    }
                });

                v_section("dialog.section", () =>{
                    setting_section("replace_editor_load_button", "Replace load button", 
                        "The open button in the editors will open Craft Manager instead of the stock craft browser.", 
                        "(you need to exit and re-enter the editors for this to take effect)"
                    );
                    setting_section("use_stock_toolbar", "Use the Stock Toolbar",
                        "Have an icon in the stock toolbar to open Craft Manager",
                        "(you need to restart KSP to see changes)"
                    );
                    setting_section("use_editor_key_shortcuts", "Enable Editor Keyboard shortcuts",
                        "ctrl+o - Open Craft Manager, ctrl+n - new craft/clear editor, ctrl+s - save current craft"
                    );
                });
                if(!String.IsNullOrEmpty(setting_error_open_opts)){
                    label(setting_error_open_opts, "error");
                }

                v_section("dialog.section", () =>{
                    setting_section("show_craft_icon_in_details", "Craft Icon in Details",
                        "shows the craft thumbnail in the right hand side details"
                    );
                });

                v_section("dialog.section", ()=>{
                    setting_section("compact_mode", "Use Compact Mode", 
                        "The interface hides the tags and craft details sections, making it more like the stock interface.",
                        "Some functionality is reduced, and uploading craft will switch you back to full size view."
                    );
                });

                v_section("dialog.section", ()=>{
                    //main_ui_height_scale
                    section(() =>{
                        v_section(lhs_width, (w2) =>{
                            label("Main UI Height", "h2.tight");
                            label("Adjust the height of the main UI (as a value between 0.2 and 1.0)", "compact");
                        });
                        v_section(rhs_width, (w2) =>{
                            GUILayout.Space(20f);
                            section(()=>{
                                new_height_scale = GUILayout.HorizontalSlider(new_height_scale, 0.2f, 1.0f);
                                new_height_scale = (float)Math.Round(new_height_scale, 2);                                
                            });
                            label(new_height_scale.ToString(), "centered", w2);
                        });
                    });
                    section((w3) =>{
                        v_section(lhs_width, (w2) =>{
                            label("Main UI Width", "h2.tight");
                            label("Adjust the width of the main UI (as a value between 1000 and " + (Screen.width-40) + ")", "compact");
                        });
                        v_section(rhs_width, (w2) =>{
                            GUILayout.Space(20f);
                            section((w4)=>{
                                new_width = (int)GUILayout.HorizontalSlider((float)new_width, 1000f, (float)Screen.width-40);
                            });
                            label(new_width.ToString(), "centered", w2);
                        });
                    });

                    if(float.Parse(settings.get("main_ui_height_scale")) != new_height_scale){
                        settings.set("main_ui_height_scale", new_height_scale.ToString());
                        CraftManager.main_ui.height_scale = new_height_scale;
                        CraftManager.main_ui.set_window_position();
                    }
                    if(float.Parse(settings.get("main_ui_width")) != new_width){
                        settings.set("main_ui_width", new_width.ToString());
                        CraftManager.main_ui.set_window_position();
                    }
                });



                v_section("dialog.section", ()=>{
                    setting_section("show_quick_tag_on_toolbar", "Show Quick Tag Icon", 
                        "Shows icon to open Quick Tag in the stock toolbar",
                        "You can also open Quick Tag with ctrl+t if Editor Shortcuts are enabled"
                    );
                    section(() =>{
                        v_section(lhs_width, (w2) =>{
                            label("Reset QuickTag position", "h2.tight");
                            label("The Quick Tag window remembers it's last position, but incase you drag it offscreen you can reset it here", "compact");
                        });
                        v_section(rhs_width, (w2) =>{
                            GUILayout.Space(20f);
                            button("Reset", ()=>{
                                settings.set("quick_tag_pos", "auto");
                                    
                            });
                        });

                    });
                });

                v_section("dialog.section", () =>{
                    string prev_new_screenshot_location = new_screenshot_location;
                    section(()=>{
                        section(lhs_width, ()=>{
                            label("Screenshot Folder:", "h2.tight");
                            new_screenshot_location = GUILayout.TextField(new_screenshot_location);                    
                            if(new_screenshot_location != prev_new_screenshot_location){
                                setting_error_screenshot_dir = "";
                                string path = new_screenshot_location.Replace("<ksp_install>", KSPUtil.ApplicationRootPath);
                                if(Directory.Exists(path)){
                                    settings.set("screenshot_dir", new_screenshot_location);
                                    //                            CraftManager.settings = new CMSettings(); //re-initialize settings so updated values are set on CraftManager static variables
                                }else{
                                    setting_error_screenshot_dir = "unable to find directory: "+ path;
                                }
                            }
                        });
                        section(rhs_width, ()=>{
                            button("default", ()=>{
                                new_screenshot_location = "<ksp_install>/Screenshots";
                            });                            
                        });                       
                    });

                    if(!String.IsNullOrEmpty(setting_error_screenshot_dir)){
                        label(setting_error_screenshot_dir, "error");                
                    }
                    v_section(()=>{
                        label("enter the full path to your Screenshot folder,\nor use a relative path in this KSP install by starting with '<ksp_install>'");
                        label("Craft Manager will show pictures from this folder when uploading craft to KerbalX & will save pictues taken here when using the UI to grab a new screenshot", "small");                            
                    });
                    
                });

                v_section("dialog.section", () =>{
                   
                    section(() =>{
                        v_section(lhs_width-20, (w2) =>{
                            label("Reset Cache", "h2");
                            label("Wait!! Don't press this button!...ok you can, but you should know what it does", "h3");                            
                        });
                        v_section(rhs_width+20, (w2) =>{
                            GUILayout.Space(20f);
                            button("Delete Cache", "button.delete", ()=>{
                                if(File.Exists(CraftDataCache.cache_path)){
                                    File.Delete(CraftDataCache.cache_path);
                                }
                                CraftData.cache = null;
                                if(CraftManager.main_ui != null){
                                    CraftManager.main_ui.hide();
                                }
                                this.close();
                            });
                        });
                    });

                    label(
                        "Craft Manager uses a cache of info about your craft. Craft info is cached the first time a craft is shown in the UI & then after that it doesn't have " +
                        "to read the craft file unless it gets changed. This lets the Craft Manager UI open much faster than if it had to read and analyse each craft every time it opens " +
                        "(which the stock craft list does)."
                    );
                    label(
                        "The cache automatically gets reset if you change your installed parts or when parts are unlocked (in career mode)."
                    );
                    label(
                        "You shouldn't need to manually reset the cache, but if you think the info about your craft is incorrect then try resetting it.\n" +
                        "You can also reset the cache by deleting the 'craft_data.cache' file in the CraftManager mod folder."
                    );
                });
            });

            section(() =>{
                fspace();
                button("close", "button.large", close);
            });        
        }

        public void setting_section(string setting_name, string label_text, string note){
            setting_section(setting_name, label_text, "Yes", "Yes", note, "");
        }
        public void setting_section(string setting_name, string label_text, string note, string sub_note){
            setting_section(setting_name, label_text, "Yes", "Yes", note, sub_note);
        }
        public void setting_section(string setting_name, string label_text, string active_text, string inactive_text, string note = "", string sub_note = ""){
            section((w2) =>{
                v_section(lhs_width, (w) =>{
                    label(label_text, "h2.tight");
                    if(!String.IsNullOrEmpty(note)){                        
                        label(note, "compact");
                    }
                    if(!String.IsNullOrEmpty(sub_note)){                        
                        label(sub_note, "small.compact");
                    }
                });
                v_section(rhs_width, (w) =>{
                    opt = bool.Parse(settings.get(setting_name));
                    if(!String.IsNullOrEmpty(note)){
                        GUILayout.Space(20f);
                    }
                    button(opt ? active_text : inactive_text, "button" + (opt ? ".down" : ""), () =>{
                        setting_error_open_opts = "";
                        settings.set(setting_name, (!opt).ToString());

                        if(bool.Parse(settings.get("replace_editor_load_button")) == false && bool.Parse(settings.get("use_stock_toolbar")) == false && bool.Parse(settings.get("use_editor_key_shortcuts")) == false){
                            settings.set("replace_editor_load_button", true.ToString());
                            setting_error_open_opts = "You need to have at least 1 of the above three options selected";
                        }

                        if(setting_name == "compact_mode"){
                            CraftManager.main_ui.toggle_compact_mode(!opt, false);
                        }
//                        CraftManager.settings = new CMSettings(); //re-initialize settings so updated values are set on CraftManager static variables

                    });
                });
            });   
        }

        public static void open(GameObject go){           
            go.AddOrGetComponent<SettingsUI>();
        }

        public void close(){            
            GameObject.Destroy(CraftManager.settings_ui);            
        }
    }



}

