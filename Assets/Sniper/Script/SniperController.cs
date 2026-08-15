using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using DG.Tweening;
using UnityEngine;

public class SniperController : MonoBehaviour
{
    [Header("Controllers")]

    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private Animator animator;
    [Header("Camera"), Space(5)]
    [SerializeField] private GameObject idleCam;
    [SerializeField] private GameObject scopeCam;
    [SerializeField] private GameObject killCam;
    [Header("GameObjects"), Space(5)]
    [SerializeField] private RectTransform scopeRectPivot;
    [SerializeField] private RectTransform scopeRect;
    [SerializeField] private RectTransform shootCircle;

    [Header("Scope Move Limits")]
    [SerializeField] private float topMargin = 40f;
    [SerializeField] private float bottomMargin = 40f;
    [SerializeField] private float leftMargin = 40f;
    [SerializeField] private float rightMargin = 40f;

    private bool isDragging;
    private Vector2 dragPointerOffset;

    [Header("Animations Properties")]
    [SerializeField] private float shootCircleDuration = 0.5f;
    [SerializeField] private float scopeRectDuration = 0.8f;
    [SerializeField] private float scopeRectDurationReturn = 0.4f;
    [SerializeField] private float timeScaleTransitionDuration = 0.25f;
    [Space(10)]
    [SerializeField] private Ease shootCircleEase = Ease.OutCubic;
    [SerializeField] private Ease scopeRectEase = Ease.OutCubic;
    [SerializeField] private Ease scopeRectReturnEase = Ease.OutCubic;

    private Tweener timeScaleTweener;

    #region Unity Cycle
    private void Start()
    {
        shootCircle.gameObject.SetActive(false);
    }

    #endregion


    #region Controller

    private void Update()
    {
        if (scopeRectPivot == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            scopeRectPivot.gameObject.SetActive(true);
            SetCameraActive(scopeCam, 0.5f);
            ScopeAnim(true);

            Vector2 clickLocalPos = GetLocalMousePositionInParent();
            scopeRectPivot.anchoredPosition = ClampScopePosition(clickLocalPos);

            isDragging = true;
            dragPointerOffset = clickLocalPos - scopeRectPivot.anchoredPosition;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2 currentMouseLocalPos = GetLocalMousePositionInParent();
            Vector2 targetPosition = currentMouseLocalPos - dragPointerOffset;
            scopeRectPivot.anchoredPosition = ClampScopePosition(targetPosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            AnimateScopeWhileShooting();
            //ScopeAnim(false);
        }
    }

    private Vector2 GetLocalMousePositionInParent()
    {
        RectTransform parentRect = scopeRectPivot.parent as RectTransform;
        if (parentRect == null)
            return Vector2.zero;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            Input.mousePosition,
            null,
            out Vector2 localMousePos
        );

        return localMousePos;
    }

    private Vector2 ClampScopePosition(Vector2 targetPosition)
    {
        RectTransform parentRect = scopeRectPivot.parent as RectTransform;
        if (parentRect == null)
            return targetPosition;

        Vector2 parentSize = parentRect.rect.size;
        Vector2 scopeHalfSize = scopeRectPivot.rect.size * 0.5f;

        float minX = -parentSize.x * 0.5f + scopeHalfSize.x + leftMargin;
        float maxX = parentSize.x * 0.5f - scopeHalfSize.x - rightMargin;
        float minY = -parentSize.y * 0.5f + scopeHalfSize.y + bottomMargin;
        float maxY = parentSize.y * 0.5f - scopeHalfSize.y - topMargin;

        return new Vector2(
            Mathf.Clamp(targetPosition.x, minX, maxX),
            Mathf.Clamp(targetPosition.y, minY, maxY)
        );
    }
    #endregion
    #region Animations
    void ScopeAnim(bool _state)
    {
        animator.SetBool("Scope", _state);
    }

    void ReloadAnim(bool _state)
    {
        animator.SetBool("Reload", _state);
    }

    void AnimateScopeWhileShooting()
    {
        if (scopeRect == null)
            return;

        scopeRect.DOKill();
        if (shootCircle != null)
            shootCircle.DOKill();

        scopeRect.localScale = Vector3.one;
        if (shootCircle != null)
        {
            shootCircle.gameObject.SetActive(true);
            shootCircle.localScale = Vector3.one;
        }

        Sequence shotSequence = DOTween.Sequence();

        // Scale up the scope rect
        shotSequence.Append(scopeRect.DOScale(Vector3.one * 1.5f, scopeRectDuration).SetEase(scopeRectEase));

        // Play the shoot circle animation independently so it doesn't block the sequence
        if (shootCircle != null)
        {
            ControlGameTimeScale(0.2f);
            shootCircle.DOScale(Vector3.zero, shootCircleDuration).SetEase(shootCircleEase);
        }

        // Return the scope rect to normal immediately after its own grow animation finishes
        shotSequence.Append(scopeRect.DOScale(Vector3.one, scopeRectDurationReturn).SetEase(scopeRectReturnEase));

        // Always run completion actions when the sequence finishes
        shotSequence.OnComplete(() =>
        {
            ShootCompleted();
        });
    }
    void ShootCompleted()
    {
        if (shootCircle != null)
        {
            shootCircle.gameObject.SetActive(false);
            shootCircle.localScale = Vector3.one;
        }
        scopeRectPivot.gameObject.SetActive(false);
        ControlGameTimeScale(1f);
        SetCameraActive(killCam, 0f);

        StartCoroutine(ResetCameraAfterDelay(1.5f));
    }
    #endregion
    #region TimeScale
    void ControlGameTimeScale(float _timeScale)
    {
        float targetTimeScale = Mathf.Clamp(_timeScale, 0.05f, 2f);

        if (timeScaleTweener != null && timeScaleTweener.IsActive())
            timeScaleTweener.Kill();

        timeScaleTweener = DOTween.To(
            () => Time.timeScale,
            value =>
            {
                Time.timeScale = value;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            },
            targetTimeScale,
            timeScaleTransitionDuration
        ).SetEase(Ease.InOutSine)
         .SetUpdate(UpdateType.Normal);
    }
    #endregion
    #region Camera
    void SetCameraActive(GameObject cam, float blend)
    {
        if (cam == null) return;

        if (cam.activeSelf)
            return;

        idleCam.SetActive(false);
        scopeCam.SetActive(false);
        killCam.SetActive(false);

        cam.SetActive(true);
        brain.m_DefaultBlend.m_Time = blend;
    }
    IEnumerator ResetCameraAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetCameraActive(idleCam, 0.5f);
        ReloadAnim(true);
        yield return new WaitForSeconds(1.5f);
        ReloadAnim(false);
        ScopeAnim(false);

    }

    #endregion
}
