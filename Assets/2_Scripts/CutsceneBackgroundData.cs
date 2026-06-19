using System.Collections.Generic;

[System.Serializable]
public class CutsceneBackgroundEntry
{
    public string endingKey;
    public int lineIndex;
    public string background;
}

[System.Serializable]
public class CutsceneBackgroundFile
{
    public List<CutsceneBackgroundEntry> entries;
}