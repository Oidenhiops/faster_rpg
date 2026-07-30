using System;
using System.Collections;
using System.Threading.Tasks;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public AudioMixer audioMixer;
    public AudioSource bgmSource;
    public SoundsDBSO soundsDB;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void PlayASound(AudioClip audioClip)
    {
        AudioSource audioBox = Instantiate(GameData.Instance.utils.prefabs["AudioBox"]).GetComponent<AudioSource>();
        audioBox.clip = audioClip;
        audioBox.Play();
        StartCoroutine(DestroyAudioBox(audioBox.gameObject, audioClip.length));
    }
    public void PlayASound(AudioClip audioClip, float initialPitch, bool randomPitch)
    {
        AudioSource audioBox = Instantiate(GameData.Instance.utils.prefabs["AudioBox"]).GetComponent<AudioSource>();
        audioBox.clip = audioClip;
        audioBox.pitch = randomPitch ? UnityEngine.Random.Range(0.5f, 1.5f) : UnityEngine.Random.Range(initialPitch - 0.1f, initialPitch + 0.1f);
        audioBox.Play();
        StartCoroutine(DestroyAudioBox(audioBox.gameObject, audioClip.length));
    }
    public void PlayASound(AudioClip audioClip, float initialPitch, bool randomPitch, out GameObject audioBoxInstance)
    {
        AudioSource audioBox = Instantiate(GameData.Instance.utils.prefabs["AudioBox"]).GetComponent<AudioSource>();
        audioBox.clip = audioClip;
        audioBox.pitch = randomPitch ? UnityEngine.Random.Range(0.5f, 1.5f) : UnityEngine.Random.Range(initialPitch - 0.1f, initialPitch + 0.1f);
        audioBox.Play();
        audioBoxInstance = audioBox.gameObject;
        StartCoroutine(DestroyAudioBox(audioBox.gameObject, audioClip.length));
    }
    public async Awaitable FadeIn()
    {
        try
        {
            if (!GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.isMute)
            {
                float targetDecibels = 20 * Mathf.Log10(GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.MASTERValue / 100);
                float currentVolume;

                if (!audioMixer.GetFloat(TypeSound.Master.ToString(), out currentVolume))
                {
                    currentVolume = -80f;
                }
                float duration = 1f;
                float elapsed = 0f;
                float updateRate = 1f / 60f;
                while (elapsed < duration)
                {
                    elapsed += updateRate;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float newVolume = Mathf.Lerp(currentVolume, targetDecibels, t);
                    audioMixer.SetFloat(TypeSound.Master.ToString(), newVolume);
                    await Task.Delay(TimeSpan.FromSeconds(updateRate));
                }
                audioMixer.SetFloat(TypeSound.Master.ToString(), targetDecibels);
                await Awaitable.NextFrameAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public async Awaitable FadeOut()
    {
        try
        {
            if (!GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.isMute)
            {
                float currentVolume;
                if (!audioMixer.GetFloat(TypeSound.Master.ToString(), out currentVolume))
                {
                    currentVolume = 20 * Mathf.Log10(GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.MASTERValue / 100);
                }
                float targetVolume = -80f;
                float duration = 1f;
                float elapsed = 0f;
                float updateRate = 1f / 60f;
                while (elapsed < duration)
                {
                    elapsed += updateRate;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float newVolume = Mathf.Lerp(currentVolume, targetVolume, t);
                    audioMixer.SetFloat(TypeSound.Master.ToString(), newVolume);
                    await Task.Delay(TimeSpan.FromSeconds(updateRate));
                }
                audioMixer.SetFloat(TypeSound.Master.ToString(), targetVolume);
                await Awaitable.NextFrameAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public AudioClip GetAudioClip(SoundsDBSO.TypeSound typeSound, string soundId)
    {
        if (soundsDB.sounds.TryGetValue(typeSound, out SerializedDictionary<string, AudioClip[]> sounds))
        {
            if (sounds.TryGetValue(soundId, out AudioClip[] clips))
            {
                return clips[UnityEngine.Random.Range(0, clips.Length - 1)];
            }
            return null;
        }
        return null;
    }
    public void SetAudioMixerData()
    {
        float decibelsMaster = 20 * Mathf.Log10(GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.MASTERValue / 100);
        float decibelsBGM = 20 * Mathf.Log10(GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.BGMalue / 100);
        float decibelsSFX = 20 * Mathf.Log10(GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.SFXalue / 100);
        if (GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.MASTERValue == 0) decibelsMaster = -80;
        if (GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.BGMalue == 0) decibelsBGM = -80;
        if (GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.SFXalue == 0) decibelsSFX = -80;
        audioMixer.SetFloat(TypeSound.BGM.ToString(), decibelsBGM);
        audioMixer.SetFloat(TypeSound.SFX.ToString(), decibelsSFX);
        audioMixer.SetFloat(TypeSound.Master.ToString(), GameData.Instance.systemDataInfo.configurationsInfo.soundConfiguration.isMute ? -80 : decibelsMaster);
        GameData.Instance.SaveSystemData();
    }
    IEnumerator DestroyAudioBox(GameObject audioBox, float timeToDestroy)
    {
        yield return new WaitForSecondsRealtime(timeToDestroy);
        Destroy(audioBox);
    }
    public enum TypeSound
    {
        Master = 0,
        BGM = 1,
        SFX = 2
    }    
}
