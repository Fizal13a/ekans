using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Top-down camera controller with follow, shake, and scale-based zoom.
/// Attach to the Main Camera. Works with an Orthographic camera (typical
/// for top-down games) but also supports Perspective (adjusts FOV instead).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("The player transform the camera follows.")]
    public Transform target;

    [Tooltip("Offset from the target (useful for angled top-down views).")]
    public Vector3 offset = new Vector3(0f, 10f, 0f);

    [Tooltip("How quickly the camera catches up to the target (higher = snappier).")]
    public float followSmoothTime = 0.2f;

    [Header("Zoom Settings (Scale-Based)")]
    [Tooltip("Camera zoom (orthographic size or FOV) when player is at baseScale.")]
    public float baseZoom = 5f;

    [Tooltip("The player scale that corresponds to baseZoom.")]
    public float baseScale = 1f;

    [Tooltip("How much zoom changes per unit of player scale growth.")]
    public float zoomPerScaleUnit = 2f;

    [Tooltip("Clamp so the camera doesn't zoom in/out too far.")]
    public float minZoom = 3f;
    public float maxZoom = 20f;

    [Tooltip("How quickly the camera zoom transitions to the target zoom.")]
    public float zoomSmoothTime = 0.3f;

    [Header("Shake Settings")]
    [Tooltip("Curve controlling shake falloff over the duration (1 = full strength, 0 = none).")]
    public AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Camera _camera;
    private Vector3 _followVelocity = Vector3.zero;
    private float _zoomVelocity = 0f;
    private float _targetZoom;

    // Shake state
    private float _shakeTimeRemaining = 0f;
    private float _shakeDuration = 0f;
    private float _shakeMagnitude = 0f;
    private Vector3 _shakeOffset = Vector3.zero;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _targetZoom = baseZoom;

        if (_camera.orthographic)
            _camera.orthographicSize = baseZoom;
        else
            _camera.fieldOfView = baseZoom;
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent<SegmentAddedData>(GameEvents.EventType.OnNewSegmentAdded, UpdateZoomForScale);
    }

    private void LateUpdate()
    {
        HandleFollow();
        HandleZoom();
        HandleShake();
    }

    // ---------------- FOLLOW ----------------

    private void HandleFollow()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset + _shakeOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _followVelocity,
            followSmoothTime
        );
    }

    // ---------------- ZOOM (SCALE-BASED) ----------------

    /// <summary>
    /// Call this whenever the player's scale changes (e.g. from a growth power-up).
    /// Pass the player's current uniform scale (or magnitude if non-uniform).
    /// </summary>
    /// <param name="currentPlayerScale">Player's current scale value.</param>
    public void UpdateZoomForScale(SegmentAddedData segmentAddedData)
    {
        int size = segmentAddedData.SegmentCount;
        float scaleDelta = size - baseScale;
        float desiredZoom = baseZoom + scaleDelta * zoomPerScaleUnit;
        _targetZoom = Mathf.Clamp(desiredZoom, minZoom, maxZoom);
        
        ShakeCamera();
    }

    /// <summary>
    /// Overload: directly set a target zoom value, bypassing the scale formula.
    /// Useful for cutscenes or manual zoom control.
    /// </summary>
    public void SetTargetZoom(float zoom)
    {
        _targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
    }

    private void HandleZoom()
    {
        float current = _camera.orthographic ? _camera.orthographicSize : _camera.fieldOfView;
        float smoothedZoom = Mathf.SmoothDamp(current, _targetZoom, ref _zoomVelocity, zoomSmoothTime);

        if (_camera.orthographic)
            _camera.orthographicSize = smoothedZoom;
        else
            _camera.fieldOfView = smoothedZoom;
    }

    // ---------------- SHAKE ----------------

    /// <summary>
    /// Triggers a camera shake effect.
    /// </summary>
    /// <param name="duration">How long the shake lasts, in seconds.</param>
    /// <param name="magnitude">Max positional offset of the shake.</param>
    public void ShakeCamera(float duration = 0.3f, float magnitude = 0.4f)
    {
        _shakeDuration = duration;
        _shakeTimeRemaining = duration;
        _shakeMagnitude = magnitude;
    }

    private void HandleShake()
    {
        if (_shakeTimeRemaining > 0f)
        {
            float normalizedTime = 1f - (_shakeTimeRemaining / _shakeDuration);
            float falloff = shakeFalloff.Evaluate(normalizedTime);

            _shakeOffset = new Vector3(
                (Random.value * 2f - 1f) * _shakeMagnitude * falloff,
                0f,
                (Random.value * 2f - 1f) * _shakeMagnitude * falloff
            );

            _shakeTimeRemaining -= Time.deltaTime;
        }
        else
        {
            _shakeOffset = Vector3.zero;
        }
    }
}