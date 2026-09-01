namespace KAM.SaveSystem
{
    public interface ISaveable
    {
        string SaveableType { get; }
        string CaptureStateJson();
        void RestoreStateJson(string json);
    }
}
