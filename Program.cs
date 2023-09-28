﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Reflection;

namespace CSGP4POC {
    internal class Program {
        // This Is My First Time Making An XML, I Might Do Dumb Shit


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
         
        param.sfo
         0x00 - 0x8: Head
         0x08 - Param Labels
         0x0C - Param Values
         0x10 - Param Count
            
         starting at 0x20:
         Param Offsets every 16 bytes
        
        */

        // Pass Game Root As Arg
        static void Main(string[] args) { // ver 0.5.1
            string[] RequiredVariables = new string[] { "APP_VER", "CATEGORY", "CONTENT_ID", "TITLE_ID", "VERSION" };


            // Main Variables
            byte[] DataBuffer;
            int chunk_count, scenario_count, default_id;
            int[] scenario_types, initial_chunk_count, scenario_chunk_range;
            string app_ver, version, content_id, title_id = "CUSA12345", passcode = "00000000000000000000000000000000", category = "?";
            string[] ChunkLabelArray, ParamNameArray;
            var TimeStamp = $"{DateTime.Now.GetDateTimeFormats()[78]}";

            // Base Xml And XmlDeclaration
            XmlElement chunk; XmlElement scenario;
            var GP4 = new XmlDocument();
            var Declaration = GP4.CreateXmlDeclaration("1.1", "utf-8", "yes");

            void LoadParameterLabels(string[] StringArray) {
                int byteIndex = 0;
                StringBuilder Builder;
                
                for(int stringIndex = 0; stringIndex < StringArray.Length; stringIndex++) {
                    Builder = new StringBuilder();

                    while(DataBuffer[byteIndex] != 0)
                        Builder.Append(Encoding.UTF8.GetString(new byte[] { DataBuffer[byteIndex++] })); // Just Take A Byte, You Fussy Prick

                    byteIndex++;
                    StringArray[stringIndex] = Builder.ToString();
                }
            }


            // Read playgo-chunks.dat And Param.sfo To Get Most Variables
            using(var playgo = File.OpenRead($@"{args[0]}\sce_sys\playgo-chunk.dat")) {
                // Read Chunk Count
                playgo.Position = 0x0A;
                chunk_count = (byte)playgo.ReadByte();
                ChunkLabelArray = new string[chunk_count];

                // Read Scenario Count
                playgo.Position = 0x0E;
                scenario_count = (byte)playgo.ReadByte();
                scenario_types = new int[scenario_count];
                initial_chunk_count = new int[scenario_count];
                scenario_chunk_range = new int[scenario_count];

                // Read Default Scenario Id
                playgo.Position = 0x14;
                default_id = (byte)playgo.ReadByte();

                // Read Content ID Here Instead Of The .sfo Because Meh, User Has Bigger Issues If Those Aren't the Same
                DataBuffer = new byte[36];
                playgo.Position = 0x40;
                playgo.Read(DataBuffer, 0, 36);
                content_id = Encoding.UTF8.GetString(DataBuffer);

                // Get Chunk Label Start Address From Pointer
                DataBuffer = new byte[4];
                playgo.Position = 0xD0;
                playgo.Read(DataBuffer, 0, 4);
                var ChunkLabelPointer = BitConverter.ToInt32(DataBuffer, 0);

                // Get Length Of Chunk Label Byte Array
                playgo.Position = 0xD4;
                playgo.Read(DataBuffer, 0, 4);
                var ChunkLabelArrayLength = BitConverter.ToInt32(DataBuffer, 0);

                // Load Scenario(s)
                playgo.Position = 0xE0;
                playgo.Read(DataBuffer, 0, 4);
                var scenarioPointer = BitConverter.ToInt32(DataBuffer, 0);
                for(int scenario_index = 0; scenario_index < scenario_count - 1; scenario_index++) {
                    // Get Scenario Type
                    playgo.Position = scenarioPointer;
                    scenario_types[scenario_index] = (byte)playgo.ReadByte();

                    // Get Scenario initial_chunk_count
                    playgo.Position = (scenarioPointer + 0x14);
                    playgo.Read(DataBuffer, 2, 2);
                    initial_chunk_count[scenario_index] = BitConverter.ToInt16(DataBuffer, 2);
                    playgo.Read(DataBuffer, 2, 2);
                    scenario_chunk_range[scenario_index] = BitConverter.ToInt16(DataBuffer, 2);
                    scenarioPointer += 0x20;
                }

                // Load Chunk Labels
                DataBuffer = new byte[ChunkLabelArrayLength];
                playgo.Position = ChunkLabelPointer;
                playgo.Read(DataBuffer, 0, DataBuffer.Length);
                LoadParameterLabels(ChunkLabelArray);
            }

            // Get Length Of Chunk Label String Array
            using(var sfo = File.OpenRead($@"{args[0]}\sce_sys\param.sfo")) {
                sfo.Position = 0x8;
                DataBuffer = new byte[4];
                sfo.Read(DataBuffer, 0, 4);
                var ParamNameArrayPointer = BitConverter.ToInt32(DataBuffer, 0);

                sfo.Position = 0x0C;
                sfo.Read(DataBuffer, 0, 4);
                var ParamVariablesPointer = BitConverter.ToInt32(DataBuffer, 0);

                sfo.Position = 0x10;
                sfo.Read(DataBuffer, 0, 4);
                var ParamNameArrayLength = BitConverter.ToInt32(DataBuffer, 0);

                int[] ParameterOffsets = new int[ParamNameArrayLength];

                // Load Parameter Names
                DataBuffer = new byte[ParamVariablesPointer - ParamNameArrayPointer];
                ParamNameArray = new string[ParamNameArrayLength];
                sfo.Position = ParamNameArrayPointer;
                sfo.Read(DataBuffer, 0, DataBuffer.Length);
                LoadParameterLabels(ParamNameArray);

                // Load Parameter Offsets
                sfo.Position = 0x20;
                DataBuffer = new byte[4];
                for(int offsetIndex = 0; offsetIndex < ParamNameArrayLength ; sfo.Position += (0x10 - DataBuffer.Length)) {
                    sfo.Read(DataBuffer, 0, 4);
                    ParameterOffsets[offsetIndex] = ParamVariablesPointer + BitConverter.ToInt32(DataBuffer, 0);
                    offsetIndex++;
                }

                // Load The Rest Of The .sfo Variables
                for(int Index = 0; Index < ParamNameArrayLength; Index++)
                    if(RequiredVariables.Contains(ParamNameArray[Index])) {
                        sfo.Position = ParameterOffsets[Index];
                        DataBuffer = new byte[4];

                        switch(ParamNameArray[Index]) { // I'm Too Tired to think of a more elegant solution right now. If it works, it works

                            case "APP_VER":
                                DataBuffer = new byte[5];
                                sfo.Read(DataBuffer, 0, 5);
                                app_ver = Encoding.UTF8.GetString(DataBuffer);
                                break;
                            case "CATEGORY":
                                sfo.Read(DataBuffer, 0, 2);
                                category = Encoding.UTF8.GetString(DataBuffer, 0, 2);
                                break;
                            case "CONTENT_ID":
                                DataBuffer = new byte[36];
                                sfo.Read(DataBuffer, 0, 36);
                                content_id = Encoding.UTF8.GetString(DataBuffer);
                                break;
                            case "TITLE_ID":
                                DataBuffer = new byte[9];
                                sfo.Read(DataBuffer, 0, 9);
                                title_id = Encoding.UTF8.GetString(DataBuffer);
                                break;
                            case "VERSION":
                                DataBuffer = new byte[5];
                                sfo.Read(DataBuffer, 0, 5);
                                version = Encoding.UTF8.GetString(DataBuffer);
                                break;
                        }
                    }
            }


            // Create Base .gp4 Elements
            var RootNode = GP4.CreateElement("psproject");
                RootNode.SetAttribute("fmt", "GP4");
                RootNode.SetAttribute("version", "1000");

            var volume = GP4.CreateElement("volume");

            var volume_type = GP4.CreateElement("volume_type");
                volume_type.InnerText = $"ps4_{(category == "gd" ? "pkg_app" : "patch_pkg")}";
            
            var volume_id = GP4.CreateElement("volume_id");
                volume_id.InnerText = "PS4VOLUME";
            
            var volume_ts = GP4.CreateElement("volume_ts");
                volume_ts.InnerText = TimeStamp;

            var package = GP4.CreateElement("package");
                package.SetAttribute("content_id", content_id);
                package.SetAttribute("passcode", passcode);
                package.SetAttribute("storage_type", (category == "gp" ? "digital25" : "digital50"));
                package.SetAttribute("app_type", "full");

            var chunk_info = GP4.CreateElement("chunk_info");
                chunk_info.SetAttribute("chunk_count", $"{chunk_count}");
                chunk_info.SetAttribute("scenario_count", $"{scenario_count}");

            var chunks = GP4.CreateElement("chunks");

            var scenarios = GP4.CreateElement("scenarios");
                scenarios.SetAttribute("default_id", $"{default_id}");

            for(int id = 0; id < scenario_count - 1; id++) {
                scenario = GP4.CreateElement("scenario");
                scenario.SetAttribute("id", $"{id}");
                scenario.SetAttribute("type", $"{id}");
                scenario.SetAttribute("initial_chunk_count", $"{initial_chunk_count[id]}");
                scenario.SetAttribute("label", $"{id}");
                scenario.InnerText = $"0-{scenario_chunk_range[id]}";
            }



            // Build .gp4 Structure
            GP4.AppendChild(Declaration);
            GP4.AppendChild(RootNode);
            RootNode.AppendChild(volume);
            volume.AppendChild(volume_type);
            volume.AppendChild(volume_id);
            volume.AppendChild(volume_ts);
            volume.AppendChild(package);
            volume.AppendChild(chunk_info);
            chunk_info.AppendChild(chunks);
            for(int chunk_id = 0; chunk_id < chunk_count; chunk_id++) {
                chunk = GP4.CreateElement("chunk");
                chunk.SetAttribute("id", $"{chunk_id}");
                chunk.SetAttribute("label", $"{ChunkLabelArray[chunk_id]}");

                chunks.AppendChild(chunk);
            }

            // gee, I wonder what this does
            GP4.Save($@"C:\Users\Blob\Desktop\_{title_id}-{(category == "gd" ? "app" : "patch")}.gp4");
        }
    }
}
