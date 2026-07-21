using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class MusicManager : MonoBehaviour
{
    public AudioClip[] MusicTracks;
    public AudioSource MusicAudioSource;
    public AudioMixerGroup MasterMixerGroup;
    public TextMeshProUGUI MasterVolumeCounterText;
    public TextMeshProUGUI MusicVolumeCounterText;
    public Slider MasterVolumeSlider;
    public Slider MusicVolumeSlider;
    public static AudioMixerGroup GlobalMixerGroup;

    private int _currMusicTrack = 0;
    private float _baseMasterMixerValue;
    private float _baseMusicMixerValue;

    void Awake()
    {
        GlobalMixerGroup = MasterMixerGroup;
        MasterMixerGroup.audioMixer.GetFloat("MasterVolume", out _baseMasterMixerValue);
        MusicAudioSource.outputAudioMixerGroup.audioMixer.GetFloat("MusicVolume", out _baseMusicMixerValue);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _currMusicTrack = UnityEngine.Random.Range(0, MusicTracks.Length - 1);
        MusicAudioSource.PlayOneShot(MusicTracks[_currMusicTrack]);

        float masterVolume = PlayerPrefs.GetFloat("MasterVolume");
        masterVolume = masterVolume < 0.0f ? (-masterVolume) * 0.8f : masterVolume * 0.2f;
        masterVolume += _baseMasterMixerValue;

        float musicVolume = PlayerPrefs.GetFloat("MusicVolume");
        musicVolume = musicVolume < 0.0f ? (-musicVolume) * 0.55f : musicVolume * 0.25f;
        musicVolume += _baseMusicMixerValue;

        MasterMixerGroup.audioMixer.SetFloat("MasterVolume", masterVolume);
        MusicAudioSource.outputAudioMixerGroup.audioMixer.SetFloat("MusicVolume", musicVolume);

        MasterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume");
        MusicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume");

        MasterVolumeCounterText.text = MasterVolumeSlider.value.ToString();
        MusicVolumeCounterText.text = MusicVolumeSlider.value.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (!MusicAudioSource.isPlaying)
        {
            _currMusicTrack++;
            if (_currMusicTrack >= MusicTracks.Length)
                _currMusicTrack = 0;
            
            MusicAudioSource.PlayOneShot(MusicTracks[_currMusicTrack]);
        }
    }

    public void OnMasterVolumeSliderChange()
    {
        float newVolume = _baseMasterMixerValue;
        if (MasterVolumeSlider.value < 0.0f)
            newVolume -= (-MasterVolumeSlider.value) * 0.8f;
        else
            newVolume += MasterVolumeSlider.value * 0.2f;
        
        MasterMixerGroup.audioMixer.SetFloat("MasterVolume", newVolume);
        MasterVolumeCounterText.text = MasterVolumeSlider.value.ToString();
        PlayerPrefs.SetFloat("MasterVolume", MasterVolumeSlider.value);
    }

    public void OnMusicVolumeSliderChange()
    {
        float newVolume = _baseMusicMixerValue;
        if (MusicVolumeSlider.value < 0.0f)
            newVolume -= (-MusicVolumeSlider.value) * 0.55f;
        else
            newVolume += MusicVolumeSlider.value * 0.25f;

        MusicAudioSource.outputAudioMixerGroup.audioMixer.SetFloat("MusicVolume", newVolume);
        MusicVolumeCounterText.text = MusicVolumeSlider.value.ToString();
        PlayerPrefs.SetFloat("MusicVolume", MusicVolumeSlider.value);
    }

    void OnDestroy()
    {
        MasterMixerGroup.audioMixer.SetFloat("MasterVolume", _baseMasterMixerValue);
        MusicAudioSource.outputAudioMixerGroup.audioMixer.SetFloat("MusicVolume", _baseMusicMixerValue);         
    }
}
