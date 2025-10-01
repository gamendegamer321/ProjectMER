namespace ProjectMER.Features.Serializable.Lockers;

[Serializable]
public class SerializableLockerItem
{
    public string Item { get; set; }

    public uint Count { get; set; }
    
    public float Chance { get; set; }
}