using System;
using System.Collections.Generic;

public class GameEvents
{
    public enum EventType
    {
        OnGameStart,
        OnFTUEStarted,
        OnFTUEStopped,
        OnSnakeInitialized,
        OnAteFood,
        OnAteRightFood,
        OnAteWrongFood,
        OnNewSegmentAdded,
        OnSegmentRemoved,
        OnLevelUp,
        OnPowerUpSelected,
        OnPowerUpCompleted,
        OnGameOverPanelTrigger,
        OnGameOver
    }

    private readonly Dictionary<EventType, List<Delegate>> eventListeners = new();

    /// <summary>
    /// Registers an event without parameters.
    /// </summary>
    public void AddEvent(EventType eventType, Action action)
    {
        var listeners = GetOrCreateListeners(eventType);

        if (!listeners.Contains(action))
            listeners.Add(action);
    }

    /// <summary>
    /// Registers an event with one parameter.
    /// </summary>
    public void AddEvent<T>(EventType eventType, Action<T> action)
    {
        var listeners = GetOrCreateListeners(eventType);

        if (!listeners.Contains(action))
            listeners.Add(action);
    }

    /// <summary>
    /// Removes an event without parameters.
    /// </summary>
    public void RemoveEvent(EventType eventType, Action action)
    {
        RemoveListener(eventType, action);
    }

    /// <summary>
    /// Removes an event with one parameter.
    /// </summary>
    public void RemoveEvent<T>(EventType eventType, Action<T> action)
    {
        RemoveListener(eventType, action);
    }

    /// <summary>
    /// Triggers an event without parameters.
    /// </summary>
    public void TriggerEvent(EventType eventType)
    {
        if (!eventListeners.TryGetValue(eventType, out var listeners))
            return;

        foreach (var listener in listeners)
        {
            if (listener is Action action)
                action.Invoke();
        }
    }

    /// <summary>
    /// Triggers an event with one parameter.
    /// Supports parameterless listeners as well.
    /// </summary>
    public void TriggerEvent<T>(EventType eventType, T value)
    {
        if (!eventListeners.TryGetValue(eventType, out var listeners))
            return;

        foreach (var listener in listeners)
        {
            switch (listener)
            {
                case Action<T> actionWithParam:
                    actionWithParam.Invoke(value);
                    break;

                case Action action:
                    action.Invoke();
                    break;
            }
        }
    }

    private List<Delegate> GetOrCreateListeners(EventType eventType)
    {
        if (!eventListeners.TryGetValue(eventType, out var listeners))
        {
            listeners = new List<Delegate>();
            eventListeners[eventType] = listeners;
        }

        return listeners;
    }

    private void RemoveListener(EventType eventType, Delegate listener)
    {
        if (!eventListeners.TryGetValue(eventType, out var listeners))
            return;

        listeners.Remove(listener);

        if (listeners.Count == 0)
            eventListeners.Remove(eventType);
    }
}