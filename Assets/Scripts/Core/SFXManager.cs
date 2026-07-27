using System;
using DG.Tweening;
using UnityEngine;

public class SFXManager : MonoBehaviour
{
   public AudioSource gameBGSource;
   public AudioSource eatSource;
   public AudioSource segmentRemovedSource;
   public AudioSource LevelUpSource;
   public AudioSource chaosSelectedSource;
   public AudioSource gameOverSource;
   public AudioSource ftueStart;
   public AudioSource ftueAte;
   public AudioSource ftueSegmentDissappear;
   
   [Header("bg")]
   [SerializeField] private float minPitch = 0.6f;
   [SerializeField] private float maxPitch = 1.2f;
   [SerializeField] private float pitchTweenDuration = 0.3f;
   
   private Tween pitchTween;

   #region Initialize

   private void OnEnable()
   {
      GameManager.events.AddEvent(GameEvents.EventType.OnGameStart, PlayFTUE);
      GameManager.events.AddEvent(GameEvents.EventType.OnAteFood, PlayEat);
      GameManager.events.AddEvent(GameEvents.EventType.OnSegmentRemoved, PlaySegmentRemoved);
      GameManager.events.AddEvent<SegmentAddedData>(GameEvents.EventType.OnNewSegmentAdded, UpdateMusicPitch);
      GameManager.events.AddEvent(GameEvents.EventType.OnLevelUp, PlayLevelUp);
      GameManager.events.AddEvent(GameEvents.EventType.OnPowerUpSelected, PlayChaosSelected);
      GameManager.events.AddEvent(GameEvents.EventType.OnGameOver, PlayGameOver);
   }

   #endregion

   #region Audio

   public void PlayEat()
   {
      eatSource.Play();
   }

   public void PlaySegmentRemoved()
   {
      segmentRemovedSource.Play();
   }

   public void PlayLevelUp()
   {
      LevelUpSource.Play();
   }

   public void PlayChaosSelected()
   {
      chaosSelectedSource.Play();
   }

   public void PlayGameOver()
   {
      gameBGSource.volume = 0.02f;
      gameOverSource.Play();
   }

   public void PlayFTUE()
   {
      ftueStart.Play();
   }

   public void PlayFTUEAte()
   {
      ftueAte.Play();
   }

   public void PlayFTUESegmentDissappear()
   {
      ftueSegmentDissappear.Play();
   }

   #endregion

   #region Pitch

   public void UpdateMusicPitch(SegmentAddedData data)
   {
      float t = Mathf.Clamp01((float)data.SegmentCount / data.MaxBodyLength);
      float targetPitch = Mathf.Lerp(minPitch, maxPitch, t);

      pitchTween?.Kill();
      pitchTween = gameBGSource
         .DOPitch(targetPitch, pitchTweenDuration)
         .SetEase(Ease.OutSine);
   }

   #endregion

   #region Terminate

   private void OnDisable()
   {
      GameManager.events.RemoveEvent(GameEvents.EventType.OnGameStart, PlayFTUE);
      GameManager.events.RemoveEvent(GameEvents.EventType.OnAteFood, PlayEat);
      GameManager.events.RemoveEvent(GameEvents.EventType.OnSegmentRemoved, PlaySegmentRemoved);
      GameManager.events.RemoveEvent<SegmentAddedData>(GameEvents.EventType.OnNewSegmentAdded, UpdateMusicPitch);
      GameManager.events.RemoveEvent(GameEvents.EventType.OnLevelUp, PlayLevelUp);
      GameManager.events.RemoveEvent(GameEvents.EventType.OnPowerUpSelected, PlayChaosSelected);
      GameManager.events.RemoveEvent(GameEvents.EventType.OnGameOver, PlayGameOver);
   }

   #endregion
   
}
