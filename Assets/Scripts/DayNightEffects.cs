using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Post-processing by phase of the day (Resources/Data/daynight.json; GDD, "Jour et nuit"), added by
/// PlayerController: a global volume of its own (bloom, colour adjustments, vignette) whose values follow the clock
/// (DayNight.Mix), the camera's post-processing switched on. postProcessing in meta.json turns it all off.
public class DayNightEffects : MonoBehaviour
{
    VolumeProfile profile;
    Volume volume;
    Bloom bloom;
    ColorAdjustments colours;
    Vignette vignette;

    void Start()
    {
        bool on = SaveData.Settings.postProcessing;
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<UniversalAdditionalCameraData>() is { } data) data.renderPostProcessing = on;
        if (!on) return;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        bloom = profile.Add<Bloom>(true);
        colours = profile.Add<ColorAdjustments>(true);
        vignette = profile.Add<Vignette>(true);
        var go = new GameObject("Post-traitement");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        Update();
    }

    void Update()
    {
        if (profile == null) return;
        bloom.threshold.value = DayNight.BloomThreshold;
        bloom.intensity.value = DayNight.Mix(p => p.bloom);
        bloom.scatter.value = 0.6f;
        colours.saturation.value = DayNight.Mix(p => p.saturation);
        colours.contrast.value = DayNight.Mix(p => p.contrast);
        colours.postExposure.value = DayNight.Mix(p => p.exposure);
        colours.colorFilter.value = DayNight.Mix(p => p.filter);
        vignette.intensity.value = DayNight.Mix(p => p.vignette);
        vignette.smoothness.value = 0.4f;
    }

    void OnDestroy()
    {
        if (profile != null) Destroy(profile);
    }
}
