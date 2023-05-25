using FMODUnity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestFmod : MonoBehaviour
{

    public Slider slider;
    public FMODUnity.StudioEventEmitter fmodEvent;
    public FMODUnity.StudioEventEmitter musicFmod;
    public FMODUnity.StudioEventEmitter crashFmod;
    FMOD.Studio.Bus MasterBus;
    FMOD.Studio.Bus MusicBus;
    FMOD.Studio.Bus SFXBus;
    float MusicVolume = 0.5f;

    float lastRPM;


    void Start()
    {
        slider.onValueChanged.AddListener(delegate { ValueChangeCheck(); });
        MasterBus  = RuntimeManager.GetBus("bus:/Master");
        MusicBus = RuntimeManager.GetBus("bus:/Master/Music"); 
        SFXBus = RuntimeManager.GetBus("bus:/Master/SFX");
        if (MusicBus.isValid())
        {
            Debug.Log("Music Valid");
        }

    }

    //Change RPM with slider
    public void ValueChangeCheck()
    {
        fmodEvent.SetParameter("RPM", slider.value);
    }


    private void Update()
    {
        //MusicBus.setVolume(MusicVolume);
    }


    // Logic of parameter "Load" if needed
    /*    void FixedUpdate()
        {
           float rpmChange = (slider.value - lastRPM) / Time.fixedDeltaTime;
            rpmChange = Mathf.Clamp(rpmChange, -2000, 2000);
            fmodEvent.SetParameter("Load", rpmChange);

            lastRPM = slider.value;

        }
    */

    public void SetMusicVolume(float vol)
    {
        MusicVolume = vol;
        MusicBus.setVolume(MusicVolume);
    }
    public void chooseMaterials(int material)
    {
        crashFmod.Play();
        crashFmod.SetParameter("Materials", material);
        if (fmodEvent.IsPlaying())
        {
            ChangeRPM(0);
        }
    }


// Start and End sounds logic 
    public void ChangeRPM(float rpm)
    {
        if (!fmodEvent.IsPlaying())
        {
       fmodEvent.Play();
        }
 
        if (lastRPM > 0 && rpm==0) {
            fmodEvent.SetParameter("DriveBool", 1);
        }
        else
        {
            fmodEvent.SetParameter("DriveBool", 0);
        }

        fmodEvent.SetParameter("RPM", rpm);
        lastRPM = rpm;
    }


    //  Start and End sounds logic  bool
    public void changeDriveBool(int drive)
    {
            fmodEvent.SetParameter("DriveBool", drive);
    }

    // Stop engine without fade
    public void stopDriving()
    {
        fmodEvent.Stop();
    }

    public void PlayMusic()
    {
        musicFmod.SetParameter("WinLose", 2);
        if (!musicFmod.IsPlaying()) musicFmod.Play();
    }

    public void MusicWinLose(int winLose)
    {
        musicFmod.SetParameter("WinLose", winLose);
    }



}
