using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BirthdayTactics.Core;
using UnityEngine;

namespace BirthdayTactics.Presentation
{
    public sealed class VerticalSliceController : MonoBehaviour
    {
        private const string CatalogResourcePath = "Data/mu2_content";
        private const string SaveKey = "birthday-tactics-unity-save-v1";
        private const string SaveBackupKey = "birthday-tactics-unity-save-v1-backup";

        private enum ScreenMode { Title, Story, ChapterStory, Choice, Downfall, Field, Preparation, War, Battle, Gift }
        private enum UnitPose { Idle, Action, Hit, Guard, Victory, Defeat }

        private sealed class UnitView
        {
            public FormationCombatant Unit;
            public GameObject Object;
            public SpriteRenderer Renderer;
            public SpriteRenderer BlendRenderer;
            public IRuntimeBoneRig2D BoneRig;
            public GameObject ShadowObject;
            public SpriteRenderer ShadowRenderer;
            public Sprite IdleSprite;
            public Sprite ActionSprite;
            public Sprite HitSprite;
            public Sprite VictorySprite;
            public Sprite DefeatSprite;
            public Sprite[] PixelRunSprites;
            public Dictionary<UnitPose, Sprite[]> PixelPoseSprites;
            public bool IsPixel;
            public Vector3 Home;
            public Vector3 BaseScale;
            public Vector3 ActionScale;
            public Vector3 HitScale;
            public Vector3 VictoryScale;
            public Vector3 DefeatScale;
            public Color BaseColor;
            public Color ShadowColor;
            public float GroundLift;
            public float IdlePhase;
            public UnitPose CurrentPose;
            public bool Animating;
        }

        private sealed class FloatingLabel
        {
            public string Text;
            public Vector3 World;
            public Color Color;
            public float Age;
            public float Duration;
        }

        private readonly Dictionary<string, UnitView> _unitViews = new Dictionary<string, UnitView>();
        private readonly Dictionary<string, Texture2D> _pixelAtlases =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _pixelMotionAtlases =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _pixelBonePartTextures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, PixelSkinCpuRenderer> _pixelSkinCpuRenderers =
            new Dictionary<string, PixelSkinCpuRenderer>(StringComparer.Ordinal);
        private readonly List<FloatingLabel> _labels = new List<FloatingLabel>();
        private readonly List<GameObject> _effects = new List<GameObject>();
        // One cinematic introduction per featured combatant keeps the battle readable.
        private readonly HashSet<string> _cutInsShown = new HashSet<string>();
        private bool _bondTechniqueShown;

        private ContentCatalogData _catalog;
        private FormationBattleCore _battle;
        private StoryExplorationCore _storyExploration;
        private FieldMapCore _fieldMap;
        private FieldExplorationCore _fieldExploration;
        private WarCampaignCore _war;
        private WarRoundReport _warReport;
        private StageData _stage;
        private BattlePreparationState _preparation;
        private CampaignSaveData _save;
        private AudioManager _audio;
        private Camera _camera;
        private GameObject _battleRoot;
        private ScreenMode _screen = ScreenMode.Title;
        private int _stageIndex;
        private bool _hasSave;
        private bool _confirmNewGame;
        private bool _paused;
        private bool _showResult;
        private float _battleSpeed = 1f;
        private float _titleFade;
        private float _giftTime;
        private float _shakeUntil;
        private float _shakeStrength;
        private float _fieldPulse;
        private float _fieldSaveCooldown;
        private bool _fieldHasMoveTarget;
        private bool _fieldEncounterStarting;
        private bool _npcEventOpen;
        private bool _storyHasMoveTarget;
        private bool _storyDialogueOpen;
        private bool _storyRecruitmentCardOpen;
        private bool _battleCompletedStage;
        private bool _battleCommandOpen;
        private int _battleActionIndex;
        private float _impactFlashAlpha;
        private Vector2 _preparationScroll;
        private Vector2 _fieldMoveTarget;
        private Vector2 _storyMoveTarget;
        private Color _impactFlashColor = Color.white;
        private Vector3 _cameraBasePosition;
        private Vector3 _cameraDesiredPosition;
        private float _cameraDesiredSize = 5.4f;
        private float _battleBaseCameraSize = 5.4f;
        private string _message = string.Empty;
        private string _titleNotice = string.Empty;
        private string _skillBanner = string.Empty;
        private string _fieldNotice = string.Empty;
        private string _activeEncounterEntityId = string.Empty;
        private string _pendingNpcEntityId = string.Empty;
        private string _pendingNpcName = string.Empty;
        private string _storyNotice = string.Empty;
        private string _recentRecruitUnitId = string.Empty;
        private string[] _storyDialogueLines = Array.Empty<string>();
        private int _storyDialogueIndex;
        private StoryEntity _pendingStoryEntity;
        private StoryEntity _pendingStoryPassage;
        private FormationCombatant _pendingCommandActor;
        private FormationBattleCommand _pendingBattleCommand;
        private ChapterStoryBeat _chapterStoryBeat;
        private int _chapterStoryLineIndex;
        // 走り表現。実際に座標が動いたかを見て 0（立ち）〜1（走り）へ補間する。
        // 入力方法（クリック移動でもキー移動でも）に依存しないよう、位置の差分で判定する。
        private Vector2 _storyPreviousPosition;
        private float _storyRunBlend;
        private Vector2 _storyFacing = new Vector2(0f, 1f);
        private Vector2 _fieldPreviousPosition;
        private float _fieldRunBlend;
        private Vector2 _fieldFacing = new Vector2(0f, 1f);
        private StoryChoicePrompt _choicePrompt;
        private StoryChoiceOption _choiceOption;
        private int _choiceOptionIndex = -1;
        private int _choiceLineIndex;
        // 試練の最中だけ非nullになる。通常の章戦闘と結果画面の分岐を切り替えるために使う。
        private OrdealEncounter _ordealEncounter;
        private string _relicNotice = string.Empty;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _heroTitleStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _buttonStyle;
        private Texture2D _effectTexture;
        private Texture2D _equipmentIconAtlas;
        private Sprite _effectSprite;
        private Sprite _shadowSprite;
        private Sprite _shadeSprite;
        private readonly HashSet<Sprite> _battleSprites = new HashSet<Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<VerticalSliceController>() == null)
            {
                var host = new GameObject("Birthday Tactics Formation Battle");
                host.AddComponent<VerticalSliceController>();
            }
        }

        private void Awake()
        {
            LoadCatalog();
            LoadSave();
            _audio = gameObject.AddComponent<AudioManager>();
            _audio.Initialize(_save.volume, _save.muted);
            _effectTexture = CreateEffectTexture();
            _equipmentIconAtlas = Resources.Load<Texture2D>("Art/UI/equipment_icon_atlas");
            if (_equipmentIconAtlas != null)
            {
                _equipmentIconAtlas.filterMode = FilterMode.Point;
                _equipmentIconAtlas.wrapMode = TextureWrapMode.Clamp;
            }
            _effectSprite = CreateSharedSprite(_effectTexture, 8f);
            _shadowSprite = CreateSharedSprite(_effectTexture, _effectTexture.width);
            _shadeSprite = CreateSharedSprite(_effectTexture, 1f);
            ShowTitle();
        }

        private void OnDestroy()
        {
            ReleaseBattleSprites();
            foreach (PixelSkinCpuRenderer renderer in _pixelSkinCpuRenderers.Values)
                renderer?.Dispose();
            _pixelSkinCpuRenderers.Clear();
            if (_effectSprite != null) Destroy(_effectSprite);
            if (_shadowSprite != null) Destroy(_shadowSprite);
            if (_shadeSprite != null) Destroy(_shadeSprite);
            if (_effectTexture != null) Destroy(_effectTexture);
        }

        private void Update()
        {
            if (_screen == ScreenMode.Title)
            {
                _titleFade = Mathf.MoveTowards(_titleFade, 1f, Time.unscaledDeltaTime / 1.1f);
                return;
            }

            if (_screen == ScreenMode.Gift)
            {
                _giftTime += Time.unscaledDeltaTime;
                return;
            }

            if (_screen == ScreenMode.Field)
            {
                _fieldPulse += Time.unscaledDeltaTime;
                UpdateFieldExploration();
                _fieldRunBlend = AdvanceRunBlendAndFacing(
                    _fieldRunBlend,
                    ref _fieldPreviousPosition,
                    ref _fieldFacing,
                    new Vector2(_fieldExploration.PlayerX, _fieldExploration.PlayerY),
                    Time.unscaledDeltaTime);
                return;
            }

            if (_screen == ScreenMode.Story)
            {
                _fieldPulse += Time.unscaledDeltaTime;
                UpdateStoryExploration();
                _storyRunBlend = AdvanceRunBlendAndFacing(
                    _storyRunBlend,
                    ref _storyPreviousPosition,
                    ref _storyFacing,
                    new Vector2(_storyExploration.PlayerX, _storyExploration.PlayerY),
                    Time.unscaledDeltaTime);
                return;
            }

            if (_screen == ScreenMode.ChapterStory) return;

            if (_screen == ScreenMode.Preparation || _screen == ScreenMode.War) return;

            _impactFlashAlpha = Mathf.MoveTowards(
                _impactFlashAlpha,
                0f,
                Time.unscaledDeltaTime * 3.8f * _battleSpeed);
            float now = Time.unscaledTime;
            foreach (UnitView view in _unitViews.Values)
            {
                if (!view.Unit.IsAlive || view.Animating || view.Object == null) continue;
                float breath = Mathf.Sin(now * 1.55f + view.IdlePhase);
                float settle = Mathf.Sin(now * 0.73f + view.IdlePhase * 0.61f);
                bool flying = string.Equals(view.Unit.ClassName, "flier", StringComparison.OrdinalIgnoreCase);
                float hover = flying ? breath * 0.055f : 0f;
                Vector3 poseScale = ScaleForPose(view, view.CurrentPose);
                if (view.IsPixel &&
                    view.CurrentPose == UnitPose.Idle &&
                    view.PixelPoseSprites != null &&
                    view.PixelPoseSprites.TryGetValue(UnitPose.Idle, out Sprite[] idleMotion) &&
                    idleMotion.Length > 0)
                {
                    int idleFrame = Math.Max(
                        0,
                        (int)Math.Floor(
                            (now + view.IdlePhase * 0.071f) * PixelAnimationProfile.FramesPerSecond)) %
                        idleMotion.Length;
                    view.Renderer.sprite = idleMotion[idleFrame];
                }
                if (view.BoneRig != null)
                {
                    float cycle = Mathf.Repeat(now * 0.22f, 1f);
                    view.BoneRig.Apply(view.BoneRig.Sample(
                        BonePoseFor(view.CurrentPose),
                        cycle,
                        view.IdlePhase * 0.037f));
                }
                view.Object.transform.position = view.Home + Vector3.up * hover;
                view.Object.transform.localScale = new Vector3(
                    poseScale.x * (1f - breath * 0.004f),
                    poseScale.y * (1f + breath * 0.011f),
                    poseScale.z);
                view.Object.transform.rotation = Quaternion.Euler(0f, 0f, breath * 0.16f + settle * 0.11f);
                if (view.ShadowRenderer != null)
                {
                    float shadowPulse = flying ? 0.72f - breath * 0.08f : 1f - breath * 0.025f;
                    view.ShadowRenderer.color = new Color(
                        view.ShadowColor.r,
                        view.ShadowColor.g,
                        view.ShadowColor.b,
                        view.ShadowColor.a * shadowPulse);
                }
            }

            for (int i = _labels.Count - 1; i >= 0; i--)
            {
                FloatingLabel label = _labels[i];
                label.Age += Time.unscaledDeltaTime * _battleSpeed;
                label.World += Vector3.up * (Time.unscaledDeltaTime * 0.52f);
                if (label.Age >= label.Duration) _labels.RemoveAt(i);
            }

            if (_camera != null)
            {
                _cameraBasePosition = Vector3.Lerp(_cameraBasePosition, _cameraDesiredPosition, 0.10f);
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _cameraDesiredSize, 0.10f);
                if (now < _shakeUntil)
                {
                    float phase = now * 92f;
                    _camera.transform.position = _cameraBasePosition +
                        new Vector3(Mathf.Sin(phase) * _shakeStrength, Mathf.Cos(phase * 1.17f) * _shakeStrength, 0f);
                }
                else
                {
                    // 係数を固定するとフレームレートで戻る速さが変わるため、指数減衰で時間基準にする。
                    _camera.transform.position = Vector3.Lerp(
                        _camera.transform.position,
                        _cameraBasePosition,
                        1f - Mathf.Exp(-26f * Time.unscaledDeltaTime));
                }
            }
        }

        private void LoadCatalog()
        {
            TextAsset json = Resources.Load<TextAsset>(CatalogResourcePath);
            if (json == null) throw new MissingReferenceException($"Missing Resources/{CatalogResourcePath}.json");
            _catalog = JsonUtility.FromJson<ContentCatalogData>(json.text);
            if (_catalog == null || _catalog.schemaVersion != 2 || _catalog.stages == null || _catalog.stages.Length == 0)
                throw new InvalidOperationException("Content catalog is invalid.");
        }

        private void LoadSave()
        {
            _hasSave = PlayerPrefs.HasKey(SaveKey);
            CampaignSaveData loaded = null;
            if (_hasSave)
            {
                try { loaded = JsonUtility.FromJson<CampaignSaveData>(PlayerPrefs.GetString(SaveKey)); }
                catch (Exception exception) { Debug.LogWarning($"Save data was ignored: {exception.Message}"); }
            }
            _save = CampaignSavePolicy.Normalize(loaded, _catalog.stages);
        }

        private void PersistSave()
        {
            try
            {
                _save = CampaignSavePolicy.Normalize(_save, _catalog.stages);
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_save));
                PlayerPrefs.Save();
                _hasSave = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save failed, but play continues: {exception.Message}");
            }
        }

        private bool HasSaveBackup()
        {
            return PlayerPrefs.HasKey(SaveBackupKey);
        }

        private void BackupCurrentSave()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            string json = PlayerPrefs.GetString(SaveKey);
            if (string.IsNullOrWhiteSpace(json)) return;
            PlayerPrefs.SetString(SaveBackupKey, json);
            PlayerPrefs.Save();
        }

        private void RestoreSaveBackup()
        {
            if (!HasSaveBackup()) return;
            try
            {
                CampaignSaveData restored = JsonUtility.FromJson<CampaignSaveData>(
                    PlayerPrefs.GetString(SaveBackupKey));
                _save = CampaignSavePolicy.Normalize(restored, _catalog.stages);
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_save));
                PlayerPrefs.Save();
                _hasSave = true;
                _confirmNewGame = false;
                _titleNotice = "前回のセーブを復元しました。";
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save restore failed: {exception.Message}");
                _titleNotice = "セーブの復元に失敗しました。";
            }
        }

        private Camera EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.AddComponent<Camera>();
            }
            _camera.orthographic = true;
            _battleBaseCameraSize =
                FormationPresentationProfile.GetSafeBattleCameraSize(Screen.width, Screen.height);
            _camera.orthographicSize = _battleBaseCameraSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            _cameraBasePosition = new Vector3(0f, 0f, -10f);
            _cameraDesiredPosition = _cameraBasePosition;
            _cameraDesiredSize = _battleBaseCameraSize;
            _camera.transform.position = _cameraBasePosition;
            _camera.transform.rotation = Quaternion.identity;
            return _camera;
        }

        private void ShowTitle()
        {
            ClearBattlePresentation();
            EnsureCamera();
            _screen = ScreenMode.Title;
            _fieldMap = null;
            _war = null;
            _preparation = null;
            _titleFade = 0f;
            _confirmNewGame = false;
            _titleNotice = string.Empty;
            _audio?.PlayBgm("BD-01", 1.5f);
        }

        private void BeginCampaign(bool reset)
        {
            if (reset)
            {
                BackupCurrentSave();
                _save = CampaignSavePolicy.NewSave(_catalog.stages.Length);
                PlayerPrefs.DeleteKey(SaveKey);
                _hasSave = false;
            }
            _audio.PlaySfx("select");
            if (!reset && CampaignSavePolicy.IsGiftUnlocked(_save, _catalog.stages.Length))
            {
                EnterGift();
                return;
            }
            if (_save.storyPrologueCompleted)
                ShowPendingChapterStoryOrField(_save.stageIndex);
            else
                ShowStoryArea(StoryAreaKind.Town);
        }

        private void ShowPendingChapterStoryOrField(int stageIndex)
        {
            ChapterStoryBeat pending = ChapterStoryPolicy.GetPending(
                stageIndex,
                _save.resolvedStoryEntityIds);
            if (pending == null)
            {
                ShowPendingChoiceOrField(stageIndex);
                return;
            }

            ClearBattlePresentation();
            _stageIndex = Mathf.Clamp(stageIndex, 0, _catalog.stages.Length - 1);
            _chapterStoryBeat = pending;
            _chapterStoryLineIndex = 0;
            _screen = ScreenMode.ChapterStory;
            EnsureCamera();
            _audio.PlayBgm("BD-01", 0.8f);
        }

        private void AdvanceChapterStory()
        {
            if (_chapterStoryBeat == null)
            {
                ShowField(_stageIndex);
                return;
            }
            _chapterStoryLineIndex++;
            if (_chapterStoryLineIndex < _chapterStoryBeat.Lines.Count) return;

            _save = CampaignSavePolicy.StoreStoryEntityResolution(
                _save,
                _chapterStoryBeat.Id,
                false,
                _catalog.stages);
            PersistSave();
            _chapterStoryBeat = null;
            ShowPendingChoiceOrField(_stageIndex, "章間の物語を終えました。次の敵部隊を追ってください。");
        }

        /// <summary>
        /// その章の選択肢がまだ決着していなければ選択肢画面へ、済んでいればフィールドへ。
        /// 破滅した場合は決着扱いにならないので、同じ場面に何度でも戻ってくる。
        /// </summary>
        private void ShowPendingChoiceOrField(int stageIndex, string notice = null)
        {
            StoryChoicePrompt pending = StoryChoicePolicy.GetPendingPrompt(
                stageIndex,
                _save.resolvedStoryEntityIds);
            if (pending == null)
            {
                ShowField(stageIndex, notice);
                return;
            }

            ClearBattlePresentation();
            _stageIndex = Mathf.Clamp(stageIndex, 0, _catalog.stages.Length - 1);
            _choicePrompt = pending;
            _choiceOption = null;
            _choiceOptionIndex = -1;
            _choiceLineIndex = 0;
            _ordealEncounter = null;
            _relicNotice = string.Empty;
            _screen = ScreenMode.Choice;
            EnsureCamera();
            _audio.PlayBgm("BD-01", 0.8f);
        }

        private void SelectChoiceOption(int optionIndex)
        {
            if (_choicePrompt == null) return;
            _choiceOptionIndex = optionIndex;
            _choiceOption = StoryChoicePolicy.Resolve(_choicePrompt.Id, optionIndex);
            _choiceLineIndex = 0;
            _audio.PlaySfx("select");
        }

        private void AdvanceChoiceLine()
        {
            if (_choicePrompt == null || _choiceOption == null) return;
            _choiceLineIndex++;
            if (_choiceLineIndex < _choiceOption.Lines.Count) return;

            // 破滅と試練開始時は決着マーカーを付けない。
            // 試練は勝利後にだけ ResolveOrdealOutcome で決着済みにする。
            foreach (string record in StoryChoicePolicy.BuildResolutionRecords(
                         _choicePrompt.Id,
                         _choiceOptionIndex))
            {
                _save = CampaignSavePolicy.StoreStoryEntityResolution(
                    _save,
                    record,
                    false,
                    _catalog.stages);
            }
            PersistSave();

            switch (_choiceOption.Outcome)
            {
                case StoryChoiceOutcome.Ordeal:
                    StartOrdealBattle(StoryChoicePolicy.FindOrdeal(_choiceOption.OrdealId));
                    break;
                case StoryChoiceOutcome.Downfall:
                    _screen = ScreenMode.Downfall;
                    _audio.PlayBgm("BD-01", 2.4f);
                    break;
                default:
                    _choicePrompt = null;
                    _choiceOption = null;
                    ShowField(_stageIndex);
                    break;
            }
        }

        /// <summary>
        /// 破滅からの復帰。セーブは書き換えないので、同じ選択肢へそのまま戻るだけでよい。
        /// </summary>
        private void RetryFromDownfall()
        {
            _choiceOption = null;
            _choiceOptionIndex = -1;
            _choiceLineIndex = 0;
            _screen = ScreenMode.Choice;
            _audio.PlayBgm("BD-01", 0.8f);
        }

        private void StartOrdealBattle(OrdealEncounter ordeal)
        {
            if (ordeal == null)
            {
                ShowField(_stageIndex);
                return;
            }

            StageData baseStage = RecruitmentRosterPolicy.CreateStage(
                _catalog.stages[_catalog.stages.Length - 1],
                _catalog.stages,
                _save.recruitedUnitIds);

            ClearBattlePresentation();
            _ordealEncounter = ordeal;
            _stage = OrdealStagePolicy.BuildStage(baseStage, ordeal);
            _preparation = null;
            _battle = new FormationBattleCore(_stage);
            _screen = ScreenMode.Battle;
            _paused = false;
            _showResult = false;
            _battleCommandOpen = false;
            _pendingCommandActor = null;
            _pendingBattleCommand = null;
            _battleCompletedStage = false;
            _skillBanner = string.Empty;
            _message = ordeal.Name;
            _battleActionIndex = 0;
            _impactFlashAlpha = 0f;
            EnsureCamera();
            BuildBattlefield();
            _audio.PlayBgm("BD-02", 1.2f);
            StartCoroutine(BattleRoutine());
        }

        /// <summary>
        /// 試練の決着。勝てば銘器を、負けても罰は無く会話へ戻す。
        /// 理不尽な相手に挑む判断そのものを罰すると、二度と挑まなくなる。
        /// </summary>
        private void ResolveOrdealOutcome(bool won)
        {
            OrdealEncounter ordeal = _ordealEncounter;
            _ordealEncounter = null;
            _relicNotice = string.Empty;

            if (won && ordeal != null)
            {
                if (_choicePrompt != null)
                {
                    _save = CampaignSavePolicy.StoreStoryEntityResolution(
                        _save,
                        StoryChoicePolicy.BuildSettledRecordId(_choicePrompt.Id),
                        false,
                        _catalog.stages);
                }
                IReadOnlyList<string> records = StoryChoicePolicy.BuildOrdealVictoryRecords(
                    _save.resolvedStoryEntityIds,
                    ordeal.Id);
                foreach (string record in records)
                {
                    _save = CampaignSavePolicy.StoreStoryEntityResolution(
                        _save,
                        record,
                        false,
                        _catalog.stages);
                }
                PersistSave();

                UniqueRelic relic = StoryChoicePolicy.FindRelic(ordeal.RelicId);
                if (relic != null)
                {
                    _relicNotice = records.Count > 0
                        ? $"{relic.AcquisitionLine}　――「{relic.Name}」を手に入れた。"
                        : $"「{relic.Name}」は既に持っている。";
                }
            }

            _choicePrompt = null;
            _choiceOption = null;
            if (won)
                ShowField(_stageIndex, _relicNotice);
            else
                ShowPendingChoiceOrField(
                    _stageIndex,
                    "試練には再び挑めます。選択をやり直してください。");
        }

        private void ShowStoryArea(StoryAreaKind area)
        {
            ClearBattlePresentation();
            _stageIndex = Mathf.Clamp(_save.stageIndex, 0, _catalog.stages.Length - 1);
            _storyExploration = StoryExplorationCore.Create(
                area,
                _save.townGuideHeard,
                CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryArcherId),
                _save.resolvedStoryEntityIds,
                _save.storyClockMinutes,
                CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryHealerId),
                CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryMinstrelId));
            _screen = ScreenMode.Story;
            _fieldPulse = 0f;
            _storyHasMoveTarget = false;
            _storyDialogueOpen = false;
            _storyRecruitmentCardOpen = false;
            _storyDialogueLines = Array.Empty<string>();
            _storyDialogueIndex = 0;
            _pendingStoryEntity = null;
            _pendingStoryPassage = null;
            _storyRunBlend = 0f;
            _storyFacing = new Vector2(0f, 1f);
            _storyPreviousPosition = new Vector2(
                _storyExploration.PlayerX,
                _storyExploration.PlayerY);
            _recentRecruitUnitId = string.Empty;
            _storyNotice = area == StoryAreaKind.Town
                ? "水鏡の町。人々と話し、工房や北東門を調べよう。"
                : area == StoryAreaKind.Interior
                    ? "思い出工房。世話人の話を聞き、預けられた小箱を調べよう。"
                : area == StoryAreaKind.Inn
                    ? "湖畔の宿。旅人の話を聞き、仲間と出発の支度を整えよう。"
                    : area == StoryAreaKind.Base
                        ? $"灯の館 第{_storyExploration.BaseGrowth.Level}段。" +
                          $"灯 {_storyExploration.BaseGrowth.LightCount}/{_storyExploration.BaseGrowth.TargetLightCount}。" +
                          "迎えた仲間と開いた施設を確かめよう。"
                    : "追憶の礼拝堂。学者や宝箱を訪ね、奥で待つ気配を探そう。";
            EnsureCamera();
            PersistSave();
            _audio.PlayBgm(
                area == StoryAreaKind.Dungeon
                    ? "BD-03"
                    : area == StoryAreaKind.Base
                        ? "BD-04"
                        : "BD-01",
                0.8f);
        }

        private void UpdateStoryExploration()
        {
            if (_storyExploration == null ||
                _storyDialogueOpen ||
                _storyRecruitmentCardOpen)
                return;

            _storyExploration.AdvanceClock(Time.unscaledDeltaTime);
            _save.storyClockMinutes = _storyExploration.StoryClockMinutes;

            if (_pendingStoryPassage != null &&
                (Input.GetKeyDown(KeyCode.Return) ||
                 Input.GetKeyDown(KeyCode.KeypadEnter) ||
                 Input.GetKeyDown(KeyCode.E)))
            {
                ConfirmStoryPassage();
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical += 1f;

            StoryExplorationResult result = StoryExplorationResult.Idle;
            float step = Time.unscaledDeltaTime * 0.22f;
            if (Mathf.Abs(horizontal) > 0.001f || Mathf.Abs(vertical) > 0.001f)
            {
                _storyHasMoveTarget = false;
                result = _storyExploration.Move(horizontal, vertical, step);
            }
            else if (_storyHasMoveTarget)
            {
                float distance = Vector2.Distance(
                    new Vector2(
                        _storyExploration.PlayerX,
                        _storyExploration.PlayerY),
                    _storyMoveTarget);
                if (distance <= 0.008f)
                {
                    _storyHasMoveTarget = false;
                }
                else
                {
                    result = _storyExploration.MoveToward(
                        _storyMoveTarget.x,
                        _storyMoveTarget.y,
                        Mathf.Min(step, distance));
                }
            }

            if (result == StoryExplorationResult.Dialogue ||
                result == StoryExplorationResult.Recruit ||
                result == StoryExplorationResult.Treasure ||
                result == StoryExplorationResult.Passage ||
                result == StoryExplorationResult.Locked)
            {
                _storyHasMoveTarget = false;
                _pendingStoryPassage = _storyExploration.LastInteractionEntity;
                if (_pendingStoryPassage != null)
                    _storyNotice = $"{_pendingStoryPassage.DisplayName}の近くです。決定ボタンで調べます。";
                return;
            }
            HandleStoryExplorationResult(result);
        }

        private void HandleStoryExplorationResult(StoryExplorationResult result)
        {
            if (result == StoryExplorationResult.Idle)
                return;

            if (result == StoryExplorationResult.Moved)
            {
                _pendingStoryPassage = null;
                return;
            }

            _storyHasMoveTarget = false;
            if (result == StoryExplorationResult.Blocked)
            {
                _storyNotice = "建物や瓦礫に阻まれています。別の経路を選んでください。";
                return;
            }
            if (result == StoryExplorationResult.Locked)
            {
                _storyNotice = "北東門は閉じています。旅の案内人から礼拝堂への道を聞きましょう。";
                _audio.PlaySfx("select");
                return;
            }
            if (result == StoryExplorationResult.Passage)
            {
                _pendingStoryPassage = _storyExploration.LastInteractionEntity;
                if (_pendingStoryPassage != null)
                    _storyNotice = $"{_pendingStoryPassage.DisplayName}の前です。［入る／移動する］を押してください。";
                _audio.PlaySfx("select");
                return;
            }
            if (result == StoryExplorationResult.Transfer)
            {
                _audio.PlaySfx("move");
                string passageId = _pendingStoryPassage?.Id ??
                                   _storyExploration.LastInteractionEntity?.Id;
                if (string.Equals(
                        passageId,
                        "town-atelier-door",
                        StringComparison.Ordinal))
                    ShowStoryArea(StoryAreaKind.Interior);
                else if (string.Equals(
                             passageId,
                             "town-inn-door",
                             StringComparison.Ordinal))
                    ShowStoryArea(StoryAreaKind.Inn);
                else if (string.Equals(
                             passageId,
                             "town-base-door",
                             StringComparison.Ordinal))
                    ShowStoryArea(StoryAreaKind.Base);
                else if (string.Equals(
                             passageId,
                             "interior-exit",
                             StringComparison.Ordinal) ||
                         string.Equals(
                             passageId,
                             "inn-exit",
                             StringComparison.Ordinal) ||
                         string.Equals(
                             passageId,
                             "base-exit",
                             StringComparison.Ordinal))
                    ShowStoryArea(StoryAreaKind.Town);
                else
                    ShowStoryArea(StoryAreaKind.Dungeon);
                return;
            }
            if (result == StoryExplorationResult.Treasure)
            {
                StoryEntity treasure = _storyExploration.LastInteractionEntity;
                if (treasure == null) return;
                _storyExploration.ResolveEntity(treasure.Id);
                _save = CampaignSavePolicy.StoreStoryEntityResolution(
                    _save,
                    treasure.Id,
                    true,
                    _catalog.stages);
                PersistSave();
                _storyNotice =
                    $"{treasure.DisplayName}を開けた。思い出の護符を入手（探索宝物 {_save.storyTreasureCount}）。";
                _audio.PlaySfx("victory");
                return;
            }

            _pendingStoryEntity = _storyExploration.LastInteractionEntity;
            if (_pendingStoryEntity == null) return;
            _storyDialogueIndex = 0;
            _storyDialogueLines = StoryDialogueLines(
                _pendingStoryEntity,
                _storyExploration.TimeOfDay);
            _storyDialogueOpen = true;
            _audio.PlaySfx("select");
        }

        private void ConfirmStoryPassage()
        {
            if (_storyExploration == null || _pendingStoryPassage == null) return;
            StoryExplorationResult result = _storyExploration.ConfirmCurrentInteraction();
            if (result == StoryExplorationResult.Idle)
            {
                _pendingStoryPassage = null;
                _storyNotice = "対象から離れました。もう一度近づいてください。";
                return;
            }
            HandleStoryExplorationResult(result);
        }

        private void AdvanceStoryDialogue()
        {
            if (!_storyDialogueOpen || _pendingStoryEntity == null) return;
            _storyDialogueIndex++;
            if (_storyDialogueIndex < _storyDialogueLines.Length) return;

            _storyDialogueOpen = false;
            _storyExploration.ResolveEntity(_pendingStoryEntity.Id);
            if (_pendingStoryEntity.Kind == StoryEntityKind.Dialogue)
            {
                if (string.Equals(
                        _pendingStoryEntity.Id,
                        "town-guide",
                        StringComparison.Ordinal))
                {
                    _save = CampaignSavePolicy.StoreTownGuideHeard(
                        _save,
                        _catalog.stages);
                    PersistSave();
                    _storyNotice = "北東門が開きました。礼拝堂へ向かいましょう。";
                }
                else
                {
                    BaseSupportResident newSupport = _pendingStoryEntity.WasPreviouslyResolved
                        ? null
                        : BaseGrowthPolicy.FindBySourceEntityId(_pendingStoryEntity.Id);
                    _save = CampaignSavePolicy.StoreStoryEntityResolution(
                        _save,
                        _pendingStoryEntity.Id,
                        false,
                        _catalog.stages);
                    PersistSave();
                    if (newSupport != null)
                    {
                        _recentRecruitUnitId = newSupport.SourceEntityId;
                        _storyRecruitmentCardOpen = true;
                        _storyNotice = $"{newSupport.Name}が灯の館に加わりました。";
                        _audio.PlaySfx("victory");
                    }
                    else
                    {
                        _storyNotice = $"{_pendingStoryEntity.DisplayName}の話を聞いた。";
                    }
                }
            }
            else if (_pendingStoryEntity.Kind == StoryEntityKind.Recruit)
            {
                string recruitUnitId = RecruitUnitIdForEntity(_pendingStoryEntity.Id);
                _save = CampaignSavePolicy.StoreRecruitment(
                    _save,
                    recruitUnitId,
                    _catalog.stages);
                _save = CampaignSavePolicy.CompleteStoryPrologue(
                    _save,
                    _catalog.stages);
                PersistSave();
                _recentRecruitUnitId = recruitUnitId;
                _storyRecruitmentCardOpen = true;
                _storyNotice = $"{_pendingStoryEntity.DisplayName}が仲間になりました。";
                _audio.PlaySfx("victory");
            }
            _pendingStoryEntity = null;
        }

        private static string[] StoryDialogueLines(
            StoryEntity entity,
            StoryTimeOfDay timeOfDay) =>
            StoryDialogueCatalog.GetLines(entity, timeOfDay);

        private static string RecruitUnitIdForEntity(string entityId)
        {
            switch (entityId)
            {
                case "dungeon-memory-healer":
                    return RecruitmentRosterPolicy.MemoryHealerId;
                case "inn-minstrel":
                    return RecruitmentRosterPolicy.MemoryMinstrelId;
                default:
                    return RecruitmentRosterPolicy.MemoryArcherId;
            }
        }

        private void ShowField(int stageIndex, string notice = null)
        {
            ClearBattlePresentation();
            _stageIndex = Mathf.Clamp(stageIndex, 0, _catalog.stages.Length - 1);
            _save = CampaignSavePolicy.SelectStage(_save, _stageIndex, _catalog.stages);
            _fieldMap = FieldMapCore.Create(_stageIndex, _save.fieldNodeIndex);
            FieldNode savedNode = _fieldMap.Nodes[_save.fieldNodeIndex];
            float startX = _save.hasFieldPosition ? _save.fieldX : savedNode.X;
            float startY = _save.hasFieldPosition ? _save.fieldY : savedNode.Y;
            _fieldExploration = FieldExplorationCore.Create(
                _stageIndex,
                startX,
                startY,
                _save.resolvedFieldEntityIds);
            if (_fieldExploration.DistanceToNearestEnemy() <= _fieldExploration.EncounterRadius + 0.025f)
            {
                FieldNode approachNode = _fieldMap.Nodes[5];
                _fieldExploration = FieldExplorationCore.Create(
                    _stageIndex,
                    approachNode.X,
                    approachNode.Y,
                    _save.resolvedFieldEntityIds);
            }
            _war = null;
            _warReport = null;
            _screen = ScreenMode.Field;
            _fieldPulse = 0f;
            _fieldSaveCooldown = 0f;
            _fieldHasMoveTarget = false;
            _fieldRunBlend = 0f;
            _fieldFacing = new Vector2(0f, 1f);
            _fieldPreviousPosition = new Vector2(
                _fieldExploration.PlayerX,
                _fieldExploration.PlayerY);
            _fieldEncounterStarting = false;
            _npcEventOpen = false;
            _activeEncounterEntityId = string.Empty;
            _fieldNotice = notice ?? $"敵部隊は「{_catalog.stages[_stageIndex].displayName}」方面に展開中";
            EnsureCamera();
            PersistSave();
            _audio.PlayBgm("BD-05", 0.8f);
        }

        private void MoveFieldTo(int nodeIndex)
        {
            if (_fieldMap == null) return;
            FieldMoveResult result = _fieldMap.MoveTo(nodeIndex);
            HandleFieldMoveResult(result);
        }

        private void HandleFieldMoveResult(FieldMoveResult result)
        {
            if (result == FieldMoveResult.Blocked)
            {
                _fieldNotice = "現在地と道がつながっていません。";
                return;
            }

            _audio.PlaySfx(result == FieldMoveResult.Encounter ? "select" : "move");
            _save = CampaignSavePolicy.StoreFieldNode(
                _save,
                _fieldMap.PlayerNodeIndex,
                _catalog.stages);
            PersistSave();
            if (result == FieldMoveResult.Encounter)
            {
                _fieldNotice = "敵部隊を捕捉。戦闘へ突入します。";
                StartEncounterBattle(_fieldMap.EncounterNode.StageIndex);
            }
            else
            {
                _fieldNotice = $"{_fieldMap.CurrentNode.Name}へ移動しました。敵影は地図上に表示されています。";
            }
        }

        private void AdvanceFieldTowardEncounter()
        {
            if (_fieldExploration == null) return;
            _fieldMoveTarget = new Vector2(
                _fieldExploration.EnemyX,
                _fieldExploration.EnemyY);
            _fieldHasMoveTarget = true;
            _fieldNotice = "敵シンボルへ接近中。接触前に大規模戦や編成方針を確認できます。";
        }

        private void UpdateFieldExploration()
        {
            if (_fieldExploration == null || _fieldEncounterStarting || _npcEventOpen) return;

            bool confirmedInteraction = false;
            FieldExplorationResult result = FieldExplorationResult.Idle;
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.E))
            {
                result = _fieldExploration.ConfirmCurrentInteraction();
                confirmedInteraction = result == FieldExplorationResult.Interacted;
            }

            float horizontal = 0f;
            float vertical = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical += 1f;

            float step = Time.unscaledDeltaTime * 0.22f;
            if (!confirmedInteraction &&
                (Mathf.Abs(horizontal) > 0.001f || Mathf.Abs(vertical) > 0.001f))
            {
                _fieldHasMoveTarget = false;
                result = _fieldExploration.Move(horizontal, vertical, step);
            }
            else if (!confirmedInteraction && _fieldHasMoveTarget)
            {
                float targetDistance = Vector2.Distance(
                    new Vector2(_fieldExploration.PlayerX, _fieldExploration.PlayerY),
                    _fieldMoveTarget);
                if (targetDistance <= 0.008f)
                {
                    _fieldHasMoveTarget = false;
                }
                else
                {
                    result = _fieldExploration.MoveToward(
                        _fieldMoveTarget.x,
                        _fieldMoveTarget.y,
                        Mathf.Min(step, targetDistance));
                }
            }

            if (result == FieldExplorationResult.Blocked)
            {
                _fieldHasMoveTarget = false;
                _fieldNotice = "瓦礫や地形に阻まれています。別の経路を選んでください。";
                return;
            }
            if (result == FieldExplorationResult.Moved)
            {
                _fieldSaveCooldown -= Time.unscaledDeltaTime;
                if (_fieldSaveCooldown <= 0f)
                {
                    PersistFieldPosition();
                    _fieldSaveCooldown = 0.35f;
                }
                return;
            }
            if (result == FieldExplorationResult.Interacted)
            {
                _fieldHasMoveTarget = false;
                FieldEntity entity = _fieldExploration.LastInteractionEntity;
                if (entity != null)
                {
                    if (!confirmedInteraction)
                    {
                        _fieldNotice = $"{entity.DisplayName}の近くです。E／Enterで調べます。";
                        return;
                    }
                    if (entity.Kind == FieldEntityKind.Npc)
                    {
                        _pendingNpcEntityId = entity.Id;
                        _pendingNpcName = entity.DisplayName;
                        _npcEventOpen = true;
                        _fieldNotice = "旅の軍師が三つの支援策を提示しています。";
                        _audio.PlaySfx("select");
                        return;
                    }
                    _save = CampaignSavePolicy.StoreFieldEntityResolution(
                        _save,
                        entity.Id,
                        entity.Kind == FieldEntityKind.Treasure,
                        _catalog.stages);
                    PersistFieldPosition();
                    if (entity.Kind == FieldEntityKind.Treasure)
                    {
                        ExpeditionBattleBonus treasureBonus =
                            ExpeditionBattleBonusPolicy.Create(
                                _save.fieldTreasureCount,
                                CampaignSavePolicy.FindFieldSupport(
                                    _save,
                                    _stageIndex));
                        _fieldNotice =
                            $"遠征物資を獲得。累計{_save.fieldTreasureCount}個：味方HP+{treasureBonus.SupplyHpBonus}／攻撃+{treasureBonus.SupplyDamageBonus}";
                    }
                    else
                    {
                        _fieldNotice = entity.Message;
                    }
                    _audio.PlaySfx("select");
                }
                return;
            }
            if (result != FieldExplorationResult.Encounter) return;

            _fieldEncounterStarting = true;
            _fieldHasMoveTarget = false;
            PersistFieldPosition();
            _fieldNotice = "敵シンボルと接触。戦闘へ突入します。";
            _audio.PlaySfx("select");
            _activeEncounterEntityId = _fieldExploration.ActiveEnemy?.Id ?? string.Empty;
            int encounterStage = _fieldExploration.ActiveEnemy == null
                ? _fieldMap.EncounterNode.StageIndex
                : _fieldExploration.ActiveEnemy.StageIndex;
            StartEncounterBattle(encounterStage);
        }

        private void PersistFieldPosition()
        {
            if (_fieldExploration == null || _fieldMap == null) return;
            int nearestNode = NearestFieldNodeIndex(
                _fieldExploration.PlayerX,
                _fieldExploration.PlayerY);
            _save = CampaignSavePolicy.StoreFieldPosition(
                _save,
                _fieldExploration.PlayerX,
                _fieldExploration.PlayerY,
                nearestNode,
                _catalog.stages);
            PersistSave();
        }

        private int NearestFieldNodeIndex(float x, float y)
        {
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            foreach (FieldNode node in _fieldMap.Nodes)
            {
                float offsetX = node.X - x;
                float offsetY = node.Y - y;
                float distance = offsetX * offsetX + offsetY * offsetY;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = node.Index;
            }
            return nearest;
        }

        private void EnterWar()
        {
            if (_catalog.warmaps == null || _catalog.warmaps.Length == 0) return;
            ClearBattlePresentation();
            int warIndex = Mathf.Clamp(_stageIndex / 2, 0, _catalog.warmaps.Length - 1);
            _war = new WarCampaignCore(_catalog.warmaps[warIndex]);
            _warReport = null;
            _screen = ScreenMode.War;
            EnsureCamera();
            _audio.PlayBgm("BD-02", 0.8f);
        }

        private void ResolveWarRound()
        {
            if (_war == null || _war.Winner != WarWinner.None) return;
            _audio.PlaySfx("attack");
            _warReport = _war.AdvanceRound();
            if (_war.Winner == WarWinner.Player)
            {
                _save = CampaignSavePolicy.CompleteWarmap(
                    _save,
                    _war.Id,
                    _catalog.stages);
                PersistSave();
                _audio.PlaySfx("victory");
            }
            else if (_war.Winner == WarWinner.Enemy)
            {
                _audio.PlaySfx("defeat");
            }
        }

        private void RebuildStage(int stageIndex)
        {
            ClearBattlePresentation();
            _stageIndex = Mathf.Clamp(stageIndex, 0, _catalog.stages.Length - 1);
            _save = CampaignSavePolicy.SelectStage(_save, _stageIndex, _catalog.stages);
            _stage = RecruitmentRosterPolicy.CreateStage(
                _catalog.stages[_stageIndex],
                _catalog.stages,
                _save.recruitedUnitIds);
            StagePreparationData saved = CampaignSavePolicy.FindPreparation(_save, _stage.id);
            _preparation = BattlePreparationState.Create(_stage, saved);
            _screen = ScreenMode.Preparation;
            _preparationScroll = Vector2.zero;
            _paused = false;
            _showResult = false;
            _battleCompletedStage = false;
            _skillBanner = string.Empty;
            _message = string.Empty;
            EnsureCamera();
            PersistSave();
            _audio.PlayBgm("BD-01", 0.8f);
        }

        private void StartEncounterBattle(int stageIndex)
        {
            RebuildStage(stageIndex);
            StartPreparedBattle();
        }

        private void StartPreparedBattle()
        {
            if (_preparation == null) return;
            StageData authoredStage = _catalog.stages[_stageIndex];
            _save = CampaignSavePolicy.StorePreparation(
                _save,
                authoredStage,
                _preparation.ToSaveData(),
                _catalog.stages);
            FieldSupportType support = CampaignSavePolicy.FindFieldSupport(
                _save,
                _stageIndex);
            StageData preparedStage = _preparation.CreateBattleStage(
                _save.fieldTreasureCount,
                support);
            PersistSave();

            ClearBattlePresentation();
            _stage = preparedStage;
            _preparation = null;
            // 本編の戦闘には銘器の効果を持ち込む。
            // 試練ステージでは RelicEffectPolicy 側で自動的に無効化される。
            _battle = new FormationBattleCore(_stage, _save.resolvedStoryEntityIds);
            _screen = ScreenMode.Battle;
            _paused = false;
            _showResult = false;
            _skillBanner = string.Empty;
            _message = "両部隊、接敵開始";
            _battleActionIndex = 0;
            _impactFlashAlpha = 0f;
            EnsureCamera();
            BuildBattlefield();
            _audio.PlayBgm("BD-02", 1.2f);
            StartCoroutine(BattleRoutine());
        }

        private void SavePreparation()
        {
            if (_preparation == null) return;
            StageData authoredStage = _catalog.stages[_stageIndex];
            _save = CampaignSavePolicy.StorePreparation(
                _save,
                authoredStage,
                _preparation.ToSaveData(),
                _catalog.stages);
            PersistSave();
        }

        private void BuildBattlefield()
        {
            _battleRoot = new GameObject("Cinematic Formation Battle");
            _battleRoot.transform.SetParent(transform);
            BuildBackground();

            foreach (FormationCombatant unit in _battle.Units)
            {
                string idleAssetId = AssetId(unit.SourceUnitId);
                string actionAssetId = PoseAssetId(unit.SourceUnitId, UnitPose.Action);
                string victoryAssetId = PoseAssetId(unit.SourceUnitId, UnitPose.Victory);
                string defeatAssetId = PoseAssetId(unit.SourceUnitId, UnitPose.Defeat);
                Texture2D texture = Resources.Load<Texture2D>($"Art/Battle/Units/{idleAssetId}");
                bool usingFallback = texture == null;
                if (usingFallback) texture = FallbackUnitTexture(unit.Team);
                BattleSpriteMetrics idleMetrics = usingFallback
                    ? new BattleSpriteMetrics(0.5f, 0f, 1f)
                    : FormationPresentationProfile.GetSpriteMetrics(idleAssetId);
                BattleSpriteMetrics actionMetrics = FormationPresentationProfile.GetSpriteMetrics(actionAssetId);
                string hitAssetId = PoseAssetId(unit.SourceUnitId, UnitPose.Hit);
                BattleSpriteMetrics hitMetrics = FormationPresentationProfile.GetSpriteMetrics(hitAssetId);
                BattleSpriteMetrics victoryMetrics = FormationPresentationProfile.GetSpriteMetrics(victoryAssetId);
                BattleSpriteMetrics defeatMetrics = FormationPresentationProfile.GetSpriteMetrics(defeatAssetId);
                Sprite sprite = CreateUnitSprite(texture, idleMetrics);
                Sprite actionSprite = CreateUnitSprite(LoadPoseTexture(unit.SourceUnitId, UnitPose.Action), actionMetrics);
                Sprite hitSprite = CreateUnitSprite(LoadPoseTexture(unit.SourceUnitId, UnitPose.Hit), hitMetrics);
                Sprite victorySprite = CreateUnitSprite(LoadPoseTexture(unit.SourceUnitId, UnitPose.Victory), victoryMetrics);
                Sprite defeatSprite = CreateUnitSprite(LoadPoseTexture(unit.SourceUnitId, UnitPose.Defeat), defeatMetrics);
                Texture2D pixelAtlas = LoadPixelAtlas(unit.SourceUnitId);
                bool usingPixel = pixelAtlas != null;
                Sprite[] pixelRunSprites = null;
                Dictionary<UnitPose, Sprite[]> pixelPoseSprites = null;
                if (usingPixel)
                {
                    if (PixelAnimationProfile.UsesQuadrupedAtlas(unit.SourceUnitId))
                    {
                        Texture2D quadruped = LoadPixelQuadrupedAtlas();
                        usingPixel = quadruped != null;
                        if (usingPixel)
                        {
                            pixelRunSprites = Enumerable.Range(12, 5)
                                .Select(index => CreateGridPixelSprite(
                                    quadruped,
                                    index,
                                    PixelAnimationProfile.QuadrupedColumns,
                                    PixelAnimationProfile.QuadrupedRows))
                                .ToArray();
                            Sprite[] bite = Enumerable.Range(18, 4)
                                .Select(index => CreateGridPixelSprite(
                                    quadruped,
                                    index,
                                    PixelAnimationProfile.QuadrupedColumns,
                                    PixelAnimationProfile.QuadrupedRows))
                                .ToArray();
                            sprite = bite[0];
                            actionSprite = bite[bite.Length - 1];
                            hitSprite = CreateGridPixelSprite(
                                quadruped, 22,
                                PixelAnimationProfile.QuadrupedColumns,
                                PixelAnimationProfile.QuadrupedRows);
                            victorySprite = CreateGridPixelSprite(
                                quadruped, 23,
                                PixelAnimationProfile.QuadrupedColumns,
                                PixelAnimationProfile.QuadrupedRows);
                            defeatSprite = CreatePixelStandaloneSprite(
                                LoadPixelDefeatTexture(unit.SourceUnitId)) ?? hitSprite;
                            pixelPoseSprites = new Dictionary<UnitPose, Sprite[]>
                            {
                                [UnitPose.Idle] = new[] { sprite },
                                [UnitPose.Action] = bite,
                                [UnitPose.Hit] = new[] { sprite, hitSprite },
                                [UnitPose.Victory] = new[] { sprite, victorySprite },
                                [UnitPose.Defeat] = new[] { sprite, defeatSprite }
                            };
                        }
                    }
                    else
                    {
                        pixelRunSprites = Enumerable.Range(8, 4)
                            .Select(index => CreatePixelSprite(pixelAtlas, index))
                            .ToArray();
                        sprite = CreatePixelSprite(
                            pixelAtlas,
                            PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Idle));
                        actionSprite = CreatePixelSprite(
                            pixelAtlas,
                            PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Attack));
                        hitSprite = CreatePixelSprite(
                            pixelAtlas,
                            PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Hit));
                        victorySprite = CreatePixelSprite(
                            pixelAtlas,
                            PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Victory));
                        defeatSprite = CreatePixelStandaloneSprite(
                            LoadPixelDefeatTexture(unit.SourceUnitId)) ??
                            CreatePixelSprite(
                                pixelAtlas,
                                PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Defeat));
                        pixelPoseSprites = BuildCrispPixelPoseSequences(
                            sprite,
                            pixelRunSprites,
                            actionSprite,
                            hitSprite,
                            victorySprite,
                            defeatSprite);
                    }

                    if (!PixelAnimationProfile.UsesQuadrupedAtlas(unit.SourceUnitId))
                    {
                        Texture2D motionA = LoadPixelMotionAtlas(unit.SourceUnitId, "battle60a");
                        Texture2D motionB = LoadPixelMotionAtlas(unit.SourceUnitId, "battle60b");
                        Texture2D fieldMotion = LoadPixelMotionAtlas(unit.SourceUnitId, "field60");
                        if (motionA != null && motionB != null && fieldMotion != null)
                        {
                            Sprite[] idle60 = CreatePixelMotionSequence(motionA, 0, 60, 180);
                            Sprite[] attack60 = CreatePixelMotionSequence(motionA, 60, 60, 180);
                            Sprite[] hit60 = CreatePixelMotionSequence(motionA, 120, 60, 180);
                            Sprite[] victory60 = CreatePixelMotionSequence(motionB, 0, 60, 120);
                            Sprite[] defeat60 = CreatePixelMotionSequence(motionB, 60, 60, 120);
                            pixelRunSprites = CreatePixelMotionSequence(fieldMotion, 40, 20, 240);
                            sprite = idle60[0];
                            actionSprite = attack60[attack60.Length - 1];
                            hitSprite = hit60[hit60.Length - 1];
                            victorySprite = victory60[victory60.Length - 1];
                            defeatSprite = defeat60[defeat60.Length - 1];
                            pixelPoseSprites = new Dictionary<UnitPose, Sprite[]>
                            {
                                [UnitPose.Idle] = idle60,
                                [UnitPose.Action] = attack60,
                                [UnitPose.Hit] = hit60,
                                [UnitPose.Guard] = hit60,
                                [UnitPose.Victory] = victory60,
                                [UnitPose.Defeat] = defeat60
                            };
                        }
                    }
                }
                var unitObject = new GameObject($"Unit {unit.Id}");
                unitObject.transform.SetParent(_battleRoot.transform);
                SpriteRenderer renderer = unitObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.flipX = usingPixel
                    ? FormationPresentationProfile.GetFlipX(unit.Team, idleAssetId)
                    : usingFallback
                    ? unit.Team == BattleTeam.Player
                    : FormationPresentationProfile.GetFlipX(unit.Team, idleAssetId);
                var blendObject = new GameObject("Pose Blend");
                blendObject.transform.SetParent(unitObject.transform, false);
                SpriteRenderer blendRenderer = blendObject.AddComponent<SpriteRenderer>();
                blendRenderer.color = new Color(1f, 1f, 1f, 0f);

                FormationAnchor anchor = FormationPresentationProfile.GetAnchor(unit.Team, unit.FormationSlot);
                // ボスと試練の主は体格そのものを大きくする。
                // 同じ大きさで並ぶと、いくら強くても「格の違う相手」に見えないため。
                float targetHeight = anchor.Height *
                    BossPresencePolicy.GetPresenceScale(unit.Id, unit.SourceUnitId);
                float groundLift = string.Equals(unit.ClassName, "flier", StringComparison.OrdinalIgnoreCase)
                    ? 0.36f * (targetHeight / 3.48f)
                    : 0f;
                Vector3 home = new Vector3(anchor.X, anchor.Y + groundLift, 0f);
                Vector3 baseScale = usingPixel
                    ? ScaleForPixelSprite(sprite, targetHeight)
                    : usingFallback
                    ? ScaleForVisibleHeight(sprite, idleMetrics, targetHeight)
                    : ScaleForPoseAsset(sprite, idleAssetId, targetHeight);
                Vector3 actionScale = usingPixel
                    ? baseScale
                    : actionSprite == null
                    ? baseScale
                    : ScaleForPoseAsset(actionSprite, actionAssetId, targetHeight);
                Vector3 hitScale = usingPixel
                    ? baseScale
                    : hitSprite == null
                    ? baseScale
                    : ScaleForPoseAsset(hitSprite, hitAssetId, targetHeight);
                Vector3 victoryScale = usingPixel
                    ? baseScale
                    : victorySprite == null
                    ? baseScale
                    : ScaleForPoseAsset(victorySprite, victoryAssetId, targetHeight);
                Vector3 defeatScale = usingPixel
                    ? baseScale
                    : defeatSprite == null
                    ? baseScale
                    : ScaleForPoseAsset(defeatSprite, defeatAssetId, targetHeight);
                unitObject.transform.position = home;
                unitObject.transform.localScale = baseScale;
                bool usesContinuousPixelFrames = pixelPoseSprites != null &&
                    pixelPoseSprites.TryGetValue(UnitPose.Idle, out Sprite[] continuousIdle) &&
                    continuousIdle.Length >= 60;
                IRuntimeBoneRig2D boneRig = usingPixel && !usesContinuousPixelFrames
                    ? PixelSkinRig2DView.TryCreate(
                        unitObject.transform,
                        unit.SourceUnitId,
                        targetHeight,
                        baseScale.y,
                        renderer.flipX)
                    : BoneRig2DProfile.ShouldUseInBattle(unit.SourceUnitId)
                        ? BoneRig2DView.TryCreate(
                            unitObject.transform,
                            unit.SourceUnitId,
                            targetHeight,
                            baseScale.y,
                            renderer.flipX)
                        : null;

                // リグが立ち上がったら全身スプライトは隠す。
                // 以前はどちらも描いていたため、有効にするとキャラの上にパーツが
                // 重なって二重に見える状態だった。
                if (boneRig != null)
                {
                    renderer.enabled = false;
                    blendRenderer.enabled = false;
                }

                var shadowObject = new GameObject($"Ground Shadow {unit.Id}");
                shadowObject.transform.SetParent(_battleRoot.transform);
                shadowObject.transform.position = new Vector3(home.x, anchor.Y, 0f);
                shadowObject.transform.localScale = string.Equals(unit.ClassName, "flier", StringComparison.OrdinalIgnoreCase)
                    ? new Vector3(anchor.ShadowWidth * 0.76f, 0.15f, 1f)
                    : new Vector3(anchor.ShadowWidth, 0.20f, 1f);
                SpriteRenderer shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
                shadowRenderer.sprite = _shadowSprite;
                Color shadowColor = new Color(0.015f, 0.02f, 0.035f,
                    string.Equals(unit.ClassName, "flier", StringComparison.OrdinalIgnoreCase) ? 0.27f : 0.52f);
                shadowRenderer.color = shadowColor;

                var view = new UnitView
                {
                    Unit = unit,
                    Object = unitObject,
                    Renderer = renderer,
                    BlendRenderer = blendRenderer,
                    BoneRig = boneRig,
                    ShadowObject = shadowObject,
                    ShadowRenderer = shadowRenderer,
                    IdleSprite = sprite,
                    ActionSprite = actionSprite,
                    HitSprite = hitSprite,
                    VictorySprite = victorySprite,
                    DefeatSprite = defeatSprite,
                    PixelRunSprites = pixelRunSprites,
                    PixelPoseSprites = pixelPoseSprites,
                    IsPixel = usingPixel,
                    Home = home,
                    BaseScale = baseScale,
                    ActionScale = actionScale,
                    HitScale = hitScale,
                    VictoryScale = victoryScale,
                    DefeatScale = defeatScale,
                    BaseColor = Color.white,
                    ShadowColor = shadowColor,
                    GroundLift = groundLift,
                    IdlePhase = unit.FormationSlot * 1.37f + (unit.Team == BattleTeam.Enemy ? 0.7f : 0f),
                    CurrentPose = UnitPose.Idle
                };
                SetBodyColor(view, Color.white);
                ApplySorting(view, anchor.Y);
                _unitViews.Add(unit.Id, view);
            }
        }

        private void BuildBackground()
        {
            string backgroundId = string.IsNullOrWhiteSpace(_stage.backgroundId) ? "forest" : _stage.backgroundId;
            string resourceId = backgroundId == "forest" ? "forest_ruins" : backgroundId;
            Texture2D texture = Resources.Load<Texture2D>($"Art/Battle/Backgrounds/{resourceId}") ??
                                Resources.Load<Texture2D>("Art/Battle/Backgrounds/forest_ruins");
            if (texture == null) return;

            Sprite sprite = CreateBattleSprite(
                texture,
                new Vector2(0.5f, 0.5f),
                100f,
                SpriteMeshType.FullRect);
            var backgroundObject = new GameObject("Battlefield Background");
            backgroundObject.transform.SetParent(_battleRoot.transform);
            backgroundObject.transform.position = new Vector3(0f, 0f, 4f);
            SpriteRenderer renderer = backgroundObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;
            float visibleHeight = _camera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * _camera.aspect;
            float scale = Mathf.Max(visibleWidth / sprite.bounds.size.x, visibleHeight / sprite.bounds.size.y);
            backgroundObject.transform.localScale = Vector3.one * scale;

            var shadeObject = new GameObject("Battlefield Color Grade");
            shadeObject.transform.SetParent(_battleRoot.transform);
            shadeObject.transform.position = new Vector3(0f, 0f, 3.8f);
            SpriteRenderer shade = shadeObject.AddComponent<SpriteRenderer>();
            shade.sprite = _shadeSprite;
            shade.color = new Color(0.04f, 0.10f, 0.16f, 0.16f);
            shade.sortingOrder = -90;
            shadeObject.transform.localScale = new Vector3(24f, 14f, 1f);
        }

        private IEnumerator BattleRoutine()
        {
            yield return AnimateEntrance();
            yield return WaitBattle(0.45f);

            while (_battle != null && _battle.Winner == BattleWinner.None)
            {
                yield return WaitUntilRunning();
                FormationCombatant actor = _battle.GetCurrentActor();
                FormationBattleCommand command = null;
                if (actor != null && actor.Team == BattleTeam.Player)
                {
                    _pendingCommandActor = actor;
                    _pendingBattleCommand = null;
                    _battleCommandOpen = true;
                    _message = $"{DisplayName(actor)} の行動を選択";
                    while (_battle != null &&
                           _battle.Winner == BattleWinner.None &&
                           _pendingBattleCommand == null)
                        yield return null;
                    command = _pendingBattleCommand;
                    _battleCommandOpen = false;
                    _pendingCommandActor = null;
                }
                FormationAction action = _battle.Advance(command);
                if (action == null) break;
                _battleActionIndex++;
                _message = action.IsDefending
                    ? $"ROUND {_battle.RoundNumber}  {DisplayName(action.Actor)} — 防御"
                    : action.IsEscape
                        ? $"ROUND {_battle.RoundNumber}  撤退"
                        : $"ROUND {_battle.RoundNumber}  {DisplayName(action.Actor)} → {DisplayName(action.Target)}";
                _skillBanner = SkillName(action);
                if (action.IsDefending)
                {
                    yield return TransitionPose(_unitViews[action.Actor.Id], UnitPose.Guard, 0.14f);
                    yield return WaitBattle(0.20f);
                    yield return TransitionPose(_unitViews[action.Actor.Id], UnitPose.Idle, 0.18f);
                }
                else if (!action.IsEscape)
                {
                    yield return AnimateAction(action);
                }
                _skillBanner = string.Empty;
                yield return WaitBattle(0.42f);
            }

            if (_battle == null) yield break;
            _message = _battle.Winner == BattleWinner.Player
                ? "VICTORY — 敵部隊を撃破"
                : _battle.Winner == BattleWinner.Escaped
                    ? "RETREAT — 戦場から離脱"
                    : "DEFEAT — 部隊再編が必要です";
            if (_battle.Winner == BattleWinner.Player)
            {
                bool scoutVictory = CampaignSavePolicy.IsScoutEnemy(
                    _activeEncounterEntityId);
                _save = CampaignSavePolicy.ResolveFieldEnemyVictory(
                    _save,
                    _activeEncounterEntityId,
                    _stageIndex,
                    _catalog.stages);
                _battleCompletedStage = !scoutVictory;
                _message = scoutVictory
                    ? "VICTORY — 敵斥候を撃破。敵主力はなお健在"
                    : "VICTORY — 敵主力を撃破";
                PersistSave();
                _audio.PlaySfx("victory");
            }
            if (_battle.Winner != BattleWinner.Escaped)
                yield return AnimateOutcome(_battle.Winner);
            yield return WaitBattle(0.8f);
            _showResult = true;
        }

        private IEnumerator AnimateEntrance()
        {
            const float duration = 1.08f;
            foreach (UnitView view in _unitViews.Values)
            {
                float direction = view.Unit.Team == BattleTeam.Player ? 1f : -1f;
                view.Object.transform.position = view.Home + Vector3.right * direction * 4.5f;
                view.Object.transform.localScale = view.BaseScale * 0.94f;
                view.Object.transform.rotation = Quaternion.Euler(0f, 0f, direction * 4f);
                SetBodyColor(view, new Color(1f, 1f, 1f, 0f));
                if (view.BoneRig != null)
                    view.BoneRig.Apply(view.BoneRig.Sample(BoneRigPose2D.Entrance, 0f, view.IdlePhase));
                view.ShadowRenderer.color = new Color(
                    view.ShadowColor.r, view.ShadowColor.g, view.ShadowColor.b, 0f);
                view.Animating = true;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                foreach (UnitView view in _unitViews.Values)
                {
                    float delay = view.Unit.FormationSlot * 0.055f;
                    float local = Mathf.Clamp01((elapsed - delay) / (duration - 0.22f));
                    float t = EaseOut(local);
                    float direction = view.Unit.Team == BattleTeam.Player ? 1f : -1f;
                    Vector3 start = view.Home + Vector3.right * direction * 4.5f + Vector3.up * 0.22f;
                    float landing = Mathf.Sin(local * Mathf.PI) * 0.16f;
                    view.Object.transform.position = Vector3.Lerp(start, view.Home, t) + Vector3.up * landing;
                    view.Object.transform.localScale = Vector3.Lerp(view.BaseScale * 0.94f, view.BaseScale, t);
                    view.Object.transform.rotation = Quaternion.Euler(0f, 0f, direction * Mathf.Lerp(4f, 0f, t));
                    SetBodyColor(view, new Color(1f, 1f, 1f, t));
                    if (view.BoneRig != null)
                        view.BoneRig.Apply(view.BoneRig.Sample(
                            BoneRigPose2D.Entrance,
                            local,
                            view.IdlePhase));
                    else if (HasContinuousPixelMotion(view, UnitPose.Idle) &&
                             UsesStablePixelEntrance(view.PixelPoseSprites[UnitPose.Idle].Length))
                    {
                        SetContinuousPixelMotionFrame(view, UnitPose.Idle, 0f);
                    }
                    else if (view.PixelRunSprites != null && view.PixelRunSprites.Length > 0)
                    {
                        int entranceFrame = Math.Min(
                            view.PixelRunSprites.Length - 1,
                            Math.Max(0, Mathf.FloorToInt(local * view.PixelRunSprites.Length)));
                        view.Renderer.sprite = view.PixelRunSprites[entranceFrame];
                    }
                    view.ShadowObject.transform.position = new Vector3(
                        Mathf.Lerp(start.x, view.Home.x, t), GroundY(view, view.Home), 0f);
                    ApplySorting(view, GroundY(view, view.Object.transform.position));
                    view.ShadowRenderer.color = new Color(
                        view.ShadowColor.r, view.ShadowColor.g, view.ShadowColor.b, view.ShadowColor.a * t);
                }
                yield return null;
            }

            foreach (UnitView view in _unitViews.Values)
            {
                view.Object.transform.position = view.Home;
                view.Object.transform.localScale = view.BaseScale;
                view.Object.transform.rotation = Quaternion.identity;
                SetBodyColor(view, Color.white);
                if (view.BoneRig != null)
                    view.BoneRig.Apply(view.BoneRig.Sample(
                        BoneRigPose2D.Idle,
                        0f,
                        view.IdlePhase));
                else if (HasContinuousPixelMotion(view, UnitPose.Idle) &&
                         UsesStablePixelEntrance(view.PixelPoseSprites[UnitPose.Idle].Length))
                    SetContinuousPixelMotionFrame(view, UnitPose.Idle, 0f);
                view.ShadowObject.transform.position = new Vector3(view.Home.x, GroundY(view, view.Home), 0f);
                ApplySorting(view, GroundY(view, view.Home));
                view.ShadowRenderer.color = view.ShadowColor;
                view.Animating = false;
            }
        }

        private IEnumerator AnimateAction(FormationAction action)
        {
            UnitView actor = _unitViews[action.Actor.Id];
            UnitView target = _unitViews[action.Target.Id];
            UnitView cooperator = action.IsCooperation && action.Cooperator != null
                ? _unitViews[action.Cooperator.Id]
                : null;
            actor.Animating = true;
            target.Animating = true;
            if (cooperator != null) cooperator.Animating = true;
            FocusBattleCamera(actor, target, action.IsSpecial || action.IsCooperation);
            if (action.IsSpecial || action.IsCooperation)
                SpawnSpecialAura(actor, action.IsCooperation);

            if (!_bondTechniqueShown && cooperator != null)
            {
                _bondTechniqueShown = true;
                _cutInsShown.Add(actor.Unit.Id);
                _cutInsShown.Add(cooperator.Unit.Id);
                yield return AnimateBondTechniqueCutIn(actor, cooperator);
            }
            else if (ShouldShowSoloCutIn(actor.ActionSprite != null, action.IsSpecial))
                yield return AnimateCutIn(actor, action);

            if (HasContinuousPixelMotion(actor, UnitPose.Action))
            {
                SetContinuousPixelMotionFrame(actor, UnitPose.Action, 0f);
                actor.CurrentPose = UnitPose.Action;
            }
            else
            {
                yield return TransitionPose(actor, UnitPose.Action, 0.13f);
            }
            if (cooperator != null)
                yield return AnimateCooperatorAssist(cooperator, target, action);

            if (action.Kind == FormationActionKind.Melee)
                yield return AnimateMelee(actor, target, action);
            else
                yield return AnimateProjectile(actor, target, action);

            yield return TransitionPose(actor, UnitPose.Idle, 0.15f);
            actor.Animating = false;
            if (cooperator != null) cooperator.Animating = false;
            if (action.DefeatedTarget)
                yield return AnimateDefeat(target);
            else
                target.Animating = false;
            ResetBattleCamera();
        }

        private IEnumerator AnimateMelee(UnitView actor, UnitView target, FormationAction action)
        {
            BattleMotionProfile motion = FormationPresentationProfile.GetMotionProfile(actor.Unit.ClassName);
            float facing = actor.Unit.Team == BattleTeam.Player ? -1f : 1f;
            Vector3 actionScale = ScaleForPose(actor, UnitPose.Action);
            Vector3 windupScale = new Vector3(
                actionScale.x * (1f + motion.Squash),
                actionScale.y * (1f - motion.Squash),
                actionScale.z);
            Vector3 strikeScale = new Vector3(
                actionScale.x * (1f - motion.Stretch * 0.42f),
                actionScale.y * (1f + motion.Stretch),
                actionScale.z);
            Vector3 windup = actor.Home + Vector3.left * (facing * motion.WindupDistance);
            SpawnUnitAfterImage(actor, 0.22f);
            yield return AnimateUnitPhase(
                actor,
                actor.Home,
                windup,
                actionScale,
                windupScale,
                motion.ApproachDuration * 0.86f,
                motion.TravelArc * 0.22f,
                facing * 2.2f,
                false,
                rigFromPose: BoneRigPose2D.Windup,
                rigToPose: BoneRigPose2D.Windup,
                locomotionCycles: 0.65f,
                upperBodyWeight: 0.92f);

            Vector3 strike = target.Home + Vector3.left * (facing * motion.StopDistance);
            SpawnUnitAfterImage(actor, 0.34f);
            yield return AnimateUnitPhase(
                actor,
                windup,
                strike,
                windupScale,
                strikeScale,
                motion.ApproachDuration,
                motion.TravelArc,
                -facing * 5.5f,
                true,
                rigFromPose: BoneRigPose2D.Windup,
                rigToPose: BoneRigPose2D.Strike,
                locomotionCycles: 1.45f,
                upperBodyWeight: 0.94f,
                pixelMotionPose: UnitPose.Action);
            SpawnSlash(target.Home + Vector3.up * 1.55f, facing, action.WasCritical);
            Coroutine hitReaction = ApplyHit(target, action);
            ClearEmbeddedAttackFrameAfterImpact(actor);
            _audio.PlaySfx("attack");
            yield return WaitBattle(0.055f);

            Vector3 recoil = strike + Vector3.left * (facing * motion.ImpactRecoil);
            yield return AnimateUnitPhase(
                actor,
                strike,
                recoil,
                strikeScale,
                actionScale,
                0.075f,
                0.025f,
                facing * 2.8f,
                true,
                rigFromPose: BoneRigPose2D.Strike,
                rigToPose: BoneRigPose2D.Return);

            Vector3 followThrough = recoil + Vector3.right * (facing * motion.FollowThrough);
            yield return AnimateUnitPhase(
                actor,
                recoil,
                followThrough,
                actionScale,
                strikeScale,
                0.095f,
                motion.TravelArc * 0.25f,
                -facing * 2.4f,
                false,
                rigFromPose: BoneRigPose2D.Return,
                rigToPose: BoneRigPose2D.Return);

            yield return AnimateUnitPhase(
                actor,
                followThrough,
                actor.Home,
                strikeScale,
                actionScale,
                motion.ReturnDuration,
                motion.TravelArc * 0.45f,
                facing * 2f,
                false,
                rigFromPose: BoneRigPose2D.Return,
                rigToPose: BoneRigPose2D.Idle,
                locomotionCycles: 1.20f,
                upperBodyWeight: 0.66f,
                pixelMotionPose: UnitPose.Idle);
            actor.Object.transform.position = actor.Home;
            actor.Object.transform.localScale = actionScale;
            yield return hitReaction;
        }

        private IEnumerator AnimateCooperatorAssist(
            UnitView cooperator,
            UnitView target,
            FormationAction action)
        {
            BattleMotionProfile motion =
                FormationPresentationProfile.GetMotionProfile(cooperator.Unit.ClassName);
            float facing = cooperator.Unit.Team == BattleTeam.Player ? -1f : 1f;
            Vector3 actionScale = ScaleForPose(cooperator, UnitPose.Action);
            yield return TransitionPose(cooperator, UnitPose.Action, 0.10f);
            Vector3 assist = target.Home + Vector3.left * (facing * (motion.StopDistance + 0.45f));
            SpawnUnitAfterImage(cooperator, 0.30f);
            yield return AnimateUnitPhase(
                cooperator,
                cooperator.Home,
                assist,
                actionScale,
                actionScale,
                motion.ApproachDuration * 0.72f,
                motion.TravelArc * 0.72f,
                -facing * 4f,
                true,
                rigFromPose: BoneRigPose2D.Windup,
                rigToPose: BoneRigPose2D.Strike,
                locomotionCycles: 1.25f,
                upperBodyWeight: 0.90f,
                pixelMotionPose: UnitPose.Action);
            SpawnSlash(
                target.Home + Vector3.up * 1.42f,
                facing,
                action.WasCritical || action.IsSpecial);
            ClearEmbeddedAttackFrameAfterImpact(cooperator);
            yield return AnimateUnitPhase(
                cooperator,
                assist,
                cooperator.Home,
                actionScale,
                actionScale,
                motion.ReturnDuration * 0.72f,
                motion.TravelArc * 0.34f,
                facing * 2f,
                false,
                rigFromPose: BoneRigPose2D.Return,
                rigToPose: BoneRigPose2D.Idle,
                locomotionCycles: 1.05f,
                upperBodyWeight: 0.62f);
            yield return TransitionPose(cooperator, UnitPose.Idle, 0.10f);
        }

        private IEnumerator AnimateCutIn(UnitView actor, FormationAction action)
        {
            Color teamColor = actor.Unit.Team == BattleTeam.Player
                ? new Color(0.08f, 0.62f, 0.86f, 0.94f)
                : new Color(0.72f, 0.08f, 0.18f, 0.94f);

            GameObject band = CreateEffect("Cinematic Cut-In Band", new Color(0.015f, 0.025f, 0.05f, 0.96f), 300);
            band.transform.position = new Vector3(0f, 0.45f, 0f);
            band.transform.localScale = new Vector3(22f, 3.65f, 1f);

            GameObject accent = CreateEffect("Cinematic Cut-In Accent", teamColor, 301);
            accent.transform.position = new Vector3(0f, -1.23f, 0f);
            accent.transform.localScale = new Vector3(22f, 0.11f, 1f);

            var portraitObject = new GameObject("Cinematic Cut-In Portrait");
            portraitObject.transform.SetParent(_battleRoot.transform);
            SpriteRenderer portrait = portraitObject.AddComponent<SpriteRenderer>();
            Texture2D cutInTexture = LoadPoseTexture(actor.Unit.SourceUnitId, UnitPose.Action);
            Sprite cutInSprite = CreateUnitSprite(
                cutInTexture,
                FormationPresentationProfile.GetSpriteMetrics(
                    PoseAssetId(actor.Unit.SourceUnitId, UnitPose.Action)));
            if (cutInSprite == null) cutInSprite = actor.ActionSprite;
            portrait.sprite = cutInSprite;
            portrait.sortingOrder = 302;
            string actionAssetId = PoseAssetId(actor.Unit.SourceUnitId, UnitPose.Action);
            portrait.flipX = FormationPresentationProfile.GetFlipX(actor.Unit.Team, actionAssetId);
            portrait.color = new Color(1f, 1f, 1f, 0f);
            portraitObject.transform.localScale = ScaleForPoseAsset(cutInSprite, actionAssetId, 6.7f);

            float side = actor.Unit.Team == BattleTeam.Player ? -1f : 1f;
            Vector3 start = new Vector3(side * 10.5f, -2.15f, 0f);
            Vector3 focus = new Vector3(side * 3.35f, -2.15f, 0f);
            Vector3 end = new Vector3(-side * 10.5f, -2.15f, 0f);
            portraitObject.transform.position = start;

            _skillBanner = $"{DisplayName(action.Actor)}  //  {SkillName(action)}";
            _shakeStrength = 0.045f;
            _shakeUntil = Time.unscaledTime + 0.46f;

            float elapsed = 0f;
            const float enterDuration = 0.17f;
            while (elapsed < enterDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = EaseOut(Mathf.Clamp01(elapsed / enterDuration));
                portraitObject.transform.position = Vector3.Lerp(start, focus, t);
                portrait.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }

            yield return WaitBattle(0.24f);

            elapsed = 0f;
            const float exitDuration = 0.16f;
            while (elapsed < exitDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / exitDuration);
                portraitObject.transform.position = Vector3.Lerp(focus, end, t);
                portrait.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }

            DestroyEffect(band);
            DestroyEffect(accent);
            Destroy(portraitObject);
        }

        private UnitView FindBondPartner(UnitView actor)
        {
            if (!FormationPresentationProfile.SupportsBondTechnique(actor.Unit.SourceUnitId)) return null;
            return _unitViews.Values.FirstOrDefault(candidate =>
                candidate != actor &&
                candidate.Unit.IsAlive &&
                candidate.Unit.Team == actor.Unit.Team &&
                FormationPresentationProfile.IsBondTechniquePair(
                    actor.Unit.SourceUnitId,
                    candidate.Unit.SourceUnitId));
        }

        private IEnumerator AnimateBondTechniqueCutIn(UnitView first, UnitView second)
        {
            if (first.ActionSprite == null || second.ActionSprite == null) yield break;

            string previousBanner = _skillBanner;
            _skillBanner = "BOND DUAL STRIKE — 絆の双撃";

            GameObject band = CreateEffect(
                "Bond Technique Band",
                new Color(0.015f, 0.025f, 0.065f, 0.97f),
                310);
            band.transform.position = new Vector3(0f, 0.35f, 0f);
            band.transform.localScale = new Vector3(22f, 4.15f, 1f);

            GameObject upperAccent = CreateEffect(
                "Bond Technique Cyan Accent",
                new Color(0.12f, 0.80f, 1f, 0.92f),
                311);
            upperAccent.transform.position = new Vector3(0f, 2.36f, 0f);
            upperAccent.transform.localScale = new Vector3(22f, 0.09f, 1f);

            GameObject lowerAccent = CreateEffect(
                "Bond Technique Gold Accent",
                new Color(1f, 0.74f, 0.24f, 0.94f),
                311);
            lowerAccent.transform.position = new Vector3(0f, -1.66f, 0f);
            lowerAccent.transform.localScale = new Vector3(22f, 0.09f, 1f);

            var firstObject = new GameObject("Bond Portrait Hero");
            firstObject.transform.SetParent(_battleRoot.transform);
            SpriteRenderer firstRenderer = firstObject.AddComponent<SpriteRenderer>();
            firstRenderer.sprite = first.ActionSprite;
            firstRenderer.sortingOrder = 312;
            firstRenderer.flipX = false;
            firstRenderer.color = new Color(1f, 1f, 1f, 0f);
            string firstAssetId = PoseAssetId(first.Unit.SourceUnitId, UnitPose.Action);
            firstObject.transform.localScale = ScaleForPoseAsset(first.ActionSprite, firstAssetId, 5.85f);

            var secondObject = new GameObject("Bond Portrait Partner");
            secondObject.transform.SetParent(_battleRoot.transform);
            SpriteRenderer secondRenderer = secondObject.AddComponent<SpriteRenderer>();
            secondRenderer.sprite = second.ActionSprite;
            secondRenderer.sortingOrder = 313;
            secondRenderer.flipX = true;
            secondRenderer.color = new Color(1f, 1f, 1f, 0f);
            string secondAssetId = PoseAssetId(second.Unit.SourceUnitId, UnitPose.Action);
            secondObject.transform.localScale = ScaleForPoseAsset(second.ActionSprite, secondAssetId, 5.85f);

            Vector3 firstStart = new Vector3(-10.5f, -2.25f, 0f);
            Vector3 firstFocus = new Vector3(-2.55f, -2.25f, 0f);
            Vector3 secondStart = new Vector3(10.5f, -2.25f, 0f);
            Vector3 secondFocus = new Vector3(2.55f, -2.25f, 0f);
            firstObject.transform.position = firstStart;
            secondObject.transform.position = secondStart;

            _shakeStrength = 0.07f;
            _shakeUntil = Time.unscaledTime + 0.68f;

            float elapsed = 0f;
            const float enterDuration = 0.22f;
            while (elapsed < enterDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = EaseOut(Mathf.Clamp01(elapsed / enterDuration));
                firstObject.transform.position = Vector3.Lerp(firstStart, firstFocus, t);
                secondObject.transform.position = Vector3.Lerp(secondStart, secondFocus, t);
                firstRenderer.color = new Color(1f, 1f, 1f, t);
                secondRenderer.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }

            yield return WaitBattle(0.36f);

            elapsed = 0f;
            const float exitDuration = 0.18f;
            while (elapsed < exitDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / exitDuration));
                firstObject.transform.position = Vector3.Lerp(firstFocus, new Vector3(10.5f, -2.25f, 0f), t);
                secondObject.transform.position = Vector3.Lerp(secondFocus, new Vector3(-10.5f, -2.25f, 0f), t);
                firstRenderer.color = new Color(1f, 1f, 1f, 1f - t);
                secondRenderer.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }

            DestroyEffect(band);
            DestroyEffect(upperAccent);
            DestroyEffect(lowerAccent);
            Destroy(firstObject);
            Destroy(secondObject);
            _skillBanner = previousBanner;
        }

        private IEnumerator AnimateProjectile(UnitView actor, UnitView target, FormationAction action)
        {
            if (action.Kind == FormationActionKind.Magic)
            {
                yield return AnimateRuneMagic(actor, target, action);
                yield break;
            }

            BattleMotionProfile motion = FormationPresentationProfile.GetMotionProfile(actor.Unit.ClassName);
            Vector3 startScale = ScaleForPose(actor, UnitPose.Action);
            float facing = actor.Unit.Team == BattleTeam.Player ? -1f : 1f;
            Vector3 windupScale = new Vector3(
                startScale.x * (1f + motion.Squash),
                startScale.y * (1f - motion.Squash),
                startScale.z);
            Vector3 castScale = new Vector3(
                startScale.x * (1f - motion.Stretch * 0.35f),
                startScale.y * (1f + motion.Stretch),
                startScale.z);
            Vector3 windup = actor.Home + Vector3.left * (facing * motion.WindupDistance);
            Vector3 castPosition = actor.Home + Vector3.right * (facing * motion.FollowThrough);
            yield return AnimateUnitPhase(
                actor,
                actor.Home,
                windup,
                startScale,
                windupScale,
                motion.ApproachDuration,
                motion.TravelArc * 0.18f,
                facing * 1.8f,
                false,
                rigFromPose: BoneRigPose2D.Windup,
                rigToPose: BoneRigPose2D.Cast,
                locomotionCycles: 0.45f,
                upperBodyWeight: 0.88f);
            yield return AnimateUnitPhase(
                actor,
                windup,
                castPosition,
                windupScale,
                castScale,
                0.13f,
                motion.TravelArc * 0.30f,
                -facing * 3.1f,
                true,
                rigFromPose: BoneRigPose2D.Cast,
                rigToPose: BoneRigPose2D.Strike,
                pixelMotionPose: UnitPose.Action);

            Color color = action.Kind == FormationActionKind.Magic
                ? new Color(0.25f, 0.88f, 1f, 0.95f)
                : new Color(1f, 0.78f, 0.28f, 0.95f);
            GameObject projectile = CreateEffect("Projectile", color, 155);
            Vector3 origin = castPosition + Vector3.up * 1.20f + Vector3.right * (actor.Unit.Team == BattleTeam.Player ? -0.38f : 0.38f);
            Vector3 destination = target.Home + Vector3.up * 1.45f;
            projectile.transform.position = origin;
            projectile.transform.localScale = action.Kind == FormationActionKind.Magic
                ? new Vector3(0.32f, 0.32f, 1f)
                : new Vector3(0.72f, 0.08f, 1f);
            float angle = Mathf.Atan2(destination.y - origin.y, destination.x - origin.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            float elapsed = 0f;
            const float duration = 0.32f;
            while (elapsed < duration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / duration);
                projectile.transform.position = Vector3.Lerp(origin, destination, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.38f);
                if (Mathf.FloorToInt(t * 12f) % 2 == 0) SpawnTrail(projectile.transform.position, color);
                yield return null;
            }

            DestroyEffect(projectile);
            SpawnBurst(destination, color, action.WasCritical ? 12 : 7);
            Coroutine hitReaction = ApplyHit(target, action);
            ClearEmbeddedAttackFrameAfterImpact(actor);
            _audio.PlaySfx("attack");
            Vector3 recoil = castPosition + Vector3.left * (facing * motion.ImpactRecoil);
            yield return AnimateUnitPhase(
                actor,
                castPosition,
                recoil,
                castScale,
                windupScale,
                0.10f,
                motion.TravelArc * 0.14f,
                facing * 2.4f,
                true,
                rigFromPose: BoneRigPose2D.Strike,
                rigToPose: BoneRigPose2D.Return);
            yield return AnimateUnitPhase(
                actor,
                recoil,
                actor.Home,
                windupScale,
                startScale,
                motion.ReturnDuration,
                motion.TravelArc * 0.30f,
                -facing * 1.8f,
                false,
                rigFromPose: BoneRigPose2D.Return,
                rigToPose: BoneRigPose2D.Idle,
                locomotionCycles: 0.85f,
                upperBodyWeight: 0.60f,
                pixelMotionPose: UnitPose.Idle);
            yield return hitReaction;
        }

        private IEnumerator AnimateRuneMagic(UnitView actor, UnitView target, FormationAction action)
        {
            BattleMotionProfile motion = FormationPresentationProfile.GetMotionProfile(actor.Unit.ClassName);
            Vector3 startScale = ScaleForPose(actor, UnitPose.Action);
            float facing = actor.Unit.Team == BattleTeam.Player ? -1f : 1f;
            Vector3 windupScale = new Vector3(
                startScale.x * (1f + motion.Squash * 0.55f),
                startScale.y * (1f - motion.Squash * 0.55f),
                startScale.z);
            Vector3 castScale = new Vector3(
                startScale.x * (1f - motion.Stretch * 0.22f),
                startScale.y * (1f + motion.Stretch * 0.68f),
                startScale.z);
            Vector3 windup = actor.Home + Vector3.left * (facing * motion.WindupDistance * 0.72f);
            Vector3 castPosition = actor.Home + Vector3.right * (facing * motion.FollowThrough * 0.45f);
            Vector3 casterCenter = actor.Home + Vector3.up * 0.82f;
            Vector3 destination = target.Home + Vector3.up * 1.36f;
            Color primary = MagicPrimaryColor(actor.Unit.SourceUnitId, actor.Unit.Team);
            Color accent = MagicAccentColor(actor.Unit.SourceUnitId, actor.Unit.Team);

            yield return AnimateUnitPhase(
                actor,
                actor.Home,
                windup,
                startScale,
                windupScale,
                motion.ApproachDuration,
                motion.TravelArc * 0.16f,
                facing * 1.25f,
                false,
                rigFromPose: BoneRigPose2D.Windup,
                rigToPose: BoneRigPose2D.Cast,
                locomotionCycles: 0.40f,
                upperBodyWeight: 0.90f);

            const int casterMoteCount = 14;
            var casterMotes = new GameObject[casterMoteCount];
            for (int i = 0; i < casterMoteCount; i++)
            {
                casterMotes[i] = CreateEffect("Rune Cast Mote", i % 2 == 0 ? primary : accent, 154 + i % 2);
                casterMotes[i].transform.localScale = Vector3.zero;
            }

            GameObject casterCore = CreateEffect("Rune Cast Core", primary, 153);
            casterCore.transform.position = casterCenter;
            casterCore.transform.localScale = Vector3.zero;
            float elapsed = 0f;
            const float castDuration = 0.44f;
            while (elapsed < castDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / castDuration);
                float gather = Mathf.SmoothStep(0f, 1f, t);
                float radius = Mathf.Lerp(1.02f, 0.34f, gather);
                for (int i = 0; i < casterMotes.Length; i++)
                {
                    float angle = i * (360f / casterMotes.Length) + t * 250f * (i % 2 == 0 ? 1f : -1f);
                    float radians = angle * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius * 0.48f, 0f);
                    SetMagicEffect(
                        casterMotes[i],
                        casterCenter + offset,
                        new Vector3(0.19f, 0.055f, 1f) * Mathf.Lerp(0.45f, 1f, gather),
                        angle + 90f,
                        Mathf.Sin(t * Mathf.PI));
                }
                SetMagicEffect(
                    casterCore,
                    casterCenter,
                    Vector3.one * Mathf.Lerp(0.10f, 0.62f, gather),
                    t * -180f,
                    Mathf.Sin(t * Mathf.PI) * 0.72f);
                if (actor.BoneRig != null)
                    actor.BoneRig.Apply(actor.BoneRig.Sample(
                        BoneRigPose2D.Cast,
                        t,
                        actor.IdlePhase));
                else
                    SetContinuousPixelMotionFrame(actor, UnitPose.Action, t * 0.62f);
                yield return null;
            }

            yield return AnimateUnitPhase(
                actor,
                windup,
                castPosition,
                windupScale,
                castScale,
                0.13f,
                motion.TravelArc * 0.24f,
                -facing * 2.4f,
                true,
                rigFromPose: BoneRigPose2D.Cast,
                rigToPose: BoneRigPose2D.Strike,
                pixelMotionPose: UnitPose.Action,
                pixelMotionFromNormalized: 0.62f,
                pixelMotionToNormalized: 1f);

            DestroyMagicEffects(casterMotes);
            DestroyEffect(casterCore);

            const int flightMoteCount = 7;
            var flightMotes = new GameObject[flightMoteCount];
            for (int i = 0; i < flightMoteCount; i++)
            {
                flightMotes[i] = CreateEffect("Rune Flight Mote", i % 2 == 0 ? accent : primary, 158 - i);
                flightMotes[i].transform.localScale = Vector3.zero;
            }
            Vector3 origin = castPosition + Vector3.up * 1.18f + Vector3.right * (facing * 0.35f);
            Vector3 control = (origin + destination) * 0.5f + Vector3.up * 1.05f;
            elapsed = 0f;
            const float flightDuration = 0.38f;
            while (elapsed < flightDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / flightDuration);
                for (int i = 0; i < flightMotes.Length; i++)
                {
                    float delayed = Mathf.Clamp01(t - i * 0.055f);
                    float travel = Mathf.SmoothStep(0f, 1f, delayed);
                    Vector3 point = QuadraticBezier(origin, control, destination, travel);
                    float orbit = (t * 540f + i * 137f) * Mathf.Deg2Rad;
                    point += new Vector3(Mathf.Cos(orbit), Mathf.Sin(orbit), 0f) * (0.10f + i * 0.008f);
                    float alpha = Mathf.Clamp01(delayed * 7f) * (1f - Mathf.Clamp01((delayed - 0.82f) * 5.5f));
                    SetMagicEffect(
                        flightMotes[i],
                        point,
                        new Vector3(0.30f - i * 0.018f, 0.10f, 1f),
                        orbit * Mathf.Rad2Deg,
                        alpha);
                }
                yield return null;
            }
            DestroyMagicEffects(flightMotes);

            const int ringMoteCount = 20;
            const int rayCount = 10;
            var ringMotes = new GameObject[ringMoteCount];
            var rays = new GameObject[rayCount];
            for (int i = 0; i < ringMotes.Length; i++)
            {
                ringMotes[i] = CreateEffect("Rune Impact Ring", i % 3 == 0 ? accent : primary, 164 + i % 2);
                ringMotes[i].transform.localScale = Vector3.zero;
            }
            for (int i = 0; i < rays.Length; i++)
            {
                rays[i] = CreateEffect("Rune Impact Ray", i % 2 == 0 ? primary : accent, 163);
                rays[i].transform.localScale = Vector3.zero;
            }
            GameObject pillar = CreateEffect("Rune Light Pillar", new Color(primary.r, primary.g, primary.b, 0.72f), 159);
            GameObject impactCore = CreateEffect("Rune Impact Core", new Color(accent.r, accent.g, accent.b, 0.90f), 166);
            Coroutine hitReaction = ApplyHit(target, action);
            ClearEmbeddedAttackFrameAfterImpact(actor);
            _impactFlashAlpha = action.WasCritical ? 0.30f : 0.20f;
            _shakeStrength = action.WasCritical ? 0.20f : 0.14f;
            _shakeUntil = Time.unscaledTime + (action.WasCritical ? 0.31f : 0.24f);
            _audio.PlaySfx("attack");

            elapsed = 0f;
            const float impactDuration = 0.58f;
            while (elapsed < impactDuration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / impactDuration);
                float burst = EaseOut(Mathf.Clamp01(t * 1.65f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.42f) / 0.58f));
                for (int i = 0; i < ringMotes.Length; i++)
                {
                    float angle = i * (360f / ringMotes.Length) - t * 230f;
                    float radians = angle * Mathf.Deg2Rad;
                    float radius = Mathf.Lerp(0.26f, 1.55f, burst);
                    Vector3 offset = new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius * 0.46f, 0f);
                    SetMagicEffect(
                        ringMotes[i],
                        destination + offset,
                        new Vector3(0.25f, 0.06f, 1f) * (0.75f + burst * 0.55f),
                        angle + 90f,
                        fade);
                }
                for (int i = 0; i < rays.Length; i++)
                {
                    float angle = i * (360f / rays.Length) + 18f;
                    float radians = angle * Mathf.Deg2Rad;
                    float length = Mathf.Lerp(0.25f, 1.46f, burst);
                    Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * (length * 0.48f);
                    SetMagicEffect(
                        rays[i],
                        destination + offset,
                        new Vector3(length, 0.075f, 1f),
                        angle,
                        fade * (1f - t * 0.55f));
                }
                SetMagicEffect(
                    pillar,
                    destination + Vector3.up * 0.42f,
                    new Vector3(Mathf.Lerp(0.18f, 0.62f, burst), Mathf.Lerp(0.35f, 3.10f, burst), 1f),
                    0f,
                    fade * 0.62f);
                SetMagicEffect(
                    impactCore,
                    destination,
                    Vector3.one * Mathf.Lerp(0.18f, 1.08f, burst),
                    t * 210f,
                    fade * 0.88f);
                yield return null;
            }

            DestroyMagicEffects(ringMotes);
            DestroyMagicEffects(rays);
            DestroyEffect(pillar);
            DestroyEffect(impactCore);
            SpawnBurst(destination, primary, action.WasCritical ? 16 : 10);

            Vector3 recoil = castPosition + Vector3.left * (facing * motion.ImpactRecoil * 0.66f);
            yield return AnimateUnitPhase(
                actor,
                castPosition,
                recoil,
                castScale,
                windupScale,
                0.10f,
                motion.TravelArc * 0.10f,
                facing * 1.7f,
                true,
                rigFromPose: BoneRigPose2D.Strike,
                rigToPose: BoneRigPose2D.Return);
            yield return AnimateUnitPhase(
                actor,
                recoil,
                actor.Home,
                windupScale,
                startScale,
                motion.ReturnDuration,
                motion.TravelArc * 0.24f,
                -facing * 1.4f,
                false,
                rigFromPose: BoneRigPose2D.Return,
                rigToPose: BoneRigPose2D.Idle,
                locomotionCycles: 0.78f,
                upperBodyWeight: 0.62f,
                pixelMotionPose: UnitPose.Idle);
            yield return hitReaction;
        }

        private static Color MagicPrimaryColor(string sourceUnitId, BattleTeam team)
        {
            if (!string.IsNullOrEmpty(sourceUnitId) && sourceUnitId.IndexOf("cleric", StringComparison.Ordinal) >= 0)
                return new Color(1f, 0.78f, 0.24f, 0.96f);
            return team == BattleTeam.Player
                ? new Color(0.22f, 0.86f, 1f, 0.96f)
                : new Color(0.94f, 0.24f, 0.58f, 0.96f);
        }

        private static Color MagicAccentColor(string sourceUnitId, BattleTeam team)
        {
            if (!string.IsNullOrEmpty(sourceUnitId) && sourceUnitId.IndexOf("cleric", StringComparison.Ordinal) >= 0)
                return new Color(0.64f, 1f, 0.90f, 0.94f);
            return team == BattleTeam.Player
                ? new Color(0.72f, 0.38f, 1f, 0.94f)
                : new Color(1f, 0.62f, 0.18f, 0.94f);
        }

        private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }

        private static void SetMagicEffect(
            GameObject effect,
            Vector3 position,
            Vector3 scale,
            float rotation,
            float alpha)
        {
            if (effect == null) return;
            effect.transform.position = position;
            effect.transform.localScale = scale;
            effect.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        private void DestroyMagicEffects(IEnumerable<GameObject> effects)
        {
            if (effects == null) return;
            foreach (GameObject effect in effects) DestroyEffect(effect);
        }

        private Coroutine ApplyHit(UnitView target, FormationAction action)
        {
            Color numberColor = action.WasCritical ? new Color(1f, 0.88f, 0.25f) : Color.white;
            string prefix = action.WasCritical ? "CRITICAL  " : action.WasGuarded ? "GUARD  " : string.Empty;
            _labels.Add(new FloatingLabel
            {
                Text = prefix + action.Damage,
                World = target.Home + Vector3.up * 2.55f,
                Color = numberColor,
                Duration = 0.92f
            });
            _impactFlashColor = action.WasCritical
                ? new Color(1f, 0.72f, 0.18f, 1f)
                : action.Kind == FormationActionKind.Magic
                    ? new Color(0.25f, 0.78f, 1f, 1f)
                    : new Color(1f, 0.94f, 0.84f, 1f);
            _impactFlashAlpha = action.WasCritical ? 0.24f : 0.12f;
            _shakeStrength = action.WasCritical ? 0.16f : 0.09f;
            _shakeUntil = Time.unscaledTime + (action.WasCritical ? 0.24f : 0.16f);
            return StartCoroutine(HitReaction(target));
        }

        private IEnumerator HitReaction(UnitView target)
        {
            BattleMotionProfile motion = FormationPresentationProfile.GetMotionProfile(target.Unit.ClassName);
            UnitPose previousPose = target.CurrentPose;
            bool continuousHit = HasContinuousPixelMotion(target, UnitPose.Hit);
            if (continuousHit)
            {
                SetContinuousPixelMotionFrame(target, UnitPose.Hit, 0f);
                target.CurrentPose = UnitPose.Hit;
            }
            else
            {
                yield return TransitionPose(target, UnitPose.Hit, 0.055f);
            }
            Vector3 start = target.Home;
            float direction = target.Unit.Team == BattleTeam.Player ? 1f : -1f;
            Vector3 startScale = ScaleForPose(target, target.CurrentPose);
            Vector3 recoilScale = new Vector3(
                startScale.x * (1f + motion.Squash),
                startScale.y * (1f - motion.Squash * 0.75f),
                startScale.z);
            Vector3 recoil = start + Vector3.right * (direction * motion.HitRecoil);
            if (target.BoneRig != null)
                target.BoneRig.Apply(target.BoneRig.Sample(
                    BoneRigPose2D.Hit,
                    1f,
                    target.IdlePhase));
            SetBodyColor(target, new Color(1f, 0.38f, 0.38f, 1f));
            yield return AnimateUnitPhase(
                target,
                start,
                recoil,
                startScale,
                recoilScale,
                0.065f,
                0.035f,
                direction * 4.2f,
                true,
                pixelMotionPose: continuousHit ? UnitPose.Hit : (UnitPose?)null,
                pixelMotionFromNormalized: 0f,
                pixelMotionToNormalized: 0.72f);
            SetBodyColor(target, new Color(1f, 0.68f, 0.68f, 1f));
            yield return AnimateUnitPhase(
                target,
                recoil,
                start,
                recoilScale,
                startScale,
                0.14f,
                0.025f,
                -direction * 2.2f,
                false,
                pixelMotionPose: continuousHit ? UnitPose.Hit : (UnitPose?)null,
                pixelMotionFromNormalized: 0.72f,
                pixelMotionToNormalized: 1f);
            SetBodyColor(target, Color.white);
            target.Object.transform.position = start;
            target.Object.transform.localScale = startScale;
            yield return TransitionPose(target, previousPose, 0.075f);
        }

        private IEnumerator AnimateDefeat(UnitView target)
        {
            target.Animating = true;
            BattleMotionProfile motion = FormationPresentationProfile.GetMotionProfile(target.Unit.ClassName);
            yield return TransitionPose(
                target,
                UnitPose.Defeat,
                FormationPresentationProfile.GetIncapacitatedTransitionDuration(target.Unit.ClassName));
            Vector3 start = target.Object.transform.position;
            float direction = target.Unit.Team == BattleTeam.Player ? 1f : -1f;
            FormationAnchor defeatedAnchor = FormationPresentationProfile.GetIncapacitatedAnchor(
                target.Unit.Team,
                target.Unit.FormationSlot);
            Vector3 settled = new Vector3(
                defeatedAnchor.X,
                defeatedAnchor.Y,
                start.z);
            Vector3 defeatBaseScale = ScaleForPose(target, UnitPose.Defeat);
            Vector3 defeatedScale = new Vector3(
                defeatBaseScale.x * 1.018f,
                defeatBaseScale.y * 0.965f,
                defeatBaseScale.z);
            yield return AnimateUnitPhase(
                target,
                start,
                settled,
                defeatBaseScale,
                defeatedScale,
                FormationPresentationProfile.GetIncapacitatedSettleDuration(target.Unit.ClassName),
                motion.TravelArc * 0.12f,
                direction * 5.5f,
                false,
                true);

            target.Object.transform.position = settled;
            target.Object.transform.localScale = defeatedScale;
            target.Object.transform.rotation = Quaternion.Euler(0f, 0f, direction * 5.5f);
            SetBodyColor(target, new Color(0.58f, 0.62f, 0.72f, 1f));
            target.ShadowRenderer.color = new Color(
                target.ShadowColor.r,
                target.ShadowColor.g,
                target.ShadowColor.b,
                target.ShadowColor.a * 0.62f);
            target.ShadowObject.transform.position = new Vector3(settled.x, defeatedAnchor.Y, 0f);
            target.ShadowObject.transform.localScale =
                new Vector3(defeatedAnchor.ShadowWidth, 0.16f, 1f);
            ApplyIncapacitatedSorting(target);
        }

        private static bool HasContinuousPixelMotion(UnitView view, UnitPose pose)
        {
            return view != null &&
                   view.BoneRig == null &&
                   view.PixelPoseSprites != null &&
                   view.PixelPoseSprites.TryGetValue(pose, out Sprite[] frames) &&
                   frames != null &&
                   frames.Length >= 20;
        }

        private static void SetContinuousPixelMotionFrame(
            UnitView view,
            UnitPose pose,
            float normalizedTime)
        {
            if (!HasContinuousPixelMotion(view, pose)) return;
            Sprite[] frames = view.PixelPoseSprites[pose];
            int index = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(normalizedTime) * (frames.Length - 1)),
                0,
                frames.Length - 1);
            view.Renderer.sprite = frames[index];
            view.Renderer.flipX = FormationPresentationProfile.GetFlipX(
                view.Unit.Team,
                PoseAssetId(view.Unit.SourceUnitId, pose));
            view.Renderer.color = Color.white;
        }

        private static void ClearEmbeddedAttackFrameAfterImpact(UnitView view)
        {
            if (!HasContinuousPixelMotion(view, UnitPose.Idle)) return;
            SetContinuousPixelMotionFrame(view, UnitPose.Idle, 0f);
            view.CurrentPose = UnitPose.Idle;
        }

        public static bool ShouldReversePixelSequenceToIdle(int currentSequenceFrameCount)
        {
            return currentSequenceFrameCount > 0 && currentSequenceFrameCount < 20;
        }

        public static bool ShouldShowSoloCutIn(bool hasActionSprite, bool isSpecial)
        {
            return hasActionSprite && isSpecial;
        }

        public static bool UsesStablePixelEntrance(int idleSequenceFrameCount)
        {
            return idleSequenceFrameCount >= 20;
        }

        public static float ResolvePixelMotionNormalized(
            float fromNormalized,
            float toNormalized,
            float phaseNormalized,
            bool reverse)
        {
            float progress = reverse
                ? 1f - Mathf.Clamp01(phaseNormalized)
                : Mathf.Clamp01(phaseNormalized);
            return Mathf.Lerp(fromNormalized, toNormalized, progress);
        }

        private IEnumerator AnimateUnitPhase(
            UnitView view,
            Vector3 from,
            Vector3 to,
            Vector3 fromScale,
            Vector3 toScale,
            float duration,
            float arcHeight,
            float rotationDegrees,
            bool fastOut,
            bool keepShadowOnHomeGround = false,
            BoneRigPose2D? rigFromPose = null,
            BoneRigPose2D? rigToPose = null,
            float locomotionCycles = 0f,
            float upperBodyWeight = 0f,
            UnitPose? pixelMotionPose = null,
            bool reversePixelMotion = false,
            float pixelMotionFromNormalized = 0f,
            float pixelMotionToNormalized = 1f)
        {
            // 疑似3D: 移動量が大きいほど、道中で体を進行方向へ捻る。
            // 横幅だけを縮めると、平面の絵でも板が回り込んだように見える
            // （Y軸回転を cos で近似したもの）。分割も差し替えも要らないので、
            // 既存のモーションすべてにそのまま効く。
            float travel = Mathf.Abs(to.x - from.x);
            float turnDepth = Mathf.Clamp01(travel / 1.2f);
            BoneRigPoseSample2D rigStart = view.BoneRig != null
                ? view.BoneRig.CurrentSample
                : default;
            if (view.BoneRig != null && rigFromPose.HasValue)
                rigStart = view.BoneRig.Sample(rigFromPose.Value, 1f, view.IdlePhase);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float linear = Mathf.Clamp01(elapsed / duration);
                float t = fastOut ? EaseOut(linear) : Mathf.SmoothStep(0f, 1f, linear);
                Vector3 ground = Vector3.Lerp(from, to, t);
                float pulse = Mathf.Sin(linear * Mathf.PI);
                float arc = pulse * arcHeight;
                view.Object.transform.position = ground + Vector3.up * arc;

                Vector3 phaseScale = Vector3.Lerp(fromScale, toScale, t);
                // 捻りの分だけ縦に伸ばして、体積が減って痩せて見えるのを防ぐ。
                float turn = 1f - pulse * 0.22f * turnDepth;
                phaseScale.x *= turn;
                phaseScale.y *= 1f + pulse * 0.06f * turnDepth;
                view.Object.transform.localScale = phaseScale;

                view.Object.transform.rotation = Quaternion.Euler(0f, 0f, pulse * rotationDegrees);
                if (view.BoneRig != null && (rigToPose.HasValue || locomotionCycles > 0f))
                {
                    BoneRigPoseSample2D actionPose = rigStart;
                    if (rigToPose.HasValue)
                    {
                        BoneRigPoseSample2D rigTarget = view.BoneRig.Sample(
                            rigToPose.Value,
                            linear,
                            view.IdlePhase);
                        actionPose = BoneRig2DProfile.Lerp(rigStart, rigTarget, t);
                    }
                    if (locomotionCycles > 0f)
                    {
                        BoneRigPoseSample2D locomotion = view.BoneRig.Sample(
                            BoneRigPose2D.Run,
                            Mathf.Repeat(linear * locomotionCycles, 1f),
                            view.IdlePhase);
                        actionPose = rigToPose.HasValue
                            ? BoneRig2DProfile.ComposeLocomotionAndUpperBody(
                                locomotion,
                                actionPose,
                                upperBodyWeight)
                            : locomotion;
                    }
                    view.BoneRig.Apply(actionPose);
                }
                if (view.IsPixel && pixelMotionPose.HasValue &&
                    HasContinuousPixelMotion(view, pixelMotionPose.Value))
                {
                    SetContinuousPixelMotionFrame(
                        view,
                        pixelMotionPose.Value,
                        ResolvePixelMotionNormalized(
                            pixelMotionFromNormalized,
                            pixelMotionToNormalized,
                            linear,
                            reversePixelMotion));
                }
                else if (view.IsPixel && travel > 0.12f && view.PixelRunSprites != null)
                {
                    int runFrame = Math.Max(
                        0,
                        (int)Math.Floor(elapsed * PixelAnimationProfile.SourceFramesPerSecond)) %
                        view.PixelRunSprites.Length;
                    view.Renderer.sprite = view.PixelRunSprites[runFrame];
                }
                float groundY = keepShadowOnHomeGround
                    ? GroundY(view, view.Home)
                    : GroundY(view, ground);
                view.ShadowObject.transform.position = new Vector3(ground.x, groundY, 0f);
                ApplySorting(view, groundY);
                yield return null;
            }
            view.Object.transform.position = to;
            view.Object.transform.localScale = toScale;
            view.Object.transform.rotation = Quaternion.identity;
            if (view.BoneRig != null && rigToPose.HasValue)
                view.BoneRig.Apply(view.BoneRig.Sample(
                    rigToPose.Value,
                    1f,
                    view.IdlePhase));
            if (view.IsPixel)
            {
                if (pixelMotionPose.HasValue && HasContinuousPixelMotion(view, pixelMotionPose.Value))
                    SetContinuousPixelMotionFrame(
                        view,
                        pixelMotionPose.Value,
                        ResolvePixelMotionNormalized(
                            pixelMotionFromNormalized,
                            pixelMotionToNormalized,
                            1f,
                            reversePixelMotion));
                else
                    view.Renderer.sprite = SpriteForPose(view, view.CurrentPose);
            }
            float finalGroundY = keepShadowOnHomeGround
                ? GroundY(view, view.Home)
                : GroundY(view, to);
            view.ShadowObject.transform.position = new Vector3(to.x, finalGroundY, 0f);
            ApplySorting(view, finalGroundY);
        }

        private IEnumerator TransitionPose(UnitView view, UnitPose pose, float duration)
        {
            if (view.BoneRig != null)
            {
                BoneRigPoseSample2D from = view.BoneRig.CurrentSample;
                BoneRigPose2D rigPose = BonePoseFor(pose);
                BoneRigPoseSample2D to = view.BoneRig.Sample(
                    rigPose,
                    rigPose == BoneRigPose2D.Victory ? 0.25f : 1f,
                    view.IdlePhase);
                float rigElapsed = 0f;
                while (rigElapsed < duration)
                {
                    yield return WaitUntilRunning();
                    rigElapsed += Time.unscaledDeltaTime * _battleSpeed;
                    float rigT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rigElapsed / duration));
                    view.BoneRig.Apply(BoneRig2DProfile.Lerp(from, to, rigT));
                    yield return null;
                }
                view.BoneRig.Apply(to);
                view.CurrentPose = pose;
                yield break;
            }

            if (view.IsPixel && view.PixelPoseSprites != null)
            {
                int currentSequenceFrameCount = 0;
                if (view.PixelPoseSprites.TryGetValue(view.CurrentPose, out Sprite[] currentMotion) &&
                    currentMotion != null)
                    currentSequenceFrameCount = currentMotion.Length;
                bool reverse = pose == UnitPose.Idle &&
                               view.CurrentPose != UnitPose.Idle &&
                               ShouldReversePixelSequenceToIdle(currentSequenceFrameCount);
                UnitPose sequencePose = reverse ? view.CurrentPose : pose;
                if (view.PixelPoseSprites.TryGetValue(sequencePose, out Sprite[] motion) &&
                    motion != null && motion.Length > 0)
                {
                    Vector3 pixelNextScale = ScaleForPose(view, pose);
                    Vector3 pixelFromScale = view.Object.transform.localScale;
                    bool pixelNextFlip = FormationPresentationProfile.GetFlipX(
                        view.Unit.Team,
                        PoseAssetId(view.Unit.SourceUnitId, sequencePose));
                    view.Renderer.flipX = pixelNextFlip;
                    view.Renderer.color = Color.white;
                    view.BlendRenderer.sprite = null;
                    view.BlendRenderer.color = new Color(1f, 1f, 1f, 0f);
                    float pixelElapsed = 0f;
                    while (pixelElapsed < duration)
                    {
                        yield return WaitUntilRunning();
                        pixelElapsed += Time.unscaledDeltaTime * _battleSpeed;
                        float normalized = Mathf.Clamp01(pixelElapsed / duration);
                        int motionFrame = Math.Min(
                            motion.Length - 1,
                            Math.Max(0, (int)Math.Floor(normalized * motion.Length)));
                        if (reverse) motionFrame = motion.Length - 1 - motionFrame;
                        view.Renderer.sprite = motion[motionFrame];
                        view.Object.transform.localScale = Vector3.Lerp(
                            pixelFromScale,
                            pixelNextScale,
                            Mathf.SmoothStep(0f, 1f, normalized));
                        yield return null;
                    }

                    view.Renderer.sprite = pose == UnitPose.Idle
                        ? view.PixelPoseSprites[UnitPose.Idle][0]
                        : motion[reverse ? 0 : motion.Length - 1];
                    view.Object.transform.localScale = pixelNextScale;
                    view.CurrentPose = pose;
                    yield break;
                }
            }

            Sprite nextSprite = SpriteForPose(view, pose);
            Vector3 nextScale = ScaleForPose(view, pose);
            string nextAssetId = PoseAssetId(view.Unit.SourceUnitId, pose);
            bool nextFlip = view.IsPixel
                ? view.Unit.Team == BattleTeam.Player
                : FormationPresentationProfile.GetFlipX(view.Unit.Team, nextAssetId);
            if (nextSprite == null) yield break;

            SpriteRenderer blend = view.BlendRenderer;
            blend.sprite = nextSprite;
            blend.flipX = nextFlip;
            blend.color = new Color(1f, 1f, 1f, 0f);
            Vector3 fromScale = view.Object.transform.localScale;
            blend.transform.localScale = DivideScale(nextScale, fromScale);
            Color fromColor = view.Renderer.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return WaitUntilRunning();
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                view.Renderer.color = new Color(fromColor.r, fromColor.g, fromColor.b, fromColor.a * (1f - t));
                blend.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }

            view.Renderer.sprite = nextSprite;
            view.Renderer.flipX = nextFlip;
            view.Renderer.color = Color.white;
            blend.sprite = null;
            blend.color = new Color(1f, 1f, 1f, 0f);
            blend.transform.localScale = Vector3.one;
            view.Object.transform.localScale = nextScale;
            view.CurrentPose = pose;
        }

        private IEnumerator AnimateOutcome(BattleWinner winner)
        {
            BattleTeam winningTeam = winner == BattleWinner.Player ? BattleTeam.Player : BattleTeam.Enemy;
            UnitView[] winners = _unitViews.Values
                .Where(view => view.Unit.IsAlive && view.Unit.Team == winningTeam)
                .OrderBy(view => view.Unit.FormationSlot)
                .ToArray();

            foreach (UnitView view in winners) view.Animating = true;
            for (int i = 0; i < winners.Length; i++)
            {
                UnitView view = winners[i];
                BattleMotionProfile motion = FormationPresentationProfile.GetMotionProfile(view.Unit.ClassName);
                yield return TransitionPose(view, UnitPose.Victory, 0.16f);
                Vector3 start = view.Home;
                Vector3 victoryScale = ScaleForPose(view, UnitPose.Victory);
                float elapsed = 0f;
                const float duration = 0.34f;
                while (elapsed < duration)
                {
                    yield return WaitUntilRunning();
                    elapsed += Time.unscaledDeltaTime * _battleSpeed;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float pulse = Mathf.Sin(t * Mathf.PI);
                    float lift = pulse * Mathf.Max(0.10f, motion.TravelArc * 0.62f);
                    view.Object.transform.position = start + Vector3.up * lift;
                    view.Object.transform.localScale = new Vector3(
                        victoryScale.x * (1f - pulse * motion.Stretch * 0.20f),
                        victoryScale.y * (1f + pulse * motion.Stretch * 0.48f),
                        victoryScale.z);
                    view.Object.transform.rotation = Quaternion.Euler(
                        0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * (1.2f + motion.Stretch * 5f));
                    if (view.BoneRig != null)
                        view.BoneRig.Apply(view.BoneRig.Sample(
                            BoneRigPose2D.Victory,
                            t,
                            view.IdlePhase));
                    else
                        SetContinuousPixelMotionFrame(view, UnitPose.Victory, t);
                    view.ShadowObject.transform.position = new Vector3(start.x, GroundY(view, start), 0f);
                    ApplySorting(view, GroundY(view, start));
                    yield return null;
                }
                view.Object.transform.position = start;
                view.Object.transform.localScale = victoryScale;
                view.Object.transform.rotation = Quaternion.identity;
                view.Animating = false;
            }
        }

        private IEnumerator WaitBattle(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!_paused) elapsed += Time.unscaledDeltaTime * _battleSpeed;
                yield return null;
            }
        }

        private IEnumerator WaitUntilRunning()
        {
            while (_paused) yield return null;
        }

        private void SpawnSlash(Vector3 position, float facing, bool critical)
        {
            Color color = critical ? new Color(1f, 0.92f, 0.36f, 0.95f) : new Color(0.86f, 0.96f, 1f, 0.92f);
            GameObject slash = CreateEffect("Sword Arc", color, 160);
            slash.transform.position = position;
            slash.transform.localScale = new Vector3(2.1f, 0.10f, 1f);
            slash.transform.rotation = Quaternion.Euler(0f, 0f, facing > 0f ? -32f : 32f);
            StartCoroutine(FadeEffect(slash, 0.20f, 1.45f));
            SpawnBurst(position, color, critical ? 12 : 6);
        }

        private void SpawnBurst(Vector3 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i + 11f;
                Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                GameObject spark = CreateEffect("Impact Spark", color, 158);
                spark.transform.position = position;
                spark.transform.localScale = new Vector3(0.38f, 0.055f, 1f);
                spark.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                StartCoroutine(MoveAndFadeEffect(spark, direction * (0.55f + i % 3 * 0.16f), 0.28f));
            }
        }

        private void SpawnTrail(Vector3 position, Color color)
        {
            GameObject trail = CreateEffect("Trail", new Color(color.r, color.g, color.b, 0.45f), 150);
            trail.transform.position = position;
            trail.transform.localScale = Vector3.one * 0.12f;
            StartCoroutine(FadeEffect(trail, 0.18f, 1.7f));
        }

        private void SpawnUnitAfterImage(UnitView view, float alpha)
        {
            if (view?.Renderer == null || view.Renderer.sprite == null || _battleRoot == null) return;
            var ghost = new GameObject("Motion Afterimage");
            ghost.transform.SetParent(_battleRoot.transform);
            ghost.transform.position = view.Object.transform.position;
            ghost.transform.localScale = view.Object.transform.localScale;
            ghost.transform.rotation = view.Object.transform.rotation;
            SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
            renderer.sprite = view.Renderer.sprite;
            renderer.flipX = view.Renderer.flipX;
            renderer.sortingOrder = view.Renderer.sortingOrder - 1;
            Color tint = view.Unit.Team == BattleTeam.Player
                ? new Color(0.25f, 0.82f, 1f, alpha)
                : new Color(1f, 0.28f, 0.38f, alpha);
            renderer.color = tint;
            _effects.Add(ghost);
            StartCoroutine(FadeEffect(ghost, 0.20f, 1.035f));
        }

        private void SpawnSpecialAura(UnitView actor, bool cooperation)
        {
            Color color = actor.Unit.Team == BattleTeam.Player
                ? new Color(0.18f, 0.82f, 1f, 0.46f)
                : new Color(1f, 0.18f, 0.34f, 0.46f);
            if (cooperation) color = new Color(1f, 0.78f, 0.24f, 0.52f);
            for (int i = 0; i < 3; i++)
            {
                GameObject aura = CreateEffect("Special Aura", color, 142 + i);
                aura.transform.position = actor.Home + Vector3.up * (1.15f + i * 0.18f);
                float scale = 0.55f + i * 0.40f;
                aura.transform.localScale = new Vector3(scale, scale * 0.62f, 1f);
                StartCoroutine(FadeEffect(aura, 0.34f + i * 0.08f, 2.8f));
            }
        }

        private void FocusBattleCamera(UnitView actor, UnitView target, bool dramatic)
        {
            Vector3 midpoint = (actor.Home + target.Home) * 0.5f;
            _cameraDesiredPosition = new Vector3(midpoint.x * 0.18f, 0.18f, -10f);
            float viewportScale = _battleBaseCameraSize / 5.4f;
            _cameraDesiredSize = (dramatic ? 4.62f : 5.05f) * viewportScale;
        }

        private void ResetBattleCamera()
        {
            _cameraDesiredPosition = new Vector3(0f, 0f, -10f);
            _cameraDesiredSize = _battleBaseCameraSize;
        }

        private GameObject CreateEffect(string effectName, Color color, int order)
        {
            var effect = new GameObject(effectName);
            effect.transform.SetParent(_battleRoot.transform);
            SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = _effectSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            _effects.Add(effect);
            return effect;
        }

        private IEnumerator FadeEffect(GameObject effect, float duration, float scaleMultiplier)
        {
            if (effect == null) yield break;
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            Color startColor = renderer.color;
            Vector3 startScale = effect.transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                effect.transform.localScale = Vector3.Lerp(startScale, startScale * scaleMultiplier, t);
                yield return null;
            }
            DestroyEffect(effect);
        }

        private IEnumerator MoveAndFadeEffect(GameObject effect, Vector3 velocity, float duration)
        {
            if (effect == null) yield break;
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            Color startColor = renderer.color;
            Vector3 start = effect.transform.position;
            float elapsed = 0f;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.unscaledDeltaTime * _battleSpeed;
                float t = Mathf.Clamp01(elapsed / duration);
                effect.transform.position = start + velocity * EaseOut(t);
                renderer.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                yield return null;
            }
            DestroyEffect(effect);
        }

        private void DestroyEffect(GameObject effect)
        {
            if (effect == null) return;
            _effects.Remove(effect);
            Destroy(effect);
        }

        private void ClearBattlePresentation()
        {
            StopAllCoroutines();
            ReleaseBattleSprites();
            _labels.Clear();
            _unitViews.Clear();
            _effects.Clear();
            _cutInsShown.Clear();
            _bondTechniqueShown = false;
            if (_battleRoot != null) Destroy(_battleRoot);
            _battleRoot = null;
            _battle = null;
            _storyExploration = null;
            _fieldMap = null;
            _fieldExploration = null;
            _war = null;
            _warReport = null;
            _stage = null;
            _showResult = false;
            _skillBanner = string.Empty;
            _battleActionIndex = 0;
            _impactFlashAlpha = 0f;
            _storyHasMoveTarget = false;
            _storyDialogueOpen = false;
            _storyRecruitmentCardOpen = false;
            _storyDialogueLines = Array.Empty<string>();
            _storyDialogueIndex = 0;
            _pendingStoryEntity = null;
            _cameraDesiredPosition = new Vector3(0f, 0f, -10f);
            _cameraDesiredSize = _battleBaseCameraSize;
        }

        private void EnterGift()
        {
            ClearBattlePresentation();
            _screen = ScreenMode.Gift;
            _giftTime = 0f;
            EnsureCamera();
            _audio.PlayBgm("BD-01", 2f);
            _audio.PlaySfx("gift");
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_screen == ScreenMode.Title) DrawTitle();
            else if (_screen == ScreenMode.Story) DrawStoryExploration();
            else if (_screen == ScreenMode.ChapterStory) DrawChapterStory();
            else if (_screen == ScreenMode.Choice) DrawChoice();
            else if (_screen == ScreenMode.Downfall) DrawDownfall();
            else if (_screen == ScreenMode.Field) DrawField();
            else if (_screen == ScreenMode.Preparation) DrawPreparation();
            else if (_screen == ScreenMode.War) DrawWar();
            else if (_screen == ScreenMode.Gift) DrawGift();
            else DrawBattle();
        }

        private void DrawTitle()
        {
            DrawFullScreenTint(new Color(0.025f, 0.04f, 0.075f, 1f));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, _titleFade));
            float width = Mathf.Min(680f, Screen.width - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 74f, width, 590f), _panelStyle);
            GUILayout.Space(28f);
            GUILayout.Label("TACTICS GIFT", _heroTitleStyle);
            GUILayout.Label("ふたりの記憶を巡る、編成型ファンタジーRPG", _centerStyle);
            GUILayout.Space(36f);
            GUILayout.Label("升目を選ぶ戦いから、部隊の個性がぶつかる戦いへ。\n編成された仲間たちは、自ら接近し、守り、魔法を放ちます。", _centerStyle);
            GUILayout.Space(28f);
            string campaignButton = !_hasSave
                ? "はじめから"
                : CampaignSavePolicy.IsGiftUnlocked(_save, _catalog.stages.Length)
                    ? "贈り物をもう一度見る"
                    : $"つづきから（第{_save.stageIndex + 1}戦）";
            if (GUILayout.Button(campaignButton, _buttonStyle)) BeginCampaign(false);
            if (_hasSave && !_confirmNewGame && GUILayout.Button("最初から", _buttonStyle)) _confirmNewGame = true;
            if (_confirmNewGame)
            {
                GUILayout.Label("保存済みの進行を消して最初から始めますか？", _centerStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("はい", _buttonStyle)) BeginCampaign(true);
                if (GUILayout.Button("戻る", _buttonStyle)) _confirmNewGame = false;
                GUILayout.EndHorizontal();
            }
            if (HasSaveBackup() &&
                GUILayout.Button("前回のセーブを復元", _buttonStyle))
            {
                RestoreSaveBackup();
            }
            if (!string.IsNullOrWhiteSpace(_titleNotice))
                GUILayout.Label(_titleNotice, _centerStyle);
            GUILayout.Space(22f);
            float newVolume = GUILayout.HorizontalSlider(_audio.Volume, 0f, 1f);
            GUILayout.Label($"音量 {Mathf.RoundToInt(newVolume * 100f)}%", _centerStyle);
            if (Mathf.Abs(newVolume - _audio.Volume) > 0.001f)
            {
                _audio.SetVolume(newVolume);
                _save.volume = newVolume;
                PersistSave();
            }
            if (GUILayout.Button(_audio.Muted ? "音を出す" : "ミュート", _buttonStyle))
            {
                _audio.SetMuted(!_audio.Muted);
                _save.muted = _audio.Muted;
                PersistSave();
            }
            GUILayout.EndArea();
            GUI.color = previous;
        }

        /// <summary>
        /// 選択肢画面。
        ///
        /// 重要: 場違いな選択肢（IsAbsurd）を色や配置で区別してはいけない。
        /// 普通の選択肢と同じ顔をして紛れていることが、この仕組みの全てなので。
        /// </summary>
        private void DrawChoice()
        {
            if (_choicePrompt == null) return;
            DrawFullScreenTint(new Color(0.012f, 0.025f, 0.045f, 1f));

            float width = Mathf.Min(960f, Screen.width - 48f);
            float height = Mathf.Min(600f, Screen.height - 64f);
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                _panelStyle);
            GUILayout.Space(18f);
            GUILayout.Label("DECISION", _titleStyle);
            GUILayout.Label("選んだ言葉が、仲間との関係と次の出来事を変える", _smallStyle);
            GUILayout.Space(18f);

            if (_choiceOption == null)
            {
                GUILayout.Label(_choicePrompt.Situation, _centerStyle, GUILayout.Height(112f));
                GUILayout.Space(18f);
                for (int i = 0; i < _choicePrompt.Options.Count; i++)
                {
                    // 一度でも選んだ選択肢には地味な印だけを付ける。
                    // 分かりすぎると緊張感が消え、分からなすぎると同じ事故を繰り返して嫌になる。
                    bool seen = StoryChoicePolicy.HasChosen(
                        _save.resolvedStoryEntityIds,
                        _choicePrompt.Id,
                        i);
                    string label = seen
                        ? $"{i + 1:00}　{_choicePrompt.Options[i].Text}　◇"
                        : $"{i + 1:00}　{_choicePrompt.Options[i].Text}";
                    if (GUILayout.Button(label, _buttonStyle)) SelectChoiceOption(i);
                    GUILayout.Space(6f);
                }
            }
            else
            {
                GUILayout.Label(
                    _choiceOption.Lines[_choiceLineIndex],
                    _centerStyle,
                    GUILayout.Height(180f));
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    $"{_choiceLineIndex + 1} / {_choiceOption.Lines.Count}",
                    _smallStyle);
                if (GUILayout.Button(
                        _choiceLineIndex + 1 < _choiceOption.Lines.Count ? "次へ" : "……",
                        _buttonStyle))
                    AdvanceChoiceLine();
            }

            GUILayout.Space(22f);
            GUILayout.EndArea();
        }

        /// <summary>
        /// 破滅画面。本気のゲームオーバー演出を出すが、進行は一切失われていない。
        /// </summary>
        private void DrawDownfall()
        {
            DrawFullScreenTint(new Color(0.05f, 0.01f, 0.02f, 1f));
            float width = Mathf.Min(620f, Screen.width - 40f);
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, (Screen.height - 330f) * 0.5f, width, 330f),
                _panelStyle);
            GUILayout.Space(32f);
            GUILayout.Label("GAME OVER", _heroTitleStyle);
            GUILayout.Space(20f);
            GUILayout.Label(
                "そこで、記憶は途切れている。\nだが、まだ何も失われてはいない。",
                _centerStyle,
                GUILayout.Height(90f));
            GUILayout.Space(24f);
            if (GUILayout.Button("あの場面からやり直す", _buttonStyle)) RetryFromDownfall();
            if (GUILayout.Button("タイトルへ", _buttonStyle)) ShowTitle();
            GUILayout.Space(20f);
            GUILayout.EndArea();
        }

        private void DrawChapterStory()
        {
            if (_chapterStoryBeat == null) return;
            Texture2D background = Resources.Load<Texture2D>(
                $"Art/Battle/Backgrounds/{_chapterStoryBeat.BackgroundId}") ??
                Resources.Load<Texture2D>("Art/Battle/Backgrounds/forest_ruins");
            if (background != null)
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    background,
                    ScaleMode.ScaleAndCrop);
            DrawFullScreenTint(new Color(0.015f, 0.025f, 0.055f, 0.66f));

            float width = Mathf.Min(960f, Screen.width - 48f);
            float height = Mathf.Min(600f, Screen.height - 64f);
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                _panelStyle);
            GUILayout.Space(34f);
            GUILayout.Label(_chapterStoryBeat.Title, _heroTitleStyle);
            GUILayout.Label(_chapterStoryBeat.Subtitle, _titleStyle);
            GUILayout.Space(48f);
            GUILayout.Label(
                _chapterStoryBeat.Lines[_chapterStoryLineIndex],
                _centerStyle,
                GUILayout.Height(150f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{_chapterStoryLineIndex + 1} / {_chapterStoryBeat.Lines.Count}",
                _smallStyle);
            string buttonLabel = _chapterStoryLineIndex + 1 < _chapterStoryBeat.Lines.Count
                ? "次へ"
                : $"第{_stageIndex + 1}章の探索へ";
            if (GUILayout.Button(buttonLabel, _buttonStyle)) AdvanceChapterStory();
            GUILayout.Space(24f);
            GUILayout.EndArea();
        }

        private void DrawStoryExploration()
        {
            if (_storyExploration == null) return;
            bool town = _storyExploration.Area == StoryAreaKind.Town;
            bool interior = _storyExploration.Area == StoryAreaKind.Interior;
            bool inn = _storyExploration.Area == StoryAreaKind.Inn;
            bool homeBase = _storyExploration.Area == StoryAreaKind.Base;
            Texture2D background = Resources.Load<Texture2D>(
                town
                    ? "Art/Story/story_town"
                    : interior
                        ? "Art/Story/story_atelier"
                        : inn
                            ? "Art/Story/story_inn"
                            : homeBase
                                ? "Art/Story/story_base"
                            : "Art/Story/story_dungeon");
            if (background == null)
            {
                background = Resources.Load<Texture2D>(
                    town || interior || inn || homeBase
                        ? "Art/Battle/Backgrounds/castle"
                        : "Art/Battle/Backgrounds/forest_ruins");
            }
            if (background != null)
            {
                GUI.color = new Color(0.72f, 0.78f, 0.86f, 1f);
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    background,
                    ScaleMode.ScaleAndCrop);
            }
            DrawFullScreenTint(new Color(
                town ? 0.03f : interior ? 0.055f : inn ? 0.045f : homeBase ? 0.035f : 0.015f,
                town ? 0.055f : interior ? 0.035f : inn ? 0.035f : homeBase ? 0.045f : 0.025f,
                town ? 0.075f : interior ? 0.025f : inn ? 0.025f : homeBase ? 0.045f : 0.055f,
                town ? 0.36f : interior ? 0.44f : inn ? 0.42f : homeBase ? 0.40f : 0.56f));

            float sideWidth = Mathf.Min(345f, Screen.width * 0.30f);
            Rect map = new Rect(
                22f,
                82f,
                Mathf.Max(520f, Screen.width - sideWidth - 58f),
                Mathf.Max(390f, Screen.height - 112f));
            GUI.color = Color.white;
            if (background != null)
                GUI.DrawTexture(map, background, ScaleMode.ScaleAndCrop);
            GUI.color = new Color(0.025f, 0.045f, 0.055f, 0.42f);
            GUI.DrawTexture(map, Texture2D.whiteTexture);
            DrawGuiFrame(
                map,
                town
                    ? new Color(0.38f, 0.85f, 0.82f, 0.92f)
                    : interior
                        ? new Color(0.94f, 0.70f, 0.34f, 0.92f)
                        : inn
                            ? new Color(0.96f, 0.72f, 0.42f, 0.92f)
                            : homeBase
                                ? new Color(0.96f, 0.82f, 0.48f, 0.92f)
                        : new Color(0.58f, 0.50f, 0.92f, 0.92f),
                4f);

            foreach (StoryWalkableZone zone in _storyExploration.WalkableZones)
            {
                Rect route = StoryWorldRect(map, zone);
                GUI.color = new Color(
                    town ? 0.20f : interior || inn || homeBase ? 0.68f : 0.34f,
                    town ? 0.72f : interior || inn || homeBase ? 0.44f : 0.30f,
                    town ? 0.70f : interior || inn || homeBase ? 0.18f : 0.72f,
                    0.04f);
                GUI.DrawTexture(route, Texture2D.whiteTexture);
            }

            foreach (StoryObstacle obstacle in _storyExploration.Obstacles)
            {
                Rect obstacleRect = StoryWorldRect(map, obstacle);
                GUI.color = town
                    ? new Color(0.20f, 0.25f, 0.27f, 0.08f)
                    : interior
                        ? new Color(0.25f, 0.16f, 0.08f, 0.08f)
                        : inn
                            ? new Color(0.24f, 0.13f, 0.065f, 0.08f)
                            : new Color(0.12f, 0.10f, 0.18f, 0.08f);
                GUI.DrawTexture(obstacleRect, Texture2D.whiteTexture);
                DrawGuiFrame(
                    obstacleRect,
                    town
                        ? new Color(0.48f, 0.58f, 0.58f, 0.18f)
                        : interior
                            ? new Color(0.72f, 0.52f, 0.24f, 0.18f)
                            : inn
                                ? new Color(0.76f, 0.50f, 0.24f, 0.18f)
                                : new Color(0.44f, 0.38f, 0.60f, 0.18f),
                    1f);
            }

            StoryEntity[] visible = _storyExploration.Entities
                .Where(entity => !entity.IsResolved)
                .OrderBy(entity => entity.Y)
                .ToArray();
            Vector2 storyPosition = new Vector2(
                _storyExploration.PlayerX,
                _storyExploration.PlayerY);
            Vector2 storyFacing = _storyFacing.sqrMagnitude > 0.001f
                ? _storyFacing.normalized
                : Vector2.down;
            Vector2 storySide = new Vector2(-storyFacing.y, storyFacing.x);
            Vector2 partnerPosition = ClampStoryFollower(
                storyPosition - storyFacing * 0.060f + storySide * 0.026f);
            Vector2 azukiPosition = ClampStoryFollower(
                storyPosition - storyFacing * 0.112f - storySide * 0.028f);

            foreach (StoryEntity entity in visible.Where(entity =>
                         entity.Y <= _storyExploration.PlayerY))
                DrawStoryEntity(map, entity);

            DrawStoryFollowerIfBehind(map, "partner", partnerPosition, true);
            DrawStoryFollowerIfBehind(map, "azuki", azukiPosition, true);

            Vector2 storyPlayerPoint = StoryWorldPoint(
                map,
                _storyExploration.PlayerX,
                _storyExploration.PlayerY);
            if (!DrawPixelFieldActor(
                    storyPlayerPoint,
                    "hero",
                    _storyFacing,
                    _storyExploration.PlayerY,
                    _storyRunBlend,
                    false))
            {
                Texture2D hero = Resources.Load<Texture2D>("Art/Battle/Units/hero");
                DrawFieldActor(
                    storyPlayerPoint,
                    hero,
                    false,
                    _storyExploration.PlayerY,
                    _storyRunBlend);
            }

            DrawStoryFollowerIfBehind(map, "partner", partnerPosition, false);
            DrawStoryFollowerIfBehind(map, "azuki", azukiPosition, false);

            foreach (StoryEntity entity in visible.Where(entity =>
                         entity.Y > _storyExploration.PlayerY))
                DrawStoryEntity(map, entity);

            if (_storyHasMoveTarget)
            {
                Vector2 target = StoryWorldPoint(
                    map,
                    _storyMoveTarget.x,
                    _storyMoveTarget.y);
                float pulse = 16f + Mathf.Sin(_fieldPulse * 5f) * 4f;
                DrawGuiFrame(
                    new Rect(
                        target.x - pulse,
                        target.y - pulse,
                        pulse * 2f,
                        pulse * 2f),
                    new Color(0.36f, 0.96f, 0.88f, 0.92f),
                    3f);
            }

            if (!_storyDialogueOpen &&
                !_storyRecruitmentCardOpen &&
                Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                map.Contains(Event.current.mousePosition))
            {
                _storyMoveTarget = new Vector2(
                    Mathf.Clamp01((Event.current.mousePosition.x - map.x) / map.width),
                    Mathf.Clamp01((Event.current.mousePosition.y - map.y) / map.height));
                _storyHasMoveTarget = true;
                Event.current.Use();
            }

            GUILayout.BeginArea(
                new Rect(
                    Screen.width - sideWidth - 20f,
                    82f,
                    sideWidth,
                    Screen.height - 112f),
                _panelStyle);
            GUILayout.Label(
                town
                    ? "水鏡の町"
                    : interior
                        ? "思い出工房"
                        : inn
                            ? "湖畔の宿"
                            : homeBase
                                ? "灯の館"
                        : "追憶の礼拝堂",
                _heroTitleStyle);
            GUILayout.Label(
                town
                    ? "TOWN EXPLORATION"
                    : interior
                        ? "INTERIOR EXPLORATION"
                        : inn
                            ? "INN EXPLORATION"
                            : homeBase
                                ? "HOME BASE"
                        : "DUNGEON EXPLORATION",
                _centerStyle);
            GUILayout.Space(12f);
            GUILayout.Label(_storyNotice, _labelStyle);
            GUILayout.Space(12f);
            if (!_storyDialogueOpen &&
                _pendingStoryPassage != null &&
                GUILayout.Button(
                    $"{StoryPassageActionLabel(_pendingStoryPassage)}  [E / Enter]",
                    _buttonStyle))
            {
                ConfirmStoryPassage();
            }
            if (_pendingStoryPassage != null) GUILayout.Space(8f);
            GUILayout.Label(
                $"{StoryTimeLabel(_storyExploration.TimeOfDay)} " +
                $"{_storyExploration.StoryClockMinutes / 60:00}:" +
                $"{_storyExploration.StoryClockMinutes % 60:00}",
                _centerStyle);
            if (!_storyDialogueOpen &&
                GUILayout.Button("1時間待つ", _buttonStyle))
            {
                _storyExploration.WaitMinutes(60);
                _save = CampaignSavePolicy.StoreStoryClock(
                    _save,
                    _storyExploration.StoryClockMinutes,
                    _catalog.stages);
                PersistSave();
                _storyNotice = "時間が進み、住民たちの居場所と話題が変わった。";
            }
            GUILayout.Space(8f);
            GUILayout.Label(
                "WASD／矢印キー：移動\n地面クリック：目的地へ移動",
                _smallStyle);
            GUILayout.Space(12f);

            StoryEntity objective = StoryObjective();
            if (objective != null &&
                GUILayout.Button(
                    StoryObjectiveLabel(objective),
                    _buttonStyle))
            {
                _storyMoveTarget = new Vector2(objective.X, objective.Y);
                _storyHasMoveTarget = true;
            }

            GUILayout.FlexibleSpace();
            bool hasArcher = CampaignSavePolicy.HasRecruited(
                _save,
                RecruitmentRosterPolicy.MemoryArcherId);
            bool hasHealer = CampaignSavePolicy.HasRecruited(
                _save,
                RecruitmentRosterPolicy.MemoryHealerId);
            bool hasMinstrel = CampaignSavePolicy.HasRecruited(
                _save,
                RecruitmentRosterPolicy.MemoryMinstrelId);
            GUILayout.Label(
                $"仲間名簿　{(hasArcher ? 1 : 0) + (hasHealer ? 1 : 0) + (hasMinstrel ? 1 : 0)}／3\n" +
                $"{(hasArcher ? "記憶の射手" : "？？？？")}／" +
                $"{(hasHealer ? "記憶の癒し手" : "？？？？")}／" +
                $"{(hasMinstrel ? "記憶の吟遊詩人" : "？？？？")}",
                _centerStyle);
            GUILayout.Label(
                $"探索宝物　{_save.storyTreasureCount}／4",
                _centerStyle);
            GUILayout.Space(12f);
            if (GUILayout.Button("タイトルへ戻る", _buttonStyle))
            {
                PersistSave();
                ShowTitle();
            }
            GUILayout.EndArea();

            GUI.color = Color.white;
            GUI.Label(
                new Rect(24f, 18f, Screen.width - 48f, 52f),
                town
                    ? "町で手掛かりを集め、仲間の待つ場所へ"
                    : interior
                        ? "町の暮らしと、大切に保管された思い出"
                        : inn
                            ? "旅立ち前のひとときと、帰還を待つ人々"
                            : homeBase
                                ? "集めた灯が同じ場所へ帰り、次の旅を待っている"
                        : "会話と探索の先に、新しい仲間が待っている",
                _titleStyle);

            if (_storyDialogueOpen) DrawStoryDialogue();
            if (_storyRecruitmentCardOpen) DrawRecruitmentCard();
        }

        private StoryEntity StoryObjective()
        {
            if (_storyExploration == null) return null;
            if (_storyExploration.Area == StoryAreaKind.Interior)
            {
                StoryEntity treasure = _storyExploration.FindEntity("interior-keepsake");
                return treasure != null && !treasure.IsResolved
                    ? treasure
                    : _storyExploration.FindEntity("interior-exit");
            }
            if (_storyExploration.Area == StoryAreaKind.Dungeon)
            {
                StoryEntity archer = _storyExploration.FindEntity(
                    "dungeon-memory-archer");
                if (archer != null && archer.Kind == StoryEntityKind.Recruit)
                    return archer;
                StoryEntity healer = _storyExploration.FindEntity(
                    "dungeon-memory-healer");
                if (healer != null && healer.Kind == StoryEntityKind.Recruit)
                    return healer;
                return _storyExploration.FindEntity("dungeon-relic-chest");
            }
            if (_storyExploration.Area == StoryAreaKind.Inn)
            {
                StoryEntity minstrel = _storyExploration.FindEntity("inn-minstrel");
                if (minstrel != null && minstrel.Kind == StoryEntityKind.Recruit)
                    return minstrel;
                StoryEntity host = _storyExploration.FindEntity("inn-host");
                if (host != null && !host.IsResolved) return host;
                return minstrel != null && !minstrel.IsResolved
                    ? minstrel
                    : _storyExploration.FindEntity("inn-exit");
            }
            if (_storyExploration.Area == StoryAreaKind.Base)
            {
                StoryEntity companion = _storyExploration.Entities.FirstOrDefault(entity =>
                    entity.Kind == StoryEntityKind.Dialogue &&
                    !entity.IsResolved);
                return companion ?? _storyExploration.FindEntity("base-exit");
            }
            if (!_save.townGuideHeard)
                return _storyExploration.FindEntity("town-guide");
            if (!CampaignSavePolicy.HasResolvedStoryEntity(
                    _save,
                    "interior-keepsake"))
                return _storyExploration.FindEntity("town-atelier-door");
            if (!CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryMinstrelId))
                return _storyExploration.FindEntity("town-inn-door");
            StoryEntity baseDoor = _storyExploration.FindEntity("town-base-door");
            if (baseDoor != null &&
                !CampaignSavePolicy.HasResolvedStoryEntity(
                    _save,
                    "base-recordkeeper"))
                return baseDoor;
            return _storyExploration.FindEntity("town-dungeon-gate");
        }

        private static string StoryObjectiveLabel(StoryEntity objective)
        {
            switch (objective?.Id)
            {
                case "town-guide": return "旅の案内人へ向かう";
                case "town-atelier-door": return "思い出工房へ向かう";
                case "interior-keepsake": return "工房の小箱へ向かう";
                case "interior-exit": return "町へ戻る";
                case "town-inn-door": return "湖畔の宿へ向かう";
                case "inn-minstrel": return "旅の楽師へ向かう";
                case "inn-host": return "宿主へ向かう";
                case "inn-exit": return "町へ戻る";
                case "town-base-door": return "灯の館へ向かう";
                case "base-recordkeeper": return "灯の名簿を見る";
                case "base-memory-archer": return "記憶の射手と話す";
                case "base-memory-healer": return "記憶の癒し手と話す";
                case "base-memory-minstrel": return "記憶の吟遊詩人と話す";
                case "base-exit": return "町へ戻る";
                case "town-dungeon-gate": return "北東門へ向かう";
                default: return "奥の人影へ向かう";
            }
        }

        private static string StoryPassageActionLabel(StoryEntity passage)
        {
            if (passage == null) return "移動する";
            if (passage.Kind == StoryEntityKind.Dialogue ||
                passage.Kind == StoryEntityKind.Recruit)
                return "話す";
            if (passage.Kind == StoryEntityKind.Treasure)
                return "調べる";
            switch (passage.Id)
            {
                case "town-atelier-door": return "工房へ入る";
                case "town-inn-door": return "宿へ入る";
                case "town-base-door": return "灯の館へ入る";
                case "town-dungeon-gate": return "北東門を出る";
                default: return "町へ戻る";
            }
        }

        private void DrawStoryEntity(Rect map, StoryEntity entity)
        {
            Vector2 point = StoryWorldPoint(map, entity.X, entity.Y);
            if (entity.Kind == StoryEntityKind.Passage)
            {
                bool lockedGate = string.Equals(
                                      entity.Id,
                                      "town-dungeon-gate",
                                      StringComparison.Ordinal) &&
                                  !_save.townGuideHeard;
                float pulse = 1f + Mathf.Sin(_fieldPulse * 3.2f) * 0.08f;
                Rect gate = new Rect(
                    point.x - 42f * pulse,
                    point.y - 78f * pulse,
                    84f * pulse,
                    78f * pulse);
                GUI.color = lockedGate
                    ? new Color(0.30f, 0.34f, 0.38f, 0.88f)
                    : ReferenceEquals(entity, _pendingStoryPassage)
                        ? new Color(1f, 0.72f, 0.24f, 0.92f)
                        : new Color(0.28f, 0.88f, 0.84f, 0.78f);
                GUI.DrawTexture(gate, Texture2D.whiteTexture);
                DrawGuiFrame(
                    gate,
                    lockedGate
                        ? new Color(0.54f, 0.58f, 0.62f, 0.94f)
                        : new Color(0.74f, 1f, 0.94f, 0.96f),
                    4f);
            }
            else if (entity.Kind == StoryEntityKind.Treasure)
            {
                float pulse = 1f + Mathf.Sin(_fieldPulse * 4.2f) * 0.06f;
                Rect chest = new Rect(
                    point.x - 34f * pulse,
                    point.y - 28f * pulse,
                    68f * pulse,
                    48f * pulse);
                GUI.color = new Color(0.72f, 0.42f, 0.12f, 0.96f);
                GUI.DrawTexture(chest, Texture2D.whiteTexture);
                DrawGuiFrame(
                    chest,
                    new Color(1f, 0.84f, 0.34f, 0.98f),
                    4f);
            }
            else
            {
                string resourcePath = StoryNpcArtPolicy.ResourcePathForEntity(entity.Id);
                Texture2D texture = string.IsNullOrWhiteSpace(resourcePath)
                    ? null
                    : Resources.Load<Texture2D>(resourcePath);
                if (texture == null)
                    texture = Resources.Load<Texture2D>("Art/Battle/Units/c_cleric");
                if (texture == null)
                    texture = Resources.Load<Texture2D>("Art/Battle/Units/partner");
                DrawFieldActor(point, texture, false, entity.Y);
            }

            GUI.color = entity.Kind == StoryEntityKind.Recruit ||
                        entity.Kind == StoryEntityKind.Treasure
                ? new Color(1f, 0.82f, 0.38f, 1f)
                : new Color(0.56f, 0.95f, 0.92f, 1f);
            GUI.Label(
                new Rect(point.x - 110f, point.y + 12f, 220f, 28f),
                entity.DisplayName,
                _centerStyle);
            GUI.color = Color.white;
        }

        private static Vector2 ClampStoryFollower(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 0.025f, 0.975f),
                Mathf.Clamp(position.y, 0.025f, 0.975f));
        }

        private void DrawStoryFollowerIfBehind(
            Rect map,
            string sourceUnitId,
            Vector2 position,
            bool behindPlayer)
        {
            bool isBehind = position.y <= _storyExploration.PlayerY;
            if (isBehind != behindPlayer) return;
            Vector2 point = StoryWorldPoint(map, position.x, position.y);
            DrawPixelFieldActor(
                point,
                sourceUnitId,
                _storyFacing,
                position.y,
                _storyRunBlend,
                false);
        }

        private void DrawStoryDialogue()
        {
            GUI.color = new Color(0.005f, 0.012f, 0.022f, 0.22f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = Mathf.Min(1120f, Screen.width - 48f);
            float height = Mathf.Min(270f, Screen.height * 0.34f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - height - 24f,
                width,
                height);
            GUI.color = new Color(0.012f, 0.026f, 0.046f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            DrawGuiFrame(panel, new Color(0.56f, 0.90f, 0.86f, 0.92f), 3f);
            GUI.color = Color.white;

            float portraitWidth = Mathf.Min(190f, panel.width * 0.20f);
            string portraitPath = _pendingStoryEntity == null
                ? null
                : StoryNpcArtPolicy.ResourcePathForEntity(_pendingStoryEntity.Id);
            Texture2D portrait = string.IsNullOrWhiteSpace(portraitPath)
                ? null
                : Resources.Load<Texture2D>(portraitPath);
            if (portrait != null)
            {
                Rect portraitRect = new Rect(
                    panel.x + 10f,
                    panel.y + 10f,
                    portraitWidth - 20f,
                    panel.height - 20f);
                GUI.DrawTexture(portraitRect, portrait, ScaleMode.ScaleToFit, true);
            }

            float textLeft = portrait == null ? panel.x + 28f : panel.x + portraitWidth;
            GUILayout.BeginArea(new Rect(
                textLeft,
                panel.y + 12f,
                panel.xMax - textLeft - 24f,
                panel.height - 24f));
            GUILayout.Label(
                _pendingStoryEntity?.DisplayName ?? "会話",
                _titleStyle);
            GUILayout.Space(8f);
            string line = _storyDialogueLines.Length == 0
                ? string.Empty
                : _storyDialogueLines[Mathf.Clamp(
                    _storyDialogueIndex,
                    0,
                    _storyDialogueLines.Length - 1)];
            GUILayout.Label(line, _labelStyle, GUILayout.MinHeight(92f));
            GUILayout.FlexibleSpace();
            string button = _storyDialogueIndex + 1 < _storyDialogueLines.Length
                ? "次へ"
                : _pendingStoryEntity != null &&
                  _pendingStoryEntity.Kind == StoryEntityKind.Recruit
                    ? "仲間として迎える"
                    : _pendingStoryEntity != null &&
                      string.Equals(
                          _pendingStoryEntity.Id,
                          "town-guide",
                          StringComparison.Ordinal)
                        ? "礼拝堂へ向かう"
                        : "話を終える";
            if (GUILayout.Button(button, _buttonStyle))
                AdvanceStoryDialogue();
            GUILayout.EndArea();
        }

        private void DrawRecruitmentCard()
        {
            BaseSupportResident support = BaseGrowthPolicy.FindBySourceEntityId(
                _recentRecruitUnitId);
            bool supportMember = support != null;
            bool healer = string.Equals(
                _recentRecruitUnitId,
                RecruitmentRosterPolicy.MemoryHealerId,
                StringComparison.Ordinal);
            bool minstrel = string.Equals(
                _recentRecruitUnitId,
                RecruitmentRosterPolicy.MemoryMinstrelId,
                StringComparison.Ordinal);
            string unitId = _recentRecruitUnitId;
            string role = supportMember
                ? support.Role
                : minstrel
                ? "帰還の旋律を奏でる楽師"
                : healer
                    ? "記憶を灯す癒し手"
                    : "約束を守る弓手";
            string name = supportMember
                ? support.Name
                : minstrel
                ? "記憶の吟遊詩人"
                : healer
                    ? "記憶の癒し手"
                    : "記憶の射手";
            string quote = supportMember
                ? support.Quote
                : minstrel
                ? "「みんなの足音を、帰還の歌につないでみせるよ」"
                : healer
                    ? "「傷ついた記憶も、皆さんとなら癒していけます」"
                    : "「今度は私も、みんなの帰る場所を守る」";
            string description = supportMember
                ? support.Description
                : minstrel
                ? "音の魔法で敵を攻める支援役。次の戦闘準備から編成に加わります。"
                : healer
                    ? "仲間を支える回復役。次の戦闘準備から編成に加わります。"
                    : "遠隔攻撃を得意とする仲間。次の戦闘準備から編成に加わります。";
            bool prologueComplete =
                CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryArcherId) &&
                CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryHealerId);
            GUI.color = new Color(0.005f, 0.01f, 0.02f, 0.90f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = Mathf.Min(760f, Screen.width - 40f);
            float height = Mathf.Min(620f, Screen.height - 40f);
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                _panelStyle);
            GUILayout.Space(16f);
            GUILayout.Label(supportMember ? "NEW LIGHT" : "NEW COMPANION", _titleStyle);
            GUILayout.Label(role, _centerStyle);
            Texture2D portrait = Resources.Load<Texture2D>(
                supportMember
                    ? support.ResourcePath
                    : $"Art/Battle/Units/{unitId}");
            if (portrait != null)
            {
                float imageHeight = Mathf.Min(300f, height * 0.48f);
                GUILayout.Label(
                    portrait,
                    GUILayout.Height(imageHeight),
                    GUILayout.ExpandWidth(true));
            }
            GUILayout.Label(name, _heroTitleStyle);
            GUILayout.Label(quote, _centerStyle);
            GUILayout.Label(description, _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    supportMember
                        ? "灯の館で新しい住人を迎える"
                        : minstrel
                        ? "宿で新しい仲間と話す"
                        : prologueComplete
                        ? "街道のフィールドへ出発"
                        : "礼拝堂の探索を続ける",
                    _buttonStyle))
            {
                _storyRecruitmentCardOpen = false;
                if (supportMember)
                {
                    ShowStoryArea(_storyExploration?.Area ?? StoryAreaKind.Town);
                }
                else if (minstrel)
                {
                    ShowStoryArea(StoryAreaKind.Inn);
                }
                else if (prologueComplete)
                {
                    ShowField(
                        _save.stageIndex,
                        "記憶の仲間が同行中。敵シンボルへ接触すると戦闘準備に参加します。");
                }
                else
                {
                    ShowStoryArea(StoryAreaKind.Dungeon);
                }
            }
            GUILayout.EndArea();
        }

        private static Vector2 StoryWorldPoint(Rect map, float x, float y)
        {
            return new Vector2(
                map.x + map.width * x,
                map.y + map.height * y);
        }

        private static Rect StoryWorldRect(Rect map, StoryObstacle obstacle)
        {
            return new Rect(
                map.x + map.width * obstacle.MinX,
                map.y + map.height * obstacle.MinY,
                map.width * (obstacle.MaxX - obstacle.MinX),
                map.height * (obstacle.MaxY - obstacle.MinY));
        }

        private static Rect StoryWorldRect(Rect map, StoryWalkableZone zone)
        {
            return new Rect(
                map.x + map.width * zone.MinX,
                map.y + map.height * zone.MinY,
                map.width * (zone.MaxX - zone.MinX),
                map.height * (zone.MaxY - zone.MinY));
        }

        private static string StoryTimeLabel(StoryTimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case StoryTimeOfDay.Morning:
                    return "朝";
                case StoryTimeOfDay.Afternoon:
                    return "昼";
                case StoryTimeOfDay.Evening:
                    return "夕";
                default:
                    return "夜";
            }
        }

        private void DrawField()
        {
            Texture2D background = Resources.Load<Texture2D>(
                $"Art/Battle/Backgrounds/{_catalog.stages[_stageIndex].backgroundId}") ??
                Resources.Load<Texture2D>("Art/Battle/Backgrounds/forest_ruins");
            if (background != null)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), background, ScaleMode.ScaleAndCrop);
            DrawFullScreenTint(new Color(0.012f, 0.026f, 0.038f, 0.44f));
            if (_fieldMap == null || _fieldExploration == null) return;

            float width = Mathf.Min(1500f, Screen.width - 28f);
            float height = Mathf.Min(940f, Screen.height - 22f);
            Rect area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.color = new Color(0.008f, 0.018f, 0.030f, 0.86f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(area.x + 20f, area.y + 18f, area.width - 40f, 48f),
                $"第{_stageIndex + 1}章　EXPLORATION FIELD", _heroTitleStyle);
            GUI.Label(new Rect(area.x + 20f, area.y + 66f, area.width - 40f, 32f),
                $"{_catalog.stages[_stageIndex].displayName}　推奨Lv {_catalog.stages[_stageIndex].recommendedLevel}　WASD／方向キー／クリックで移動",
                _centerStyle);

            Rect map = new Rect(area.x + 32f, area.y + 108f, area.width - 64f, area.height - 246f);
            if (background != null)
                GUI.DrawTexture(map, background, ScaleMode.ScaleAndCrop);
            GUI.color = new Color(0.015f, 0.045f, 0.055f, 0.48f);
            GUI.DrawTexture(map, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawGuiFrame(map, new Color(0.58f, 0.74f, 0.70f, 0.60f), 2f);

            DrawTileFieldGround(map);

            foreach (FieldObstacle obstacle in _fieldExploration.Obstacles)
            {
                Rect obstacleRect = FieldWorldRect(map, obstacle);
                GUI.color = new Color(0.02f, 0.025f, 0.028f, 0.78f);
                GUI.DrawTexture(new Rect(
                    obstacleRect.x + 5f,
                    obstacleRect.y + 7f,
                    obstacleRect.width,
                    obstacleRect.height), Texture2D.whiteTexture);
                GUI.color = new Color(0.18f, 0.22f, 0.20f, 0.92f);
                GUI.DrawTexture(obstacleRect, Texture2D.whiteTexture);
                DrawGuiFrame(obstacleRect, new Color(0.48f, 0.55f, 0.47f, 0.55f), 2f);
                GUI.color = Color.white;
                GUI.Label(obstacleRect, "瓦礫", _smallStyle);
            }

            Vector2 playerPoint = FieldWorldPoint(
                map,
                _fieldExploration.PlayerX,
                _fieldExploration.PlayerY);
            FieldEntity[] visibleEntities = _fieldExploration.Entities
                .Where(entity => !entity.IsResolved)
                .OrderBy(entity => entity.Y)
                .ThenBy(entity => entity.Id, StringComparer.Ordinal)
                .ToArray();
            foreach (FieldEntity entity in visibleEntities.Where(entity => entity.Y <= _fieldExploration.PlayerY))
                DrawFieldEntity(map, entity);
            if (!DrawPixelFieldActor(
                    playerPoint,
                    "hero",
                    _fieldFacing,
                    _fieldExploration.PlayerY,
                    _fieldRunBlend,
                    false))
            {
                Texture2D playerTexture = Resources.Load<Texture2D>("Art/Battle/Units/hero");
                DrawFieldActor(playerPoint, playerTexture, false, _fieldExploration.PlayerY, _fieldRunBlend);
            }
            foreach (FieldEntity entity in visibleEntities.Where(entity => entity.Y > _fieldExploration.PlayerY))
                DrawFieldEntity(map, entity);

            if (_fieldHasMoveTarget)
            {
                Vector2 target = FieldWorldPoint(map, _fieldMoveTarget.x, _fieldMoveTarget.y);
                float targetPulse = 13f + Mathf.Sin(_fieldPulse * 7f) * 4f;
                DrawGuiFrame(
                    new Rect(target.x - targetPulse, target.y - targetPulse, targetPulse * 2f, targetPulse * 2f),
                    new Color(0.30f, 0.90f, 1f, 0.90f),
                    3f);
            }

            Event currentEvent = Event.current;
            if (!_npcEventOpen &&
                currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                map.Contains(currentEvent.mousePosition))
            {
                _fieldMoveTarget = new Vector2(
                    Mathf.Clamp01((currentEvent.mousePosition.x - map.x) / map.width),
                    Mathf.Clamp01((currentEvent.mousePosition.y - map.y) / map.height));
                _fieldHasMoveTarget = true;
                _fieldNotice = "指定地点へ移動中。WASDまたは方向キーでいつでも直接操作できます。";
                currentEvent.Use();
            }

            float distance = _fieldExploration.DistanceToNearestEnemy();
            int visibleEnemyCount = _fieldExploration.Entities.Count(
                entity => !entity.IsResolved && entity.Kind == FieldEntityKind.Enemy);
            int visibleTreasureCount = _fieldExploration.Entities.Count(
                entity => !entity.IsResolved && entity.Kind == FieldEntityKind.Treasure);
            int visibleNpcCount = _fieldExploration.Entities.Count(
                entity => !entity.IsResolved && entity.Kind == FieldEntityKind.Npc);
            ExpeditionBattleBonus activeBonus = ExpeditionBattleBonusPolicy.Create(
                _save.fieldTreasureCount,
                CampaignSavePolicy.FindFieldSupport(_save, _stageIndex));
            GUI.Label(new Rect(area.x + 32f, area.yMax - 133f, area.width - 64f, 28f),
                $"最寄りの敵まで {Mathf.CeilToInt(distance * 100f)}　敵 {visibleEnemyCount}／宝箱 {visibleTreasureCount}／NPC {visibleNpcCount}／物資 {_save.fieldTreasureCount}／{activeBonus.SupportName}",
                _centerStyle);
            GUI.Label(new Rect(area.x + 32f, area.yMax - 105f, area.width - 64f, 28f),
                _fieldNotice, _centerStyle);
            GUILayout.BeginArea(new Rect(area.x + 32f, area.yMax - 72f, area.width - 64f, 48f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("タイトルへ", _buttonStyle))
            {
                PersistFieldPosition();
                ShowTitle();
            }
            if (_catalog.warmaps != null && _catalog.warmaps.Length > 0 &&
                GUILayout.Button("大規模戦へ", _buttonStyle))
            {
                PersistFieldPosition();
                EnterWar();
            }
            if (GUILayout.Button("編成・装備", _buttonStyle))
            {
                PersistFieldPosition();
                RebuildStage(_stageIndex);
            }
            if (GUILayout.Button("敵シンボルへ自動接近", _buttonStyle)) AdvanceFieldTowardEncounter();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            if (_npcEventOpen) DrawNpcEvent();
        }

        private void DrawTileFieldGround(Rect map)
        {
            const int columns = 24;
            const int rows = 14;
            float tileWidth = map.width / columns;
            float tileHeight = map.height / rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    bool road = Mathf.Abs(y - (8 - x / 7)) <= 1 ||
                                (x > 15 && Mathf.Abs(y - 5) <= 1);
                    int variation = (x * 17 + y * 31 + _stageIndex * 7) % 5;
                    GUI.color = road
                        ? new Color(0.43f + variation * 0.012f, 0.36f, 0.23f, 0.80f)
                        : new Color(0.10f, 0.25f + variation * 0.012f, 0.18f, 0.78f);
                    GUI.DrawTexture(
                        new Rect(
                            map.x + x * tileWidth,
                            map.y + y * tileHeight,
                            tileWidth + 1f,
                            tileHeight + 1f),
                        Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
        }

        private void DrawNpcEvent()
        {
            GUI.color = new Color(0.005f, 0.012f, 0.022f, 0.82f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = Mathf.Min(680f, Screen.width - 36f);
            float height = 390f;
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                _panelStyle);
            GUILayout.Space(16f);
            GUILayout.Label($"NPC EVENT　{_pendingNpcName}", _heroTitleStyle);
            GUILayout.Label(
                "旅の軍師「敵主力へ挑む前に、一つだけ支援策を選ぶとよい」",
                _centerStyle);
            GUILayout.Space(12f);
            if (GUILayout.Button(
                    "偵察支援 — 敵の弱点を共有（味方攻撃力+1）",
                    _buttonStyle))
                ChooseNpcSupport(FieldSupportType.Recon);
            if (GUILayout.Button(
                    "救護支援 — 応急処置を準備（味方最大HP+15%）",
                    _buttonStyle))
                ChooseNpcSupport(FieldSupportType.Medical);
            if (GUILayout.Button(
                    "奇襲支援 — 先制配置を共有（味方攻撃力+20%）",
                    _buttonStyle))
                ChooseNpcSupport(FieldSupportType.Ambush);
            GUILayout.Space(12f);
            GUILayout.Label(
                "選択した支援はこの章の戦闘準備と戦闘に反映されます。",
                _smallStyle);
            GUILayout.EndArea();
        }

        private void ChooseNpcSupport(FieldSupportType support)
        {
            _save = CampaignSavePolicy.StoreFieldNpcSupport(
                _save,
                _pendingNpcEntityId,
                _stageIndex,
                support,
                _catalog.stages);
            ExpeditionBattleBonus bonus = ExpeditionBattleBonusPolicy.Create(
                _save.fieldTreasureCount,
                support);
            _npcEventOpen = false;
            _pendingNpcEntityId = string.Empty;
            _pendingNpcName = string.Empty;
            _fieldNotice = $"{bonus.SupportName}を受けました。{bonus.Description}";
            PersistFieldPosition();
            _audio.PlaySfx("select");
        }

        private void DrawWar()
        {
            Texture2D background = Resources.Load<Texture2D>("Art/Battle/Backgrounds/castle") ??
                                   Resources.Load<Texture2D>("Art/Battle/Backgrounds/forest_ruins");
            if (background != null)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), background, ScaleMode.ScaleAndCrop);
            DrawFullScreenTint(new Color(0.035f, 0.02f, 0.025f, 0.72f));
            if (_war == null) return;

            float width = Mathf.Min(1320f, Screen.width - 40f);
            float height = Mathf.Min(870f, Screen.height - 30f);
            Rect area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.color = new Color(0.03f, 0.025f, 0.035f, 0.94f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x + 20f, area.y + 18f, area.width - 40f, 48f),
                _war.DisplayName, _heroTitleStyle);
            GUI.Label(new Rect(area.x + 20f, area.y + 66f, area.width - 40f, 30f),
                $"WAR CAMPAIGN　ROUND {_war.Round + 1}/{_war.MaxRounds}　偵察可能 {_war.ScoutsRemaining}　敵の一戦線を調べて命令を出してください",
                _centerStyle);

            int cycleLane = -1;
            int scoutLane = -1;
            float laneWidth = (area.width - 100f) / 3f;
            for (int i = 0; i < _war.Lanes.Count; i++)
            {
                WarLaneState lane = _war.Lanes[i];
                Rect laneRect = new Rect(area.x + 34f + i * (laneWidth + 16f), area.y + 122f, laneWidth, 510f);
                GUI.Box(laneRect, GUIContent.none, _panelStyle);
                GUI.Label(new Rect(laneRect.x + 12f, laneRect.y + 18f, laneRect.width - 24f, 34f),
                    lane.Name, _titleStyle);
                GUI.Label(new Rect(laneRect.x + 12f, laneRect.y + 64f, laneRect.width - 24f, 28f),
                    lane.RevealedEnemyIntent.HasValue
                        ? $"敵予測：{WarOrderName(lane.RevealedEnemyIntent.Value)}"
                        : "敵予測：未偵察",
                    _centerStyle);
                if (_war.Winner == WarWinner.None &&
                    !lane.IsIntentRevealed &&
                    _war.ScoutsRemaining > 0 &&
                    GUI.Button(new Rect(laneRect.x + 18f, laneRect.y + 92f, laneRect.width - 36f, 34f),
                        "この戦線を偵察", _buttonStyle))
                    scoutLane = i;

                GUI.Label(new Rect(laneRect.x + 18f, laneRect.y + 140f, laneRect.width - 36f, 24f),
                    $"味方戦力 {lane.PlayerStrength}", _smallStyle);
                DrawBar(new Rect(laneRect.x + 18f, laneRect.y + 168f, laneRect.width - 36f, 16f),
                    lane.PlayerStrength / 90f, new Color(0.18f, 0.82f, 0.70f));
                GUI.Label(new Rect(laneRect.x + 18f, laneRect.y + 204f, laneRect.width - 36f, 24f),
                    $"敵戦力 {lane.EnemyStrength}", _smallStyle);
                DrawBar(new Rect(laneRect.x + 18f, laneRect.y + 232f, laneRect.width - 36f, 16f),
                    lane.EnemyStrength / 90f, new Color(0.92f, 0.27f, 0.32f));

                GUI.Label(new Rect(laneRect.x + 18f, laneRect.y + 274f, laneRect.width - 36f, 26f),
                    $"戦線支配 {ControlName(lane.Control)}", _centerStyle);
                Rect control = new Rect(laneRect.x + 26f, laneRect.y + 312f, laneRect.width - 52f, 18f);
                GUI.color = new Color(0.16f, 0.18f, 0.22f, 1f);
                GUI.DrawTexture(control, Texture2D.whiteTexture);
                float normalizedControl = (lane.Control + 2f) / 4f;
                GUI.color = lane.Control >= 0
                    ? new Color(0.22f, 0.75f, 0.86f, 1f)
                    : new Color(0.90f, 0.28f, 0.34f, 1f);
                GUI.DrawTexture(new Rect(control.x, control.y, control.width * normalizedControl, control.height),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;

                Rect orderButton = new Rect(laneRect.x + 18f, laneRect.yMax - 104f, laneRect.width - 36f, 58f);
                if (_war.Winner == WarWinner.None &&
                    GUI.Button(orderButton, $"命令：{WarOrderName(lane.PlayerOrder)}", _buttonStyle))
                    cycleLane = i;
                GUI.Label(new Rect(laneRect.x + 16f, laneRect.yMax - 42f, laneRect.width - 32f, 28f),
                    WarOrderEffect(lane.PlayerOrder), _smallStyle);
            }

            if (scoutLane >= 0)
            {
                _war.RevealIntent(scoutLane);
                _audio.PlaySfx("select");
            }
            if (cycleLane >= 0) _war.CycleOrder(cycleLane);

            string report = _warReport == null
                ? "偵察できるのは各ラウンド一戦線です。未偵察の戦線は戦力と相性から推測してください。"
                : $"第{_warReport.Round}報　味方損耗 {_warReport.PlayerLosses}／敵損耗 {_warReport.EnemyLosses}／支配変動 {SignedValue(_warReport.ControlShift)}";
            GUI.Label(new Rect(area.x + 30f, area.yMax - 204f, area.width - 60f, 34f), report, _centerStyle);

            GUILayout.BeginArea(new Rect(area.x + 34f, area.yMax - 158f, area.width - 68f, 120f));
            if (_war.Winner == WarWinner.None)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("フィールドへ撤収", _buttonStyle)) ShowField(_stageIndex);
                if (GUILayout.Button("命令を確定して進軍", _buttonStyle)) ResolveWarRound();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(_war.Winner == WarWinner.Player ? "戦役勝利 — 敵司令部を制圧" : "戦役敗北 — 戦線を再編してください", _titleStyle);
                GUILayout.BeginHorizontal();
                if (_war.Winner == WarWinner.Player &&
                    GUILayout.Button("フィールドへ帰還", _buttonStyle))
                    ShowField(_stageIndex, "大規模戦に勝利しました。敵部隊への進路が開いています。");
                if (_war.Winner == WarWinner.Enemy &&
                    GUILayout.Button("戦役を再試行", _buttonStyle))
                    EnterWar();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void DrawPreparation()
        {
            DrawFullScreenTint(new Color(0.02f, 0.035f, 0.065f, 1f));
            if (_preparation == null || _stage == null) return;

            float width = Mathf.Min(1240f, Screen.width - 36f);
            float height = Mathf.Min(860f, Screen.height - 30f);
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height),
                _panelStyle);
            GUILayout.Label("編成・装備", _heroTitleStyle);
            GUILayout.Label(
                $"{_stage.displayName}　前列3名＋後列3名　フィールド移動中にいつでも変更できます",
                _centerStyle);
            FieldSupportType fieldSupport = CampaignSavePolicy.FindFieldSupport(
                _save,
                _stageIndex);
            ExpeditionBattleBonus fieldBonus = ExpeditionBattleBonusPolicy.Create(
                _save.fieldTreasureCount,
                fieldSupport);
            GUILayout.Label(
                $"遠征物資 {_save.fieldTreasureCount}　HP+{fieldBonus.SupplyHpBonus}／攻撃+{fieldBonus.SupplyDamageBonus}　" +
                $"{fieldBonus.SupportName}：{fieldBonus.Description}",
                _smallStyle);
            if (CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryArcherId))
            {
                GUILayout.Label(
                    "加入仲間　記憶の射手 — 遠隔攻撃担当として出撃編成に参加",
                    _smallStyle);
            }
            if (CampaignSavePolicy.HasRecruited(
                    _save,
                    RecruitmentRosterPolicy.MemoryMinstrelId))
            {
                GUILayout.Label(
                    "加入仲間　記憶の吟遊詩人 — 上位5名が出撃、6人目は控えとして隊列変更可能",
                    _smallStyle);
            }
            GUILayout.Space(10f);
            float scrollHeight = Mathf.Max(260f, height - 270f);
            _preparationScroll = GUILayout.BeginScrollView(
                _preparationScroll,
                false,
                true,
                GUILayout.Height(scrollHeight));
            string moveUnitId = null;
            int moveDirection = 0;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("PARTY", _titleStyle);
            for (int i = 0; i < _preparation.Loadouts.Count; i++)
            {
                UnitLoadout loadout = _preparation.Loadouts[i];
                StageUnitData unit = _stage.units.First(candidate => candidate.id == loadout.unitId);
                WeaponDefinition weapon = BattlePreparationCatalog.GetWeapon(loadout.weaponId);
                ArmorDefinition armor = ArmorEquipmentCatalog.GetArmor(loadout.armorId);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(74f));
                DrawEquipmentIcon((int)weapon.Id);
                DrawEquipmentIcon(8 + (int)armor.Id);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                string deployment = i < BattlePreparationState.MaxDeployedPlayers
                    ? "出撃"
                    : "控え";
                string row = i < BattlePreparationState.MaxDeployedPlayers
                    ? FormationPresentationProfile.GetFormationRow(loadout.formationSlot) == FormationRow.Front
                        ? "前列"
                        : "後列"
                    : "待機";
                GUILayout.Label(
                    $"{i + 1}. [{deployment}／{row}] {unit.displayName}　{PreparationClassName(unit.className)}　Lv {Mathf.Max(1, unit.level)}",
                    _labelStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", _buttonStyle, GUILayout.Width(48f)))
                {
                    moveUnitId = loadout.unitId;
                    moveDirection = -1;
                }
                GUI.enabled = i < _preparation.Loadouts.Count - 1;
                if (GUILayout.Button("▼", _buttonStyle, GUILayout.Width(48f)))
                {
                    moveUnitId = loadout.unitId;
                    moveDirection = 1;
                }
                GUI.enabled = true;
                if (GUILayout.Button($"武器：{weapon.DisplayName}", _buttonStyle))
                {
                    CycleWeapon(unit, loadout);
                    SavePreparation();
                }
                if (GUILayout.Button($"防具：{armor.DisplayName}", _buttonStyle))
                {
                    CycleArmor(unit, loadout);
                    SavePreparation();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(
                    $"武器：{AttackKindName(weapon.AttackKind)}／威力 {weapon.PowerPercent}%／射程 {weapon.Range}／速度 {SignedValue(weapon.SpeedModifier)}",
                    _smallStyle);
                GUILayout.Label(
                    $"防具：最大HP {armor.MaxHpPercent}%／軽減 {armor.DamageReductionPercent}%／速度 {SignedValue(armor.SpeedModifier)}　必殺：{weapon.SpecialName}",
                    _smallStyle);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (moveUnitId != null && _preparation.MoveUnit(moveUnitId, moveDirection))
                SavePreparation();

            GUILayout.Space(8f);
            if (GUILayout.Button("フィールドへ戻る", _buttonStyle)) ShowField(_stageIndex);
            GUILayout.EndArea();
        }

        private void CycleWeapon(StageUnitData unit, UnitLoadout loadout)
        {
            IReadOnlyList<WeaponDefinition> compatible =
                BattlePreparationCatalog.GetCompatibleWeapons(unit.className);
            int current = 0;
            for (int i = 0; i < compatible.Count; i++)
            {
                if (compatible[i].Id != loadout.weaponId) continue;
                current = i;
                break;
            }
            WeaponId next = compatible[(current + 1) % compatible.Count].Id;
            _preparation.SetWeapon(loadout.unitId, next);
        }

        private void CycleTactic(UnitLoadout loadout)
        {
            TacticPolicy next = (TacticPolicy)(((int)loadout.tactic + 1) % 3);
            _preparation.SetTactic(loadout.unitId, next);
        }

        private void CycleArmor(StageUnitData unit, UnitLoadout loadout)
        {
            IReadOnlyList<ArmorDefinition> compatible =
                ArmorEquipmentCatalog.GetCompatibleArmors(unit.className);
            int current = 0;
            for (int i = 0; i < compatible.Count; i++)
            {
                if (compatible[i].Id != loadout.armorId) continue;
                current = i;
                break;
            }
            ArmorId next = compatible[(current + 1) % compatible.Count].Id;
            _preparation.SetArmor(loadout.unitId, next);
        }

        private void DrawEquipmentIcon(int atlasIndex)
        {
            Rect rect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
            if (_equipmentIconAtlas == null) return;
            int column = atlasIndex % 4;
            int row = atlasIndex / 4;
            Rect uv = new Rect(column * 0.25f, 1f - (row + 1) * 0.25f, 0.25f, 0.25f);
            GUI.DrawTextureWithTexCoords(rect, _equipmentIconAtlas, uv, true);
        }

        private static string PreparationClassName(string className)
        {
            switch (className)
            {
                case "knight": return "騎士";
                case "cavalry": return "騎兵";
                case "archer": return "弓兵";
                case "flier": return "飛行";
                case "mage": return "魔導";
                case "cleric": return "回復";
                case "trickster": return "斥候";
                default: return className ?? string.Empty;
            }
        }

        private static string ReadinessName(PreparationReadiness readiness)
        {
            switch (readiness)
            {
                case PreparationReadiness.Ready: return "優勢";
                case PreparationReadiness.Contested: return "拮抗";
                default: return "要再編";
            }
        }

        private static string AttackKindName(FormationActionKind kind)
        {
            switch (kind)
            {
                case FormationActionKind.Melee: return "近接";
                case FormationActionKind.Ranged: return "遠隔";
                case FormationActionKind.Magic: return "魔法";
                default: return kind.ToString();
            }
        }

        private static string DangerName(EnemyDangerLevel danger)
        {
            switch (danger)
            {
                case EnemyDangerLevel.Standard: return "標準";
                case EnemyDangerLevel.High: return "高";
                case EnemyDangerLevel.Critical: return "重大";
                default: return danger.ToString();
            }
        }

        private static string TacticName(TacticPolicy tactic)
        {
            switch (tactic)
            {
                case TacticPolicy.Balanced: return "均衡";
                case TacticPolicy.Aggressive: return "攻勢";
                case TacticPolicy.Defensive: return "守勢";
                default: return tactic.ToString();
            }
        }

        private static string TacticEffectName(TacticPolicy tactic)
        {
            switch (tactic)
            {
                case TacticPolicy.Aggressive: return "与ダメ120%・被ダメ110%・弱った敵を優先";
                case TacticPolicy.Defensive: return "与ダメ85%・被物理/守勢ガード75%・狙われにくい";
                default: return "攻防100%・武器特性に従う";
            }
        }

        private static string SignedValue(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private static string StatusName(FormationStatus status)
        {
            switch (status)
            {
                case FormationStatus.Weakened: return "弱体：与ダメ80%";
                case FormationStatus.Exposed: return "崩し：被ダメ120%";
                case FormationStatus.Fortified: return "堅守：被ダメ80%";
                default: return "効果なし";
            }
        }

        private static Vector2 FieldNodePoint(Rect map, FieldNode node)
        {
            return new Vector2(
                map.x + map.width * node.X,
                map.y + map.height * node.Y);
        }

        private static Vector2 FieldWorldPoint(Rect map, float x, float y)
        {
            return new Vector2(
                map.x + map.width * x,
                map.y + map.height * y);
        }

        private static Rect FieldWorldRect(Rect map, FieldObstacle obstacle)
        {
            return new Rect(
                map.x + map.width * obstacle.MinX,
                map.y + map.height * obstacle.MinY,
                map.width * (obstacle.MaxX - obstacle.MinX),
                map.height * (obstacle.MaxY - obstacle.MinY));
        }

        private void DrawFieldEntity(Rect map, FieldEntity entity)
        {
            Vector2 point = FieldWorldPoint(map, entity.X, entity.Y);
            if (entity.Kind == FieldEntityKind.Enemy)
            {
                StageUnitData[] enemyUnits = _catalog.stages[_stageIndex].units
                    .Where(unit => string.Equals(
                        unit.team,
                        "enemy",
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                int enemyIndex = entity.Id.EndsWith("-scout", StringComparison.Ordinal) ? 1 : 0;
                StageUnitData enemyData = enemyUnits.Length == 0
                    ? null
                    : enemyUnits[enemyIndex % enemyUnits.Length];
                string assetId = enemyData == null || string.IsNullOrWhiteSpace(enemyData.sourceUnitId)
                    ? "e_knight"
                    : enemyData.sourceUnitId;
                Texture2D texture = Resources.Load<Texture2D>($"Art/Battle/Units/{assetId}");
                float pulse = 1f + Mathf.Sin(_fieldPulse * 3.4f + enemyIndex) * 0.06f;
                Rect alert = new Rect(
                    point.x - 68f * pulse,
                    point.y - 88f * pulse,
                    136f * pulse,
                    106f * pulse);
                DrawGuiFrame(alert, new Color(1f, 0.19f, 0.22f, 0.75f), 4f);
                if (!DrawPixelFieldActor(
                        point,
                        assetId,
                        new Vector2(-1f, 0f),
                        entity.Y,
                        0f,
                        true))
                    DrawFieldActor(point, texture, true, entity.Y);
                GUI.color = new Color(0.95f, 0.20f, 0.24f, 0.96f);
                GUI.Label(
                    new Rect(point.x - 125f, point.y + 18f, 250f, 28f),
                    $"{entity.DisplayName}　危険度 {entity.Threat}",
                    _centerStyle);
                GUI.color = Color.white;
                return;
            }

            if (entity.Kind == FieldEntityKind.Npc)
            {
                Texture2D texture = Resources.Load<Texture2D>("Art/Battle/Units/c_cleric") ??
                                    Resources.Load<Texture2D>("Art/Battle/Units/partner");
                DrawFieldActor(point, texture, false, entity.Y);
                GUI.color = new Color(0.48f, 0.92f, 1f, 0.96f);
                GUI.Label(
                    new Rect(point.x - 105f, point.y + 18f, 210f, 28f),
                    $"NPC　{entity.DisplayName}",
                    _centerStyle);
                GUI.color = Color.white;
                return;
            }

            float bounce = Mathf.Sin(_fieldPulse * 2.8f) * 3f;
            Rect shadow = new Rect(point.x - 31f, point.y - 6f, 62f, 12f);
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(shadow, Texture2D.whiteTexture);
            Rect chest = new Rect(point.x - 29f, point.y - 48f + bounce, 58f, 45f);
            GUI.color = new Color(0.48f, 0.22f, 0.05f, 1f);
            GUI.DrawTexture(chest, Texture2D.whiteTexture);
            GUI.color = new Color(0.94f, 0.72f, 0.18f, 1f);
            GUI.DrawTexture(
                new Rect(chest.x + 4f, chest.y + 5f, chest.width - 8f, 12f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(chest.center.x - 4f, chest.y + 16f, 8f, chest.height - 20f),
                Texture2D.whiteTexture);
            DrawGuiFrame(chest, new Color(1f, 0.84f, 0.36f, 0.95f), 3f);
            GUI.color = new Color(1f, 0.86f, 0.38f, 0.98f);
            GUI.Label(
                new Rect(point.x - 90f, point.y + 8f, 180f, 28f),
                "宝箱　遠征物資",
                _centerStyle);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 走っているときに足元へ出す砂埃。1歩ごとに小さな粒が後ろへ流れる。
        /// 位置と大きさは歩調から決まるので、乱数を使わず毎回同じ見え方になる。
        /// </summary>
        private void DrawRunDust(
            Vector2 ground,
            float width,
            float depthScale,
            float stride,
            float runBlend)
        {
            Color previous = GUI.color;
            for (int i = 0; i < 3; i++)
            {
                // 粒ごとに歩調をずらし、古い粒ほど広がって薄くなるようにする。
                float age = Mathf.Repeat(stride + i * 0.33f, 1f);
                float size = (4f + age * 9f) * depthScale;
                float alpha = (1f - age) * 0.30f * runBlend;
                if (alpha <= 0.01f) continue;

                GUI.color = new Color(0.82f, 0.78f, 0.70f, alpha);
                GUI.DrawTexture(
                    new Rect(
                        ground.x - width * 0.16f - age * 22f * depthScale,
                        ground.y - 6f - age * 7f * depthScale,
                        size,
                        size * 0.55f),
                    Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        /// <summary>
        /// 座標が動いたかどうかから走りの強さを求める。
        /// 走り出しは素早く、止まったあとはゆっくり収める（急に立ち止まると硬く見えるため）。
        /// </summary>
        private static float AdvanceRunBlendAndFacing(
            float blend,
            ref Vector2 previous,
            ref Vector2 facing,
            Vector2 current,
            float deltaTime)
        {
            Vector2 movement = current - previous;
            float moved = movement.magnitude;
            previous = current;
            if (deltaTime <= 0f) return blend;

            if (moved > 0.0004f)
                facing = movement.normalized;

            float target = moved > 0.0004f ? 1f : 0f;
            float rate = target > blend ? 9f : 4.5f;
            return Mathf.MoveTowards(blend, target, rate * deltaTime);
        }

        // 既存の探索描画テストと外部デバッグ呼び出し向けの互換入口。
        private static float AdvanceRunBlend(
            float blend,
            ref Vector2 previous,
            Vector2 current,
            float deltaTime)
        {
            Vector2 unusedFacing = Vector2.down;
            return AdvanceRunBlendAndFacing(
                blend,
                ref previous,
                ref unusedFacing,
                current,
                deltaTime);
        }

        private bool DrawPixelFieldActor(
            Vector2 ground,
            string sourceUnitId,
            Vector2 facingVector,
            float normalizedDepth,
            float runBlend,
            bool enemy)
        {
            if (DrawRealtimePixelBoneActor(
                    ground,
                    sourceUnitId,
                    facingVector,
                    normalizedDepth,
                    runBlend,
                    enemy))
                return true;

            Texture2D sourceAtlas = LoadPixelAtlas(sourceUnitId);
            if (sourceAtlas == null) return false;

            PixelFacing facing = ResolvePixelFacing(facingVector);
            bool moving = runBlend > 0.08f;
            bool quadruped = PixelAnimationProfile.UsesQuadrupedAtlas(sourceUnitId);
            Texture2D atlas = quadruped ? LoadPixelQuadrupedAtlas() : sourceAtlas;
            if (atlas == null) return false;
            int columns = quadruped
                ? PixelAnimationProfile.QuadrupedColumns
                : PixelAnimationProfile.Columns;
            int rows = quadruped
                ? PixelAnimationProfile.QuadrupedRows
                : PixelAnimationProfile.Rows;
            int frame = quadruped
                ? PixelAnimationProfile.GetQuadrupedFieldFrameIndex(facing, moving, _fieldPulse)
                : PixelAnimationProfile.GetFieldFrameIndex(facing, moving, _fieldPulse);
            int column = frame % columns;
            int row = frame / columns;
            Rect uv = new Rect(
                column / (float)columns,
                1f - (row + 1f) / rows,
                1f / columns,
                1f / rows);
            if (PixelAnimationProfile.ShouldFlipField(facing))
            {
                uv.x += uv.width;
                uv.width = -uv.width;
            }

            float depthScale = Mathf.Lerp(0.76f, 1.14f, Mathf.Clamp01(normalizedDepth));
            float size = 132f * depthScale;
            float stride = moving ? Mathf.Abs(Mathf.Sin(_fieldPulse * 6f)) : 0f;
            float lift = stride * 3f * runBlend;
            float shadowScale = 1f - stride * 0.18f * runBlend;
            GUI.color = new Color(0f, 0f, 0f, enemy ? 0.48f : 0.38f);
            GUI.DrawTexture(
                new Rect(
                    ground.x - size * 0.28f * shadowScale,
                    ground.y - 6f,
                    size * 0.56f * shadowScale,
                    11f * depthScale),
                Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect actorRect = new Rect(
                ground.x - size * 0.5f,
                ground.y - size + lift,
                size,
                size);
            GUI.DrawTextureWithTexCoords(actorRect, atlas, uv, true);
            if (!enemy)
            {
                DrawGuiFrame(
                    new Rect(actorRect.x + size * 0.18f, actorRect.y + size * 0.10f,
                        size * 0.64f, size * 0.82f),
                    new Color(0.28f, 0.90f, 1f, 0.32f),
                    2f);
            }
            return true;
        }

        private bool DrawRealtimePixelBoneActor(
            Vector2 ground,
            string sourceUnitId,
            Vector2 facingVector,
            float normalizedDepth,
            float runBlend,
            bool enemy)
        {
            PixelFacing facing = ResolvePixelFacing(facingVector);
            if (!PixelAnimationProfile.UsesQuadrupedAtlas(sourceUnitId))
            {
                Texture2D motionAtlas = LoadPixelMotionAtlas(sourceUnitId, "field60");
                if (motionAtlas != null)
                    return DrawMotion60FieldActor(
                        ground,
                        motionAtlas,
                        facing,
                        normalizedDepth,
                        runBlend,
                        enemy);
            }
            bool flip = facing == PixelFacing.Left;
            string skinKey = sourceUnitId + (flip ? ":left" : ":right");
            if (!_pixelSkinCpuRenderers.TryGetValue(skinKey, out PixelSkinCpuRenderer skin))
            {
                skin = PixelSkinCpuRenderer.TryCreate(sourceUnitId);
                _pixelSkinCpuRenderers[skinKey] = skin;
            }
            if (skin == null) return false;

            float depthScale = Mathf.Lerp(0.76f, 1.14f, Mathf.Clamp01(normalizedDepth));
            float size = 132f * depthScale;
            float cycle = _fieldPulse * Mathf.PI * 2f * 2.1f;
            float stride = Mathf.Sin(cycle) * Mathf.Clamp01(runBlend);
            float lift = Mathf.Abs(stride) * 7f * Mathf.Clamp01(runBlend);
            float shadowScale = 1f - Mathf.Abs(stride) * 0.22f;
            GUI.color = new Color(0f, 0f, 0f, enemy ? 0.48f : 0.38f);
            GUI.DrawTexture(
                new Rect(
                    ground.x - size * 0.28f * shadowScale,
                    ground.y - 6f,
                    size * 0.56f * shadowScale,
                    11f * depthScale),
                Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect actor = new Rect(
                ground.x - size * 0.5f,
                ground.y - size + lift,
                size,
                size);
            BoneRigPoseSample2D pose = skin.Walk(_fieldPulse, runBlend);
            Texture2D texture = skin.Render(pose, flip);
            GUI.DrawTexture(actor, texture, ScaleMode.StretchToFill, true);

            if (!enemy)
            {
                DrawGuiFrame(
                    new Rect(actor.x + size * 0.18f, actor.y + size * 0.10f,
                        size * 0.64f, size * 0.82f),
                    new Color(0.28f, 0.90f, 1f, 0.32f),
                    2f);
            }
            return true;
        }

        private bool DrawMotion60FieldActor(
            Vector2 ground,
            Texture2D atlas,
            PixelFacing facing,
            float normalizedDepth,
            float runBlend,
            bool enemy)
        {
            bool moving = runBlend > 0.08f;
            int direction = facing == PixelFacing.Down
                ? 0
                : facing == PixelFacing.Up ? 1 : 2;
            int count = moving ? 20 : 60;
            int start = moving ? direction * 20 : 60 + direction * 60;
            int frame = start + Math.Max(
                0,
                (int)Math.Floor(_fieldPulse * PixelAnimationProfile.FramesPerSecond)) % count;
            const int columns = PixelAnimationProfile.MotionColumns;
            int rows = PixelAnimationProfile.GetMotionRows(240);
            int column = frame % columns;
            int rowFromTop = frame / columns;
            bool flip = facing == PixelFacing.Left;
            Rect uv = flip
                ? new Rect(
                    (column + 1f) / columns,
                    1f - (rowFromTop + 1f) / rows,
                    -1f / columns,
                    1f / rows)
                : new Rect(
                    column / (float)columns,
                    1f - (rowFromTop + 1f) / rows,
                    1f / columns,
                    1f / rows);

            float depthScale = Mathf.Lerp(0.76f, 1.14f, Mathf.Clamp01(normalizedDepth));
            float size = 132f * depthScale;
            float stride = moving ? Mathf.Sin(_fieldPulse * Mathf.PI * 4.2f) : 0f;
            float lift = moving ? Mathf.Abs(stride) * 3f : 0f;
            GUI.color = new Color(0f, 0f, 0f, enemy ? 0.48f : 0.38f);
            GUI.DrawTexture(
                new Rect(ground.x - size * 0.28f, ground.y - 6f, size * 0.56f, 11f * depthScale),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            Rect actor = new Rect(
                ground.x - size * 0.5f,
                ground.y - size + lift,
                size,
                size);
            GUI.DrawTextureWithTexCoords(actor, atlas, uv, true);
            if (!enemy)
            {
                DrawGuiFrame(
                    new Rect(actor.x + size * 0.18f, actor.y + size * 0.10f,
                        size * 0.64f, size * 0.82f),
                    new Color(0.28f, 0.90f, 1f, 0.32f),
                    2f);
            }
            return true;
        }

        private Texture2D LoadPixelBonePart(string sourceUnitId, string part)
        {
            string key = sourceUnitId + "/" + part;
            if (_pixelBonePartTextures.TryGetValue(key, out Texture2D cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>($"Art/Pixel/BoneParts/{key}");
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            _pixelBonePartTextures[key] = texture;
            return texture;
        }

        private static Vector2 PixelFieldJoint(string part, bool quadruped)
        {
            if (quadruped)
            {
                switch (part)
                {
                    case "head": return new Vector2(0.76f, 0.59f);
                    case "upper_arm_left": return new Vector2(0.65f, 0.44f);
                    case "forearm_left": return new Vector2(0.69f, 0.24f);
                    case "upper_arm_right": return new Vector2(0.57f, 0.43f);
                    case "forearm_right": return new Vector2(0.59f, 0.23f);
                    case "thigh_left": return new Vector2(0.39f, 0.41f);
                    case "shin_left": return new Vector2(0.42f, 0.22f);
                    case "thigh_right": return new Vector2(0.29f, 0.40f);
                    case "shin_right": return new Vector2(0.31f, 0.21f);
                    default: return new Vector2(0.54f, 0.39f);
                }
            }
            switch (part)
            {
                case "head": return new Vector2(0.50f, 0.73f);
                case "upper_arm_left": return new Vector2(0.40f, 0.64f);
                case "forearm_left":
                case "weapon": return new Vector2(0.33f, 0.52f);
                case "upper_arm_right": return new Vector2(0.60f, 0.64f);
                case "forearm_right": return new Vector2(0.67f, 0.52f);
                case "thigh_left": return new Vector2(0.46f, 0.42f);
                case "shin_left": return new Vector2(0.45f, 0.23f);
                case "thigh_right": return new Vector2(0.54f, 0.42f);
                case "shin_right": return new Vector2(0.55f, 0.23f);
                default: return new Vector2(0.50f, 0.42f);
            }
        }

        private static float PixelFieldPartAngle(
            string part,
            float stride,
            float runBlend,
            bool quadruped)
        {
            float idle = Mathf.Sin(Time.unscaledTime * 2.4f) * 1.1f;
            float gait = stride * (quadruped ? 6f : 7f);
            switch (part)
            {
                case "head": return -idle * 0.35f - gait * 0.08f;
                case "torso": return idle * 0.24f + gait * 0.05f;
                case "cape": return -idle - gait * 0.38f;
                case "upper_arm_left": return -gait;
                case "forearm_left": return gait * 0.72f;
                case "upper_arm_right": return gait;
                case "forearm_right": return -gait * 0.72f;
                case "thigh_left": return gait;
                case "shin_left": return -gait * 0.65f;
                case "thigh_right": return -gait;
                case "shin_right": return gait * 0.65f;
                case "weapon": return -gait * 0.45f;
                default: return idle * (1f - runBlend);
            }
        }

        private static PixelFacing ResolvePixelFacing(Vector2 facing)
        {
            if (Mathf.Abs(facing.y) >= Mathf.Abs(facing.x))
                return facing.y < 0f ? PixelFacing.Up : PixelFacing.Down;
            return facing.x < 0f ? PixelFacing.Left : PixelFacing.Right;
        }

        private void DrawFieldActor(
            Vector2 ground,
            Texture2D texture,
            bool enemy,
            float normalizedDepth,
            float runBlend = 0f)
        {
            runBlend = Mathf.Clamp01(runBlend);
            float depthScale = Mathf.Lerp(0.72f, 1.12f, Mathf.Clamp01(normalizedDepth));
            float breathe = 1f + Mathf.Sin(_fieldPulse * (enemy ? 2.2f : 2.8f)) * 0.018f;

            // 走りの歩調。1歩ごとに接地→浮きを繰り返すので、絶対値で山を2つ作る。
            float stride = Mathf.Abs(Mathf.Sin(_fieldPulse * 7.5f));
            // 接地の瞬間に潰れ、浮いている間に伸びる。体積を保つため横は逆向きに変える。
            float squash = 1f + (stride - 0.5f) * 0.11f * runBlend;

            float height = 154f * depthScale * breathe * squash;
            float width = texture == null || texture.height <= 0
                ? 92f * depthScale
                : height * texture.width / texture.height;
            width = Mathf.Clamp(width, 68f * depthScale, 138f * depthScale) / Mathf.Max(0.01f, squash);
            float hover = Mathf.Sin(_fieldPulse * (enemy ? 2.5f : 3.1f)) * 3f
                          + stride * 9f * depthScale * runBlend;

            // 走っている間は影が小さく薄くなる。浮いている実感はここで出る。
            float shadowShrink = 1f - stride * 0.3f * runBlend;
            GUI.color = new Color(
                0f,
                0f,
                0f,
                (enemy ? 0.48f : 0.38f) * (1f - stride * 0.35f * runBlend));
            GUI.DrawTexture(
                new Rect(
                    ground.x - width * 0.34f * shadowShrink,
                    ground.y - 8f,
                    width * 0.68f * shadowShrink,
                    13f * depthScale * shadowShrink),
                Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (runBlend > 0.05f) DrawRunDust(ground, width, depthScale, stride, runBlend);

            Rect actorRect = new Rect(
                ground.x - width * 0.5f,
                ground.y - height + hover,
                width,
                height);
            if (texture != null)
            {
                GUI.DrawTexture(actorRect, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.color = enemy
                    ? new Color(0.72f, 0.10f, 0.14f, 0.95f)
                    : new Color(0.18f, 0.72f, 0.82f, 0.95f);
                GUI.DrawTexture(actorRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (!enemy)
            {
                DrawGuiFrame(
                    new Rect(ground.x - width * 0.34f, ground.y - height * 0.93f, width * 0.68f, height * 0.88f),
                    new Color(0.28f, 0.90f, 1f, 0.42f),
                    2f);
            }
        }

        private static void DrawGuiFrame(Rect rect, Color color, float width)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - width, rect.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - width, rect.y, width, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawGuiLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            Vector2 direction = end - start;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, direction.magnitude, width),
                Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static string WarOrderName(WarOrder order)
        {
            switch (order)
            {
                case WarOrder.Assault: return "突撃";
                case WarOrder.Hold: return "防衛";
                case WarOrder.Support: return "支援";
                default: return order.ToString();
            }
        }

        private static string WarOrderEffect(WarOrder order)
        {
            switch (order)
            {
                case WarOrder.Assault: return "支援を破る";
                case WarOrder.Hold: return "突撃を止める";
                case WarOrder.Support: return "防衛を崩し、隣接を援護";
                default: return string.Empty;
            }
        }

        private static string ControlName(int control)
        {
            if (control >= 2) return "味方制圧";
            if (control == 1) return "味方優勢";
            if (control == 0) return "拮抗";
            if (control == -1) return "敵優勢";
            return "敵制圧";
        }

        private void DrawBattle()
        {
            DrawBattleVignette();
            if (_impactFlashAlpha > 0.001f)
            {
                Color flash = _impactFlashColor;
                flash.a = _impactFlashAlpha;
                GUI.color = flash;
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            GUI.color = new Color(0.01f, 0.02f, 0.035f, 0.88f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 78f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - 116f, Screen.width, 116f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(24f, 12f, Screen.width - 48f, 34f), $"第{_stageIndex + 1}戦　FORMATION ENCOUNTER", _titleStyle);
            GUI.Label(new Rect(24f, 45f, Screen.width - 48f, 26f), _message, _centerStyle);

            float playerVitality = TeamHealthRatio(BattleTeam.Player);
            float enemyVitality = TeamHealthRatio(BattleTeam.Enemy);
            DrawBar(
                new Rect(24f, 70f, Mathf.Min(300f, Screen.width * 0.22f), 5f),
                playerVitality,
                new Color(0.18f, 0.82f, 0.70f));
            DrawBar(
                new Rect(
                    Screen.width - 24f - Mathf.Min(300f, Screen.width * 0.22f),
                    70f,
                    Mathf.Min(300f, Screen.width * 0.22f),
                    5f),
                enemyVitality,
                new Color(0.92f, 0.27f, 0.32f));

            GUI.color = new Color(0.015f, 0.035f, 0.065f, 0.84f);
            GUI.DrawTexture(new Rect(20f, 88f, 310f, 52f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(32f, 94f, 286f, 22f),
                $"TACTICAL FLOW　ACTION {_battleActionIndex:00}",
                _smallStyle);
            GUI.Label(
                new Rect(32f, 116f, 286f, 20f),
                $"部隊活力　味方 {Mathf.RoundToInt(playerVitality * 100f)}% ／ 敵 {Mathf.RoundToInt(enemyVitality * 100f)}%",
                _smallStyle);

            if (!string.IsNullOrEmpty(_skillBanner))
            {
                float bannerWidth = Mathf.Min(760f, Screen.width - 40f);
                GUI.color = new Color(0.03f, 0.07f, 0.12f, 0.90f);
                GUI.DrawTexture(new Rect((Screen.width - bannerWidth) * 0.5f, 92f, bannerWidth, 54f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect((Screen.width - bannerWidth) * 0.5f, 100f, bannerWidth, 38f), _skillBanner, _titleStyle);
            }

            DrawBattleTimeline();
            if (_battleCommandOpen) DrawBattleCommandPanel();
            DrawUnitStatus(BattleTeam.Enemy, 20f, Screen.height - 102f, Screen.width * 0.43f);
            DrawUnitStatus(BattleTeam.Player, Screen.width * 0.57f, Screen.height - 102f, Screen.width * 0.41f);

            foreach (FloatingLabel label in _labels)
            {
                Vector3 screen = _camera.WorldToScreenPoint(label.World);
                float alpha = 1f - Mathf.Clamp01(label.Age / label.Duration);
                Color previous = GUI.color;
                GUI.color = new Color(label.Color.r, label.Color.g, label.Color.b, alpha);
                GUI.Label(new Rect(screen.x - 110f, Screen.height - screen.y - 30f, 220f, 44f), label.Text, _titleStyle);
                GUI.color = previous;
            }

            GUILayout.BeginArea(new Rect(Screen.width - 292f, 86f, 272f, 54f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_paused ? "再開" : "一時停止", _buttonStyle)) _paused = !_paused;
            if (GUILayout.Button($"速度 ×{_battleSpeed:0}", _buttonStyle)) _battleSpeed = _battleSpeed < 2f ? 2f : 1f;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (_showResult) DrawResult();
        }

        private void DrawBattleCommandPanel()
        {
            if (_pendingCommandActor == null) return;
            float width = Mathf.Min(820f, Screen.width - 32f);
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, Screen.height - 278f, width, 92f),
                _panelStyle);
            GUILayout.Label($"{DisplayName(_pendingCommandActor)} COMMAND", _smallStyle);
            GUILayout.BeginHorizontal();
            DrawCommandButton("攻撃", FormationCommandKind.Attack);
            DrawCommandButton("協力", FormationCommandKind.Cooperation);
            DrawCommandButton("魔法", FormationCommandKind.Magic);
            DrawCommandButton("防御", FormationCommandKind.Defend);
            DrawCommandButton("逃走", FormationCommandKind.Flee);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCommandButton(string label, FormationCommandKind kind)
        {
            if (!GUILayout.Button(label, _buttonStyle)) return;
            _pendingBattleCommand = new FormationBattleCommand(kind);
            _audio.PlaySfx("select");
        }

        private void DrawBattleTimeline()
        {
            if (_battle == null) return;
            FormationCombatant[] upcoming = _battle.GetUpcomingUnits(8).ToArray();
            if (upcoming.Length == 0) return;

            const float badge = 48f;
            const float gap = 7f;
            float width = upcoming.Length * badge + (upcoming.Length - 1) * gap;
            float left = (Screen.width - width) * 0.5f;
            float top = Screen.height - 172f;
            GUI.Label(new Rect(left, top - 24f, width, 22f), "ACTION ORDER", _smallStyle);
            for (int i = 0; i < upcoming.Length; i++)
            {
                FormationCombatant unit = upcoming[i];
                Rect cell = new Rect(left + i * (badge + gap), top, badge, badge);
                Color teamColor = unit.Team == BattleTeam.Player
                    ? new Color(0.16f, 0.72f, 0.82f, 0.94f)
                    : new Color(0.78f, 0.18f, 0.25f, 0.94f);
                GUI.color = new Color(0.01f, 0.025f, 0.045f, 0.92f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                DrawGuiFrame(cell, teamColor, i == 0 ? 4f : 2f);
                GUI.color = Color.white;
                string display = DisplayName(unit);
                string shortName = string.IsNullOrEmpty(display)
                    ? "--"
                    : display.Substring(0, Math.Min(2, display.Length));
                GUI.Label(new Rect(cell.x, cell.y + 4f, cell.width, 23f), shortName, _centerStyle);
                string row = FormationPresentationProfile.GetFormationRow(unit.FormationSlot) == FormationRow.Front
                    ? "前"
                    : "後";
                GUI.Label(new Rect(cell.x, cell.y + 27f, cell.width, 18f), row, _smallStyle);
            }
            GUI.color = Color.white;
        }

        private void DrawBattleVignette()
        {
            float edgeWidth = Mathf.Max(36f, Screen.width * 0.065f);
            float edgeHeight = Mathf.Max(30f, Screen.height * 0.055f);
            GUI.color = new Color(0.005f, 0.008f, 0.016f, 0.42f);
            GUI.DrawTexture(new Rect(0f, 0f, edgeWidth, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - edgeWidth, 0f, edgeWidth, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edgeHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - edgeHeight, Screen.width, edgeHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private float TeamHealthRatio(BattleTeam team)
        {
            if (_battle == null) return 0f;
            FormationCombatant[] units = _battle.Units
                .Where(unit => unit.Team == team)
                .ToArray();
            int maximum = units.Sum(unit => Mathf.Max(0, unit.MaxHp));
            if (maximum <= 0) return 0f;
            int current = units.Sum(unit => Mathf.Max(0, unit.Hp));
            return Mathf.Clamp01(current / (float)maximum);
        }

        private void DrawUnitStatus(BattleTeam team, float x, float y, float width)
        {
            // 再生中に再コンパイルが走るとドメインリロードで _battle が失われる。
            // そのまま参照すると毎フレーム例外が出て OnGUI が途中で止まり、
            // 画面が半分しか描かれない状態になるため、ここで打ち切る。
            if (_battle == null) return;
            FormationCombatant[] units = _battle.Units.Where(unit => unit.Team == team).OrderBy(unit => unit.FormationSlot).ToArray();
            float cellWidth = width / Mathf.Max(1, units.Length);
            for (int i = 0; i < units.Length; i++)
            {
                FormationCombatant unit = units[i];
                float cellX = x + i * cellWidth;
                string row = FormationPresentationProfile.GetFormationRow(unit.FormationSlot) == FormationRow.Front
                    ? "前"
                    : "後";
                GUI.Label(new Rect(cellX, y, cellWidth - 6f, 22f), $"{row}｜{DisplayName(unit)}", _smallStyle);
                Rect bar = new Rect(cellX, y + 25f, cellWidth - 8f, 10f);
                DrawBar(bar, unit.MaxHp <= 0 ? 0f : unit.Hp / (float)unit.MaxHp,
                    team == BattleTeam.Player ? new Color(0.18f, 0.82f, 0.70f) : new Color(0.92f, 0.27f, 0.32f));
                GUI.Label(new Rect(cellX, y + 38f, cellWidth - 6f, 20f), $"{unit.Hp}/{unit.MaxHp}", _smallStyle);
            }
        }

        private void DrawResult()
        {
            GUI.color = new Color(0.01f, 0.02f, 0.035f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = Mathf.Min(560f, Screen.width - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, (Screen.height - 300f) * 0.5f, width, 300f), _panelStyle);
            GUILayout.Space(24f);

            if (_battle.Winner == BattleWinner.Escaped)
            {
                GUILayout.Label("RETREAT", _heroTitleStyle);
                GUILayout.Label("部隊は安全に離脱しました。敵シンボルはフィールドに残ります。", _centerStyle);
                GUILayout.Space(22f);
                if (GUILayout.Button("フィールドへ戻る", _buttonStyle))
                    ShowField(_stageIndex, "敵との距離を取りました。装備や隊列を整えて再挑戦できます。");
                if (GUILayout.Button("タイトルへ", _buttonStyle)) ShowTitle();
                GUILayout.EndArea();
                return;
            }

            // 試練は通常の章進行と切り離す。勝っても章は進まず、負けても罰は無い。
            if (_ordealEncounter != null)
            {
                bool wonOrdeal = _battle.Winner == BattleWinner.Player;
                UniqueRelic relic = StoryChoicePolicy.FindRelic(_ordealEncounter.RelicId);
                GUILayout.Label(wonOrdeal ? "VICTORY" : "DEFEAT", _heroTitleStyle);
                GUILayout.Label(_ordealEncounter.Name, _titleStyle);
                GUILayout.Space(18f);
                GUILayout.Label(
                    wonOrdeal
                        ? $"{relic?.AcquisitionLine}\n「{relic?.Name}」を手に入れた。"
                        : "届かなかった。だが、失ったものは何もない。",
                    _centerStyle,
                    GUILayout.Height(96f));
                GUILayout.Space(20f);
                if (GUILayout.Button("物語へ戻る", _buttonStyle)) ResolveOrdealOutcome(wonOrdeal);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label(_battle.Winner == BattleWinner.Player ? "VICTORY" : "DEFEAT", _heroTitleStyle);
            string resultDescription = _battle.Winner != BattleWinner.Player
                ? "編成を立て直し、もう一度挑みましょう。"
                : _battleCompletedStage
                    ? "敵主力を撃破し、次の地域への道を開きました。"
                    : "敵斥候を撃破しました。フィールドには敵主力が残っています。";
            GUILayout.Label(resultDescription, _centerStyle);
            GUILayout.Space(22f);
            if (_battle.Winner == BattleWinner.Player)
            {
                if (!_battleCompletedStage)
                {
                    if (GUILayout.Button("フィールドへ戻り敵主力を追う", _buttonStyle))
                        ShowField(
                            _stageIndex,
                            "敵斥候を撃破しました。敵主力を倒すまで章は完了しません。");
                }
                else if (_stageIndex < _catalog.stages.Length - 1)
                {
                    if (GUILayout.Button("次の地域へ", _buttonStyle))
                        ShowPendingChapterStoryOrField(_save.stageIndex);
                }
                else if (GUILayout.Button("贈り物を受け取る", _buttonStyle)) EnterGift();
            }
            else
            {
                if (GUILayout.Button("再戦する", _buttonStyle)) StartEncounterBattle(_stageIndex);
                if (GUILayout.Button("フィールドへ撤退", _buttonStyle)) ShowField(_stageIndex);
            }
            if (GUILayout.Button("タイトルへ", _buttonStyle)) ShowTitle();
            GUILayout.EndArea();
        }

        private void DrawGift()
        {
            DrawFullScreenTint(new Color(0.035f, 0.025f, 0.07f, 1f));
            float pulse = 0.82f + Mathf.Sin(_giftTime * 1.8f) * 0.08f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.92f, 0.58f, pulse);
            float width = Mathf.Min(720f, Screen.width - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 90f, width, 560f), _panelStyle);
            GUILayout.Space(32f);
            GUILayout.Label("HAPPY BIRTHDAY", _heroTitleStyle);
            GUILayout.Space(28f);
            GUILayout.Label("すべての戦いを越えたあなたへ。\nこの物語と、いっしょに過ごした時間を贈ります。", _centerStyle);
            GUILayout.Space(34f);
            GUILayout.Label("これからの一年にも、\nたくさんの素敵な冒険がありますように。", _titleStyle);
            GUILayout.Space(34f);
            if (GUILayout.Button("タイトルへ戻る", _buttonStyle)) ShowTitle();
            GUILayout.EndArea();
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            // ドメインリロード後はスタイルの参照だけが残り、中のテクスチャが破棄されている
            // ことがある。その状態で描画すると例外になるので、作り直す。
            if (_panelStyle != null && _panelStyle.normal.background != null) return;
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(28, 28, 24, 24),
                normal = { background = MakeTexture(new Color(0.025f, 0.045f, 0.075f, 0.94f)) }
            };
            _heroTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 38,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0.42f) }
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.93f, 1f) }
            };
            _labelStyle = new GUIStyle(_centerStyle) { alignment = TextAnchor.MiddleLeft };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                fixedHeight = 42f,
                margin = new RectOffset(6, 6, 5, 5)
            };
        }

        private static void DrawBar(Rect rect, float value, Color fill)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.70f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f) * Mathf.Clamp01(value), rect.height - 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawFullScreenTint(Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Vector3 FormationPosition(BattleTeam team, int slot)
        {
            FormationAnchor anchor = FormationPresentationProfile.GetAnchor(team, slot);
            return new Vector3(anchor.X, anchor.Y, 0f);
        }

        private static string AssetId(string sourceUnitId)
        {
            if (string.IsNullOrWhiteSpace(sourceUnitId)) return "hero";
            return sourceUnitId;
        }

        private static string PoseAssetId(string sourceUnitId, UnitPose pose)
        {
            if (pose == UnitPose.Idle) return AssetId(sourceUnitId);
            BattlePose battlePose = pose == UnitPose.Action
                ? BattlePose.Attack
                : pose == UnitPose.Hit || pose == UnitPose.Guard
                    ? BattlePose.Hit
                    : pose == UnitPose.Victory ? BattlePose.Victory : BattlePose.Incapacitated;
            return FormationPresentationProfile.GetPoseAssetId(sourceUnitId, battlePose);
        }

        private static Texture2D LoadPoseTexture(string sourceUnitId, UnitPose pose)
        {
            if (pose == UnitPose.Idle) return null;
            BattlePose battlePose = pose == UnitPose.Action
                ? BattlePose.Attack
                : pose == UnitPose.Hit || pose == UnitPose.Guard
                    ? BattlePose.Hit
                    : pose == UnitPose.Victory ? BattlePose.Victory : BattlePose.Incapacitated;
            string assetId = FormationPresentationProfile.GetPoseAssetId(sourceUnitId, battlePose);
            // partner だけ partner_cast へフォールバックしていたが、呼び出し側は
            // GetSpriteMetrics(partner_attack) で算出したピボットとスケールを渡すため、
            // 別画像を返すとメトリクスが一致せず接地と身長が破綻する。
            // partner_attack.png は常に存在するのでフォールバックは到達せず、削除した。
            string directory = string.Equals(
                sourceUnitId,
                RecruitmentRosterPolicy.MemoryMinstrelId,
                StringComparison.Ordinal)
                ? "Art/Battle/Units"
                : "Art/Battle/Units/Variants";
            return Resources.Load<Texture2D>($"{directory}/{assetId}");
        }

        private Texture2D LoadPixelAtlas(string sourceUnitId)
        {
            if (!PixelAnimationProfile.IsSupported(sourceUnitId)) return null;
            if (_pixelAtlases.TryGetValue(sourceUnitId, out Texture2D cached))
                return cached;
            Texture2D texture = Resources.Load<Texture2D>(
                $"Art/Pixel/Characters/{sourceUnitId}_atlas");
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            _pixelAtlases[sourceUnitId] = texture;
            return texture;
        }

        private Texture2D LoadPixelMotionAtlas(string sourceUnitId, string suffix)
        {
            if (!PixelAnimationProfile.IsSupported(sourceUnitId)) return null;
            string cacheKey = $"{sourceUnitId}:{suffix}";
            if (_pixelMotionAtlases.TryGetValue(cacheKey, out Texture2D cached))
                return cached;
            Texture2D texture = Resources.Load<Texture2D>(
                $"Art/Pixel/Characters/Motion60/{sourceUnitId}_{suffix}");
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            _pixelMotionAtlases[cacheKey] = texture;
            return texture;
        }

        private Texture2D LoadPixelQuadrupedAtlas()
        {
            const string cacheKey = "azuki:quadruped";
            if (_pixelMotionAtlases.TryGetValue(cacheKey, out Texture2D cached))
                return cached;
            Texture2D texture = Resources.Load<Texture2D>(
                "Art/Pixel/Characters/azuki_quadruped");
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            _pixelMotionAtlases[cacheKey] = texture;
            return texture;
        }

        private static Texture2D LoadPixelDefeatTexture(string sourceUnitId)
        {
            Texture2D texture = Resources.Load<Texture2D>(
                $"Art/Pixel/Characters/Defeat/{sourceUnitId}_defeat");
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }

        private Sprite[] CreatePixelMotionSequence(
            Texture2D atlas,
            int startFrame,
            int frameCount,
            int atlasFrameCount)
        {
            if (atlas == null) return Array.Empty<Sprite>();
            if (startFrame < 0 || frameCount < 1 || startFrame + frameCount > atlasFrameCount)
                throw new ArgumentOutOfRangeException(nameof(startFrame));
            var sequence = new Sprite[frameCount];
            for (int index = 0; index < frameCount; index++)
                sequence[index] = CreatePixelMotionSprite(atlas, startFrame + index, atlasFrameCount);
            return sequence;
        }

        private Sprite CreatePixelMotionSprite(Texture2D atlas, int frameIndex, int atlasFrameCount)
        {
            if (atlas == null) return null;
            if (frameIndex < 0 || frameIndex >= atlasFrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            int rows = PixelAnimationProfile.GetMotionRows(atlasFrameCount);
            float cellWidth = atlas.width / (float)PixelAnimationProfile.MotionColumns;
            float cellHeight = atlas.height / (float)rows;
            int column = frameIndex % PixelAnimationProfile.MotionColumns;
            int rowFromTop = frameIndex / PixelAnimationProfile.MotionColumns;
            Sprite sprite = Sprite.Create(
                atlas,
                new Rect(
                    column * cellWidth,
                    atlas.height - (rowFromTop + 1) * cellHeight,
                    cellWidth,
                    cellHeight),
                new Vector2(0.5f, 0.045f),
                32f,
                0u,
                SpriteMeshType.FullRect);
            _battleSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreatePixelSprite(Texture2D atlas, int frameIndex)
        {
            return CreateGridPixelSprite(
                atlas,
                frameIndex,
                PixelAnimationProfile.Columns,
                PixelAnimationProfile.Rows);
        }

        private Sprite CreateGridPixelSprite(
            Texture2D atlas,
            int frameIndex,
            int columns,
            int rows)
        {
            if (atlas == null) return null;
            if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));
            if (frameIndex < 0 || frameIndex >= columns * rows)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            float cellWidth = atlas.width / (float)columns;
            float cellHeight = atlas.height / (float)rows;
            int column = frameIndex % columns;
            int rowFromTop = frameIndex / columns;
            Sprite sprite = Sprite.Create(
                atlas,
                new Rect(
                    column * cellWidth,
                    atlas.height - (rowFromTop + 1) * cellHeight,
                    cellWidth,
                    cellHeight),
                new Vector2(0.5f, 0.045f),
                32f,
                0u,
                SpriteMeshType.FullRect);
            _battleSprites.Add(sprite);
            return sprite;
        }

        private static Dictionary<UnitPose, Sprite[]> BuildCrispPixelPoseSequences(
            Sprite idle,
            Sprite[] run,
            Sprite action,
            Sprite hit,
            Sprite victory,
            Sprite defeat)
        {
            Sprite approach = run != null && run.Length > 1 ? run[1] : idle;
            return new Dictionary<UnitPose, Sprite[]>
            {
                [UnitPose.Idle] = new[] { idle },
                [UnitPose.Action] = new[] { idle, approach, action },
                [UnitPose.Hit] = new[] { idle, hit },
                [UnitPose.Guard] = new[] { idle, hit },
                [UnitPose.Victory] = new[] { idle, victory },
                [UnitPose.Defeat] = new[] { idle, defeat }
            };
        }

        private Sprite CreatePixelStandaloneSprite(Texture2D texture)
        {
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.045f),
                32f,
                0u,
                SpriteMeshType.FullRect);
            _battleSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateUnitSprite(Texture2D texture, BattleSpriteMetrics metrics)
        {
            return CreateBattleSprite(
                texture,
                new Vector2(metrics.PivotX, metrics.PivotY),
                100f,
                SpriteMeshType.Tight);
        }

        private Sprite CreateBattleSprite(
            Texture2D texture,
            Vector2 pivot,
            float pixelsPerUnit,
            SpriteMeshType meshType)
        {
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                pixelsPerUnit,
                0u,
                meshType);
            _battleSprites.Add(sprite);
            return sprite;
        }

        private void ReleaseBattleSprites()
        {
            foreach (Sprite sprite in _battleSprites)
            {
                if (sprite != null) Destroy(sprite);
            }
            _battleSprites.Clear();
        }

        private static Sprite CreateSharedSprite(Texture2D texture, float pixelsPerUnit)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
        }

        private static Vector3 ScaleForVisibleHeight(
            Sprite sprite,
            BattleSpriteMetrics metrics,
            float targetHeight)
        {
            float visibleHeight = metrics.VisibleHeight * sprite.texture.height / sprite.pixelsPerUnit;
            float uniform = targetHeight / Mathf.Max(0.01f, visibleHeight);
            return Vector3.one * uniform;
        }

        private static Vector3 ScaleForPixelSprite(Sprite sprite, float targetHeight)
        {
            if (sprite == null) return Vector3.one;
            // 128pxセルには上下余白があるため、可視部分を約88%として身長を合わせる。
            float visibleHeight = Mathf.Max(0.01f, sprite.bounds.size.y * 0.88f);
            return Vector3.one * (targetHeight / visibleHeight);
        }

        private static Vector3 ScaleForPoseAsset(Sprite sprite, string assetId, float targetHeight)
        {
            float uniform = FormationPresentationProfile.GetNormalizedPoseScale(
                assetId,
                targetHeight,
                sprite.texture.height,
                sprite.pixelsPerUnit);
            return Vector3.one * uniform;
        }

        private static Vector3 DivideScale(Vector3 numerator, Vector3 denominator)
        {
            return new Vector3(
                numerator.x / Mathf.Max(0.0001f, denominator.x),
                numerator.y / Mathf.Max(0.0001f, denominator.y),
                numerator.z / Mathf.Max(0.0001f, denominator.z));
        }

        private static float GroundY(UnitView view, Vector3 bodyPosition)
        {
            return bodyPosition.y - view.GroundLift;
        }

        private static void ApplySorting(UnitView view, float groundY)
        {
            view.ShadowRenderer.sortingOrder = FormationPresentationProfile.GetSortingOrder(
                view.Unit.Team,
                groundY,
                FormationRenderLayer.Shadow);
            int bodyOrder = FormationPresentationProfile.GetSortingOrder(
                view.Unit.Team,
                groundY,
                FormationRenderLayer.Body);
            view.Renderer.sortingOrder = bodyOrder;
            if (view.BoneRig != null) view.BoneRig.SetSortingOrder(bodyOrder);
            view.BlendRenderer.sortingOrder = FormationPresentationProfile.GetSortingOrder(
                view.Unit.Team,
                groundY,
                FormationRenderLayer.Blend);
        }

        private static void ApplyIncapacitatedSorting(UnitView view)
        {
            view.ShadowRenderer.sortingOrder =
                FormationPresentationProfile.GetIncapacitatedSortingOrder(
                    view.Unit.Team,
                    FormationRenderLayer.Shadow);
            int bodyOrder = FormationPresentationProfile.GetIncapacitatedSortingOrder(
                view.Unit.Team,
                FormationRenderLayer.Body);
            view.Renderer.sortingOrder = bodyOrder;
            if (view.BoneRig != null) view.BoneRig.SetSortingOrder(bodyOrder);
            view.BlendRenderer.sortingOrder =
                FormationPresentationProfile.GetIncapacitatedSortingOrder(
                    view.Unit.Team,
                    FormationRenderLayer.Blend);
        }

        private static Sprite SpriteForPose(UnitView view, UnitPose pose)
        {
            switch (pose)
            {
                case UnitPose.Action: return view.ActionSprite ?? view.IdleSprite;
                case UnitPose.Hit: return view.HitSprite ?? view.IdleSprite;
                case UnitPose.Guard: return view.HitSprite ?? view.IdleSprite;
                case UnitPose.Victory: return view.VictorySprite ?? view.IdleSprite;
                case UnitPose.Defeat: return view.DefeatSprite ?? view.IdleSprite;
                default: return view.IdleSprite;
            }
        }

        private static Vector3 ScaleForPose(UnitView view, UnitPose pose)
        {
            if (view.BoneRig != null) return view.BaseScale;
            switch (pose)
            {
                case UnitPose.Action: return view.ActionSprite == null ? view.BaseScale : view.ActionScale;
                case UnitPose.Hit: return view.HitSprite == null ? view.BaseScale : view.HitScale;
                case UnitPose.Guard: return view.HitSprite == null ? view.BaseScale : view.HitScale;
                case UnitPose.Victory: return view.VictorySprite == null ? view.BaseScale : view.VictoryScale;
                case UnitPose.Defeat: return view.DefeatSprite == null ? view.BaseScale : view.DefeatScale;
                default: return view.BaseScale;
            }
        }

        private static BoneRigPose2D BonePoseFor(UnitPose pose)
        {
            switch (pose)
            {
                case UnitPose.Action: return BoneRigPose2D.Windup;
                case UnitPose.Hit: return BoneRigPose2D.Hit;
                case UnitPose.Guard: return BoneRigPose2D.Guard;
                case UnitPose.Victory: return BoneRigPose2D.Victory;
                case UnitPose.Defeat: return BoneRigPose2D.Defeat;
                default: return BoneRigPose2D.Idle;
            }
        }

        private static void SetBodyColor(UnitView view, Color color)
        {
            if (view.BoneRig == null)
            {
                view.Renderer.color = color;
                return;
            }

            view.Renderer.color = new Color(1f, 1f, 1f, 0f);
            view.BoneRig.SetColor(color);
        }

        private static string DisplayName(FormationCombatant unit)
        {
            switch (unit.SourceUnitId)
            {
                case "hero": return "ケイハン";
                case "partner": return "みんも";
                case "azuki": return "あずき";
                case "memory1": return "思い出の射手";
                case "memory2": return "思い出の癒し手";
                case "memory3": return "記憶の吟遊詩人";
                case "c_lancer": return "蒼槍騎士";
                case "c_skywarden": return "空護騎士";
                case "c_cleric": return "旅の司祭";
                case "c_guard": return "白銀衛士";
                case "c_archer": return "森の射手";
                case "c_mage": return "星詠み";
                case "e_boss": return "黒鎧将";
                default: return EnemyClassName(unit.ClassName);
            }
        }

        private static string EnemyClassName(string className)
        {
            switch (className)
            {
                case "cavalry": return "敵騎兵";
                case "archer": return "敵弓兵";
                case "flier": return "敵飛兵";
                case "mage": return "敵魔術師";
                case "cleric": return "敵司祭";
                default: return "敵重装兵";
            }
        }

        private static string SkillName(FormationAction action)
        {
            if (action.IsDefending) return "DEFEND — 防御姿勢";
            if (action.IsEscape) return "RETREAT — 離脱";
            if (action.IsCooperation)
            {
                string cooperation =
                    $"RESONANCE ASSAULT  {DisplayName(action.Actor)} + {DisplayName(action.Cooperator)}";
                if (action.IsSpecial) cooperation += $"  //  {action.SpecialName}";
                if (action.WasOutOfRange) cooperation += "  LONG RANGE";
                return cooperation;
            }

            if (action.IsSpecial)
            {
                string special = action.SpecialName;
                if (action.AppliedStatus != FormationStatus.None)
                    special += $"  [{StatusName(action.AppliedStatus)}]";
                if (action.WasCritical) special += "  CRITICAL";
                return special;
            }

            string skill;
            switch (action.Kind)
            {
                case FormationActionKind.Magic: skill = "ARCANE IMPACT — 魔導衝"; break;
                case FormationActionKind.Ranged: skill = "SKYLINE SHOT — 追撃射"; break;
                default: skill = "VALIANT EDGE — 勇剣閃"; break;
            }
            if (action.WasCritical) skill += "  CRITICAL";
            else if (action.WasGuarded) skill += "  GUARD";
            if (action.WasOutOfRange) skill += "  LONG RANGE 50%";
            return skill;
        }

        private static float EaseOut(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateEffectTexture()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - distance));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D FallbackUnitTexture(BattleTeam team)
        {
            return MakeTexture(team == BattleTeam.Player ? new Color(0.2f, 0.75f, 0.9f) : new Color(0.85f, 0.25f, 0.3f));
        }
    }
}
