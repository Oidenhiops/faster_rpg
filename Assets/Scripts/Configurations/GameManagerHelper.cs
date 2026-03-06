using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManagerHelper : MonoBehaviour
{
    public IScene sceneData;
    GameObject audioBoxInstance;
    [NonSerialized] public GameObject lastButtonSelected;
    public void ChangeScene(int typeScene)
    {
        GameManager.TypeScene scene = (GameManager.TypeScene)typeScene;
        _ = GameManager.Instance.LoadScene(scene);
    }
    public void ChangeScene(GameManager.TypeScene typeScene)
    {
        _ = GameManager.Instance.LoadScene(typeScene);
    }
    public void SaveGameData()
    {
        GameData.Instance.SaveGameData();
    }
    public void SaveSystemData()
    {
        GameData.Instance.SaveSystemData();
    }
    public void PlayASound(SoundsDBSO.TypeSound typeSound, string soundId)
    {
        AudioManager.Instance.PlayASound(AudioManager.Instance.GetAudioClip(typeSound, soundId));
    }
    public void PlayASound(SoundsDBSO.TypeSound typeSound, string soundId, float initialRandomPitch)
    {
        AudioManager.Instance.PlayASound(AudioManager.Instance.GetAudioClip(typeSound, soundId), initialRandomPitch, false);
    }
    public void PlayASoundButton(string soundId)
    {
        AudioManager.Instance.PlayASound(AudioManager.Instance.GetAudioClip(SoundsDBSO.TypeSound.SFX, soundId), 1, false);
    }
    public void PlayASoundButtonUniqueInstance(string soundId)
    {
        if (audioBoxInstance == null)
        {
            AudioManager.Instance.PlayASound(AudioManager.Instance.GetAudioClip(SoundsDBSO.TypeSound.SFX, soundId), 1, false, out GameObject audioBox);
            audioBoxInstance = audioBox;
        }
    }
    public void VibrateGamePad()
    {
        if (GameManager.Instance.currentDevice == GameManager.TypeDevice.GAMEPAD)
        {
            var gamepad = Gamepad.current;
            Gamepad.current.SetMotorSpeeds(0.5f, 0.5f);
            StartCoroutine(StopVibration(gamepad));
        }
    }
    IEnumerator StopVibration(Gamepad gamepad)
    {
        if (gamepad != null)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }
    public void SetAudioMixerData()
    {
        AudioManager.Instance.SetAudioMixerData();
    }
    public interface IScene
    {
        public void PlayEndAnimation();
        public bool AnimationEnded();
    }
}
