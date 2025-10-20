using Unity.Netcode;

public struct PlayerJoinMessage : INetworkSerializable
{
    public string PlayerId; // keep same name as before (compatibility)

    public PlayerJoinMessage(string id)
    {
        PlayerId = id;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
    }
}
