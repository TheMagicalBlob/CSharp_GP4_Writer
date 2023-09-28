﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CSGP4POC {
    internal class Program {
        // This Is My First Time Making An XML, I Might Do Dumb Shit
        
        // Pass Game Root As Arg
        static void Main(string[] args) { // ver 0.2

            // Main Variables
            byte chunk_count, scenario_count, default_id;
            byte[] DataBuffer;
            string content_id, passcode = "00000000000000000000000000000000", storage_type, app_type;
            string[] ChunkLabelArray;
            var TimeStamp = $"{DateTime.Now.GetDateTimeFormats()[78]}";

            // Base Xml And XmlDeclaration
            var GP4 = new XmlDocument();
            var Declaration = GP4.CreateXmlDeclaration("1.1", "utf-8", "yes");

            StringBuilder ReadLabel() {


                return new String();
            }

            // Read playgo-chunks.dat And Param.sfo To Get Most Variables
            using(var playgo_chunks_dat = File.OpenRead($@"{args[0]}\sce_sys\playgo-chunk.dat")) {
                playgo_chunks_dat.Position = 0x0A;
                chunk_count = (byte)playgo_chunks_dat.ReadByte();
                ChunkLabelArray = new string[chunk_count];

                playgo_chunks_dat.Position = 0x0E;
                scenario_count = (byte)playgo_chunks_dat.ReadByte();

                playgo_chunks_dat.Position = 0x14;
                default_id = (byte)playgo_chunks_dat.ReadByte();

                DataBuffer = new byte[36];
                playgo_chunks_dat.Position = 0x40;
                playgo_chunks_dat.Read(DataBuffer, 0, 36);
                content_id = Encoding.UTF8.GetString(DataBuffer);


                // Get Length Of Chunk Label String Array
                DataBuffer = new byte[4];
                playgo_chunks_dat.Position = 0xD0;
                playgo_chunks_dat.Read(DataBuffer, 0, 4);
                var ChunkLabelPointer = BitConverter.ToInt32(DataBuffer, 0);
                DataBuffer = new byte[4];
                playgo_chunks_dat.Position = 0xD8;
                playgo_chunks_dat.Read(DataBuffer, 0, 4);
                var EndOfChunkLabels = BitConverter.ToInt32(DataBuffer, 0);

                // Load Chunk Labels
                DataBuffer = new byte[EndOfChunkLabels - ChunkLabelPointer];
                playgo_chunks_dat.Position = ChunkLabelPointer;
                for(int i = 0; i < chunk_count; i++) {
                    playgo_chunks_dat.Read(DataBuffer, 0, DataBuffer.Length);
                    Console.WriteLine($"{DataBuffer.Length} {Encoding.UTF8.GetString(DataBuffer)}");
                    Console.Read();
                }
            }
            using(var param_sfo = File.OpenRead($@"{args[0]}\sce_sys\param.sfo")) {

            }




            var RootNode = GP4.CreateElement("psproject");
            RootNode.SetAttribute("fmt", "GP4");
            RootNode.SetAttribute("version", "1000");

            var volume = GP4.CreateElement("volume");
            var volume_type = GP4.CreateElement("volume_type");
            volume_type.InnerText = "ps4_pkg_app";
            var volume_id = GP4.CreateElement("volume_id");
            volume_id.InnerText = "PS4VOLUME";
            var volume_ts = GP4.CreateElement("volume_ts");
            volume_ts.InnerText = TimeStamp;

            var package = GP4.CreateElement("package");
            package.SetAttribute("content_id", content_id);
            package.SetAttribute("passcode", passcode);
            package.SetAttribute("storage_type", "(storage_type Here)");
            package.SetAttribute("app_type", "(app_type Here)");

            var chunk_info = GP4.CreateElement("chunk_info");
            chunk_info.SetAttribute("chunk_count", $"{chunk_count}");
            chunk_info.SetAttribute("scenario_count", $"{scenario_count}");

            var chunks = GP4.CreateElement("chunks");
            var chunk = GP4.CreateElement("chunk");

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
                chunk.SetAttribute("id", $"{chunk_id}");
                chunk.SetAttribute("label", $"{chunk_id}");
                chunks.AppendChild(chunk);
            }
            // gee, I wonder what this does
            GP4.Save(@"C:\Users\Blob\Desktop\CUSA00557-Test.gp4");
        }
    }
}
