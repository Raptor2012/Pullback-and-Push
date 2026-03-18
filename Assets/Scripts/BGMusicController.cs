using UnityEngine;

public class BGMusicController : MonoBehaviour
{
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioClip[] bgMusicClips; // Array to hold the two background music clips

    void Awake()
    {
        //Randomize between the two audioclips and play BG
        int randomIndex = Random.Range(0, bgMusicClips.Length); // Generates a random index (0 or 1)
        bgMusicSource.clip = bgMusicClips[randomIndex];
        bgMusicSource.Play();
    }
}
