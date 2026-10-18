using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class SoundSystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameFlow gameFlow;
    [SerializeField] private PlayerMove player;
    [SerializeField] private BossDoorChase boss;
    [SerializeField] private HiddenEnemy hiddenEnemy;

    [Header("Volumes gerais")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;

    [Header("Musicas")]
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField] private AudioClip creditsMusic;
    [SerializeField, Min(0f)] private float musicFadeDuration = 0.6f;

    [Header("Movimento do jogador")]
    [SerializeField] private AudioClip caneSound;
    [SerializeField, Range(0f, 1f)] private float caneVolume = 0.6f;
    [SerializeField, Min(0.1f)] private float caneStepInterval = 0.65f;
    [Tooltip("Pula o silencio no inicio do arquivo da bengala.")]
    [SerializeField, Min(0f)] private float caneStartTime = 0.42f;

    [Header("Boss - velhice")]
    [SerializeField] private AudioClip bossStepSound;
    [SerializeField] private AudioClip bossGruntSound;
    [SerializeField, Range(0f, 1f)] private float bossStepVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float bossGruntVolume = 0.7f;
    [SerializeField, Min(0.1f)] private float bossStepInterval = 0.38f;
    [SerializeField] private Vector2 bossGruntInterval = new Vector2(3f, 7f);

    [Header("Inimigo escondido - juventude")]
    [SerializeField] private AudioClip hiddenCrySound;
    [SerializeField, Range(0f, 1f)] private float hiddenCryVolume = 0.7f;

    [Header("Ambiente aleatorio")]
    [SerializeField] private AudioClip[] ambientSounds;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.4f;
    [Tooltip("Espera aleatoria entre o fim de um som e o proximo.")]
    [SerializeField] private Vector2 ambientInterval = new Vector2(12f, 25f);

    [Header("Posicao dos inimigos")]
    [SerializeField, Min(0f)] private float nearDistance = 2f;
    [SerializeField, Min(0.1f)] private float farDistance = 18f;
    [SerializeField, Min(0.1f)] private float stereoRange = 8f;
    [SerializeField, Range(0f, 1f)] private float otherRoomVolume = 0.35f;

    public static SoundSystem Instance { get; private set; }
    public static float EffectsGain => Instance != null && Instance.isActiveAndEnabled
        ? Instance.masterVolume * Instance.effectsVolume : 1f;

    private AudioSource musicSource, caneSource, bossStepSource, bossGruntSource, hiddenCrySource, ambientSource;
    private Rigidbody2D playerBody, bossBody;
    private PlayerRoomTracker roomTracker;
    private Vector2 previousPlayerPosition, previousBossPosition;
    private float lastPlayerMotion = float.NegativeInfinity, lastBossMotion = float.NegativeInfinity;
    private float nextPlayerStep, nextBossStep, nextBossGrunt, nextAmbientTime;
    private bool wasPlaying, wasBossChasing, musicFading;
    private EnemyHidingSpot soundingSpot;
    private AudioClip requestedMusic;
    private AudioClip previousAmbient;
    private Coroutine musicRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (gameFlow == null) gameFlow = GameFlow.Instance;
        if (player == null) player = FindAnyObjectByType<PlayerMove>();
        if (boss == null) boss = FindAnyObjectByType<BossDoorChase>();
        if (hiddenEnemy == null) hiddenEnemy = FindAnyObjectByType<HiddenEnemy>();
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody2D>();
            roomTracker = player.GetComponent<PlayerRoomTracker>();
        }
        if (boss != null) bossBody = boss.GetComponent<Rigidbody2D>();
        musicSource = CreateSource("Music", true, 32);
        caneSource = CreateSource("PlayerCane", false, 96);
        bossStepSource = CreateSource("BossSteps", false, 96);
        bossGruntSource = CreateSource("BossVoice", false, 80);
        hiddenCrySource = CreateSource("HiddenEnemyCry", true, 80);
        ambientSource = CreateSource("Ambience", false, 144);
        ResetMotion();
    }

    private AudioSource CreateSource(string sourceName, bool loop, int priority)
    {
        var node = new GameObject(sourceName);
        node.transform.SetParent(transform, false);
        var source = node.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.priority = priority;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    private void OnEnable()
    {
        if (gameFlow != null) gameFlow.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(GameState state)
    {
        StopGameplaySounds();
        ResetMotion();
        wasPlaying = false;
    }

    private void LateUpdate()
    {
        UpdateMusic();
        if (!GameFlow.CanPlay)
        {
            if (wasPlaying) StopGameplaySounds();
            ResetMotion();
            wasPlaying = false;
            return;
        }
        if (!wasPlaying)
        {
            ResetMotion();
            nextPlayerStep = nextBossStep = nextBossGrunt = Time.time;
            nextAmbientTime = Time.time + RandomDelay(ambientInterval);
            wasPlaying = true;
        }
        UpdatePlayerSounds();
        UpdateBossSounds();
        UpdateHiddenEnemySound();
        UpdateAmbientSounds();
    }

    private void UpdateMusic()
    {
        AudioClip desired = gameFlow == null ? null : gameFlow.State == GameState.Credits
            ? creditsMusic : GameFlow.CanPlay ? gameplayMusic : null;
        if (desired != requestedMusic)
        {
            requestedMusic = desired;
            if (musicRoutine != null) StopCoroutine(musicRoutine);
            musicRoutine = StartCoroutine(SwitchMusic(desired));
        }
        if (!musicFading && musicSource != null)
            musicSource.volume = masterVolume * musicVolume;
    }

    private IEnumerator SwitchMusic(AudioClip clip)
    {
        musicFading = true;
        if (musicSource.isPlaying) yield return FadeMusic(0f);
        musicSource.Stop();
        musicSource.clip = clip;
        if (clip != null)
        {
            musicSource.volume = 0f;
            musicSource.Play();
            yield return FadeMusic(masterVolume * musicVolume);
        }
        musicFading = false;
        musicRoutine = null;
    }

    private IEnumerator FadeMusic(float target)
    {
        float start = musicSource.volume;
        for (float elapsed = 0f; elapsed < musicFadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(start, target, elapsed / musicFadeDuration);
            yield return null;
        }
        musicSource.volume = target;
    }

    private void UpdatePlayerSounds()
    {
        caneSource.volume = caneVolume * EffectsGain;
        bool moving = TrackMotion(playerBody, ref previousPlayerPosition, ref lastPlayerMotion);
        if (player == null || !player.isActiveAndEnabled || player.IsDefeated || player.IsInteracting ||
            player.ReachedTarget || !moving ||
            caneSound == null || Time.time < nextPlayerStep)
            return;
        caneSource.Stop();
        caneSource.clip = caneSound;
        caneSource.time = Mathf.Clamp(caneStartTime, 0f, Mathf.Max(0f, caneSound.length - 0.01f));
        caneSource.Play();
        nextPlayerStep = Time.time + Mathf.Max(0.1f, caneStepInterval);
    }

    private void UpdateBossSounds()
    {
        if (boss == null || !boss.isActiveAndEnabled)
        {
            bossStepSource.Stop(); bossGruntSource.Stop(); wasBossChasing = false;
            return;
        }
        SetPosition(bossStepSource, boss.transform.position, boss.CurrentRoom, bossStepVolume);
        SetPosition(bossGruntSource, boss.transform.position, boss.CurrentRoom, bossGruntVolume);
        bool moving = TrackMotion(bossBody, ref previousBossPosition, ref lastBossMotion);
        if (moving && bossStepSound != null && Time.time >= nextBossStep)
        {
            if (bossStepSource.volume > 0.001f)
            {
                bossStepSource.pitch = Random.Range(0.95f, 1.05f);
                bossStepSource.PlayOneShot(bossStepSound);
            }
            nextBossStep = Time.time + Mathf.Max(0.1f, bossStepInterval);
        }
        if (boss.IsChasing && !wasBossChasing) nextBossGrunt = Time.time;
        if (boss.IsChasing && bossGruntSound != null && Time.time >= nextBossGrunt &&
            !bossGruntSource.isPlaying && bossGruntSource.volume > 0.001f)
        {
            bossGruntSource.clip = bossGruntSound;
            bossGruntSource.Play();
            nextBossGrunt = Time.time + bossGruntSound.length + RandomDelay(bossGruntInterval);
        }
        wasBossChasing = boss.IsChasing;
    }

    private void UpdateHiddenEnemySound()
    {
        EnemyHidingSpot spot = hiddenEnemy != null && hiddenEnemy.isActiveAndEnabled && hiddenEnemy.IsVisible
            ? hiddenEnemy.ActiveSpot : null;
        if (spot != soundingSpot)
        {
            hiddenCrySource.Stop();
            soundingSpot = spot;
        }
        if (spot == null || hiddenCrySound == null) { hiddenCrySource.Stop(); return; }
        SetPosition(hiddenCrySource, spot.AppearancePoint.position, spot.Room, hiddenCryVolume);
        if (hiddenCrySource.clip != hiddenCrySound) hiddenCrySource.clip = hiddenCrySound;
        if (!hiddenCrySource.isPlaying && hiddenCrySource.volume > 0.001f) hiddenCrySource.Play();
    }

    private void UpdateAmbientSounds()
    {
        ambientSource.volume = ambientVolume * EffectsGain;
        if (Time.time < nextAmbientTime || ambientSource.isPlaying || ambientSounds == null || ambientSounds.Length == 0)
            return;
        int start = Random.Range(0, ambientSounds.Length);
        AudioClip clip = null;
        for (int i = 0; i < ambientSounds.Length; i++)
        {
            AudioClip candidate = ambientSounds[(start + i) % ambientSounds.Length];
            if (candidate == null) continue;
            clip = candidate;
            if (candidate != previousAmbient) break;
        }
        if (clip == null) { nextAmbientTime = Time.time + RandomDelay(ambientInterval); return; }
        ambientSource.clip = clip;
        ambientSource.panStereo = Random.Range(-0.8f, 0.8f);
        ambientSource.Play();
        previousAmbient = clip;
        nextAmbientTime = Time.time + clip.length + RandomDelay(ambientInterval);
    }

    private void SetPosition(AudioSource source, Vector2 position, RoomZone room, float volume)
    {
        if (player == null) { source.volume = 0f; return; }
        Vector2 direction = position - (Vector2)player.transform.position;
        float proximity = 1f - Mathf.InverseLerp(nearDistance, Mathf.Max(nearDistance + 0.1f, farDistance), direction.magnitude);
        float roomGain = roomTracker != null && room != null && roomTracker.CurrentRoom != room ? otherRoomVolume : 1f;
        source.volume = volume * EffectsGain * proximity * proximity * roomGain;
        source.panStereo = Mathf.Clamp(direction.x / Mathf.Max(0.1f, stereoRange), -0.9f, 0.9f);
        source.transform.position = position;
    }

    private static bool TrackMotion(Rigidbody2D body, ref Vector2 previous, ref float lastMotion)
    {
        if (body == null) return false;
        float distance = Vector2.Distance(body.position, previous);
        previous = body.position;
        // Uma troca instantanea de andar/sala nao gera um passo.
        float maximum = Mathf.Max(2f, body.linearVelocity.magnitude * Time.deltaTime * 2f);
        if (distance > maximum) lastMotion = float.NegativeInfinity;
        else if (distance > 0.001f) lastMotion = Time.time;
        // Frames sem um FixedUpdate nao interrompem a deteccao do movimento.
        return Time.time - lastMotion < 0.08f;
    }

    private void ResetMotion()
    {
        if (playerBody != null) previousPlayerPosition = playerBody.position;
        if (bossBody != null) previousBossPosition = bossBody.position;
        lastPlayerMotion = lastBossMotion = float.NegativeInfinity;
    }

    private static float RandomDelay(Vector2 interval)
    {
        float minimum = Mathf.Max(0.1f, Mathf.Min(interval.x, interval.y));
        return Random.Range(minimum, Mathf.Max(minimum, Mathf.Max(interval.x, interval.y)));
    }

    private void StopGameplaySounds()
    {
        if (caneSource == null) return;
        caneSource.Stop(); bossStepSource.Stop(); bossGruntSource.Stop(); hiddenCrySource.Stop(); ambientSource.Stop();
        soundingSpot = null;
        wasBossChasing = false;
    }

    private void OnDisable()
    {
        if (gameFlow != null) gameFlow.StateChanged -= OnStateChanged;
        StopAllCoroutines();
        musicFading = false;
        musicRoutine = null;
        requestedMusic = null;
        if (musicSource != null) musicSource.Stop();
        StopGameplaySounds();
        wasPlaying = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
