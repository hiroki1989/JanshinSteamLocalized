using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CutinSpriteAnimator : MonoBehaviour
{
[System.Serializable]
public class CutinClip
{
    public string clipId;
    public Sprite[] frames;

    [Header("Playback")]
    public float fps = 10f;
    public float holdSeconds = 0.2f;
    public float fadeOutSeconds = 0.15f;

    [Header("Transform Motion")]
    public bool useCurrentAnchoredPositionAsSettle = true;
    public Vector2 startAnchoredOffsetFromSettle = new Vector2(-260f, 0f);
    public Vector2 startAnchoredPos = new Vector2(-260f, 0f);
    public Vector2 settleAnchoredPos = Vector2.zero;
    public Vector3 startScale = new Vector3(0.94f, 0.94f, 1f);
    public Vector3 settleScale = Vector3.one;
    public float startRotationZ = -3f;
    public float settleRotationZ = 0f;
}

    [Header("UI References")]
    [SerializeField] private RectTransform cutinRoot;
    [SerializeField] private Image cutinImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Default / Legacy Clip")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 10f;
    [SerializeField] private float holdSeconds = 0.2f;
    [SerializeField] private float fadeOutSeconds = 0.15f;
    [SerializeField] private Vector2 startAnchoredPos = new Vector2(-260f, 0f);
    [SerializeField] private Vector2 settleAnchoredPos = Vector2.zero;
    [SerializeField] private Vector3 startScale = new Vector3(0.94f, 0.94f, 1f);
    [SerializeField] private Vector3 settleScale = Vector3.one;
    [SerializeField] private float startRotationZ = -3f;
    [SerializeField] private float settleRotationZ = 0f;

    [Header("Named Clips")]
    [SerializeField] private List<CutinClip> clips = new List<CutinClip>();

[Header("General")]
[SerializeField] private bool unscaledTime = true;
[SerializeField] private bool playOnEnable = false;
[SerializeField] private bool setNativeSizeEachFrame = true;

[Header("Debug")]
[SerializeField] private bool debugHoldLastFrame = false;
[SerializeField] private string debugPreviewClipId = "";

[Header("Fallback")]
[SerializeField] private float missingClipStaticHoldSeconds = 0.25f;
[SerializeField] private float missingClipFadeOutSeconds = 0.15f;
private Coroutine playRoutine;
private bool isPlaying = false;
private Vector2 _capturedSettleAnchoredPos;
private Vector2 _initialAnchoredPos;
private Vector3 _initialScale;
private Quaternion _initialRotation;
private bool _initialTransformCaptured = false;

private void CaptureInitialTransformIfNeeded()
{
    if (_initialTransformCaptured)
        return;

    if (cutinRoot == null)
        return;

    _initialAnchoredPos = cutinRoot.anchoredPosition;
    _initialScale = cutinRoot.localScale;
    _initialRotation = cutinRoot.localRotation;
    _initialTransformCaptured = true;
}

private void RestoreInitialTransform()
{
    if (cutinRoot == null)
        return;

    CaptureInitialTransformIfNeeded();

    cutinRoot.anchoredPosition = _initialAnchoredPos;
    cutinRoot.localScale = _initialScale;
    cutinRoot.localRotation = _initialRotation;
}
    public bool IsPlaying
    {
        get { return isPlaying; }
    }
private void OnEnable()
{
    CaptureInitialTransformIfNeeded();
    RestoreInitialTransform();

    if (playOnEnable)
    {
        Play();
    }
}
    public void Play()
    {
        Play(null, null);
    }

    public void Play(string clipId)
    {
        Play(clipId, null);
    }
public void Play(string clipId, Sprite fallbackStaticSprite)
{
    CaptureInitialTransformIfNeeded();

    if (playRoutine != null)
    {
        StopCoroutine(playRoutine);
        playRoutine = null;
    }

    RestoreInitialTransform();

    playRoutine = StartCoroutine(PlayRoutine(clipId, fallbackStaticSprite));
}
public void StopImmediately()
{
    if (playRoutine != null)
    {
        StopCoroutine(playRoutine);
        playRoutine = null;
    }

    isPlaying = false;

    RestoreInitialTransform();

    if (canvasGroup != null)
    {
        canvasGroup.alpha = 0f;
    }

    if (gameObject.activeSelf)
    {
        gameObject.SetActive(false);
    }
}
private IEnumerator PlayRoutine(string clipId, Sprite fallbackStaticSprite)
{
    isPlaying = true;

    if (cutinRoot == null || cutinImage == null || canvasGroup == null)
    {
        isPlaying = false;
        playRoutine = null;
        yield break;
    }

    CaptureInitialTransformIfNeeded();
    RestoreInitialTransform();

    CutinClip clip = FindClip(clipId);
if (clip != null && clip.frames != null && clip.frames.Length > 0)
{
    gameObject.SetActive(true);
    canvasGroup.alpha = 1f;

Vector2 settlePos = clip.useCurrentAnchoredPositionAsSettle
    ? _initialAnchoredPos
    : clip.settleAnchoredPos;

Vector2 startPos = clip.useCurrentAnchoredPositionAsSettle
    ? settlePos + clip.startAnchoredOffsetFromSettle
    : clip.startAnchoredPos;

    _capturedSettleAnchoredPos = settlePos;

    cutinRoot.anchoredPosition = startPos;
    cutinRoot.localScale = clip.startScale;
    cutinRoot.localRotation = Quaternion.Euler(0f, 0f, clip.startRotationZ);

    float frameInterval = 1f / Mathf.Max(1f, clip.fps);

    for (int i = 0; i < clip.frames.Length; i++)
    {
        Sprite frame = clip.frames[i];
        if (frame != null)
        {
            cutinImage.enabled = true;
            cutinImage.sprite = frame;
            if (setNativeSizeEachFrame)
                cutinImage.SetNativeSize();
            cutinImage.preserveAspect = true;
        }

        float t = clip.frames.Length <= 1 ? 1f : (float)i / (clip.frames.Length - 1);
        float eased = EaseOutBack(t);

        cutinRoot.anchoredPosition = Vector2.Lerp(startPos, settlePos, eased);
        cutinRoot.localScale = Vector3.Lerp(clip.startScale, clip.settleScale, eased);

        float rot = Mathf.Lerp(clip.startRotationZ, clip.settleRotationZ, t);
        cutinRoot.localRotation = Quaternion.Euler(0f, 0f, rot);

        yield return Wait(frameInterval);
    }

    if (debugHoldLastFrame)
    {
        playRoutine = null;
        yield break;
    }

    if (clip.holdSeconds > 0f)
    {
        yield return Wait(clip.holdSeconds);
    }

    yield return FadeOut(clip.fadeOutSeconds);
}
else if (HasLegacyFrames())
{
    gameObject.SetActive(true);
    canvasGroup.alpha = 1f;

Vector2 settlePos = _initialAnchoredPos;
Vector2 startPos = settlePos + startAnchoredPos;

    _capturedSettleAnchoredPos = settlePos;

    cutinRoot.anchoredPosition = startPos;
    cutinRoot.localScale = startScale;
    cutinRoot.localRotation = Quaternion.Euler(0f, 0f, startRotationZ);

    float frameInterval = 1f / Mathf.Max(1f, fps);

    for (int i = 0; i < frames.Length; i++)
    {
        Sprite frame = frames[i];
        if (frame != null)
        {
            cutinImage.enabled = true;
            cutinImage.sprite = frame;
            if (setNativeSizeEachFrame)
                cutinImage.SetNativeSize();
            cutinImage.preserveAspect = true;
        }

        float t = frames.Length <= 1 ? 1f : (float)i / (frames.Length - 1);
        float eased = EaseOutBack(t);

        cutinRoot.anchoredPosition = Vector2.Lerp(startPos, settlePos, eased);
        cutinRoot.localScale = Vector3.Lerp(startScale, settleScale, eased);

        float rot = Mathf.Lerp(startRotationZ, settleRotationZ, t);
        cutinRoot.localRotation = Quaternion.Euler(0f, 0f, rot);

        yield return Wait(frameInterval);
    }

    if (debugHoldLastFrame)
    {
        playRoutine = null;
        yield break;
    }

    if (holdSeconds > 0f)
    {
        yield return Wait(holdSeconds);
    }

    yield return FadeOut(fadeOutSeconds);
}
else if (fallbackStaticSprite != null)
{
    gameObject.SetActive(true);
    canvasGroup.alpha = 1f;

    cutinImage.enabled = true;
    cutinImage.sprite = fallbackStaticSprite;
    if (setNativeSizeEachFrame)
        cutinImage.SetNativeSize();
    cutinImage.preserveAspect = true;
Vector2 settlePos = _initialAnchoredPos;
Vector2 startPos = settlePos + startAnchoredPos;

    _capturedSettleAnchoredPos = settlePos;

    cutinRoot.anchoredPosition = startPos;
    cutinRoot.localScale = startScale;
    cutinRoot.localRotation = Quaternion.Euler(0f, 0f, startRotationZ);

    yield return Wait(0.1f);
    cutinRoot.anchoredPosition = Vector2.Lerp(startPos, settlePos, 0.75f);
    cutinRoot.localScale = Vector3.Lerp(startScale, settleScale, 0.9f);
    cutinRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRotationZ, settleRotationZ, 0.75f));

    if (debugHoldLastFrame)
    {
        playRoutine = null;
        yield break;
    }

    yield return Wait(missingClipStaticHoldSeconds);
    yield return FadeOut(missingClipFadeOutSeconds);
}
RestoreInitialTransform();

canvasGroup.alpha = 0f;
gameObject.SetActive(false);

isPlaying = false;
playRoutine = null;
    }
[ContextMenu("Preview Debug Clip")]
private void PreviewDebugClip()
{
    if (!string.IsNullOrEmpty(debugPreviewClipId))
    {
        Play(debugPreviewClipId, null);
    }
}
[ContextMenu("Restore Settled Position")]
private void RestoreSettledPosition()
{
    RestoreInitialTransform();

    if (canvasGroup != null)
        canvasGroup.alpha = 1f;

    if (gameObject != null)
        gameObject.SetActive(true);
}
    private CutinClip FindClip(string clipId)
    {
        if (string.IsNullOrEmpty(clipId) || clips == null)
            return null;

        for (int i = 0; i < clips.Count; i++)
        {
            CutinClip c = clips[i];
            if (c == null) continue;
            if (string.IsNullOrEmpty(c.clipId)) continue;

            if (string.Equals(c.clipId.Trim(), clipId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private bool HasLegacyFrames()
    {
        return frames != null && frames.Length > 0;
    }

    private IEnumerator FadeOut(float seconds)
    {
        float dur = Mathf.Max(0f, seconds);
        if (dur <= 0f)
        {
            canvasGroup.alpha = 0f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / dur);
            canvasGroup.alpha = 1f - t;
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
private object Wait(float seconds)
{
    if (unscaledTime)
        return new WaitForSecondsRealtime(seconds);

    return new WaitForSeconds(seconds);
}

    private float DeltaTime()
    {
        return unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}