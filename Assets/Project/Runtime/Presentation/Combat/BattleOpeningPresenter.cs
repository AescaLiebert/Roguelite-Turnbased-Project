using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Plays the battle's combat-power comparison and initiative intro.</summary>
    public sealed class BattleOpeningPresenter : MonoBehaviour
    {
        public Transform CoreHandContainer => handContainer;
        public Transform CoreActionContainer => actionSlotContainer;

        [Header("Combat Power UI")]
        [SerializeField] private GameObject combatPowerPanel;
        [SerializeField] private TextMeshProUGUI playerCCValueText;
        [SerializeField] private TextMeshProUGUI enemyCCValueText;
        [SerializeField] private TextMeshProUGUI turnDecisionText;

        [Header("Animation Settings")]
        [SerializeField] private float countDuration = 1.0f;

        [Header("Unit Icons")]
        [SerializeField] private Image player1Icon;
        [SerializeField] private Image enemy1Icon;

        [Header("Battle HUD Anchors")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private Transform actionSlotContainer;

        public void HideCoreIntro()
        {
            StopAllCoroutines();
            if (combatPowerPanel != null) combatPowerPanel.SetActive(false);
        }

        /// <summary>Runs the existing CC intro for the Core-owned battle state.</summary>
        public void PlayCoreStartSequence(float playerCC, float enemyCC, bool isPlayerFirst,
            Sprite playerIcon, Sprite opponentIcon, System.Action onComplete)
        {
            gameObject.SetActive(true);
            PrepareIcon(player1Icon, playerIcon);
            PrepareIcon(enemy1Icon, opponentIcon);
            StopAllCoroutines();
            StartCoroutine(AnimateSequence(playerCC, enemyCC, isPlayerFirst, onComplete));
        }

        private static void PrepareIcon(Image icon, Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.gameObject.SetActive(false);
            icon.transform.localScale = Vector3.zero;
        }

        private IEnumerator AnimateSequence(float playerCC, float enemyCC, bool isPlayerFirst, System.Action onComplete)
        {
            if (combatPowerPanel != null) combatPowerPanel.SetActive(true);
            if (turnDecisionText != null) turnDecisionText.gameObject.SetActive(false);

            yield return CountUpTo(playerCCValueText, playerCC);
            yield return CountUpTo(enemyCCValueText, enemyCC);
            yield return AnimateUnitPortraits();
            yield return new WaitForSeconds(0.5f);

            if (turnDecisionText != null)
            {
                turnDecisionText.gameObject.SetActive(true);
                turnDecisionText.text = isPlayerFirst ? "PLAYER TURN" : "ENEMY TURN";
            }
            yield return new WaitForSeconds(1.5f);
            if (combatPowerPanel != null) combatPowerPanel.SetActive(false);
            onComplete?.Invoke();
        }

        private IEnumerator CountUpTo(TextMeshProUGUI textComponent, float targetValue)
        {
            if (textComponent == null) yield break;
            var duration = Mathf.Max(0.01f, countDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                textComponent.text = Mathf.RoundToInt(Mathf.Lerp(0f, targetValue, elapsed / duration)).ToString("N0");
                yield return null;
            }
            textComponent.text = Mathf.RoundToInt(targetValue).ToString("N0");
        }

        private IEnumerator AnimateUnitPortraits()
        {
            if (player1Icon != null && player1Icon.sprite != null) StartCoroutine(AnimateIconEntrance(player1Icon));
            if (enemy1Icon != null && enemy1Icon.sprite != null) StartCoroutine(AnimateIconEntrance(enemy1Icon));
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator AnimateIconEntrance(Image icon)
        {
            icon.gameObject.SetActive(true);
            const float duration = 0.5f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                var scale = Mathf.Sin(-13f * (t + 1f) * Mathf.PI * 0.5f) * Mathf.Pow(2f, -10f * t) + 1f;
                icon.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            icon.transform.localScale = Vector3.one;
        }
    }
}
