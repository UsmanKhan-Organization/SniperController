using System.Collections;
using Cinemachine;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SniperController : MonoBehaviour
{
    [Header("Controllers")]

    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private Animator animator;
    [SerializeField] private RayCastHanlder rayCastHandler;
    [Header("Camera"), Space(5)]
    [SerializeField] private GameObject idleCam;
    [SerializeField] private GameObject scopeCam;
    [SerializeField] private GameObject killCam;
    [Header("________________________GameObjects________________________"), Space(1)]
    [Header("OuterAnimationProperties")]
    [SerializeField] private RectTransform scopeRectPivot;
    [SerializeField] private RectTransform scopeRect;
    [SerializeField] private RectTransform shootCircle;
    [Header("InnerAnimationProperties"), Space(5)]
    [SerializeField] private GameObject shootPanel;
    [SerializeField] private RectTransform innerCircleRect;
    [SerializeField] private RectTransform outerCircleRect;
    [SerializeField] private Image dotRect;

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
    [SerializeField] private Color hitColor = new Color(1f, 0.2f, 0.2f, 1f);
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
            ScopeAnimation();
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

    void ScopeAnimation()
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
            rayCastHandler.CheckHit();
            if (rayCastHandler.HasHit)
            {
                Debug.Log($"Hit: {rayCastHandler.Hit.collider.name}");
            }
            ShootCompleted();
        });
    }
    
    void ShootAnimation()
    {
        if (innerCircleRect == null || outerCircleRect == null || dotRect == null)
            return;
        Color startColor = dotRect.color;
        float duration = 0.7f;
        float hitTime = duration * 0.5f;

        innerCircleRect.localScale = Vector3.one;
        outerCircleRect.localScale = Vector3.one;
        dotRect.color = startColor;

        Sequence shotSequence = DOTween.Sequence();

        shotSequence.Append(innerCircleRect.DOScale(Vector3.zero, duration).SetEase(Ease.InOutCubic));

        shotSequence.InsertCallback(hitTime, () =>
        {
            dotRect.DOColor(hitColor, 0.12f).SetEase(Ease.InOutSine);
        });

        shotSequence.OnComplete(() =>
        {
            dotRect.DOColor(startColor, 0.08f).SetEase(Ease.InOutSine);
            innerCircleRect.localScale = Vector3.one;
            outerCircleRect.localScale = Vector3.one;
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
        shootPanel.SetActive(true);
        ControlGameTimeScale(1f);
        SetCameraActive(killCam, 0f);
        ShootAnimation();

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
        shootPanel.SetActive(false);

        ReloadAnim(true);
        yield return new WaitForSeconds(1.5f);
        ReloadAnim(false);
        ScopeAnim(false);

    }

    #endregion
}
