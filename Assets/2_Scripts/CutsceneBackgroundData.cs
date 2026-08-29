using System.Collections.Generic;

[System.Serializable]
public class CutsceneBackgroundEntry
{
    public string endingKey;
    public int lineIndex;
    public string background;
    public string backgroundMale;
    public string backgroundFemale;
}

[System.Serializable]
public class CutsceneBackgroundFile
{
    public List<CutsceneBackgroundEntry> entries;
}