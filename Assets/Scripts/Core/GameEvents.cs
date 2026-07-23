using System;
using System.Collections.Generic;
using UnityEngine;

public class GameEvents
{
    public enum EventType
    {
        OnGameStart,
        OnFTUEStarted,
        OnFTUEStopped,
        OnAteRightFood,
        OnAteWrongFood,
        OnNewSegmentAdded,
        OnSegmentRemoved,
        OnLevelUp,
        OnPowerUpSelected,
        OnPowerUpCompleted,
        OnGameOver
    }
    
    private Dictionary<EventType, List<Delegate>> events =
        new Dictionary<EventType, List<Delegate>>();
    
    public void AddEvent(EventType eventType, Action action)
    {
        if (!events.TryGetValue(eventType, out List<Delegate> delegates))
        {
            delegates = new List<Delegate>();
            events.Add(eventType, delegates);
        }

        delegates.Add(action);
    }

    public void AddEvent<T>(EventType eventType, Action<T> action)
    {
        if (!events.TryGetValue(eventType, out List<Delegate> delegates))
        {
            delegates = new List<Delegate>();
            events.Add(eventType, delegates);
        }

        delegates.Add(action);
    }

    public void RemoveEvent(EventType eventType, Action action)
    {
        if (!events.TryGetValue(eventType, out List<Delegate> delegates))
            return;

        delegates.Remove(action);

        if (delegates.Count == 0)
            events.Remove(eventType);
    }

    public void RemoveEvent<T>(EventType eventType, Action<T> action)
    {
        if (!events.TryGetValue(eventType, out List<Delegate> delegates))
            return;

        delegates.Remove(action);

        if (delegates.Count == 0)
            events.Remove(eventType);
    }

    public void TriggerEvent(EventType eventType)
    {
        if (!events.TryGetValue(eventType, out List<Delegate> delegates))
            return;

        foreach (Delegate del in delegates)
        {
            del.DynamicInvoke();
        }
    }

    public void TriggerEvent<T>(EventType eventType, T value)
    {
        if (!events.TryGetValue(eventType, out List<Delegate> delegates))
            return;

        foreach (Delegate del in delegates)
        {
            del.DynamicInvoke(value);
        }
    }
}
