using System;
using UnityEngine;

[Serializable]
public class CodebreakerLevelConfig_Final
{
    public int level = 1;
    public int digitCount = 4;
    public bool allowDuplicate = false;
    [Min(1)] public int maxSameDigitCount = 1;
    [Min(0)] public int minDuplicatePairCount = 0;
    [Min(0)] public int maxDuplicatePairCount = 0;
}

[Serializable]
public class CodebreakerLevelConfig : CodebreakerLevelConfig_Final
{
}

public struct CodebreakerLogEntry
{
    public int level;
    public int attemptNumber;
    public string input;
    public string result;

    public CodebreakerLogEntry(int level, int attemptNumber, string input, string result)
        : this()
    {
        this.level = level;
        this.attemptNumber = attemptNumber;
        this.input = input;
        this.result = result;
    }
}

public struct CodebreakerLogEntry_Final
{
    public int level;
    public int attemptNumber;
    public string input;
    public string result;

    public CodebreakerLogEntry_Final(int level, int attemptNumber, string input, string result)
    {
        this.level = level;
        this.attemptNumber = attemptNumber;
        this.input = input;
        this.result = result;
    }
}
