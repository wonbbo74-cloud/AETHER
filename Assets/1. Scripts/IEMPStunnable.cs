using UnityEngine;

public interface IEMPStunnable
{
    bool IsEMPActive { get; }
    void ApplyEMP(float duration);
}