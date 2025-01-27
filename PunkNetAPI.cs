using System;
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using Mirror;
using UnityEngine;
using System.Collections.Generic;
using static PunkNetAPI.PunkNetAPI.PunkNetwork;
using System.Linq;

namespace PunkNetAPI;

[BepInPlugin("punkalyn.punknet", "PunkNet API", "0.1.0")]
public class PunkNetAPI : BaseUnityPlugin
{
    public static ManualLogSource Log;
    public void Awake()
    {
        Log = base.Logger;
        var harmony = new Harmony("punkalyn.punknet");
        try
        {
            harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Log.LogError($"Exception caught while patching: {ex.Message}");
        }

    }

    public class PunkNetwork : NetworkBehaviour
    {
        Player player;

        public void Awake()
        {
            player = this.GetComponent<Player>();
        }

        public void Start()
        {
            RegisterHandlers();
        }

        public void RegisterHandlers()
        {
            Log.LogInfo("Registering handlers.");
            if (NetworkServer.active)
            {
                // Register handler for receiving messages on the server
                NetworkServer.RegisterHandler<PunkNetMessage>(Server_HandleMessage);
            }

            if (NetworkClient.active)
            {
                // Register handler for receiving messages on the client
                NetworkClient.RegisterHandler<PunkNetMessage>(Client_HandleMessage);
            }

            Writer<PunkNetMessage>.write = PunkWriter;
            Reader<PunkNetMessage>.read = PunkReader;
        }

        public struct PunkNetMessage : NetworkMessage
        {
            public uint netId; // The ID of the player sending the message
            public string modSource; // A string to identify the source of the message
            public Dictionary<string, object> data; // Flexible key-value data for modders to use

            // Constructor to initialize the message
            public PunkNetMessage(uint netId, string modSource, Dictionary<string, object> data)
            {
                this.netId = netId;
                this.modSource = modSource;
                this.data = data;
            }
        }

        public static void PunkWriter(NetworkWriter writer, PunkNetMessage message)
        {
            writer.WriteUInt(message.netId);
            writer.WriteString(message.modSource);

            // Write the dictionary size
            writer.WriteInt(message.data.Count);

            foreach (var pair in message.data)
            {
                writer.WriteString(pair.Key); // Write the key as a string
                var value = pair.Value;

                if (value == null)
                {
                    writer.WriteString("null"); // Indicate null value
                }
                else
                {
                    Type valueType = value.GetType();
                    writer.WriteString(valueType.FullName); // Write the type as a string

                    // Use the correct writer based on the type
                    if (value is int intValue) writer.WriteInt(intValue);
                    else if (value is float floatValue) writer.WriteFloat(floatValue);
                    else if (value is string stringValue) writer.WriteString(stringValue);
                    else if (value is bool boolValue) writer.WriteBool(boolValue);
                    else if (value is uint uintValue) writer.WriteUInt(uintValue);
                    else if (value is long longValue) writer.WriteLong(longValue);
                    else if (value is double doubleValue) writer.WriteDouble(doubleValue);
                    else
                    {
                        Debug.LogError($"Unsupported type: {valueType}");
                    }
                }
            }
        }

        public static PunkNetMessage PunkReader(NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            string modSource = reader.ReadString();

            int dataCount = reader.ReadInt();
            var data = new Dictionary<string, object>();

            for (int i = 0; i < dataCount; i++)
            {
                string key = reader.ReadString();
                string typeName = reader.ReadString(); // Read the type name

                if (typeName == "null")
                {
                    data[key] = null;
                }
                else
                {
                    // Use the type name to determine the correct reader
                    switch (typeName)
                    {
                        case "System.Int32":
                            data[key] = reader.ReadInt();
                            break;
                        case "System.Single":
                            data[key] = reader.ReadFloat();
                            break;
                        case "System.String":
                            data[key] = reader.ReadString();
                            break;
                        case "System.Boolean":
                            data[key] = reader.ReadBool();
                            break;
                        case "System.UInt32":
                            data[key] = reader.ReadUInt();
                            break;
                        case "System.Int64":
                            data[key] = reader.ReadLong();
                            break;
                        case "System.Double":
                            data[key] = reader.ReadDouble();
                            break;
                        default:
                            Debug.LogError($"Unsupported type: {typeName}");
                            break;
                    }
                }
            }

            return new PunkNetMessage(netId, modSource, data);
        }

        private void Server_HandleMessage(NetworkConnectionToClient conn, PunkNetMessage message)
        {
            if (!NetworkServer.active || message.netId == 0)
            {
                Log.LogWarning($"[Server_HandleMessage] Invalid netId: {message.netId}");
                return;
            }
            // Process the message through the API's handlers
            PunkNet.HandleMessage(message);

            // Handle the message on the server
            Log.LogInfo($"Server received message from {message.netId}: {message.modSource}. {message.data}");

            // Broadcast the message to all clients
            NetworkServer.SendToAll(message);
        }

        private void Client_HandleMessage(PunkNetMessage message)
        {
            PunkNet.HandleMessage(message);
            // Handle the message on the client
            Log.LogInfo($"Client received message: {message.netId}: {message.modSource}.");
        }
    }

    [HarmonyPatch]
    public static class PunkNetPatches
    {
        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPrefix]
        public static void Network_Prefix(Player __instance)
        {
            // Check if PunkNet is already attached
            if (!__instance.gameObject.GetComponent<PunkNetwork>())
            {
                // Add PunkNet component to the Player
                PunkNetwork punkNet = __instance.gameObject.AddComponent<PunkNetwork>();
                // Log success
                Log.LogInfo("PunkNet added to player at startup.");
            }
            else
            {
                // Log if PunkNet is already present
                Log.LogInfo("PunkNet already present in player.");
            }
        }

        [HarmonyPatch(typeof(ChatBehaviour), "Send_ChatMessage")]
        [HarmonyPrefix]
        static bool Send_Chat_Prefix(ref string _message, ChatBehaviour __instance)
        {
            if (string.IsNullOrEmpty(_message)) return true;

            // Check for the "/em " command
            if (!_message.StartsWith("/debug", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                Dictionary<string, object> dict = new Dictionary<string, object>();
                dict["dick"] = "fuckedup";
                dict["int"] = 1;
                Player player = Player._mainPlayer;
                PunkNetwork punkNet = player.GetComponent<PunkNetwork>();
                var content = new PunkNetwork.PunkNetMessage
                {
                    netId = player.netId,
                    modSource = "PunkNetAPI",
                    data = dict
                };
                Log.LogInfo("Send from Server:");
                NetworkServer.SendToAll(content);

                Log.LogInfo("Send from Client:");
                NetworkClient.Send(content);
                return false;
            }
        }
    }
}

public static class PunkNet
{
    private static List<IMessageHandler> messageHandlers = new List<IMessageHandler>();

    /// <summary>
    /// A handler method that allows custom logic processing when receiving specific types of PunkNetMessage.
    /// </summary>
    /// <param name="handler"> Represents a (class : IMessageHandler) in which a PunkHandler method has been declared.</param>
    public static void RegisterPunkHandler(IMessageHandler handler)
    {
        messageHandlers.Add(handler);
    }

    // Method for handling incoming messages (to be called in PunkNet's message processing)
    public static void HandleMessage(PunkNetMessage message)
    {
        foreach (var handler in messageHandlers)
        {
            handler.PunkHandler(message);
        }
    }

    /// <summary>
    /// Used for clients to send information to the server. Automatically has player.netId attached.
    /// </summary>
    /// <param name="modSource">The name of your mod, as a string parameter, for uniqueness among PunkNetMessages</param>
    /// <param name="data">Keys are identifying strings for your use. Values are any common data type. Handles most common types natively.</param>
    public static void ToServerMessage(string modSource, Dictionary<string, object> data)
    {
        Player player = Player._mainPlayer;
        uint netId = player.netId;
        var message = new PunkNetMessage(netId, modSource, data);
        NetworkClient.Send(message);
    }

    /// <summary>
    /// Used for servers to send information to (ALL!) clients. If your mod requires targetting specific players, make sure to include that in your data dictionary and handler methods.
    /// </summary>
    /// <param name="modSource">The name of your mod, as a string parameter, for uniqueness among PunkNetMessages</param>
    /// <param name="data">Keys are identifying strings for your use. Values are any common data type. Handles most common types natively.</param>
    public static void ToClientsMessage(string modSource, Dictionary<string, object> data)
    {
        Player player = Player._mainPlayer;
        uint netId = player.netId;
        var message = new PunkNetMessage(netId, modSource, data);
        NetworkServer.SendToAll(message);
    }

    /// <summary>
    /// Used for specific player targetting when necessary.
    /// </summary>
    /// <param name="targetNetId"></param>
    /// <param name="message"></param>
    public static void ToTargetMessage(uint targetNetId, string modSource, Dictionary<string, object> data)
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("[PunkNetAPI] Command called from non-server location.");
            return;
        }

        NetworkConnection connection = NetworkServer.connections.Values.FirstOrDefault(conn => conn.identity != null && conn.identity.netId == targetNetId);

        if (connection != null)
        {
            Player player = Player._mainPlayer;
            uint netId = player.netId;
            var message = new PunkNetMessage(netId, modSource, data);
            NetworkServer.SendToAll(message);
            connection.Send(message);
            Debug.Log($"[PunkNetAPI] Send message to netId: {targetNetId}");
        }
        else
        {
            Debug.LogWarning($"[PunkNetAPI] No connection found for netId: {targetNetId}");
        }
    }

    /// <summary>
    /// A shorthand method to send a single key/value pair to the server without building a new dictionary.
    /// </summary>
    /// <param name="modSource"></param>
    /// <param name="customKey"></param>
    /// <param name="customValue"></param>
    public static void SingleToServerMessage(string modSource, string customKey, object customValue)
    {
        var data = new Dictionary<string, object>
        {
            { customKey, customValue }
        };
        ToServerMessage(modSource, data);
    }

    /// <summary>
    /// A shorthand method to send a single key/value pair to ALL clients without building a new dictionary.
    /// </summary>
    /// <param name="modSource"></param>
    /// <param name="customKey"></param>
    /// <param name="customValue"></param>
    public static void SingleToClientsMessage(string modSource, string customKey, object customValue)
    {
        var data = new Dictionary<string, object>
        {
            { customKey, customValue }
        };
        ToClientsMessage(modSource, data);
    }
}

public interface IMessageHandler
{
    void PunkHandler(PunkNetMessage message);
}