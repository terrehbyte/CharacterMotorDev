using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CallbackGroup
{
    private struct CallbackBind
    {
        public InputActionPhase phase;
        public Action<InputAction.CallbackContext> callback;
    }

    private Dictionary<string, List<CallbackBind>> callbacks = new();
    private InputActionMap boundActionMap;

    public bool IsBound => boundActionMap != null;

    public void BindActionMap(InputActionMap actionMap)
    {
        if(boundActionMap != null)
        {
            throw new Exception("Already bound to an action map! Cannot bind again!");
        }

        boundActionMap = actionMap;

        foreach(var callbackPair in callbacks)
        {
            foreach (var binding in callbackPair.Value)
            {
                ApplyBinding(callbackPair.Key, binding);
            }
        }
    }

    public void UnbindActionMap()
    {
        if(boundActionMap == null) { return; }

        foreach (var callbackPair in callbacks)
        {
            foreach (var binding in callbackPair.Value)
            {
                RemoveBinding(callbackPair.Key, binding);
            }
        }

        callbacks.Clear();
        boundActionMap = null;
    }
    
    public void AddBinding(string actionName, InputActionPhase phase, Action<InputAction.CallbackContext> callback)
    {
        var newBinding = new CallbackBind { phase = phase, callback = callback };

        bool exists = callbacks.TryGetValue(actionName, out var bindings);
        if(!exists){ callbacks.Add(actionName, new()); }
        callbacks[actionName].Add(newBinding);

        if(boundActionMap != null)
        {
            ApplyBinding(actionName, newBinding);
        }
    }
    
    // TODO: Remove binding
    
    private void ApplyBinding(string actionName, CallbackBind binding)
    {
        Debug.Assert(boundActionMap != null);

        switch (binding.phase)
        {
            case InputActionPhase.Disabled:
                throw new Exception("Cannot bind to Disabled event");
            case InputActionPhase.Started:
                boundActionMap[actionName].started += binding.callback;
                break;
            case InputActionPhase.Canceled:
                boundActionMap[actionName].canceled += binding.callback;
                break;
            case InputActionPhase.Performed:
                boundActionMap[actionName].performed += binding.callback;
                break;
            case InputActionPhase.Waiting:
                throw new Exception("Cannot bind to Waiting event");
        }
    }

    private void RemoveBinding(string actionName, CallbackBind binding)
    {
        Debug.Assert(boundActionMap != null);

        switch (binding.phase)
        {
            case InputActionPhase.Disabled:
                throw new Exception("Cannot unbind from Disabled event");
            case InputActionPhase.Started:
                boundActionMap[actionName].started -= binding.callback;
                break;
            case InputActionPhase.Canceled:
                boundActionMap[actionName].canceled -= binding.callback;
                break;
            case InputActionPhase.Performed:
                boundActionMap[actionName].performed -= binding.callback;
                break;
            case InputActionPhase.Waiting:
                throw new Exception("Cannot bind from Waiting event");
        }
    }
}
