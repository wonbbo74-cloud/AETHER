using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Hankyul_WeaponUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Hankyul_Fire fire;

    [Header("Images")]
    [SerializeField] private Image weaponUIImage;
    [SerializeField] private Image weaponImage;

    [Header("Texts")]
    [SerializeField] private TMP_Text fireModeText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text currentAmmoText;
    [SerializeField] private TMP_Text totalAmmoText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Color reloadingColor = Color.black;
    [SerializeField] private Color emptyColorText = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Energy Slots")]
    [SerializeField] private Image[] slots = new Image[5];
    [SerializeField] private Color filledColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.12f);

    [Header("Blink")]
    [SerializeField] private float emptyBlinkInterval = 0.4f;

    private Coroutine emptyBlinkCoroutine;

    private void Reset()
    {
        if (slots == null || slots.Length != 5)
            slots = new Image[5];

        Transform energySlotRoot = transform.Find("EnergySlot");
        if (energySlotRoot != null)
        {
            for (int i = 0; i < 5; i++)
            {
                Transform slot = energySlotRoot.Find($"Slot{i + 1}");
                if (slot != null)
                    slots[i] = slot.GetComponent<Image>();
            }
        }

        Transform fireMode = transform.Find("FireMode");
        if (fireMode != null) fireModeText = fireMode.GetComponent<TMP_Text>();

        Transform energy = transform.Find("Energy");
        if (energy != null) energyText = energy.GetComponent<TMP_Text>();

        Transform currentAmmo = transform.Find("CurrentAmmo");
        if (currentAmmo != null) currentAmmoText = currentAmmo.GetComponent<TMP_Text>();

        Transform totalAmmo = transform.Find("TotalAmmo");
        if (totalAmmo != null) totalAmmoText = totalAmmo.GetComponent<TMP_Text>();

        Transform state = transform.Find("State");
        if (state != null) stateText = state.GetComponent<TMP_Text>();

        Transform weaponUI = transform.Find("WeaponUI");
        if (weaponUI != null) weaponUIImage = weaponUI.GetComponent<Image>();

        Transform weapon = transform.Find("Weapon");
        if (weapon != null) weaponImage = weapon.GetComponent<Image>();
    }

    private void Start()
    {
        if (energyText != null)
            energyText.text = "Energy";

        if (stateText != null)
        {
            stateText.text = "";
            stateText.gameObject.SetActive(false);
        }

        if (weaponImage != null)
            weaponImage.preserveAspect = true;

        RefreshUI();
    }

    private void Update()
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (fire == null) return;

        int currentMagazineAmmo = fire.CurrentMagazineAmmo;
        int reserveAmmo = fire.CurrentReserveAmmo;

        UpdateFireModeText();
        UpdateAmmoTexts(currentMagazineAmmo, reserveAmmo);
        UpdateEnergySlots(currentMagazineAmmo, reserveAmmo);
        UpdateStateText(currentMagazineAmmo, reserveAmmo);
    }

    private void UpdateFireModeText()
    {
        if (fireModeText == null || fire == null) return;

        fireModeText.text = fire.CurrentFireMode == Hankyul_Fire.FireMode.Single
            ? "Single"
            : "Auto";
    }

    private void UpdateAmmoTexts(int currentMagazineAmmo, int reserveAmmo)
    {
        if (currentAmmoText != null)
            currentAmmoText.text = currentMagazineAmmo.ToString();

        if (totalAmmoText != null)
            totalAmmoText.text = reserveAmmo.ToString();
    }

    private void UpdateEnergySlots(int currentMagazineAmmo, int reserveAmmo)
    {
        if (slots == null || slots.Length == 0) return;

        // 칸은 "예비탄 + 현재 탄창" 기준 30발당 1칸으로 보이게 유지
        // 시작: 30 + 120 = 150 -> 5칸
        // 1회 장전 후: 30 + 90 = 120 -> 4칸
        // 2회 장전 후: 30 + 60 = 90 -> 3칸
        // ...
        int totalEnergyAmmo = currentMagazineAmmo + reserveAmmo;
        int activeSlotCount = Mathf.Clamp(Mathf.CeilToInt(totalEnergyAmmo / 30f), 0, 5);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].color = i < activeSlotCount ? filledColor : emptyColor;
        }
    }

    private void UpdateStateText(int currentMagazineAmmo, int reserveAmmo)
    {
        if (stateText == null || fire == null) return;

        if (fire.IsReloading)
        {
            StopEmptyBlink();
            stateText.gameObject.SetActive(true);
            stateText.text = "Reloading...";
            stateText.color = reloadingColor;
            return;
        }

        if (currentMagazineAmmo <= 0 && reserveAmmo <= 0)
        {
            stateText.text = "Empty";
            stateText.color = emptyColorText;

            if (emptyBlinkCoroutine == null)
                emptyBlinkCoroutine = StartCoroutine(BlinkEmpty());

            return;
        }

        StopEmptyBlink();
        stateText.text = "";
        stateText.gameObject.SetActive(false);
    }

    private IEnumerator BlinkEmpty()
    {
        while (true)
        {
            if (stateText != null)
                stateText.gameObject.SetActive(!stateText.gameObject.activeSelf);

            yield return new WaitForSeconds(emptyBlinkInterval);
        }
    }

    private void StopEmptyBlink()
    {
        if (emptyBlinkCoroutine != null)
        {
            StopCoroutine(emptyBlinkCoroutine);
            emptyBlinkCoroutine = null;
        }

        if (stateText != null)
            stateText.gameObject.SetActive(true);
    }

    public void SetFire(Hankyul_Fire targetFire)
    {
        fire = targetFire;
        RefreshUI();
    }
}