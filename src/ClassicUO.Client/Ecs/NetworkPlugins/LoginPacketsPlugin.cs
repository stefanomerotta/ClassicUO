using System;
using System.Collections.Generic;
using System.Net;
using ClassicUO.Configuration;
using ClassicUO.IO;
using ClassicUO.Network;
using ClassicUO.Network.Encryption;
using ClassicUO.Utility;
using TinyEcs;

namespace ClassicUO.Ecs.NetworkPlugins;

using PacketsMap = Dictionary<byte, OnPacket>;

readonly struct LoginPacketsPlugin : IPlugin
{
    public void Build(Scheduler scheduler)
    {
        scheduler.AddSystem((
            Res<Settings> settings,
            Res<PacketsMap> packetsMap,
            Res<NetClient> network,
            Res<GameContext> gameCtx
        ) => {
            // server list
            packetsMap.Value[0xA8] = buffer => {
                var reader = new StackDataReader(buffer);
                var flags = reader.ReadUInt8();
                var count = reader.ReadUInt16BE();
                var serverList = new List<(ushort index, string name)>();

                for (var i = 0; i < count; ++i)
                {
                    var index = reader.ReadUInt16BE();
                    var name = reader.ReadASCII(32, true);
                    var percFull = reader.ReadUInt8();
                    var timeZone = reader.ReadUInt8();
                    var address = reader.ReadUInt32BE();

                    serverList.Add((index, name));
                    Console.WriteLine("server entry -> {0}", name);
                }

                network.Value.Send_SelectServer((byte) serverList[0].index);
            };

            // characters list
            packetsMap.Value[0xA9] = buffer => {
                var reader = new StackDataReader(buffer);
                var charactersCount = reader.ReadUInt8();
                var characterNames = new List<string>();
                for (var i = 0; i < charactersCount; ++i)
                {
                    characterNames.Add(reader.ReadASCII(30).TrimEnd('\0'));
                    reader.Skip(30);
                }

                var cityCount = reader.ReadUInt8();
                // bla bla

                network.Value.Send_SelectCharacter(0, characterNames[0], network.Value.LocalIP, gameCtx.Value.Protocol);
            };

            // server relay
            packetsMap.Value[0x8C] = buffer => {
                var reader = new StackDataReader(buffer);
                var ip = reader.ReadUInt32LE();
                var port = reader.ReadUInt16BE();
                var seed = reader.ReadUInt32BE();

                network.Value.Disconnect();
                network.Value.Connect(new IPAddress(ip).ToString(), port);

                if (network.Value.IsConnected)
                {
                    network.Value.EnableCompression();
                    Span<byte> b = [(byte)(seed >> 24), (byte)(seed >> 16), (byte)(seed >> 8), (byte)seed];
                    network.Value.Send(b, true, true);
                    network.Value.Send_SecondLogin(settings.Value.Username, Crypter.Decrypt(settings.Value.Password), seed);
                }
            };
        }, Stages.Startup, ThreadingMode.Single);
    }
}