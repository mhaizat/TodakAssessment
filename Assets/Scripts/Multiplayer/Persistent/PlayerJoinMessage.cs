using Unity.Collections;
using Unity.Netcode;

public struct PlayerJoinMessage : INetworkSerializable
{
    public FixedString128Bytes PlayerId; // use this exact name everywhere

    public PlayerJoinMessage(string id)
    {
        PlayerId = id;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
    }
}
