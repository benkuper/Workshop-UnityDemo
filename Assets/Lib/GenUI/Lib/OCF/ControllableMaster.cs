using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ControllableMaster 
{
    public static bool debug;

    public static Dictionary<string, Controllable> RegisteredControllables = new Dictionary<string, Controllable>();

    public delegate void ControllableAddedEvent(Controllable controllable);
    public static event ControllableAddedEvent controllableAdded;

    public delegate void ControllableRemovedEvent(Controllable controllable);
    public static event ControllableRemovedEvent controllableRemoved;

    public static void Register(Controllable candidate)
    {
        /*
        if (candidate.id == "")
        {
            Debug.LogWarning("ControllableMaster :: can't register an empty id (" + candidate.name + ")");
            return;
        }

        if (RegisteredControllables.ContainsValue(candidate)) UnRegister(candidate);
        */

        if (!RegisteredControllables.ContainsKey(candidate.id))
        {
            RegisteredControllables.Add(candidate.id, candidate);
            candidate.controllableNameChanged += controllableNameChanged;
            if(controllableAdded != null) controllableAdded(candidate);

            //Debug.Log("Added " + candidate.id);
        }
        else
        {
            Debug.LogWarning("ControllerMaster already contains a Controllable named " + candidate.id);
        }
    }

    public static void UnRegister(Controllable candidate)
    {
        if (RegisteredControllables.ContainsKey(candidate.id))
        {
            candidate.controllableNameChanged -= controllableNameChanged;
            RegisteredControllables.Remove(candidate.id);
            if (controllableRemoved != null) controllableRemoved(candidate);
        }
    }

    private static void controllableNameChanged(Controllable c, string oldName)
    {
        if (RegisteredControllables.ContainsKey(oldName))
        {
            RegisteredControllables.Remove(oldName);
            RegisteredControllables.Add(c.id, c);
        }
    }

    public static void UpdateValue(string target, string property, List<object> values)
    {
        if (RegisteredControllables.ContainsKey(target))
            RegisteredControllables[target].setProp(property, values);
        else
            Debug.LogWarning("Target : \"" + target + "\" is unknown !");
    }

    public static void LoadEveryPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.LoadLatestUsedPreset();
        }
    }

    public static void SaveAllPresets()
    {
        foreach (var controllable in RegisteredControllables)
        {
            controllable.Value.SaveAs();
        }
    }
}