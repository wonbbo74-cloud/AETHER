using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class StartButtonController : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Sprite normalSprite;
    public Sprite hoverSprite;

    public AudioClip hoverSound;
    public AudioClip clickSound;

    public float scaleUpSize = 1.1f;
    public float scaleSpeed = 1.2f;

    public string nextSceneName = "MainScene";

    private Image btnImage;
    private Coroutine scaleCoroutine;
    private bool isHovering;
    private Vector3 originalScale;

    private void Start()
    {
        btnImage = GetComponent<Image>();
        btnImage.sprite = normalSprite;
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        btnImage.sprite = hoverSprite;
        GlobalManager.Audio.PlaySFX(hoverSound);

        isHovering = true;
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleLoop());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        btnImage.sprite = normalSprite;
        isHovering = false;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GlobalManager.Audio.PlaySFX(clickSound);

        // MainManager에게 씬 전환 요청
        FindObjectOfType<MainManager>().StartGame(nextSceneName);
    }

    private IEnumerator ScaleLoop()
    {
        while (isHovering)
        {
            yield return StartCoroutine(ScaleTo(originalScale * scaleUpSize));
            yield return StartCoroutine(ScaleTo(originalScale));
        }
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                target,
                Time.deltaTime * scaleSpeed
            );
            yield return null;
        }
        transform.localScale = target;
    }
}
