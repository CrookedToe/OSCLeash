using System.Collections.Concurrent;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.Modules.OSCLeash.Constants;
using VRCOSC.Modules.OSCLeash.Enums;

namespace VRCOSC.Modules.OSCLeash.Models;

public class LeashParameterHandler
{
    private readonly ConcurrentQueue<RegisteredParameter> _parameterQueue = new();
    private readonly ConcurrentDictionary<string, float> _parameterValues = new();
    private readonly Action<string> _logAction;
    private readonly bool _debugLogging;
    private int _updateCounter;

    public LeashParameterHandler(Action<string> logAction, bool debugLogging = false)
    {
        _logAction = logAction;
        _debugLogging = debugLogging;
    }

    private void DebugLog(string message)
    {
        if (_debugLogging)
        {
            _logAction(message);
        }
    }

    public void EnqueueParameter(RegisteredParameter parameter)
    {
        if (_parameterQueue.Count < LeashConstants.MAX_QUEUE_SIZE)
        {
            _parameterQueue.Enqueue(parameter);
        }
        else
        {
            _logAction("Parameter queue overflow - dropping parameter");
        }
    }

    public bool ValidateParameter(RegisteredParameter? parameter)
    {
        if (parameter == null) return false;
        if (string.IsNullOrEmpty(parameter.Name)) return false;
        
        try
        {
            switch (parameter.Lookup)
            {
                case LeashParameter.IsGrabbed:
                    parameter.GetValue<bool>();
                    break;
                    
                case LeashParameter.Stretch:
                case LeashParameter.ZPositive:
                case LeashParameter.ZNegative:
                case LeashParameter.XPositive:
                case LeashParameter.XNegative:
                case LeashParameter.YPositive:
                case LeashParameter.YNegative:
                    var value = parameter.GetValue<float>();
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        _logAction($"Invalid float value for parameter {parameter.Name}: {value}");
                        return false;
                    }
                    break;
                    
                default:
                    _logAction($"Unknown parameter type: {parameter.Lookup}");
                    return false;
            }
        }
        catch (Exception)
        {
            _logAction($"Invalid value type for parameter {parameter.Name}");
            return false;
        }

        return true;
    }

    public void UpdateParameterValue(string name, float value)
    {
        _parameterValues.AddOrUpdate(name, value, (_, _) => value);
    }

    public void CleanupCache(string currentBaseName)
    {
        if (string.IsNullOrEmpty(currentBaseName)) return;
        if (++_updateCounter % LeashConstants.CACHE_CLEANUP_INTERVAL != 0) return;

        try
        {
            var keysToRemove = _parameterValues.Keys
                .Where(key => key != null && !key.StartsWith(currentBaseName, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (key != null && _parameterValues.TryRemove(key, out _))
                {
                    DebugLog($"Removed cached value for {key}");
                }
            }
        }
        catch (Exception ex)
        {
            _logAction($"Error during cache cleanup: {ex.Message}");
        }
    }

    public void Clear()
    {
        _parameterQueue.Clear();
        _parameterValues.Clear();
    }

    public bool TryDequeueParameter(out RegisteredParameter? parameter)
    {
        return _parameterQueue.TryDequeue(out parameter);
    }
} 