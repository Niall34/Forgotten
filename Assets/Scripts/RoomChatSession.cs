using System;
using System.Collections.Generic;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Uses the existing PUN room: no separate chat account or global channel.
public sealed class RoomChatSession : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte MessageEvent = 71;
    public const int MaximumCharacters = 120;
    public const int HistoryLimit = 24;
    public const double SendInterval = 1.0;
    private readonly List<string> messages = new List<string>();
    private readonly Dictionary<int, double> receivedAt = new Dictionary<int, double>();
    private double nextSendTime;
    public IReadOnlyList<string> Messages => messages;
    public bool ChatEnabled { get; private set; }
    public event Action Changed;
    public bool IsInRoom => PhotonNetwork.InRoom;

    public override void OnEnable()
    {
        base.OnEnable();
        ChatEnabled = ForgottenGameSettings.IsGameChatEnabled;
        ForgottenGameSettings.Saved += OnSettingsSaved;
    }

    public override void OnDisable()
    {
        ForgottenGameSettings.Saved -= OnSettingsSaved;
        base.OnDisable();
        Clear();
    }

    private void OnSettingsSaved(ForgottenSettingsSnapshot settings)
    {
        ChatEnabled = settings.GameChatEnabled;
        if (!ChatEnabled) Clear();
        Changed?.Invoke();
    }

    public bool TrySend(string value, out string feedback)
    {
        feedback = "";
        if (!ChatEnabled) { feedback = "Chat is off in Settings."; return false; }
        if (!IsInRoom) { feedback = "Disconnected. Join a room to send messages."; return false; }
        string message = CleanText(value, MaximumCharacters);
        if (message.Length == 0) { feedback = "Type a message first."; return false; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextSendTime) { feedback = "Please wait a moment before sending again."; return false; }
        bool sent = PhotonNetwork.RaiseEvent(MessageEvent, message,
            new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.DoNotCache },
            SendOptions.SendReliable);
        if (!sent) { feedback = "Message could not be sent. Check your connection."; return false; }
        nextSendTime = now + SendInterval;
        return true;
    }

    public void OnEvent(EventData data)
    {
        if (data.Code != MessageEvent || !ChatEnabled || !IsInRoom) return;
        if (!(data.CustomData is string body) || body.Length > MaximumCharacters) return;
        // Sender identity comes from Photon, never from an untrusted message payload.
        Player sender = PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
        if (sender == null) return;
        body = CleanText(body, MaximumCharacters);
        if (body.Length == 0) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (receivedAt.TryGetValue(data.Sender, out double previous) && now - previous < 0.75) return;
        receivedAt[data.Sender] = now;
        string name = CleanText(sender.NickName, 24);
        if (name.Length == 0) name = "Player " + sender.ActorNumber;
        if (messages.Count == HistoryLimit) messages.RemoveAt(0);
        messages.Add(name + ": " + body);
        Changed?.Invoke();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        receivedAt.Remove(otherPlayer.ActorNumber);
        Changed?.Invoke();
    }
    public override void OnPlayerEnteredRoom(Player newPlayer) => Changed?.Invoke();
    public override void OnJoinedRoom() => Clear();
    public override void OnLeftRoom() => Clear();
    public override void OnDisconnected(DisconnectCause cause) => Clear();

    private void Clear()
    {
        messages.Clear();
        receivedAt.Clear();
        nextSendTime = 0;
        Changed?.Invoke();
    }

    public static string CleanText(string value, int limit)
    {
        if (string.IsNullOrEmpty(value) || limit <= 0) return "";
        var result = new StringBuilder(Math.Min(limit, value.Length));
        foreach (char c in value)
        {
            // Keep messages on one line and prevent invisible/bidi formatting spoofing.
            if (char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
                continue;
            if (result.Length == limit) break;
            result.Append(c);
        }
        if (result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1])) result.Length--;
        return result.ToString().Trim();
    }
}
