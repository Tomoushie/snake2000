// Game/Gameplay/Movement/Tools/MovementProfiler.cs
using System;
using System.Diagnostics;
using System.Collections.Generic;

public class MovementProfiler
{
    private Dictionary<Type, Stopwatch> _stopwatches = new();
    private Dictionary<Type, float> _totalTimes = new();
    private Dictionary<Type, int> _callCounts = new();

    public void StartProfiling(Type systemType)
    {
        if (!_stopwatches.ContainsKey(systemType))
        {
            _stopwatches[systemType] = new Stopwatch();
            _totalTimes[systemType] = 0.0f;
            _callCounts[systemType] = 0;
        }
        _stopwatches[systemType].Restart();
    }

    public void StopProfiling(Type systemType)
    {
        if (_stopwatches.ContainsKey(systemType))
        {
            _stopwatches[systemType].Stop();
            _totalTimes[systemType] += _stopwatches[systemType].ElapsedTicks / (float)Stopwatch.Frequency * 1000.0f;
            _callCounts[systemType]++;
        }
    }

    public float GetAverageTimeMs(Type systemType)
    {
        if (_callCounts.ContainsKey(systemType) && _callCounts[systemType] > 0)
        {
            return _totalTimes[systemType] / _callCounts[systemType];
        }
        return 0.0f;
    }

    public void PrintStats()
    {
        Console.WriteLine("--- Movement Profiling Stats ---");
        foreach (var kvp in _totalTimes)
        {
            Console.WriteLine($"{kvp.Key.Name}: Avg Time = {GetAverageTimeMs(kvp.Key):F3}ms, Calls = {_callCounts[kvp.Key]}");
        }
    }

    public void Reset()
    {
        foreach (var sw in _stopwatches.Values) sw.Reset();
        _totalTimes.Clear();
        _callCounts.Clear();
    }
}