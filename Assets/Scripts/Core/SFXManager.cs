using System;
using DG.Tweening;
using UnityEngine;

public class SFXManager : MonoBehaviour
{
   public static SFXManager instance;
   
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

   private void Awake()
   {
      if (instance == null)
      {
         instance = this;
      }
   }

   private void OnEnable()
   {
      GameManager.events.AddEvent(GameEvents.EventType.OnAteFood, PlayEat);
   }

   public void UpdateMusicPitch(int segmentCount, int maxBodyLength)
   {
      float t = Mathf.Clamp01((float)segmentCount / maxBodyLength);
      float targetPitch = Mathf.Lerp(minPitch, maxPitch, t);

      pitchTween?.Kill();
      pitchTween = gameBGSource
         .DOPitch(targetPitch, pitchTweenDuration)
         .SetEase(Ease.OutSine);
   }
   
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

}
