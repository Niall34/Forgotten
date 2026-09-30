using System;
using System.IO;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEditor;
using UnityEngine;

public static class ChatPeer
{
    [Serializable] private class Config { public AppSettings settings; public string room; }
    private static LoadBalancingClient client;
    private static Config config;
    private static int stage;
    private static double deadline;
    private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    public static void Run()
    {
        config = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(Root, "peer.json")));
        client = new LoadBalancingClient();
        client.NickName = "ChatPeer";
        client.EventReceived += OnEvent;
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update += Tick;
        if (!client.ConnectUsingSettings(config.settings)) Finish("FAIL: connection rejected", 1);
    }
    private static void Tick()
    {
        client.Service();
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAIL: timeout " + client.State, 1); return; }
        if (stage == 0 && client.State == ClientState.ConnectedToMasterServer)
        {
            client.OpJoinRoom(new EnterRoomParams { RoomName = config.room });
            stage = 1;
        }
        else if (stage == 1 && client.InRoom)
        {
            client.OpRaiseEvent(71, "<b>Peer hello</b>", new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
            stage = 2;
        }
    }
    private static void OnEvent(EventData data)
    {
        if (data.Code == 71 && data.Sender != client.LocalPlayer.ActorNumber && data.CustomData as string == "Host reply")
            Finish("PASS: separate process received host reply", 0);
    }
    private static void Finish(string result, int exit)
    {
        File.WriteAllText(Path.Combine(Root, "result.txt"), result);
        Debug.Log(result);
        EditorApplication.update -= Tick;
        client.Disconnect();
        EditorApplication.Exit(exit);
    }
}
