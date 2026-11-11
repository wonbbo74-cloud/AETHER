using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 칸을 끌 수 있게 해 주는 작은 도우미. 끄는 동안의 모양·놓을 자리 판단은 주인 화면이 맡는다.
///
/// Unity 는 IDragHandler 가 있는 오브젝트에서만 끌기를 시작한다. 예전 모듈 목록은 시작·끝만 받고
/// 이 인터페이스가 없어 끌기 자체가 시작되지 않았다.
/// </summary>
public class AetherDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Func<PointerEventData, bool> BeginDrag;
    public Action<PointerEventData> Drag;
    public Action<PointerEventData> EndDrag;

    private bool active;

    public void OnBeginDrag(PointerEventData eventData)
    {
        active = BeginDrag != null && BeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (active)
            Drag?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!active)
            return;

        active = false;
        EndDrag?.Invoke(eventData);
    }
}
