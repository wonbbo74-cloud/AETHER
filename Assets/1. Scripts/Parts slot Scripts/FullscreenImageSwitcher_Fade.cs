using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AETHER
{
    public enum 선택화면상태
    {
        기본,
        플레이어1,
        플레이어2
    }

    public class FullscreenImageSwitcher_Fade : MonoBehaviour
    {
        [Header("배경(바뀔 대상)")]
        [SerializeField] private Image 전체화면이미지;

        [Header("페이드 오버레이(위에 덮는 Image)")]
        [SerializeField] private Image 페이드오버레이이미지;

        [Header("상태별 Sprite")]
        [SerializeField] private Sprite 기본이미지;
        [SerializeField] private Sprite 플레이어1이미지;
        [SerializeField] private Sprite 플레이어2이미지;

        [Header("페이드 설정")]
        [SerializeField, Range(0.05f, 1f)] private float 페이드시간 = 0.2f;
        [SerializeField] private bool 시작시기본이미지로초기화 = true;
        [SerializeField] private bool 전환중입력잠금 = true;

        [Header("현재 상태(읽기 전용)")]
        [SerializeField] private 선택화면상태 현재상태 = 선택화면상태.기본;

        private bool 전환중 = false;

        private void Awake()
        {
            if (전체화면이미지 == null)
            {
                Debug.LogError("[FullscreenImageSwitcher_Fade] 전체화면이미지(Image)가 연결되지 않았습니다.");
                enabled = false;
                return;
            }
            if (페이드오버레이이미지 == null)
            {
                Debug.LogError("[FullscreenImageSwitcher_Fade] 페이드오버레이이미지(Image)가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            // 오버레이는 기본적으로 투명
            SetOverlayAlpha(0f);
            페이드오버레이이미지.raycastTarget = 전환중입력잠금; // 전환 중 클릭 막기용

            if (시작시기본이미지로초기화)
            {
                현재상태 = 선택화면상태.기본;
                if (기본이미지 != null) 전체화면이미지.sprite = 기본이미지;
            }
        }

        // 버튼에 연결할 함수들 (토글)
        public void 토글_플레이어1()
        {
            if (전환중 && 전환중입력잠금) return;
            var 목표 = (현재상태 == 선택화면상태.플레이어1) ? 선택화면상태.기본 : 선택화면상태.플레이어1;
            StartCoroutine(전환코루틴(목표));
        }

        public void 토글_플레이어2()
        {
            if (전환중 && 전환중입력잠금) return;
            var 목표 = (현재상태 == 선택화면상태.플레이어2) ? 선택화면상태.기본 : 선택화면상태.플레이어2;
            StartCoroutine(전환코루틴(목표));
        }

        public void 기본화면()
        {
            if (전환중 && 전환중입력잠금) return;
            StartCoroutine(전환코루틴(선택화면상태.기본));
        }

        private IEnumerator 전환코루틴(선택화면상태 목표상태)
        {
            if (목표상태 == 현재상태) yield break;

            Sprite 목표스프라이트 = 목표상태 switch
            {
                선택화면상태.기본 => 기본이미지,
                선택화면상태.플레이어1 => 플레이어1이미지,
                선택화면상태.플레이어2 => 플레이어2이미지,
                _ => 기본이미지
            };

            if (목표스프라이트 == null)
            {
                Debug.LogError($"[FullscreenImageSwitcher_Fade] {목표상태} Sprite가 비어있습니다.");
                yield break;
            }

            전환중 = true;
            if (전환중입력잠금) 페이드오버레이이미지.raycastTarget = true;

            // 1) 페이드 인 (0 -> 1)
            yield return FadeOverlay(0f, 1f, 페이드시간);

            // 2) 중간에 이미지 교체
            전체화면이미지.sprite = 목표스프라이트;
            현재상태 = 목표상태;

            // 3) 페이드 아웃 (1 -> 0)
            yield return FadeOverlay(1f, 0f, 페이드시간);

            if (전환중입력잠금) 페이드오버레이이미지.raycastTarget = false;
            전환중 = false;
        }

        private IEnumerator FadeOverlay(float 시작알파, float 목표알파, float 시간)
        {
            float t = 0f;
            while (t < 시간)
            {
                t += Time.unscaledDeltaTime; // UI는 보통 unscaled가 안정적
                float 진행 = Mathf.Clamp01(t / 시간);
                float 알파 = Mathf.Lerp(시작알파, 목표알파, 진행);
                SetOverlayAlpha(알파);
                yield return null;
            }
            SetOverlayAlpha(목표알파);
        }

        private void SetOverlayAlpha(float 알파)
        {
            var c = 페이드오버레이이미지.color;
            c.a = 알파;
            페이드오버레이이미지.color = c;
        }
    }
}