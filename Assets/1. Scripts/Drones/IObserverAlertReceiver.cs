using UnityEngine;

public interface IObserverAlertReceiver
{
    void OnObserverAlert(Vector3 alertPosition, float searchDuration);
}
