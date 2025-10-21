using Unity.Netcode;

/// <summary>
/// Message used to notify the server that a player has joined.
/// Compatible with the previous PlayerId naming.
/// </summary>
public struct PlayerJoinMessage : INetworkSerializable
{
    /// <summary>
    /// Unique player identifier.
    /// </summary>
    public string PlayerId;

    /// <summary>
    /// Constructor for creating a new join message.
    /// </summary>
    /// <param name="id">The player's unique ID.</param>
    public PlayerJoinMessage(string id)
    {
        PlayerId = id;
    }

    /// <summary>
    /// Serialize/deserialize the message for network transport.
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
    }
}
