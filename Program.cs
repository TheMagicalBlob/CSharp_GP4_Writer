using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Reflection;
using System.Data;

namespace CSGP4POC {
    internal class Program {
        // TODO: 
        // - figure out pfs compression / chunk bs for certain file formats
        // - Figure Out What To Include And What Not To, And Implement It Without It Being Messy Af
        // - Improve Readability, It's A Bit Messy

       /*
         
        playgo-chunks.dat
         0x00 - 0x8: Head
         0x0A: Chunk Count
         0x10: File End
         0x0E: Scenario Count
         0xE0: Scenario Data Section(s)
         0x14: Default ID

         0xD0: chunk label beggining
         0xD4: Chunk Label Byte Array Length
         0xD8: chunk label end (Padded)
         0xE0: Senario 1 type (*0xE0 + 0x20 For Each Scenario After?)
         0xF0: Scenario Labels
         0xF4: Scenario Label Array Byte Length
         
        param.sfo
         0x00 - 0x8: Head
         0x08 - Param Labels
         0x0C - Param Values
         0x10 - Param Count
            
         starting at 0x20:
         Param Offsets every 16 bytes
        
        */

        // Pass Game Root As Arg
        static void Main(string[] args) { // ver 0.9.3 - Can Function In Certain Cases; I Don't Know Everything That Needs To Be Excluded. orbis-pub-cmd will error out on certain files or formats
                                          // Debug
#if DEBUG
            bool Output = false;
            void W(object o) { Console.WriteLine(o); Output = true; } // Only Wait On Close If Anything's Actually Been Written
            void Read() => Console.Read();
#endif

            string APP_FOLDER = args?[0];
            if(APP_FOLDER == "") Environment.Exit(0);

            // Main Variables
            var TimeStamp = $"{DateTime.Now.GetDateTimeFormats()[78]}";
            var miliseconds = DateTime.Now.Millisecond; // Format Sony Used Doesn't Have Miliseconds, But I Still Wanna Track It For Now

            byte[] BufferArray;
            int chunk_count, scenario_count, default_id, index = 0;
            int[] scenario_types, scenario_chunk_range, initial_chunk_count;
            string app_ver = "", version = "", content_id, title_id = "CUSA12345", passcode = "00000000000000000000000000000000", category = "?";
            string[] chunk_labels, parameter_labels, scenario_labels, file_paths,
            RequiredVariables = new string[] { "APP_VER", "CATEGORY", "CONTENT_ID", "TITLE_ID", "VERSION" };

            // Base Xml And XmlDeclaration
            XmlElement chunk, scenario, file, dir, subdir;
            var GP4 = new XmlDocument();
            var Declaration = GP4.CreateXmlDeclaration("1.1", "utf-8", "yes");


            // Some Files Or Formats Aren't Supposed To Be Included. I Doubt I Got Lucky And My Game Happened To Have Them All, So I'm Probably Missing Shit
            bool FileShouldBeExcluded(string filepath) {

                // To Exclude certain Files From sce_sys while not excluding them entirely
                var filename = filepath.Remove(filepath.LastIndexOf(".")).Substring(filepath.LastIndexOf('\\') + 1);

                string[] blacklist = new string[] {
                    $"sce_sys\\{filename}.dds",
                    "right.sprx",
                    "sce_sys/origin-deltainfo.dat",
                    "sce_sys/psreserved.dat",
                    "sce_sys/playgo-chunk.dat",
                    "sce_sys/playgo-chunk.sha",
                    "sce_sys/playgo-manifest.xml",
                  // \/ (Al Azif)
                    "sce_sys/.digests",
                    "sce_sys/.entry_keys",
                    "sce_sys/.image_key",
                    "sce_sys/.unknown_0x21",
                    "sce_sys/.general_digests",
                    "sce_sys/.unknown_0xC0",
                    "sce_sys/.metas",
                    "sce_sys/.entry_names",
                    "sce_sys/license.dat",
                    "sce_sys/license.info",
                    "sce_sys/selfinfo.dat",
                    "sce_sys/imageinfo.dat",
                    "sce_sys/target-deltainfo.dat",
                    "sce_sys/pubtoolinfo.dat",
                    "sce_sys/app/playgo-chunk.dat",
                    "sce_sys/app/playgo-chunk.sha",
                    "sce_sys/app/playgo-manifest.xml",
                    "sce_sys/icon0.dds",
                    "sce_sys/pic0.dds",
                    "sce_sys/pic1.dds"
                    //
                    };

                foreach(var blacklisted_file_or_folder in blacklist)
                if(filepath.Contains(blacklisted_file_or_folder)) return true;

                return false;
            }

            void LoadParameterLabels(string[] StringArray) {
                int byteIndex = 0;
                StringBuilder Builder;
                
                for(int stringIndex = 0; stringIndex < StringArray.Length; stringIndex++) {
                    Builder = new StringBuilder();

                    while(BufferArray[byteIndex] != 0)
                        Builder.Append(Encoding.UTF8.GetString(new byte[] { BufferArray[byteIndex++] })); // Just Take A Byte, You Fussy Prick

                    byteIndex++;
                    StringArray[stringIndex] = Builder.ToString();
                }
            }


            //////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
            ///--    Parse playgo-chunks.dat And Param.sfo To Get Most Variables    --\\\
            //////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
            using(var playgo = File.OpenRead($@"{args[0]}\sce_sys\playgo-chunk.dat")) {
                // Read Chunk Count
                playgo.Position = 0x0A;
                chunk_count = (byte)playgo.ReadByte();
                chunk_labels = new string[chunk_count];

                // Read Scenario Count
                playgo.Position = 0x0E;
                scenario_count = (byte)playgo.ReadByte();
                scenario_types = new int[scenario_count];
                scenario_labels = new string[scenario_count];
                initial_chunk_count = new int[scenario_count];
                scenario_chunk_range = new int[scenario_count];

                // Read Default Scenario Id
                playgo.Position = 0x14;
                default_id = (byte)playgo.ReadByte();

                // Read Content ID Here Instead Of The .sfo Because Meh, User Has Bigger Issues If Those Aren't the Same
                BufferArray = new byte[36];
                playgo.Position = 0x40;
                playgo.Read(BufferArray, 0, 36);
                content_id = Encoding.UTF8.GetString(BufferArray);

                // Read Chunk Label Start Address From Pointer
                BufferArray = new byte[4];
                playgo.Position = 0xD0;
                playgo.Read(BufferArray, 0, 4);
                var chunk_label_pointer = BitConverter.ToInt32(BufferArray, 0);

                // Read Length Of Chunk Label Byte Array
                playgo.Position = 0xD4;
                playgo.Read(BufferArray, 0, 4);
                var chunk_label_array_length = BitConverter.ToInt32(BufferArray, 0);

                // Load Scenario(s)
                playgo.Position = 0xE0;
                playgo.Read(BufferArray, 0, 4);
                var scenarioPointer = BitConverter.ToInt32(BufferArray, 0);
                for(int scenario_index = 0; scenario_index < scenario_count; scenario_index++) {
                    // Read Scenario Type
                    playgo.Position = scenarioPointer;
                    scenario_types[scenario_index] = (byte)playgo.ReadByte();

                    // Read Scenario initial_chunk_count
                    playgo.Position = (scenarioPointer + 0x14);
                    playgo.Read(BufferArray, 2, 2);
                    initial_chunk_count[scenario_index] = BitConverter.ToInt16(BufferArray, 2);
                    playgo.Read(BufferArray, 2, 2);
                    scenario_chunk_range[scenario_index] = BitConverter.ToInt16(BufferArray, 2);
                    scenarioPointer += 0x20;
                }

                // Load Scenario Label Array Byte Length
                BufferArray = new byte[2];
                playgo.Position = 0xF4;
                playgo.Read(BufferArray, 0, 2);
                var scenario_label_array_length = BitConverter.ToInt16(BufferArray, 0);

                // Load Scenario Label Pointer
                playgo.Position = 0xF0;
                BufferArray = new byte[4];
                playgo.Read(BufferArray, 0, 4);
                var scenario_label_array_pointer = BitConverter.ToInt32(BufferArray, 0);

                // Load Scenario Labels
                playgo.Position = scenario_label_array_pointer;
                BufferArray = new byte[scenario_label_array_length];
                playgo.Read(BufferArray, 0, BufferArray.Length);
                LoadParameterLabels(scenario_labels);

                // Load Chunk Labels
                BufferArray = new byte[chunk_label_array_length];
                playgo.Position = chunk_label_pointer;
                playgo.Read(BufferArray, 0, BufferArray.Length);
                LoadParameterLabels(chunk_labels);
            }


            ////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\
            ///--    Parse param.sfo For Various Parameters    --\\\
            ////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\
            using(var sfo = File.OpenRead($@"{args[0]}\sce_sys\param.sfo")) {
                // Read Pointer For Array Of Parameter Names
                sfo.Position = 0x8;
                BufferArray = new byte[4];
                sfo.Read(BufferArray, 0, 4);
                var ParamNameArrayPointer = BitConverter.ToInt32(BufferArray, 0);

                // Read Base Pointer For .pkg Parameters
                sfo.Position = 0x0C;
                sfo.Read(BufferArray, 0, 4);
                var ParamVariablesPointer = BitConverter.ToInt32(BufferArray, 0);

                // Read Parameter Name Array Length And Initialize Offset Array
                sfo.Position = 0x10;
                sfo.Read(BufferArray, 0, 4);
                var ParamNameArrayLength = BitConverter.ToInt32(BufferArray, 0);
                int[] ParameterOffsets = new int[ParamNameArrayLength];

                // Load Parameter Names
                BufferArray = new byte[ParamVariablesPointer - ParamNameArrayPointer];
                parameter_labels = new string[ParamNameArrayLength];
                sfo.Position = ParamNameArrayPointer;
                sfo.Read(BufferArray, 0, BufferArray.Length);
                LoadParameterLabels(parameter_labels);

                // Load Parameter Offsets
                sfo.Position = 0x20;
                BufferArray = new byte[4];
                for(int offsetIndex = 0; offsetIndex < ParamNameArrayLength ; sfo.Position += (0x10 - BufferArray.Length)) {
                    sfo.Read(BufferArray, 0, 4);
                    ParameterOffsets[offsetIndex] = ParamVariablesPointer + BitConverter.ToInt32(BufferArray, 0);
                    offsetIndex++;
                }

                // Load The Rest Of The Required .pkg Variables From param.sfo
                for(int Index = 0; Index < ParamNameArrayLength; Index++)
                    if(RequiredVariables.Contains(parameter_labels[Index])) { // Ignore Variables Not Needed For .gp4 Project Creation

                        sfo.Position = ParameterOffsets[Index];
                        BufferArray = new byte[4];

                        switch(parameter_labels[Index]) { // I'm Too Tired to think of a more elegant solution right now. If it works, it works

                            case "APP_VER":
                                BufferArray = new byte[5];
                                sfo.Read(BufferArray, 0, 5);
                                app_ver = Encoding.UTF8.GetString(BufferArray);
                                break;
                            case "CATEGORY": // gd / gp
                                sfo.Read(BufferArray, 0, 2);
                                category = Encoding.UTF8.GetString(BufferArray, 0, 2);
                                break;
                            case "CONTENT_ID":
                                BufferArray = new byte[36];
                                sfo.Read(BufferArray, 0, 36);
                                content_id = Encoding.UTF8.GetString(BufferArray);
                                break;
                            case "TITLE_ID":
                                BufferArray = new byte[9];
                                sfo.Read(BufferArray, 0, 9);
                                title_id = Encoding.UTF8.GetString(BufferArray);
                                break;
                            case "VERSION": // Remaster
                                BufferArray = new byte[5];
                                sfo.Read(BufferArray, 0, 5);
                                version = Encoding.UTF8.GetString(BufferArray);
                                break;
                        }
                    }
            } 

            ////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\
            ///--     Read Project Files And Directories     --\\\ this part could use some work
            ////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\
            DirectoryInfo directoryInfo = new DirectoryInfo(args[0]);

            FileInfo[] file_info = directoryInfo.GetFiles(".", SearchOption.AllDirectories); // How The Fuck Does This Find The Keystone?
            DirectoryInfo[] directory_info = directoryInfo.GetDirectories(".", SearchOption.AllDirectories);

            file_paths = new string[file_info.Length];
            for(int file_index = 0; file_index < file_info.Length - 1; file_index++) {
                file_paths[file_index] = file_info[file_index].FullName;
            }

            ///////////////////////\\\\\\\\\\\\\\\\\\\\\\
            ///--     Create Base .gp4 Elements     --\\\
            ///////////////////////\\\\\\\\\\\\\\\\\\\\\\
            var psproject = GP4.CreateElement("psproject");
                psproject.SetAttribute("fmt", "gp4");
                psproject.SetAttribute("version", "1000");

            var volume = GP4.CreateElement("volume");

            var volume_type = GP4.CreateElement("volume_type");
                volume_type.InnerText = $"pkg_{(category == "gd" ? "ps4_app" : "ps4_patch")}";
            
            var volume_id = GP4.CreateElement("volume_id");
                volume_id.InnerText = "PS4VOLUME";
            
            var volume_ts = GP4.CreateElement("volume_ts");
                volume_ts.InnerText = TimeStamp;

            var package = GP4.CreateElement("package");
                package.SetAttribute("content_id", content_id);
                package.SetAttribute("passcode", passcode);
                package.SetAttribute("storage_type", (category == "gp" ? "digital25" : "digital50"));
                package.SetAttribute("app_type", "full");
                if (category == "gp")
                package.SetAttribute("app_path", $"{content_id}-A{app_ver.Replace(".", "")}-V{version.Replace(".", "")}.pkg");

            var chunk_info = GP4.CreateElement("chunk_info");
                chunk_info.SetAttribute("chunk_count", $"{chunk_count}");
                chunk_info.SetAttribute("scenario_count", $"{scenario_count}");


            var chunks = GP4.CreateElement("chunks");
            for(int chunk_id = 0; chunk_id < chunk_count; chunk_id++) {
                chunk = GP4.CreateElement("chunk");
                chunk.SetAttribute("id", $"{chunk_id}");
                if(chunk_labels[chunk_id] == "") {
                    W($"Chunk Label #{chunk_id} Was Null, I Hope This Fix Works For Every Game...");
                    chunk.SetAttribute("label", $"Chunk #{chunk_id}");
                }
                else
                chunk.SetAttribute("label", $"{chunk_labels[chunk_id]}");
                chunks.AppendChild(chunk);
            }


            var scenarios = GP4.CreateElement("scenarios");
                scenarios.SetAttribute("default_id", $"{default_id}");

            for(int id = 0; id < scenario_count; id++) {
                scenario = GP4.CreateElement("scenario");
                scenario.SetAttribute("id", $"{id}");
                scenario.SetAttribute("type", $"{(scenario_types[id] == 1 ? "sp" : "mp")}");
                scenario.SetAttribute("initial_chunk_count", $"{initial_chunk_count[id]}");
                scenario.SetAttribute("label", $"{scenario_labels[id]}");
                scenario.InnerText = $"0-{scenario_chunk_range[id] - 1}";
                scenarios.AppendChild(scenario);
            }


            var files = GP4.CreateElement("files");
            for(int path_index = 0; path_index < file_paths.Length - 1; path_index++) {
                file = GP4.CreateElement("file");
                file.SetAttribute("targ_path", (file_paths[path_index].Replace(args[0] + "\\", string.Empty)).Replace('\\', '/'));
                file.SetAttribute("orig_path", file_paths[path_index]);
                /* 
                   if (FileUsedPfsComporession(file_paths[path_index]))
                   file.SetAttribute("pfs_compression", "enabled");
                   if (FileWantsChunkyBeefStew(file_paths[path_index]))
                   file.SetAttribute("chunks", file_paths[path_index]);
                */
                if (!FileShouldBeExcluded(file_paths[path_index]))
                files.AppendChild(file);
            }



            //////////////////////\\\\\\\\\\\\\\\\\\\\\\
            ///--     rootdir Directory Nesing     --\\\
            //////////////////////\\\\\\\\\\\\\\\\\\\\\\
            
            var rootdir = GP4.CreateElement("rootdir");

            void AppendSubfolder(string _dir, XmlElement node) {
                foreach(string folder in Directory.GetDirectories(_dir)) {
                    subdir = GP4.CreateElement("dir");
                    subdir.SetAttribute("targ_name", folder.Substring(folder.LastIndexOf('\\') + 1));
                    if (folder.Substring(folder.LastIndexOf('\\') + 1) != "about")
                    node.AppendChild(subdir);
                    if(Directory.GetDirectories(folder).Length > 0) AppendSubfolder(folder, subdir);
                    index++;
                }
            }

            foreach(string folder in Directory.GetDirectories(APP_FOLDER)) {
                dir = GP4.CreateElement("dir");
                dir.SetAttribute("targ_name", folder.Substring(folder.LastIndexOf('\\') + 1));
                rootdir.AppendChild(dir);
                if(Directory.GetDirectories(folder).Length > 0) AppendSubfolder(folder, dir);
                index++;
            }

            ////////////////////\\\\\\\\\\\\\\\\\\\\
            ///--     Build .gp4 Structure     --\\\
            ////////////////////\\\\\\\\\\\\\\\\\\\\
            GP4.AppendChild(Declaration);
            GP4.AppendChild(psproject);
            psproject.AppendChild(volume);
            psproject.AppendChild(files);
            psproject.AppendChild(rootdir);
            volume.AppendChild(volume_type);
            volume.AppendChild(volume_id);
            volume.AppendChild(volume_ts);
            volume.AppendChild(package);
            volume.AppendChild(chunk_info);
            chunk_info.AppendChild(chunks);
            chunk_info.AppendChild(scenarios);
#if DEBUG
            var stamp = GP4.CreateComment($"gengp4.exe Alternative {DateTime.Parse(TimeStamp).Minute}:{DateTime.Parse(TimeStamp).Second}.{miliseconds} => {DateTime.Now.Minute}:{DateTime.Now.Second}.{DateTime.Now.Millisecond}");
            GP4.AppendChild(stamp);
#endif

            // gee, I wonder what this does
            GP4.Save($@"C:\Users\Blob\Desktop\{title_id}-{(category == "gd" ? "app" : "patch")}.gp4");

            if (Output) Read();
        }
    }
}
